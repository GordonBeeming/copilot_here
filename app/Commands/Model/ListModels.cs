using System.CommandLine;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetListModelsCommand()
  {
    var command = new Command("--list-models", "List available AI models");
    command.SetAction(async _ =>
    {
      Console.WriteLine("🤖 Fetching available models...");
      Console.WriteLine();
      
      var ctx = AppContext.Create();
      
      // Check if the tool supports models
      if (!ctx.ActiveTool.SupportsModels)
      {
        Console.WriteLine($"❌ The {ctx.ActiveTool.DisplayName} tool does not support model selection");
        return 1;
      }
      
      // Get models from the tool's model provider
      var modelProvider = ctx.ActiveTool.GetModelProvider();
      var models = await modelProvider.ListAvailableModels(ctx);
      
      if (models.Count == 0)
      {
        Console.WriteLine($"❌ No models available for {ctx.ActiveTool.DisplayName}");
        Console.WriteLine();
        Console.WriteLine("This could mean:");
        Console.WriteLine("  • The tool doesn't support model listing");
        Console.WriteLine("  • There was an error fetching the model list");
        Console.WriteLine("  • Authentication may be required");
        return 1;
      }
      
      Console.WriteLine("Available models:");
      foreach (var model in models)
      {
        Console.WriteLine($"  • {model}");
      }
      Console.WriteLine();
      Console.WriteLine("💡 Set your preferred model:");
      Console.WriteLine("   copilot_here --set-model <model-id>       (local project)");
      Console.WriteLine("   copilot_here --set-model-global <model-id> (all projects)");
      
      return 0;
    });
    return command;
  }
}
