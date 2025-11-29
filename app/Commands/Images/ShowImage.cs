using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetShowImageCommand()
  {
    var command = new Command("--show-image", "Show current default image configuration");
    command.Aliases.Add("-ShowImage");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = ImageConfig.Load(paths);

      Console.WriteLine("Image configuration:");
      Console.WriteLine();
      Console.WriteLine($"  Active: {config.Tag} (from {config.Source})");

      if (config.LocalTag is not null)
        Console.WriteLine($"  Local:  {config.LocalTag}");

      if (config.GlobalTag is not null)
        Console.WriteLine($"  Global: {config.GlobalTag}");

      Console.WriteLine($"  Default: latest");
      return 0;
    });
    return command;
  }
}
