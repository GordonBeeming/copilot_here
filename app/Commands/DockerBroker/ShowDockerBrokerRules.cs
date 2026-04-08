using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetShowDockerBrokerRulesCommand()
  {
    var command = new Command("--show-docker-broker-rules", "Show current Docker broker rules");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();

      Console.WriteLine("📋 Docker Broker Rules [BETA]");
      Console.WriteLine("=============================");
      Console.WriteLine();
      Console.WriteLine("The host process brokers every Docker API call from the workload");
      Console.WriteLine("container. Endpoints listed below are forwarded to the host runtime;");
      Console.WriteLine("everything else is denied with a 403 in 'enforce' mode, or logged");
      Console.WriteLine("only in 'monitor' mode.");
      Console.WriteLine();

      var defaults = DockerBrokerConfigLoader.LoadDefaultRules();
      if (defaults is not null)
      {
        Console.WriteLine("📦 Default Rules (embedded):");
        Console.WriteLine($"   mode={defaults.Mode}, allowed_endpoints={defaults.AllowedEndpoints.Count}");
        foreach (var endpoint in defaults.AllowedEndpoints)
        {
          Console.WriteLine($"   {endpoint.Method,-7} {endpoint.Path}");
        }
        Console.WriteLine();
      }

      var globalRulesPath = DockerBrokerConfigLoader.GetGlobalRulesPath(paths);
      if (File.Exists(globalRulesPath))
      {
        Console.WriteLine("🌍 Global Config:");
        Console.WriteLine($"   {globalRulesPath}");
        foreach (var line in File.ReadAllLines(globalRulesPath))
        {
          Console.WriteLine($"   {line}");
        }
        Console.WriteLine();
      }
      else
      {
        Console.WriteLine("🌍 Global Config: Not configured");
        Console.WriteLine();
      }

      var localRulesPath = DockerBrokerConfigLoader.GetLocalRulesPath(paths);
      if (File.Exists(localRulesPath))
      {
        Console.WriteLine("📁 Local Config:");
        Console.WriteLine($"   {localRulesPath}");
        foreach (var line in File.ReadAllLines(localRulesPath))
        {
          Console.WriteLine($"   {line}");
        }
        Console.WriteLine();
      }
      else
      {
        Console.WriteLine("📁 Local Config: Not configured");
        Console.WriteLine();
      }

      return 0;
    });
    return command;
  }
}
