using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEnableGlobalAirlockCommand()
  {
    var command = new Command("--enable-global-airlock", "Enable Airlock with global rules (~/.config/copilot_here/network.json)");
    command.Aliases.Add("-EnableGlobalAirlock");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      AirlockConfig.EnableGlobal(paths);
      Console.WriteLine("✅ Airlock enabled (global)");
      Console.WriteLine($"   Rules: {AirlockConfig.GetGlobalRulesPath(paths)}");
      return 0;
    });
    return command;
  }
}
