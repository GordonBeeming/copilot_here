using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Tool;

public sealed partial class ToolCommands
{
  /// <summary>
  /// Lists all available CLI tools.
  /// </summary>
  private static Command CreateListCommand()
  {
    var command = new Command("--list-tools", "List all available CLI tools");
    
    command.SetAction(_ =>
    {
      Console.WriteLine("📋 Available CLI Tools:");
      Console.WriteLine();
      
      var tools = ToolRegistry.GetAll();
      foreach (var tool in tools)
      {
        var isDefault = tool.Name == ToolRegistry.GetDefault().Name;
        var marker = isDefault ? "★" : " ";
        Console.WriteLine($"  {marker} {tool.Name}");
        Console.WriteLine($"      {tool.DisplayName}");
        
        // Show capabilities
        var capabilities = new List<string>();
        if (tool.SupportsModels) capabilities.Add("models");
        if (tool.SupportsYoloMode) capabilities.Add("yolo");
        if (tool.SupportsInteractiveMode) capabilities.Add("interactive");
        
        if (capabilities.Count > 0)
        {
          Console.WriteLine($"      Capabilities: {string.Join(", ", capabilities)}");
        }
        Console.WriteLine();
      }
      
      Console.WriteLine("★ = default tool");
      Console.WriteLine();
      Console.WriteLine("💡 Set tool:");
      Console.WriteLine("   copilot_here --set-tool <name>        (global)");
      Console.WriteLine("   copilot_here --set-tool-local <name>  (this project)");
      
      return 0;
    });
    
    return command;
  }
}
