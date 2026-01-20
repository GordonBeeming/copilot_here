using System.CommandLine;

namespace CopilotHere.Commands.Tool;

/// <summary>
/// Commands for managing CLI tool selection.
/// </summary>
public sealed partial class ToolCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(CreateListCommand());
    root.Add(CreateShowCommand());
    root.Add(CreateSetCommand());
    root.Add(CreateSetLocalCommand());
  }
}
