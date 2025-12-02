using System.CommandLine;

namespace CopilotHere.Commands.Mounts;

/// <summary>
/// Commands for managing mount configurations.
/// </summary>
public sealed partial class MountCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetListMountsCommand());
    root.Add(SetSaveMountCommand());
    root.Add(SetSaveMountGlobalCommand());
    root.Add(SetRemoveMountCommand());
  }
}
