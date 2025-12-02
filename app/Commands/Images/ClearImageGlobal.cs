using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetClearImageGlobalCommand()
  {
    var command = new Command("--clear-image-global", "Clear default image from global config");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var cleared = ImageConfig.ClearGlobal(paths);

      if (cleared)
        Console.WriteLine("✅ Cleared global default image");
      else
        Console.WriteLine("ℹ️  No global default image configured");
      return 0;
    });
    return command;
  }
}
