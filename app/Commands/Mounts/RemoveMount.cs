using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Mounts;

public sealed partial class MountCommands
{
  private static Command SetRemoveMountCommand()
  {
    var command = new Command("--remove-mount", "Remove mount from configs");

    var pathArg = new Argument<string>("path") { Description = "Path to remove" };
    command.Add(pathArg);

    command.SetAction(parseResult =>
    {
      var path = parseResult.GetValue(pathArg)!;
      var paths = AppPaths.Resolve();
      var removed = MountsConfig.Remove(paths, path);

      if (removed)
        Console.WriteLine($"✅ Removed mount: {path}");
      else
        Console.WriteLine($"⚠️  Mount not found: {path}");
      return 0;
    });
    return command;
  }
}
