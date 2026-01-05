using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetClearModelCommand()
  {
    var command = new Command("--clear-model", "Clear local model configuration");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var deleted = ModelConfig.ClearLocal(paths);
      if (deleted)
        Console.WriteLine("✅ Cleared local model configuration");
      else
        Console.WriteLine("ℹ️  No local model configuration to clear");
      return 0;
    });
    return command;
  }
}
