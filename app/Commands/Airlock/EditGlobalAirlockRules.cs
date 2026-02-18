using System.CommandLine;
using CopilotHere.Infrastructure;
using AppCtx = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEditGlobalAirlockRulesCommand()
  {
    var command = new Command("--edit-global-airlock-rules", "Edit global Airlock rules in $EDITOR");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var rulesPath = AirlockConfig.GetGlobalRulesPath(paths);
      var ctx = AppCtx.Create();
      OpenInEditor(rulesPath, ctx.ActiveTool);
      return 0;
    });
    return command;
  }
}
