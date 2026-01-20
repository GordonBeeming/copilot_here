using CopilotHere.Infrastructure;
using CopilotHere.Tools;
using TUnit.Core;

namespace CopilotHere.Tests;

public class GitHubCopilotToolTests
{
  private readonly GitHubCopilotTool _tool = new();

  [Test]
  public async Task Name_ReturnsCorrectValue()
  {
    // Assert
    await Assert.That(_tool.Name).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task DisplayName_ReturnsCorrectValue()
  {
    // Assert
    await Assert.That(_tool.DisplayName).IsEqualTo("GitHub Copilot CLI");
  }

  [Test]
  public async Task GetImageName_WithLatest_ReturnsCorrectFormat()
  {
    // Act
    var imageName = _tool.GetImageName("latest");

    // Assert
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:copilot-latest");
  }

  [Test]
  public async Task GetImageName_WithDotnet_ReturnsCorrectFormat()
  {
    // Act
    var imageName = _tool.GetImageName("dotnet");

    // Assert
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:copilot-dotnet");
  }

  [Test]
  public async Task GetImageName_WithEmptyTag_ReturnsDefault()
  {
    // Act
    var imageName = _tool.GetImageName("");

    // Assert
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:copilot-latest");
  }

  [Test]
  public async Task GetDockerfile_ReturnsCorrectPath()
  {
    // Act
    var dockerfile = _tool.GetDockerfile();

    // Assert
    await Assert.That(dockerfile).IsEqualTo("docker/tools/github-copilot/Dockerfile");
  }

  [Test]
  public async Task GetDefaultNetworkRulesPath_ReturnsCorrectPath()
  {
    // Act
    var path = _tool.GetDefaultNetworkRulesPath();

    // Assert
    await Assert.That(path).IsEqualTo("docker/tools/github-copilot/default-airlock-rules.json");
  }

  [Test]
  public async Task BuildCommand_BasicMode_ContainsCopilot()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string>(),
      IsYolo = false,
      IsInteractive = true,
      Model = null,
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).IsNotEmpty();
    await Assert.That(command[0]).IsEqualTo("copilot");
  }

  [Test]
  public async Task BuildCommand_WithYoloMode_AddsYoloFlags()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string>(),
      IsYolo = true,
      IsInteractive = true,
      Model = null,
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).Contains("--allow-all-tools");
    await Assert.That(command).Contains("--allow-all-paths");
  }

  [Test]
  public async Task BuildCommand_WithModel_AddsModelFlag()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string>(),
      IsYolo = false,
      IsInteractive = true,
      Model = "gpt-4",
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).Contains("--model");
    await Assert.That(command).Contains("gpt-4");
  }

  [Test]
  public async Task BuildCommand_InteractiveMode_AddsBannerFlag()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string>(),
      IsYolo = false,
      IsInteractive = true,
      Model = null,
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).Contains("--banner");
  }

  [Test]
  public async Task BuildCommand_WithUserArgs_PassesThroughArgs()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string> { "--prompt", "hello world", "--continue" },
      IsYolo = false,
      IsInteractive = false,
      Model = null,
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).Contains("--prompt");
    await Assert.That(command).Contains("hello world");
    await Assert.That(command).Contains("--continue");
  }

  [Test]
  public async Task BuildCommand_YoloModeWithModel_ContainsBothFlags()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string>(),
      IsYolo = true,
      IsInteractive = true,
      Model = "claude-sonnet-4.5",
      ImageTag = "latest",
      Mounts = new List<string>(),
      Environment = new Dictionary<string, string>()
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command).Contains("--allow-all-tools");
    await Assert.That(command).Contains("--allow-all-paths");
    await Assert.That(command).Contains("--model");
    await Assert.That(command).Contains("claude-sonnet-4.5");
  }

  [Test]
  public async Task GetYoloModeFlags_ReturnsCorrectFlags()
  {
    // Act
    var flags = _tool.GetYoloModeFlags();

    // Assert
    await Assert.That(flags).IsNotEmpty();
    await Assert.That(flags).Contains("--allow-all-tools");
    await Assert.That(flags).Contains("--allow-all-paths");
  }

  [Test]
  public async Task GetInteractiveFlag_ReturnsCorrectFlag()
  {
    // Act
    var flag = _tool.GetInteractiveFlag();

    // Assert
    await Assert.That(flag).IsEqualTo("--banner");
  }

  [Test]
  public async Task GetConfigDirName_ReturnsCorrectValue()
  {
    // Act
    var dirName = _tool.GetConfigDirName();

    // Assert
    await Assert.That(dirName).IsEqualTo(".copilot");
  }

  [Test]
  public async Task GetSessionDataPath_ReturnsNull()
  {
    // Act
    var path = _tool.GetSessionDataPath();

    // Assert
    await Assert.That(path).IsNull();
  }

  [Test]
  public async Task GetRequiredDependencies_ContainsDockerAndGh()
  {
    // Act
    var deps = _tool.GetRequiredDependencies();

    // Assert
    await Assert.That(deps).IsNotEmpty();
    await Assert.That(deps).Contains("docker");
    await Assert.That(deps).Contains("gh");
  }

  [Test]
  public async Task GetAuthProvider_ReturnsNonNull()
  {
    // Act
    var authProvider = _tool.GetAuthProvider();

    // Assert
    await Assert.That(authProvider).IsNotNull();
  }

  [Test]
  public async Task GetModelProvider_ReturnsNonNull()
  {
    // Act
    var modelProvider = _tool.GetModelProvider();

    // Assert
    await Assert.That(modelProvider).IsNotNull();
  }

  [Test]
  public async Task SupportsModels_ReturnsTrue()
  {
    // Assert
    await Assert.That(_tool.SupportsModels).IsTrue();
  }

  [Test]
  public async Task SupportsYoloMode_ReturnsTrue()
  {
    // Assert
    await Assert.That(_tool.SupportsYoloMode).IsTrue();
  }

  [Test]
  public async Task SupportsInteractiveMode_ReturnsTrue()
  {
    // Assert
    await Assert.That(_tool.SupportsInteractiveMode).IsTrue();
  }

  [Test]
  public async Task BuildCommand_ComplexScenario_AllFeaturesEnabled()
  {
    // Arrange - complex real-world scenario
    var context = new CommandContext
    {
      UserArgs = new List<string> { "--prompt", "fix the bug", "--output", "json" },
      IsYolo = true,
      IsInteractive = false,
      Model = "gpt-4.5",
      ImageTag = "dotnet",
      Mounts = new List<string> { "/src:/work/src" },
      Environment = new Dictionary<string, string> { { "DEBUG", "true" } }
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command[0]).IsEqualTo("copilot");
    await Assert.That(command).Contains("--allow-all-tools");
    await Assert.That(command).Contains("--allow-all-paths");
    await Assert.That(command).Contains("--model");
    await Assert.That(command).Contains("gpt-4.5");
    await Assert.That(command).Contains("--prompt");
    await Assert.That(command).Contains("fix the bug");
    await Assert.That(command).Contains("--output");
    await Assert.That(command).Contains("json");
  }
}
