using System.Net;
using CopilotHere.Commands.DockerBroker;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.IntegrationTests;

/// <summary>
/// End-to-end smoke tests for the standard --dind path: real broker, real
/// Docker daemon, real workload container. These tests boot a long-lived
/// alpine container with the broker socket pointed at via DOCKER_HOST, then
/// exec docker subcommands inside it. The broker logs every decision so we
/// can assert which paths actually got traversed.
///
/// Tests are gated by [LiveDockerTest] so they only run when
/// RUN_LIVE_DOCKER_TESTS=1 (CI) and the daemon is reachable. A regular
/// developer doing `dotnet test` from the unit-test project never sees them.
///
/// Image: docker:cli — the official, alpine-based, ~30 MB image that ships
/// just the docker CLI. Avoids depending on the copilot_here app images so
/// these tests run independently of the build pipeline.
/// </summary>
public class BrokerSmokeTests
{
  private const string TestImage = "docker:cli";

  private static (DockerSocketBroker Broker, int Port, string LogPath) StartBroker(string testName)
  {
    var rt = ContainerRuntimeConfig.CreateConfig("docker");
    var hostSocket = rt.ResolveHostRuntimeSocket()
      ?? throw new InvalidOperationException("Could not locate host docker socket — DOCKER_HOST not set and no default socket found");

    var rules = DockerBrokerConfigLoader.LoadDefaultRules()
      ?? throw new InvalidOperationException("Default broker rules missing from embedded resources");
    rules.EnableLogging = true;

    var logPath = Path.Combine(Path.GetTempPath(), $"copilot-broker-it-{testName}-{Guid.NewGuid():N}.jsonl");
    var listen = BrokerListenEndpoint.Tcp(IPAddress.Loopback, 0);
    var broker = new DockerSocketBroker(rules, hostSocket, listen, logPath);
    broker.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

    return (broker, broker.BoundTcpEndpoint!.Port, logPath);
  }

  private static string RunWorkload(int brokerPort, string testName)
  {
    var containerName = $"copilot-broker-it-{testName}-{Guid.NewGuid():N}";
    DockerHelper.ForceRemove(containerName);

    var startResult = DockerHelper.Run(
      "run", "-d", "--rm", "--name", containerName,
      "-e", $"DOCKER_HOST=tcp://host.docker.internal:{brokerPort}",
      "--add-host", "host.docker.internal:host-gateway",
      TestImage,
      "sleep", "3600");

    if (!startResult.Succeeded)
    {
      throw new InvalidOperationException(
        $"Failed to start workload container: exit={startResult.ExitCode}\nstderr={startResult.Stderr}");
    }

    return containerName;
  }

  private static string[] BrokerLog(string logPath)
  {
    if (!File.Exists(logPath)) return [];
    return File.ReadAllLines(logPath);
  }

  [Test]
  public async Task DockerVersion_Succeeds_AndBrokerLogsAllow()
  {
    if (!LiveDockerTest.ShouldRun) return;
    var (broker, port, logPath) = StartBroker(nameof(DockerVersion_Succeeds_AndBrokerLogsAllow));
    string? containerName = null;
    try
    {
      containerName = RunWorkload(port, "ver");

      var versionResult = DockerHelper.Run(
        "exec", containerName,
        "docker", "version", "--format", "{{.Server.Version}}");

      await Assert.That(versionResult.Succeeded).IsTrue().Because($"docker version failed: stderr={versionResult.Stderr}");
      await Assert.That(versionResult.Stdout.Trim()).IsNotEmpty();

      var entries = BrokerLog(logPath);
      await Assert.That(entries.Length).IsGreaterThan(0);
      await Assert.That(entries.Any(e => e.Contains("\"action\":\"ALLOW\"") && e.Contains("/version"))).IsTrue();
    }
    finally
    {
      if (containerName is not null) DockerHelper.ForceRemove(containerName);
      await broker.DisposeAsync();
      try { File.Delete(logPath); } catch { /* best effort */ }
    }
  }

  [Test]
  public async Task DockerRunAlpine_Succeeds_AndBrokerLogsCreate()
  {
    if (!LiveDockerTest.ShouldRun) return;
    var (broker, port, logPath) = StartBroker(nameof(DockerRunAlpine_Succeeds_AndBrokerLogsCreate));
    string? containerName = null;
    try
    {
      containerName = RunWorkload(port, "run");

      var runResult = DockerHelper.Run(
        "exec", containerName,
        "docker", "run", "--rm", "alpine:3.21", "echo", "hello-from-sibling");

      await Assert.That(runResult.Succeeded).IsTrue().Because($"docker run failed: stdout={runResult.Stdout} stderr={runResult.Stderr}");
      var combined = runResult.Stdout + runResult.Stderr;
      await Assert.That(combined).Contains("hello-from-sibling");

      var entries = BrokerLog(logPath);
      // Must see /containers/create traverse the broker
      await Assert.That(entries.Any(e => e.Contains("/containers/create"))).IsTrue();
      // …and an attach (Phase 1 added the rule, this validates it still works)
      await Assert.That(entries.Any(e => e.Contains("attach"))).IsTrue();
    }
    finally
    {
      if (containerName is not null) DockerHelper.ForceRemove(containerName);
      await broker.DisposeAsync();
      try { File.Delete(logPath); } catch { /* best effort */ }
    }
  }

  [Test]
  public async Task PrivilegedContainer_IsRejectedByBodyInspection()
  {
    if (!LiveDockerTest.ShouldRun) return;
    var (broker, port, logPath) = StartBroker(nameof(PrivilegedContainer_IsRejectedByBodyInspection));
    string? containerName = null;
    try
    {
      containerName = RunWorkload(port, "priv");

      var runResult = DockerHelper.Run(
        "exec", containerName,
        "docker", "run", "--rm", "--privileged", "alpine:3.21", "echo", "should-not-run");

      await Assert.That(runResult.Succeeded).IsFalse();
      // The broker emits "blocked by copilot_here docker broker: HostConfig.Privileged ..."
      // The docker CLI surfaces this in its stderr.
      var combined = runResult.Stdout + runResult.Stderr;
      await Assert.That(combined).Contains("Privileged");

      var entries = BrokerLog(logPath);
      await Assert.That(entries.Any(e => e.Contains("\"action\":\"BLOCK\"") && e.Contains("Privileged"))).IsTrue();
    }
    finally
    {
      if (containerName is not null) DockerHelper.ForceRemove(containerName);
      await broker.DisposeAsync();
      try { File.Delete(logPath); } catch { /* best effort */ }
    }
  }

  [Test]
  public async Task HostBindMount_IsRejectedByBodyInspection()
  {
    if (!LiveDockerTest.ShouldRun) return;
    var (broker, port, logPath) = StartBroker(nameof(HostBindMount_IsRejectedByBodyInspection));
    string? containerName = null;
    try
    {
      containerName = RunWorkload(port, "bind");

      var runResult = DockerHelper.Run(
        "exec", containerName,
        "docker", "run", "--rm", "-v", "/etc:/host-etc", "alpine:3.21", "echo", "should-not-run");

      await Assert.That(runResult.Succeeded).IsFalse();

      var entries = BrokerLog(logPath);
      await Assert.That(entries.Any(e => e.Contains("\"action\":\"BLOCK\"") && e.Contains("forbidden"))).IsTrue();
    }
    finally
    {
      if (containerName is not null) DockerHelper.ForceRemove(containerName);
      await broker.DisposeAsync();
      try { File.Delete(logPath); } catch { /* best effort */ }
    }
  }
}
