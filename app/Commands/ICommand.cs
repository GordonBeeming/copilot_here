using System.CommandLine;

namespace CopilotHere.Commands;

/// <summary>
/// Interface for all command handlers.
/// Each command is responsible for configuring itself on the root command.
/// </summary>
public interface ICommand
{
  /// <summary>
  /// Configures this command on the root command.
  /// </summary>
  void Configure(RootCommand root);
}
