using System.CommandLine;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEnableGlobalAirlockCommand()
  {
    var command = new Command("--enable-global-airlock", "Enable Airlock with global rules (~/.config/copilot_here/network.json)");
    command.SetAction(_ => RunToggle(
      "✅ Airlock enabled (global)",
      paths => (AirlockConfig.EnableGlobal(paths), AirlockConfig.GetGlobalRulesPath(paths))));
    return command;
  }
}
