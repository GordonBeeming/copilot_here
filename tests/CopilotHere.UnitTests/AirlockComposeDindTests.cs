using CopilotHere.Commands.DockerBroker;
using CopilotHere.Infrastructure;
using TUnit.Core;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Tests;

/// <summary>
/// Verifies that the docker-compose airlock template substitutes the broker
/// placeholders correctly when --dind is active and removes them otherwise.
/// </summary>
[NotInParallel]
public class AirlockComposeDindTests
{
  private string _tempDir = null!;
  private string _originalWorkingDir = null!;
  private string _testProjectDir = null!;
  private string _testHomeDir = null!;
  private string? _originalHome;

  [Before(Test)]
  public void Setup()
  {
    _originalWorkingDir = Directory.GetCurrentDirectory();
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    _testProjectDir = Path.Combine(_tempDir, "project");
    _testHomeDir = Path.Combine(_tempDir, "home");

    Directory.CreateDirectory(_tempDir);
    Directory.CreateDirectory(_testProjectDir);
    Directory.CreateDirectory(_testHomeDir);

    _originalHome = Environment.GetEnvironmentVariable("HOME");
    Environment.SetEnvironmentVariable("HOME", _testHomeDir);
    if (OperatingSystem.IsWindows())
    {
      Environment.SetEnvironmentVariable("USERPROFILE", _testHomeDir);
    }

    Directory.SetCurrentDirectory(_testProjectDir);
  }

