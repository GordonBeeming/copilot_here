using System.CommandLine;
using CopilotHere.Infrastructure;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Runtime;

public sealed partial class RuntimeCommands
{
  /// <summary>
  /// Shows the currently active container runtime.
  /// </summary>
  private static Command CreateShowRuntimeCommand()
  {
    var command = new Command("--show-runtime", "Show current container runtime configuration");
    
    command.SetAction(_ =>
    {
      var ctx = AppContext.Create();
      var runtime = ctx.RuntimeConfig;
      
      var supportsVariant = !OperatingSystem.IsWindows();
      
      Console.WriteLine($"🐳 Container Runtime: {runtime.RuntimeFlavor}");
      Console.WriteLine($"   Command: {runtime.Runtime}");
      Console.WriteLine($"   Version: {runtime.GetVersion()}");
      Console.WriteLine();
      
      // Show configuration source
      var sourceLabel = runtime.Source switch
      {
        RuntimeConfigSource.Local => $"{Emoji.Local(supportsVariant)} Local (.copilot_here/runtime.conf)",
        RuntimeConfigSource.Global => $"{Emoji.Global(supportsVariant)} Global (~/.config/copilot_here/runtime.conf)",
        RuntimeConfigSource.AutoDetected => $"{Emoji.Info(supportsVariant)} Auto-detected (no config file)",
        _ => "Unknown"
      };
      Console.WriteLine($"   Source: {sourceLabel}");
      
      // Show config values if they exist
      if (runtime.LocalRuntime is not null)
      {
        Console.WriteLine($"   {Emoji.Local(supportsVariant)} Local config: {runtime.LocalRuntime}");
      }
      if (runtime.GlobalRuntime is not null)
      {
        Console.WriteLine($"   {Emoji.Global(supportsVariant)} Global config: {runtime.GlobalRuntime}");
      }
      
      // Show capabilities
      Console.WriteLine();
      Console.WriteLine("   Capabilities:");
      Console.WriteLine($"     • Compose command: {runtime.Runtime} {runtime.ComposeCommand}");
      Console.WriteLine($"     • Airlock support: {(runtime.SupportsAirlock ? "Yes" : "No")}");
      Console.WriteLine($"     • Default network: {runtime.DefaultNetworkName}");
      
      // Show all available runtimes
      var available = ContainerRuntimeConfig.ListAvailable();
      if (available.Count > 0)
      {
        Console.WriteLine();
        Console.WriteLine("   Available runtimes:");
        foreach (var rt in available)
        {
          var marker = rt.Runtime == runtime.Runtime ? "▶" : " ";
          Console.WriteLine($"   {marker} {rt.RuntimeFlavor} ({rt.Runtime})");
        }
      }
      
      return 0;
    });
    
    return command;
  }
}
