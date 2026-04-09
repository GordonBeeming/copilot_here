using System.Net;
using System.Reflection;
using CopilotHere.Commands.DockerBroker;
using CopilotHere.Commands.Mounts;
using CopilotHere.Infrastructure;
using TUnit.Core;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.IntegrationTests;

/// <summary>
/// End-to-end smoke test for the airlock + DinD path. This is the unblocker
/// scenario from issue #20: a workload container that runs on an
/// `internal: true` airlock network must still be able to spawn and reach
/// sibling Docker containers via the brokered socket.
///
/// What gets exercised:
///   * <see cref="AirlockRunner.GenerateComposeFile"/> with a real broker so
///     placeholder substitution (DOCKER_HOST, BROKER_BRIDGE_TARGET, proxy
///     extra_hosts) is validated against the actual template.
///   * The proxy image's socat bridge (proxy-entrypoint.sh) — workload reaches
///     the host broker through tcp://proxy:2375.
///   * <see cref="DockerSocketBroker.SiblingNetworkName"/> + Phase 2 body
///     inspection — every spawned sibling has its NetworkMode rewritten to
///     the airlock compose network so the workload can reach it by Docker DNS.
///
/// Gating: needs RUN_LIVE_DOCKER_TESTS=1 plus reachable Docker plus the
/// COPILOT_HERE_PROXY_IMAGE / COPILOT_HERE_APP_IMAGE env vars set to test
/// images. CI sets all three after building -st:&lt;sha&gt; tags. Local devs can
/// set them to point at local builds. Otherwise the test self-skips.
/// </summary>
public class AirlockSmokeTests
{
  [Test]
  public async Task AirlockComposeWithBroker_SpawnsSiblingOnAirlockNetwork()
  {
    if (!LiveDockerTest.ShouldRun) return;

    var proxyImage = Environment.GetEnvironmentVariable("COPILOT_HERE_PROXY_IMAGE");
    var appImage = Environment.GetEnvironmentVariable("COPILOT_HERE_APP_IMAGE");
    if (string.IsNullOrEmpty(proxyImage) || string.IsNullOrEmpty(appImage))
    {
      // Without explicit test image overrides we'd be testing whatever is
      // already pulled, which is non-deterministic. Skip in that case.
      return;
    }

    var rt = ContainerRuntimeConfig.CreateConfig("docker");
    var hostSocket = rt.ResolveHostRuntimeSocket()
      ?? throw new InvalidOperationException("no host docker socket");

    var rules = DockerBrokerConfigLoader.LoadDefaultRules()
      ?? throw new InvalidOperationException("default broker rules missing");
    rules.EnableLogging = true;

    var sessionId = Guid.NewGuid().ToString("N")[..8];
    var projectName = $"copilot-airlock-it-{sessionId}";
    var logPath = Path.Combine(Path.GetTempPath(), $"airlock-broker-{sessionId}.jsonl");

    await using var broker = new DockerSocketBroker(
      rules,
      hostSocket,
      BrokerListenEndpoint.Tcp(IPAddress.Loopback, 0),
      logPath);
    await broker.StartAsync(CancellationToken.None);

    // Mirror what AirlockRunner.Run does: tell the broker which compose
    // network to inject as NetworkMode for spawned siblings.
    broker.SiblingNetworkName = $"{projectName}_airlock";

    // Build the compose file via the real generator. We construct an
    // AppContext rooted at a throwaway directory so the airlock loader has
    // somewhere to look (it'll fall back to defaults).
    var tempProjectDir = Path.Combine(Path.GetTempPath(), $"copilot-airlock-it-proj-{sessionId}");
    Directory.CreateDirectory(tempProjectDir);
    var origCwd = Directory.GetCurrentDirectory();
    Directory.SetCurrentDirectory(tempProjectDir);

    string? composeFile = null;
    try
    {
      var ctx = AppContext.Create();
      var template = AirlockRunner.GetEmbeddedTemplate()
        ?? throw new InvalidOperationException("airlock compose template missing");

      // Synthetic processed network config — empty allowlist is fine for the
      // smoke test because the workload only talks to the proxy on the
      // internal network. The proxy still enforces its own rules.
      var rulesPath = Path.Combine(tempProjectDir, "rules.json");
      File.WriteAllText(rulesPath, "{\"enabled\":true,\"mode\":\"enforce\",\"allowed_rules\":[]}");

      composeFile = AirlockRunner.GenerateComposeFile(
        rt,
        ctx,
        template,
        projectName,
        appImage,
        proxyImage,
        rulesPath,
        externalNetwork: rt.DefaultNetworkName,
        appSandboxFlags: [],
        mounts: [],
        toolArgs: ["sleep", "3600"],
        isYolo: false,
        broker)
        ?? throw new InvalidOperationException("compose generation failed");

      // For the test we need a long-running workload. The airlock template's
      // entrypoint runs the appuser setup then execs whatever toolArgs is —
      // we passed "sleep 3600" so the container stays up for the duration.

      var upResult = DockerHelper.RunWithTimeout(
        TimeSpan.FromMinutes(3),
        "compose", "-f", composeFile, "-p", projectName, "up", "-d", "--wait");
      await Assert.That(upResult.Succeeded).IsTrue()
        .Because($"docker compose up failed: stdout={upResult.Stdout} stderr={upResult.Stderr}");

      var workloadContainer = $"{projectName}-app";

      // Wait for entrypoint to finish setting up appuser.
      for (int i = 0; i < 30; i++)
      {
        var ready = DockerHelper.Run("exec", workloadContainer, "id", "appuser");
        if (ready.Succeeded) break;
        Thread.Sleep(1000);
      }

      // Step 1: workload can reach the broker via the proxy bridge.
      var versionResult = DockerHelper.Run(
        "exec", "-u", "appuser", workloadContainer,
        "docker", "version", "--format", "{{.Server.Version}}");
      await Assert.That(versionResult.Succeeded).IsTrue()
        .Because($"docker version through proxy bridge failed: stderr={versionResult.Stderr}");
      await Assert.That(versionResult.Stdout.Trim()).IsNotEmpty();

      // Step 2: workload can spawn a sibling AND see its output.
      var runResult = DockerHelper.Run(
        "exec", "-u", "appuser", workloadContainer,
        "docker", "run", "--rm", "alpine:3.21", "echo", "from-airlocked-sibling");
      await Assert.That(runResult.Succeeded).IsTrue()
        .Because($"sibling docker run failed: stderr={runResult.Stderr}");
      var combinedOut = runResult.Stdout + runResult.Stderr;
      await Assert.That(combinedOut).Contains("from-airlocked-sibling");

      // Step 3: the broker actually injected NetworkMode into the create body.
      var entries = File.Exists(logPath) ? File.ReadAllLines(logPath) : [];
      var rewriteEntries = entries.Where(e =>
        e.Contains("\"action\":\"REWRITE\"") &&
        e.Contains("/containers/create") &&
        e.Contains("NetworkMode")).ToList();
      await Assert.That(rewriteEntries.Count).IsGreaterThan(0)
        .Because($"expected at least one REWRITE log entry with NetworkMode injection. Log:\n{string.Join('\n', entries)}");
    }
    finally
    {
      try
      {
        if (composeFile is not null)
        {
          DockerHelper.RunWithTimeout(
            TimeSpan.FromMinutes(2),
            "compose", "-f", composeFile, "-p", projectName, "down", "--volumes", "--remove-orphans");
          try { File.Delete(composeFile); } catch { /* best effort */ }
        }
      }
      finally
      {
        Directory.SetCurrentDirectory(origCwd);
        try { Directory.Delete(tempProjectDir, recursive: true); } catch { /* best effort */ }
        try { File.Delete(logPath); } catch { /* best effort */ }
      }
    }
  }
}
