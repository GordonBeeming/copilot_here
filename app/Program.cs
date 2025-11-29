using System.CommandLine;
using CopilotHere.Commands;
using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;

class Program
{
  static async Task<int> Main(string[] args)
  {
    var rootCommand = new RootCommand("GitHub Copilot CLI in a secure Docker container");

    // Register all commands
    ICommand[] commands =
    [
      new RunCommand(),      // Main run command (default) - includes update option
      new MountCommands(),   // Mount management
      new ImageCommands(),   // Image management
      new AirlockCommands(), // Airlock proxy
    ];

    foreach (var command in commands)
    {
      command.Configure(rootCommand);
    }

    return await rootCommand.Parse(args).InvokeAsync();
  }
}