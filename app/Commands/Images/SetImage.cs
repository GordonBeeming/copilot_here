using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetSetImageCommand()
  {
    var command = new Command("--set-image", "Set default image in local config");
    command.Aliases.Add("-SetImage");

    var tagArg = new Argument<string>("tag") { Description = "Image tag to set" };
    command.Add(tagArg);

    command.SetAction(parseResult =>
    {
      var tag = parseResult.GetValue(tagArg)!;
      var paths = AppPaths.Resolve();
      ImageConfig.SaveLocal(paths, tag);
      Console.WriteLine($"✅ Set local default image: {tag}");
      return 0;
    });
    return command;
  }
}
