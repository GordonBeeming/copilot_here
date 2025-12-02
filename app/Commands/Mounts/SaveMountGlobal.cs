using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Mounts;

public sealed partial class MountCommands
{
  private static Command SetSaveMountGlobalCommand()
  {
    var command = new Command("--save-mount-global", "Save mount to global config (~/.config/copilot_here/mounts.conf)");

    var pathArg = new Argument<string>("path") { Description = "Path to mount" };
    command.Add(pathArg);

    command.SetAction(parseResult =>
    {
      var path = parseResult.GetValue(pathArg)!;
      var paths = AppPaths.Resolve();
      MountsConfig.SaveGlobal(paths, path, isReadWrite: false);
      return 0;
    });
    return command;
  }
}
