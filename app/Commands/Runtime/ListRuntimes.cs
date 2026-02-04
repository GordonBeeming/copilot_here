using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Runtime;

public sealed partial class RuntimeCommands
{
  /// <summary>
  /// Lists all available container runtimes.
  /// </summary>
  private static Command CreateListRuntimesCommand()
  {
    var command = new Command("--list-runtimes", "List all available container runtimes");
    
    command.SetAction(_ =>
    {
      Console.WriteLine("🐳 Available Container Runtimes:");
      Console.WriteLine();
      
      var available = ContainerRuntimeConfig.ListAvailable();
      
      if (available.Count == 0)
      {
        Console.WriteLine("   No container runtimes found.");
        Console.WriteLine();
        Console.WriteLine("   Please install Docker or Podman:");
        Console.WriteLine("     • Docker: https://docs.docker.com/get-docker/");
        Console.WriteLine("     • OrbStack (macOS): https://orbstack.dev/");
        Console.WriteLine("     • Podman: https://podman.io/getting-started/installation");
        return 1;
      }
      
      foreach (var runtime in available)
      {
        Console.WriteLine($"   {runtime.RuntimeFlavor}");
        Console.WriteLine($"     Command: {runtime.Runtime}");
        Console.WriteLine($"     Version: {runtime.GetVersion()}");
        Console.WriteLine($"     Compose: {runtime.Runtime} {runtime.ComposeCommand}");
        Console.WriteLine($"     Airlock: {(runtime.SupportsAirlock ? "Supported" : "Not supported")}");
        Console.WriteLine($"     Default network: {runtime.DefaultNetworkName}");
        Console.WriteLine();
        Console.WriteLine($"     💡 Switch to this runtime:");
        Console.WriteLine($"        copilot_here --set-runtime {runtime.Runtime}           (local)");
        Console.WriteLine($"        copilot_here --set-runtime-global {runtime.Runtime}    (global)");
        Console.WriteLine();
      }
      
      Console.WriteLine("ℹ️  Or use auto-detection:");
      Console.WriteLine("   copilot_here --set-runtime auto           (local)");
      Console.WriteLine("   copilot_here --set-runtime-global auto    (global)");
      
      return 0;
    });
    
    return command;
  }
}
