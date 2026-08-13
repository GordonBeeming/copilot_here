using System.CommandLine;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetDisableGlobalAirlockCommand()
  {
    var command = new Command("--disable-global-airlock", "Disable Airlock for global config");
    command.SetAction(_ => RunToggle(
      "✅ Airlock disabled (global)",
      paths => (AirlockConfig.DisableGlobal(paths), AirlockConfig.GetGlobalRulesPath(paths))));
    return command;
  }
}
