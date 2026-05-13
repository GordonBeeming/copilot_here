using System.CommandLine;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetShowDockerBrokerStatusCommand()
  {
    var command = new Command("--show-docker-broker-status", "Show Docker broker runtime status (bind address, reachability, tips)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var runtimeConfig = ContainerRuntimeConfig.Load(paths);
      var bindAddress = BrokerBindResolver.ResolveTcpBindAddress();
      var hostSocket = runtimeConfig.ResolveHostRuntimeSocket();

      Console.WriteLine("🔎 Docker Broker Status [BETA]");
      Console.WriteLine("===============================");
      Console.WriteLine();
      Console.WriteLine($"  Runtime:        {runtimeConfig.RuntimeFlavor} ({DescribePlatform()})");
      Console.WriteLine($"  Upstream:       {hostSocket ?? "<not detected — set DOCKER_HOST or start your runtime>"}");

      if (OperatingSystem.IsLinux())
      {
        Console.WriteLine($"  Listen:         /tmp/copilot-broker-<sessionId>.sock (Unix Domain Socket)");
        Console.WriteLine($"  Reachable as:   unix:///var/run/docker.sock (inside workload, bind-mounted)");
        Console.WriteLine($"  DOCKER_HOST:    unix:///var/run/docker.sock");
      }
      else
      {
        var bindLabel = IPAddress.IsLoopback(bindAddress) ? "Loopback" : "Any";
        Console.WriteLine($"  Listen:         {bindAddress}:<ephemeral> ({bindLabel})");
        Console.WriteLine($"  Reachable as:   tcp://host.docker.internal:<port> (inside workload, via --add-host host-gateway)");
        Console.WriteLine($"  DOCKER_HOST:    tcp://host.docker.internal:<port>");
        Console.WriteLine();
        Console.WriteLine("  Local IPs:");
        foreach (var ip in EnumerateLocalIPv4())
        {
          Console.WriteLine($"    • {ip}");
        }
      }

      Console.WriteLine();
      Console.WriteLine("  Env:");
      Console.WriteLine($"    {BrokerBindResolver.BindLoopbackEnvVar} = {Environment.GetEnvironmentVariable(BrokerBindResolver.BindLoopbackEnvVar) ?? "<unset>"}");

      Console.WriteLine();
      Console.WriteLine("  Tips:");
      if (OperatingSystem.IsLinux())
      {
        Console.WriteLine("    • Linux uses a UDS — gvproxy / VirtioFS issues do not apply.");
        Console.WriteLine("    • Socket is permission-restricted to your user; the container runs with the same UID/GID via PUID/PGID.");
      }
      else
      {
        Console.WriteLine("    • host.docker.internal in the workload is wired via --add-host host-gateway. On runtimes without native host.docker.internal (Linux Docker, Podman + gvproxy on Windows/macOS) that resolves to a non-loopback bridge/proxy IP, so the broker must bind 0.0.0.0 (default).");
        Console.WriteLine("    • Docker Desktop provides host.docker.internal natively; binding 0.0.0.0 stays compatible.");
        Console.WriteLine($"    • To restrict the broker to 127.0.0.1 only, set {BrokerBindResolver.BindLoopbackEnvVar}=1 — fine on Docker Desktop, breaks Podman and Linux Docker.");
        Console.WriteLine("    • If the workload still cannot reach the broker, check the host firewall for the ephemeral broker port.");
      }
      Console.WriteLine("    • Use --show-docker-broker-rules to inspect the allowlist that gates every API call.");

      return 0;
    });
    return command;
  }

  private static string DescribePlatform()
  {
    if (OperatingSystem.IsWindows()) return "Windows";
    if (OperatingSystem.IsMacOS()) return "macOS";
    if (OperatingSystem.IsLinux()) return "Linux";
    return "Unknown";
  }

  private static IEnumerable<string> EnumerateLocalIPv4()
  {
    string[]? results = null;
    try
    {
      results = NetworkInterface.GetAllNetworkInterfaces()
        .Where(n => n.OperationalStatus == OperationalStatus.Up)
        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
        .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(a => a.Address.ToString())
        .Distinct()
        .ToArray();
    }
    catch
    {
      // Enumerating interfaces can fail on locked-down systems; fall through to the placeholder.
    }

    if (results is null || results.Length == 0)
    {
      return ["<unable to enumerate interfaces>"];
    }
    return results;
  }
}
