using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetClearImageCommand()
  {
    var command = new Command("--clear-image", "Clear default image from local config");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var cleared = ImageConfig.ClearLocal(paths);

      if (cleared)
        Console.WriteLine("✅ Cleared local default image");
      else
        Console.WriteLine("ℹ️  No local default image configured");
      return 0;
    });
    return command;
  }
}
