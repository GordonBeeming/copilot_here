using CopilotHere.Infrastructure;
using CopilotHere.Tools;
using TUnit.Core;

namespace CopilotHere.Tests;

public class EchoToolTests
{
  private readonly EchoTool _tool = new();

  [Test]
  public async Task Name_ReturnsCorrectValue()
  {
    // Assert
    await Assert.That(_tool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task DisplayName_ReturnsCorrectValue()
  {
    // Assert
    await Assert.That(_tool.DisplayName).IsEqualTo("Echo (Test Provider)");
  }

  [Test]
  public async Task GetImageName_WithLatest_ReturnsCorrectFormat()
  {
    // Act
    var imageName = _tool.GetImageName("latest");

    // Assert - Echo uses the same copilot images
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:copilot-latest");
  }

  [Test]
  public async Task GetImageName_WithDotnet_ReturnsCorrectFormat()
  {
    // Act
    var imageName = _tool.GetImageName("dotnet");

    // Assert - Echo uses the same copilot images
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:copilot-dotnet");
  }

  [Test]
  public async Task GetDockerfile_ReturnsCorrectPath()
  {
    // Act
    var dockerfile = _tool.GetDockerfile();

    // Assert
    await Assert.That(dockerfile).IsEqualTo("docker/echo/Dockerfile");
  }

  [Test]
  public async Task GetDefaultNetworkRulesPath_ReturnsCorrectPath()
  {
    // Act
    var path = _tool.GetDefaultNetworkRulesPath();

    // Assert
    await Assert.That(path).IsEqualTo("docker/echo/default-airlock-rules.json");
  }

  [Test]
  public async Task BuildCommand_GeneratesBashScript()
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
    await Assert.That(command[0]).IsEqualTo("bash");
    await Assert.That(command[1]).IsEqualTo("-c");
    await Assert.That(command.Count).IsEqualTo(3);
  }

  [Test]
  public async Task BuildCommand_ScriptContainsToolName()
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
    await Assert.That(command[2]).Contains("echo");
    await Assert.That(command[2]).Contains("ECHO PROVIDER");
  }

  [Test]
  public async Task BuildCommand_WithYoloMode_IncludesYoloFlag()
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
    await Assert.That(command[2]).Contains("YOLO Mode: True");
  }

  [Test]
  public async Task BuildCommand_WithModel_IncludesModel()
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
    await Assert.That(command[2]).Contains("Model: gpt-4");
  }

  [Test]
  public async Task BuildCommand_WithUserArgs_IncludesUserArgs()
  {
    // Arrange
    var context = new CommandContext
    {
      UserArgs = new List<string> { "--prompt", "test", "--continue" },
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
    await Assert.That(command[2]).Contains("--prompt test --continue");
  }

  [Test]
  public async Task BuildCommand_WithEnvironment_IncludesEnvironment()
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
      Environment = new Dictionary<string, string> { { "DEBUG", "true" }, { "ENV", "test" } }
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command[2]).Contains("ENVIRONMENT VARIABLES:");
    await Assert.That(command[2]).Contains("DEBUG=true");
    await Assert.That(command[2]).Contains("ENV=test");
  }

  [Test]
  public async Task BuildCommand_NoEnvironment_ShowsNone()
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
    await Assert.That(command[2]).Contains("ENVIRONMENT VARIABLES:");
    await Assert.That(command[2]).Contains("(none)");
  }

  [Test]
  public async Task GetInteractiveFlag_ReturnsNull()
  {
    // Act
    var flag = _tool.GetInteractiveFlag();

    // Assert
    await Assert.That(flag).IsNull();
  }

  [Test]
  public async Task GetYoloModeFlags_ReturnsEmptyList()
  {
    // Act
    var flags = _tool.GetYoloModeFlags();

    // Assert
    await Assert.That(flags).IsEmpty();
  }

  [Test]
  public async Task GetConfigDirName_ReturnsCliMate()
  {
    // Act
    var dirName = _tool.GetConfigDirName();

    // Assert
    await Assert.That(dirName).IsEqualTo("cli_mate");
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
  public async Task GetRequiredDependencies_ContainsOnlyDocker()
  {
    // Act
    var deps = _tool.GetRequiredDependencies();

    // Assert
    await Assert.That(deps).IsNotEmpty();
    await Assert.That(deps.Length).IsEqualTo(1);
    await Assert.That(deps).Contains("docker");
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
  public async Task BuildCommand_ComplexScenario_AllFeaturesIncluded()
  {
    // Arrange - complex scenario with all features
    var context = new CommandContext
    {
      UserArgs = new List<string> { "--prompt", "test prompt", "--verbose" },
      IsYolo = true,
      IsInteractive = false,
      Model = "claude-sonnet-4.5",
      ImageTag = "rust",
      Mounts = new List<string> { "/src:/work/src" },
      Environment = new Dictionary<string, string> { { "DEBUG", "true" }, { "LOG_LEVEL", "info" } }
    };

    // Act
    var command = _tool.BuildCommand(context);

    // Assert
    await Assert.That(command[0]).IsEqualTo("bash");
    await Assert.That(command[1]).IsEqualTo("-c");
    await Assert.That(command[2]).Contains("ECHO PROVIDER");
    await Assert.That(command[2]).Contains("Image: ghcr.io/gordonbeeming/copilot_here:copilot-rust");
    await Assert.That(command[2]).Contains("YOLO Mode: True");
    await Assert.That(command[2]).Contains("Interactive: False");
    await Assert.That(command[2]).Contains("Model: claude-sonnet-4.5");
    await Assert.That(command[2]).Contains("--prompt test prompt --verbose");
    await Assert.That(command[2]).Contains("DEBUG=true");
    await Assert.That(command[2]).Contains("LOG_LEVEL=info");
    await Assert.That(command[2]).Contains("/src:/work/src");
  }
}
