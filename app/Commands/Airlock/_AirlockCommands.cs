using System.CommandLine;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Commands for managing the Airlock network proxy.
/// </summary>
public sealed partial class AirlockCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetEnableAirlockCommand());
    root.Add(SetEnableGlobalAirlockCommand());
    root.Add(SetDisableAirlockCommand());
    root.Add(SetDisableGlobalAirlockCommand());
    root.Add(SetShowAirlockRulesCommand());
    root.Add(SetEditAirlockRulesCommand());
    root.Add(SetEditGlobalAirlockRulesCommand());
  }
}
