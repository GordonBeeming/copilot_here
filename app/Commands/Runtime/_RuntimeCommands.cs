using System.CommandLine;
using CopilotHere.Commands;

namespace CopilotHere.Commands.Runtime;

/// <summary>
/// Commands for managing container runtime selection.
/// </summary>
public sealed partial class RuntimeCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(CreateShowRuntimeCommand());
    root.Add(CreateListRuntimesCommand());
    root.Add(CreateSetRuntimeCommand());
    root.Add(CreateSetRuntimeGlobalCommand());
  }
}
