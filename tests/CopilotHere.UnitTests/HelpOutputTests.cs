using System.CommandLine;
using CopilotHere.Commands;
using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Tests that verify the CLI has all expected options and commands registered.
/// This ensures users can discover features and that options aren't accidentally removed.
/// </summary>
public class HelpOutputTests
{
  private static RootCommand CreateRootCommand(bool isYolo = false)
  {
    var appName = isYolo ? "copilot_yolo" : "copilot_here";
    var rootCommand = new RootCommand($"{appName} - GitHub Copilot CLI in a secure Docker container");

    ICommand[] commands =
    [
      new RunCommand(isYolo),
      new MountCommands(),
      new ImageCommands(),
      new AirlockCommands(),
    ];

    foreach (var command in commands)
    {
      command.Configure(rootCommand);
    }

    return rootCommand;
  }

  private static IEnumerable<string> GetAllOptionNames(RootCommand rootCommand)
  {
    foreach (var option in rootCommand.Options)
    {
      yield return option.Name;
      foreach (var alias in option.Aliases)
      {
        yield return alias;
      }
    }
  }

  private static IEnumerable<string> GetAllCommandNames(RootCommand rootCommand)
  {
    foreach (var command in rootCommand.Subcommands)
    {
      yield return command.Name;
      foreach (var alias in command.Aliases)
      {
        yield return alias;
      }
    }
  }

  // ===== MAIN OPTIONS REGISTERED =====

  [Test]
  [Arguments("--dotnet")]
  [Arguments("--dotnet8")]
  [Arguments("--dotnet9")]
  [Arguments("--dotnet10")]
  [Arguments("--playwright")]
  [Arguments("--dotnet-playwright")]
  [Arguments("--rust")]
  [Arguments("--dotnet-rust")]
  [Arguments("--mount")]
  [Arguments("--mount-rw")]
  [Arguments("--no-cleanup")]
  [Arguments("--no-pull")]
  [Arguments("--yolo")]
  [Arguments("--update")]
  [Arguments("--install-shells")]
  [Arguments("--help2")]
  public async Task RootCommand_HasOption(string option)
  {
    // Arrange
    var rootCommand = CreateRootCommand();

    // Act
    var allOptions = GetAllOptionNames(rootCommand).ToList();

    // Assert
    await Assert.That(allOptions).Contains(option);
  }

  // ===== COMMANDS REGISTERED =====

  [Test]
  [Arguments("--list-mounts")]
  [Arguments("--save-mount")]
  [Arguments("--save-mount-global")]
  [Arguments("--remove-mount")]
  [Arguments("--list-images")]
  [Arguments("--show-image")]
  [Arguments("--set-image")]
  [Arguments("--set-image-global")]
  [Arguments("--clear-image")]
  [Arguments("--clear-image-global")]
  [Arguments("--enable-airlock")]
  [Arguments("--enable-global-airlock")]
  [Arguments("--disable-airlock")]
  [Arguments("--disable-global-airlock")]
  [Arguments("--show-airlock-rules")]
  [Arguments("--edit-airlock-rules")]
  [Arguments("--edit-global-airlock-rules")]
  public async Task RootCommand_HasCommand(string commandName)
  {
    // Arrange
    var rootCommand = CreateRootCommand();

    // Act
    var allCommands = GetAllCommandNames(rootCommand).ToList();

    // Assert
    await Assert.That(allCommands).Contains(commandName);
  }

  // ===== COPILOT PASSTHROUGH OPTIONS =====

  [Test]
  [Arguments("--prompt")]
  [Arguments("--model")]
  [Arguments("--continue")]
  [Arguments("--resume")]
  [Arguments("--silent")]
  [Arguments("--agent")]
  [Arguments("--no-color")]
  [Arguments("--allow-tool")]
  [Arguments("--deny-tool")]
  [Arguments("--stream")]
  [Arguments("--log-level")]
  [Arguments("--screen-reader")]
  [Arguments("--no-custom-instructions")]
  [Arguments("--additional-mcp-config")]
  public async Task RootCommand_HasCopilotPassthroughOption(string option)
  {
    // Arrange
    var rootCommand = CreateRootCommand();

    // Act
    var allOptions = GetAllOptionNames(rootCommand).ToList();

    // Assert
    await Assert.That(allOptions).Contains(option);
  }

  // ===== APP NAME CHANGES WITH YOLO =====

  [Test]
  public async Task RootCommand_DescriptionContainsCopilotHereInNormalMode()
  {
    // Arrange
    var rootCommand = CreateRootCommand(isYolo: false);

    // Assert
    await Assert.That(rootCommand.Description).Contains("copilot_here");
  }

  [Test]
  public async Task RootCommand_DescriptionContainsCopilotYoloInYoloMode()
  {
    // Arrange
    var rootCommand = CreateRootCommand(isYolo: true);

    // Assert
    await Assert.That(rootCommand.Description).Contains("copilot_yolo");
  }
}
