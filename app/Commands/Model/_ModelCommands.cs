using System.CommandLine;

namespace CopilotHere.Commands.Model;

/// <summary>
/// Commands for managing model configurations.
/// </summary>
public sealed partial class ModelCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetListModelsCommand());
    root.Add(SetShowModelCommand());
    root.Add(SetSetModelCommand());
    root.Add(SetSetModelGlobalCommand());
    root.Add(SetClearModelCommand());
    root.Add(SetClearModelGlobalCommand());
  }
}
