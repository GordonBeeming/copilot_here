using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetShowModelCommand()
  {
    var command = new Command("--show-model", "Show current default model configuration");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = ModelConfig.Load(paths);

      Console.WriteLine("🤖 Model Configuration:");
      Console.WriteLine();

      if (config.LocalModel is not null)
        Console.WriteLine($"  📍 Local config (.copilot_here/model.conf): {config.LocalModel}");
      else
        Console.WriteLine("  📍 Local config: (not set)");

      if (config.GlobalModel is not null)
        Console.WriteLine($"  🌍 Global config (~/.config/copilot_here/model.conf): {config.GlobalModel}");
      else
        Console.WriteLine("  🌍 Global config: (not set)");

      Console.WriteLine();
      Console.WriteLine("  🏭 Application default: (GitHub Copilot CLI default)");
      return 0;
    });
    return command;
  }
}
