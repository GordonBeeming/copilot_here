using System.CommandLine;
using CopilotHere.Infrastructure;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Tool;

public sealed partial class ToolCommands
{
  /// <summary>
  /// Shows the currently active CLI tool.
  /// </summary>
  private static Command CreateShowCommand()
  {
    var command = new Command("--show-tool", "Show the currently active CLI tool");
    
    command.SetAction(_ =>
    {
      var ctx = AppContext.Create();
      var tool = ctx.ActiveTool;
      
      Console.WriteLine($"🔧 Active Tool: {tool.Name}");
      Console.WriteLine($"   Display Name: {tool.DisplayName}");
      Console.WriteLine();
      
      // Check where config came from
      var localToolFile = Path.Combine(ctx.Paths.CurrentDirectory, ".cli_mate", "tool.conf");
      var legacyLocalToolFile = Path.Combine(ctx.Paths.CurrentDirectory, ".copilot_here", "tool.conf");
      var globalToolFile = Path.Combine(ctx.Paths.UserHome, ".config", "cli_mate", "tool.conf");
      var legacyGlobalToolFile = Path.Combine(ctx.Paths.UserHome, ".config", "copilot_here", "tool.conf");
      
      if (File.Exists(localToolFile))
      {
        Console.WriteLine($"   Source: Local (.cli_mate/tool.conf)");
      }
      else if (File.Exists(legacyLocalToolFile))
      {
        Console.WriteLine($"   Source: Local (.copilot_here/tool.conf - legacy)");
      }
      else if (File.Exists(globalToolFile))
      {
        Console.WriteLine($"   Source: Global (~/.config/cli_mate/tool.conf)");
      }
      else if (File.Exists(legacyGlobalToolFile))
      {
        Console.WriteLine($"   Source: Global (~/.config/copilot_here/tool.conf - legacy)");
      }
      else
      {
        Console.WriteLine($"   Source: Default");
      }
      
      // Show capabilities
      Console.WriteLine();
      Console.WriteLine("   Capabilities:");
      Console.WriteLine($"     • Models: {(tool.SupportsModels ? "Yes" : "No")}");
      Console.WriteLine($"     • YOLO Mode: {(tool.SupportsYoloMode ? "Yes" : "No")}");
      Console.WriteLine($"     • Interactive: {(tool.SupportsInteractiveMode ? "Yes" : "No")}");
      
      // Show dependencies
      var deps = tool.GetRequiredDependencies();
      if (deps.Length > 0)
      {
        Console.WriteLine();
        Console.WriteLine("   Required Dependencies:");
        foreach (var dep in deps)
        {
          Console.WriteLine($"     • {dep}");
        }
      }
      
      return 0;
    });
    
    return command;
  }
}
