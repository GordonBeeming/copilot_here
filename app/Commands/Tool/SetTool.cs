using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Tool;

public sealed partial class ToolCommands
{
  /// <summary>
  /// Sets the CLI tool globally.
  /// </summary>
  private static Command CreateSetCommand()
  {
    var nameArg = new Argument<string>("name") { Description = "Tool name (e.g., 'github-copilot', 'echo')" };
    var command = new Command("--set-tool", "Set the active CLI tool (global)")
    {
      nameArg
    };
    
    command.SetAction(parseResult =>
    {
      var toolName = parseResult.GetValue(nameArg);
      
      if (string.IsNullOrWhiteSpace(toolName))
      {
        Console.WriteLine("❌ Tool name cannot be empty");
        return 1;
      }
      
      // Validate tool exists
      if (!ToolRegistry.Exists(toolName))
      {
        Console.WriteLine($"❌ Unknown tool: {toolName}");
        Console.WriteLine();
        Console.WriteLine("Available tools:");
        foreach (var name in ToolRegistry.GetToolNames())
        {
          Console.WriteLine($"  • {name}");
        }
        return 1;
      }
      
      // Save to global config
      var globalConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "cli_mate"
      );
      
      Directory.CreateDirectory(globalConfigDir);
      var globalToolFile = Path.Combine(globalConfigDir, "tool.conf");
      
      try
      {
        File.WriteAllText(globalToolFile, toolName);
        Console.WriteLine($"✅ Set global tool to: {toolName}");
        Console.WriteLine($"   Config: ~/.config/cli_mate/tool.conf");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"❌ Failed to save config: {ex.Message}");
        return 1;
      }
      
      return 0;
    });
    
    return command;
  }

  /// <summary>
  /// Sets the CLI tool for the current project only.
  /// </summary>
  private static Command CreateSetLocalCommand()
  {
    var nameArg = new Argument<string>("name") { Description = "Tool name (e.g., 'github-copilot', 'echo')" };
    var command = new Command("--set-tool-local", "Set the active CLI tool (local project)")
    {
      nameArg
    };
    
    command.SetAction(parseResult =>
    {
      var toolName = parseResult.GetValue(nameArg);
      
      if (string.IsNullOrWhiteSpace(toolName))
      {
        Console.WriteLine("❌ Tool name cannot be empty");
        return 1;
      }
      
      // Validate tool exists
      if (!ToolRegistry.Exists(toolName))
      {
        Console.WriteLine($"❌ Unknown tool: {toolName}");
        Console.WriteLine();
        Console.WriteLine("Available tools:");
        foreach (var name in ToolRegistry.GetToolNames())
        {
          Console.WriteLine($"  • {name}");
        }
        return 1;
      }
      
      // Save to local config
      var localConfigDir = Path.Combine(Directory.GetCurrentDirectory(), ".cli_mate");
      Directory.CreateDirectory(localConfigDir);
      var localToolFile = Path.Combine(localConfigDir, "tool.conf");
      
      try
      {
        File.WriteAllText(localToolFile, toolName);
        Console.WriteLine($"✅ Set local tool to: {toolName}");
        Console.WriteLine($"   Config: .cli_mate/tool.conf");
        Console.WriteLine();
        Console.WriteLine("💡 Add .cli_mate/ to your .gitignore if needed");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"❌ Failed to save config: {ex.Message}");
        return 1;
      }
      
      return 0;
    });
    
    return command;
  }
}
