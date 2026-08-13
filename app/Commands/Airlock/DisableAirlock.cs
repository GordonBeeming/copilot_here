using System.CommandLine;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetDisableAirlockCommand()
  {
    var command = new Command("--disable-airlock", "Disable Airlock for local config");
    command.SetAction(_ => RunToggle(
      "✅ Airlock disabled (local)",
      paths => (AirlockConfig.DisableLocal(paths), AirlockConfig.GetLocalRulesPath(paths))));
    return command;
  }
}
