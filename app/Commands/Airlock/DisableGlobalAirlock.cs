using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetDisableGlobalAirlockCommand()
  {
    var command = new Command("--disable-global-airlock", "Disable Airlock for global config");
    command.Aliases.Add("-DisableGlobalAirlock");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      AirlockConfig.DisableGlobal(paths);
      Console.WriteLine("✅ Airlock disabled (global)");
      return 0;
    });
    return command;
  }
}
