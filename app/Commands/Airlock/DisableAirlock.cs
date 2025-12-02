using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetDisableAirlockCommand()
  {
    var command = new Command("--disable-airlock", "Disable Airlock for local config");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      AirlockConfig.DisableLocal(paths);
      Console.WriteLine("✅ Airlock disabled (local)");
      return 0;
    });
    return command;
  }
}
