using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetShowImageCommand()
  {
    var command = new Command("--show-image", "Show current default image configuration");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = ImageConfig.Load(paths);

      Console.WriteLine("🖼️  Image Configuration:");
      Console.WriteLine();

      if (config.LocalTag is not null)
        Console.WriteLine($"  📍 Local config (.copilot_here/image.conf): {config.LocalTag}");
      else
        Console.WriteLine("  📍 Local config: (not set)");

      if (config.GlobalTag is not null)
        Console.WriteLine($"  🌍 Global config (~/.config/copilot_here/image.conf): {config.GlobalTag}");
      else
        Console.WriteLine("  🌍 Global config: (not set)");

      Console.WriteLine();
      Console.WriteLine("  🏭 Application default: latest");
      Console.WriteLine();
      Console.WriteLine("  💡 Tip: Use --image <name> to run with any custom Docker image");
      return 0;
    });
    return command;
  }
}