  [After(Test)]
  public void Cleanup()
  {
    Environment.SetEnvironmentVariable("HOME", _originalHome);
    if (OperatingSystem.IsWindows())
    {
      Environment.SetEnvironmentVariable("USERPROFILE", _originalHome);
    }

    Directory.SetCurrentDirectory(_originalWorkingDir);

    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  private string GenerateCompose(DockerSocketBroker? broker, bool rootlessPodman = false)
  {
    var ctx = AppContext.Create();
    var runtimeConfig = ContainerRuntimeConfig.CreateConfig("docker");
    var template = AirlockRunner.GetEmbeddedTemplate()
      ?? throw new InvalidOperationException("airlock compose template not embedded");

    var processedConfigPath = Path.Combine(_tempDir, "rules.json");
    File.WriteAllText(processedConfigPath, "{}");

    var composePath = AirlockRunner.GenerateComposeFile(
      runtimeConfig,
      ctx,
      template,
      projectName: "test-project",
      appImage: "ghcr.io/gordonbeeming/copilot_here:latest",
      proxyImage: "ghcr.io/gordonbeeming/copilot_here:proxy",
      processedConfigPath: processedConfigPath,
      externalNetwork: "bridge",
      appSandboxFlags: [],
      mounts: [],
      toolArgs: ["copilot"],
      imageTag: "latest",
      isYolo: false,
      broker: broker,
      rootlessPodman: rootlessPodman)
      ?? throw new InvalidOperationException("GenerateComposeFile returned null");

    var content = File.ReadAllText(composePath);
    File.Delete(composePath);
    return content;
  }

  [Test]
  public async Task GenerateComposeFile_NoBroker_RemovesAllBrokerPlaceholders()
  {
    var content = GenerateCompose(broker: null);

    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_MOUNT}}");
    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_ENV}}");
    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_EXTRA_HOSTS}}");

    // No DinD-specific values should leak in either.
    await Assert.That(content).DoesNotContain("docker.sock");
    await Assert.That(content).DoesNotContain("DOCKER_HOST");
    await Assert.That(content).DoesNotContain("host.docker.internal");
  }

  [Test]
  public async Task GenerateComposeFile_WithUnixBroker_SubstitutesBrokerSocketMount()
  {
    var sockPath = Path.Combine(_tempDir, "broker.sock");
    var brokerCfg = DockerBrokerConfig.CreateDefault(enabled: true);
    await using var broker = new DockerSocketBroker(brokerCfg, "/var/run/docker.sock", BrokerListenEndpoint.Unix(sockPath));

    var content = GenerateCompose(broker: broker);

    await Assert.That(content).Contains($"{sockPath}:/var/run/docker.sock");
    await Assert.That(content).Contains("DOCKER_HOST=unix:///var/run/docker.sock");
    await Assert.That(content).Contains("TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal");
    await Assert.That(content).Contains("host.docker.internal:host-gateway");
  }

  [Test]
  public async Task GenerateComposeFile_WithUnixBroker_RemovesPlaceholderTokens()
  {
    var sockPath = Path.Combine(_tempDir, "broker.sock");
    var brokerCfg = DockerBrokerConfig.CreateDefault(enabled: true);
    await using var broker = new DockerSocketBroker(brokerCfg, "/var/run/docker.sock", BrokerListenEndpoint.Unix(sockPath));

    var content = GenerateCompose(broker: broker);

    // Substitutions complete: no raw placeholders remain.
    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_MOUNT}}");
    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_ENV}}");
    await Assert.That(content).DoesNotContain("{{DOCKER_BROKER_EXTRA_HOSTS}}");
    await Assert.That(content).DoesNotContain("{{PROXY_BROKER_ENV}}");
    await Assert.That(content).DoesNotContain("{{PROXY_BROKER_EXTRA_HOSTS}}");
    // No proxy-side bridge for the UDS path — bind mount handles it directly.
    await Assert.That(content).DoesNotContain("BROKER_BRIDGE_TARGET");
  }

  [Test]
  public async Task GenerateComposeFile_WithTcpBroker_AddsProxySideBridgeAndDockerHost()
  {
    // The TCP path is what macOS / Windows / CI use. We can't actually bind
    // a TCP listener inside a unit test reliably, but we can construct the
    // broker object with a synthetic bound endpoint by reaching through the
    // public ctor + StartAsync; we then immediately dispose the listener and
    // pass the broker to GenerateComposeFile, which only reads BoundTcpEndpoint.
    var brokerCfg = DockerBrokerConfig.CreateDefault(enabled: true);
    await using var broker = new DockerSocketBroker(
      brokerCfg,
      "/var/run/docker.sock",
      BrokerListenEndpoint.Tcp(System.Net.IPAddress.Loopback, 0));
    await broker.StartAsync(CancellationToken.None);

    var content = GenerateCompose(broker: broker);

    var port = broker.BoundTcpEndpoint!.Port;

    // Workload talks to the proxy directly over TCP — no host.docker.internal in DOCKER_HOST.
    await Assert.That(content).Contains("DOCKER_HOST=tcp://proxy:2375");

    // Proxy gets the bridge target env so its socat sidecar starts up.
    await Assert.That(content).Contains($"BROKER_BRIDGE_TARGET=host.docker.internal:{port}");

    // Proxy gets host.docker.internal:host-gateway so socat can resolve the host gateway.
    // (The workload also gets it via brokerExtraHosts for Testcontainers reachability.)
    await Assert.That(content).Contains("host.docker.internal:host-gateway");

    // Workload bypasses the airlock HTTP proxy for the daemon socket.
    await Assert.That(content).Contains("NO_PROXY=proxy,localhost,127.0.0.1");

    // No raw placeholders left over.
    await Assert.That(content).DoesNotContain("{{PROXY_BROKER_ENV}}");
    await Assert.That(content).DoesNotContain("{{PROXY_BROKER_EXTRA_HOSTS}}");
  }

  [Test]
  public async Task GenerateComposeFile_NotRootless_RemovesUsernsPlaceholder()
  {
    var content = GenerateCompose(broker: null);

    await Assert.That(content).DoesNotContain("{{USERNS}}");
    await Assert.That(content).DoesNotContain("userns_mode");
    await Assert.That(content).DoesNotContain("keep-id");
  }

  [Test]
  public async Task GenerateComposeFile_RootlessPodman_AddsKeepIdUserns()
  {
    // #119: keep-id remaps the host UID/GID into the rootless-Podman container so
    // appuser owns the bind mounts; user 0:0 keeps the entrypoint running as root.
    var ctx = AppContext.Create();
    var content = GenerateCompose(broker: null, rootlessPodman: true);

    await Assert.That(content).DoesNotContain("{{USERNS}}");
    await Assert.That(content).Contains("user: \"0:0\"");
    await Assert.That(content)
      .Contains($"userns_mode: \"keep-id:uid={ctx.Environment.UserId},gid={ctx.Environment.GroupId}\"");
  }
}
