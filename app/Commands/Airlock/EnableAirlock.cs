using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEnableAirlockCommand()
  {
    var command = new Command("--enable-airlock", "Enable Airlock with local rules (.copilot_here/network.json)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      AirlockConfig.EnableLocal(paths);
      Console.WriteLine("✅ Airlock enabled (local)");
      Console.WriteLine($"   📁 Rules: {AirlockConfig.GetLocalRulesPath(paths)}");
      return 0;
    });
    return command;
  }
}
