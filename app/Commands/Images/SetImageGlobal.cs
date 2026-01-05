using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static Command SetSetImageGlobalCommand()
  {
    var command = new Command("--set-image-global", "Set default image in global config");

    var tagArg = new Argument<string>("tag") { Description = "Image tag to set" };
    command.Add(tagArg);

    command.SetAction(parseResult =>
    {
      var tag = parseResult.GetValue(tagArg)!;
      var paths = AppPaths.Resolve();
      ImageConfig.SaveGlobal(paths, tag);
      Console.WriteLine($"✅ Set global image: {tag}");
      return 0;
    });
    return command;
  }
}
