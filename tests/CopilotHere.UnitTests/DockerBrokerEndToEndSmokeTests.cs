using System.Diagnostics;
using CopilotHere.Commands.DockerBroker;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Live end-to-end smoke tests for the Docker socket broker. These actually
/// boot the broker against the host's Docker daemon and run real `docker`
/// commands inside an alpine container that mounts the broker socket. They
/// only run when Docker is reachable on the host, so CI on machines without
/// Docker silently skips them.
/// </summary>
[NotInParallel]
public class DockerBrokerEndToEndSmokeTests
{
  private static bool DockerAvailable()
  {
    try
    {
      var psi = new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };
      using var p = Process.Start(psi);
      if (p is null) return false;
      p.WaitForExit(5000);
      return p.ExitCode == 0;
    }
    catch
    {
      return false;
    }
  }

  private static (int ExitCode, string Stdout, string Stderr) RunDocker(params string[] args)
  {
    var psi = new ProcessStartInfo("docker")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };
    foreach (var a in args) psi.ArgumentList.Add(a);

    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEnd();
    var stderr = p.StandardError.ReadToEnd();
    p.WaitForExit();
    return (p.ExitCode, stdout, stderr);
  }

  [Test]
  public async Task Broker_HostCli_VersionForwardsToRealDaemon()
  {
    if (!DockerAvailable())
    {
      Console.WriteLine("[skip] Docker not reachable on host");
      return;
    }

    // /tmp keeps us under the macOS 104-char UDS limit and ensures the path
    // is shareable into containers via OrbStack/Docker Desktop file mounts.
    var sockPath = $"/tmp/cb-{Guid.NewGuid():N}.sock";

    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await Assert.That(defaults).IsNotNull();

    await using var broker = new DockerSocketBroker(defaults!, "/var/run/docker.sock", BrokerListenEndpoint.Unix(sockPath));
    await broker.StartAsync(CancellationToken.None);

    // Talk to the broker from the host's docker CLI.
    var (code, stdout, stderr) = RunDocker("--host", $"unix://{sockPath}", "version");
    Console.WriteLine($"[smoke] exit={code}");
    Console.WriteLine($"[smoke] stdout=\n{stdout}");
    if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"[smoke] stderr=\n{stderr}");

    await Assert.That(code).IsEqualTo(0);
    await Assert.That(stdout).Contains("Server:");
  }

  [Test]
  public async Task Broker_HostCli_DeniedEndpointReturns403()
  {
    if (!DockerAvailable())
    {
      Console.WriteLine("[skip] Docker not reachable on host");
      return;
    }

    // /tmp keeps us under the macOS 104-char UDS limit and ensures the path
    // is shareable into containers via OrbStack/Docker Desktop file mounts.
    var sockPath = $"/tmp/cb-{Guid.NewGuid():N}.sock";

    // Tight allowlist that explicitly does NOT include /info.
    var rules = new DockerBrokerConfig
    {
      Enabled = true,
      EnableLogging = true,
      Mode = "enforce",
      AllowedEndpoints =
      [
        new DockerBrokerEndpoint { Method = "GET", Path = "/_ping" },
        new DockerBrokerEndpoint { Method = "GET", Path = "/version" }
      ]
    };

    var logPath = $"/tmp/cb-deny-{Guid.NewGuid():N}.log";

    await using var broker = new DockerSocketBroker(rules, "/var/run/docker.sock", BrokerListenEndpoint.Unix(sockPath), logPath);
    await broker.StartAsync(CancellationToken.None);

    // `docker info` calls GET /info — not in our allowlist, must be denied.
    // We avoid `docker events` because it hangs in some configurations.
    var (code, stdout, stderr) = RunDocker("--host", $"unix://{sockPath}", "info");
    Console.WriteLine($"[smoke-deny] exit={code}");
    Console.WriteLine($"[smoke-deny] stdout=\n{stdout}");
    Console.WriteLine($"[smoke-deny] stderr=\n{stderr}");
    if (File.Exists(logPath))
    {
      Console.WriteLine($"[smoke-deny] broker log:\n{File.ReadAllText(logPath)}");
      File.Delete(logPath);
    }
    else
    {
      Console.WriteLine($"[smoke-deny] broker log file not created at {logPath}");
    }

    await Assert.That(code).IsNotEqualTo(0);
    await Assert.That(stderr).Contains("blocked by copilot_here docker broker");
  }

  [Test]
  public async Task Broker_FromInsideContainer_TcpLoopback_ForwardsToHostDaemon()
  {
    // This is the production path on macOS and Windows: the broker listens on
    // TCP loopback, the workload container reaches it via host.docker.internal.
    // UDS bind-mounting an arbitrary host socket into a Linux container does
    // not work on macOS/Windows because the container runtime (OrbStack,
    // Docker Desktop) runs containers in a Linux VM and its file-sharing layer
    // does not proxy connect() calls on UDS files back to the macOS/Windows
    // host. TCP sidesteps that entirely.
    if (!DockerAvailable())
    {
      Console.WriteLine("[skip] Docker not reachable on host");
      return;
    }

    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await Assert.That(defaults).IsNotNull();

    await using var broker = new DockerSocketBroker(
      defaults!,
      "/var/run/docker.sock",
      BrokerListenEndpoint.Tcp(System.Net.IPAddress.Loopback, 0));
    await broker.StartAsync(CancellationToken.None);

    var port = broker.BoundTcpEndpoint!.Port;

    var (code, stdout, stderr) = RunDocker(
      "run", "--rm",
      "--add-host", "host.docker.internal:host-gateway",
      "-e", $"DOCKER_HOST=tcp://host.docker.internal:{port}",
      "docker:28-cli",
      "docker", "version");

    Console.WriteLine($"[smoke-container-tcp] exit={code}");
    Console.WriteLine($"[smoke-container-tcp] stdout=\n{stdout}");
    Console.WriteLine($"[smoke-container-tcp] stderr=\n{stderr}");

    await Assert.That(code).IsEqualTo(0);
    await Assert.That(stdout).Contains("Server:");
  }

  [Test]
  public async Task Broker_FromInsideContainer_TcpLoopback_RealTestcontainersStyleRequest()
  {
    // Reproduces the failure mode Gordon hit: a container inside copilot_here
    // using docker-cli to reach the host daemon via the broker. Listing the
    // host's images is the cheapest API call that exercises a real Docker REST
    // endpoint (GET /images/json) so we can confirm forwarding works for the
    // shape of calls Testcontainers makes.
    if (!DockerAvailable())
    {
      Console.WriteLine("[skip] Docker not reachable on host");
      return;
    }

    var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
    await using var broker = new DockerSocketBroker(
      defaults!,
      "/var/run/docker.sock",
      BrokerListenEndpoint.Tcp(System.Net.IPAddress.Loopback, 0));
    await broker.StartAsync(CancellationToken.None);

    var port = broker.BoundTcpEndpoint!.Port;

    var (code, stdout, stderr) = RunDocker(
      "run", "--rm",
      "--add-host", "host.docker.internal:host-gateway",
      "-e", $"DOCKER_HOST=tcp://host.docker.internal:{port}",
      "docker:28-cli",
      "docker", "image", "ls", "--format", "{{.Repository}}");

    Console.WriteLine($"[smoke-images-tcp] exit={code}");
    Console.WriteLine($"[smoke-images-tcp] stdout=\n{stdout}");
    Console.WriteLine($"[smoke-images-tcp] stderr=\n{stderr}");

    await Assert.That(code).IsEqualTo(0);
    // Should list at least one image (we just pulled docker:28-cli, alpine, etc.).
    await Assert.That(stdout.Trim().Length).IsGreaterThan(0);
  }
}
