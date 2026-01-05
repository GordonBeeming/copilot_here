using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetClearModelGlobalCommand()
  {
    var command = new Command("--clear-model-global", "Clear global model configuration");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var deleted = ModelConfig.ClearGlobal(paths);
      if (deleted)
        Console.WriteLine("✅ Cleared global model configuration");
      else
        Console.WriteLine("ℹ️  No global model configuration to clear");
      return 0;
    });
    return command;
  }
}
