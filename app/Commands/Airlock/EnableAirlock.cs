using System.CommandLine;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEnableAirlockCommand()
  {
    var command = new Command("--enable-airlock", "Enable Airlock with local rules (.copilot_here/network.json)");
    command.SetAction(_ => RunToggle(
      "✅ Airlock enabled (local)",
      paths => (AirlockConfig.EnableLocal(paths), AirlockConfig.GetLocalRulesPath(paths))));
    return command;
  }
}
