using CopilotHere.Commands.DockerBroker;
using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;
using CopilotHere.Infrastructure;
using TUnit.Core;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Tests;

/// <summary>
/// Verifies that BuildDockerArgs wires the brokered Docker socket into the
/// container's docker run invocation when --dind is active.
/// </summary>
[NotInParallel]
public class DindArgsTests
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

  private static DockerSocketBroker MakeUnixBroker(string sockPath)
  {
    var cfg = DockerBrokerConfig.CreateDefault(enabled: true);
    return new DockerSocketBroker(cfg, "/var/run/docker.sock", BrokerListenEndpoint.Unix(sockPath));
  }

  [Test]
  public async Task BuildDockerArgs_WithoutBroker_DoesNotMountDockerSocket()
  {
    var ctx = AppContext.Create();
    var args = RunCommand.BuildDockerArgs(
      ctx,
      "ghcr.io/gordonbeeming/copilot_here:latest",
      "copilot_here-test",
      mounts: [],
      toolCommand: ["copilot"],
      isYolo: false,
      imageTag: "latest",
      noPull: false,
      broker: null);

    // Joined for substring assertions.
    var joined = string.Join(" ", args);
    await Assert.That(joined).DoesNotContain("docker.sock");
    await Assert.That(joined).DoesNotContain("DOCKER_HOST");
    await Assert.That(joined).DoesNotContain("TESTCONTAINERS_HOST_OVERRIDE");
    await Assert.That(joined).DoesNotContain("host.docker.internal");
  }

  [Test]
  public async Task BuildDockerArgs_WithUnixBroker_MountsBrokerSocket()
  {
    var ctx = AppContext.Create();
    var sockPath = Path.Combine(_tempDir, "broker.sock");
    await using var broker = MakeUnixBroker(sockPath);

    var args = RunCommand.BuildDockerArgs(
      ctx,
      "ghcr.io/gordonbeeming/copilot_here:latest",
      "copilot_here-test",
      mounts: [],
      toolCommand: ["copilot"],
      isYolo: false,
      imageTag: "latest",
      noPull: false,
      broker: broker);

    // Mount the broker UDS into the standard /var/run/docker.sock location.
    var mountIdx = args.FindIndex(a => a == $"{sockPath}:/var/run/docker.sock");
    await Assert.That(mountIdx).IsGreaterThanOrEqualTo(0);
    // Mounted with -v
    await Assert.That(args[mountIdx - 1]).IsEqualTo("-v");
  }

  [Test]
  public async Task BuildDockerArgs_WithUnixBroker_SetsDockerHostUnix()
  {
    var ctx = AppContext.Create();
    await using var broker = MakeUnixBroker(Path.Combine(_tempDir, "b.sock"));

    var args = RunCommand.BuildDockerArgs(
      ctx, "img", "name", [], ["copilot"], false, "latest", false, broker);

    var hasDockerHost = args.Any(a => a == "DOCKER_HOST=unix:///var/run/docker.sock");
    await Assert.That(hasDockerHost).IsTrue();
  }

  [Test]
  public async Task BuildDockerArgs_WithBroker_SetsTestcontainersHostOverride()
  {
    var ctx = AppContext.Create();
    await using var broker = MakeUnixBroker(Path.Combine(_tempDir, "b.sock"));

    var args = RunCommand.BuildDockerArgs(
      ctx, "img", "name", [], ["copilot"], false, "latest", false, broker);

    var hasOverride = args.Any(a => a == "TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal");
    await Assert.That(hasOverride).IsTrue();
  }

  [Test]
  public async Task BuildDockerArgs_WithBroker_AddsHostGatewayMapping()
  {
    var ctx = AppContext.Create();
    await using var broker = MakeUnixBroker(Path.Combine(_tempDir, "b.sock"));

    var args = RunCommand.BuildDockerArgs(
      ctx, "img", "name", [], ["copilot"], false, "latest", false, broker);

    var hostIdx = args.FindIndex(a => a == "host.docker.internal:host-gateway");
    await Assert.That(hostIdx).IsGreaterThanOrEqualTo(0);
    await Assert.That(args[hostIdx - 1]).IsEqualTo("--add-host");
  }
}
