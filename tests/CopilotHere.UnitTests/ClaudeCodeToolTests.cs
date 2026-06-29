using CopilotHere.Infrastructure;
using CopilotHere.Tools;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ClaudeCodeToolTests
{
  private readonly ClaudeCodeTool _tool = new();

  [Test]
  public async Task Name_ReturnsCorrectValue()
  {
    await Assert.That(_tool.Name).IsEqualTo("claude");
  }

  [Test]
  public async Task DisplayName_ReturnsCorrectValue()
  {
    await Assert.That(_tool.DisplayName).IsEqualTo("Claude Code");
  }

  [Test]
  public async Task GetImageName_WithLatest_ReturnsCorrectFormat()
  {
    var imageName = _tool.GetImageName("latest");
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:claude-latest");
  }

  [Test]
  public async Task GetImageName_WithDotnet_ReturnsCorrectFormat()
  {
    var imageName = _tool.GetImageName("dotnet");
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:claude-dotnet");
  }

  [Test]
  public async Task GetImageName_WithEmptyTag_ReturnsDefault()
  {
    var imageName = _tool.GetImageName("");
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:claude-latest");
  }

  [Test]
  public async Task GetImageName_WithAbsoluteImage_ReturnsAsIs()
  {
    var imageName = _tool.GetImageName("myregistry.io/custom-claude:v1");
    await Assert.That(imageName).IsEqualTo("myregistry.io/custom-claude:v1");
  }

  [Test]
  public async Task GetImageName_WithLocalTaggedImageNoSlash_ReturnsAsIs()
  {
    var imageName = _tool.GetImageName("my-local-image:dev");
    await Assert.That(imageName).IsEqualTo("my-local-image:dev");
  }

  [Test]
  public async Task GetDefaultNetworkRulesPath_ReturnsCorrectPath()
  {
    var path = _tool.GetDefaultNetworkRulesPath();
    await Assert.That(path).IsEqualTo("docker/tools/claude/default-airlock-rules.json");
  }

  [Test]
  public async Task BuildCommand_BasicMode_ContainsClaude()
  {
    var context = NewContext();

    var command = _tool.BuildCommand(context);

    await Assert.That(command).IsNotEmpty();
    await Assert.That(command[0]).IsEqualTo("claude");
  }

  [Test]
  public async Task BuildCommand_BasicMode_HasNoExtraFlags()
  {
    // With no yolo/model/args, the command is just `claude` (interactive TUI).
    var context = NewContext();

    var command = _tool.BuildCommand(context);

    await Assert.That(command.Count).IsEqualTo(1);
    await Assert.That(command).DoesNotContain("--dangerously-skip-permissions");
  }

  [Test]
  public async Task BuildCommand_WithYoloMode_AddsSkipPermissionsFlag()
  {
    var context = NewContext(isYolo: true);

    var command = _tool.BuildCommand(context);

    await Assert.That(command[0]).IsEqualTo("claude");
    await Assert.That(command).Contains("--dangerously-skip-permissions");
  }

  [Test]
  public async Task BuildCommand_WithModel_AddsModelFlag()
  {
    var context = NewContext(model: "opus");

    var command = _tool.BuildCommand(context);

    await Assert.That(command).Contains("--model");
    await Assert.That(command).Contains("opus");
  }

  [Test]
  public async Task BuildCommand_WithUserArgs_PassesThroughArgs()
  {
    var context = NewContext(userArgs: ["-p", "fix the bug"]);

    var command = _tool.BuildCommand(context);

    await Assert.That(command).Contains("-p");
    await Assert.That(command).Contains("fix the bug");
  }

  [Test]
  public async Task BuildCommand_ComplexScenario_AllFeaturesEnabled()
  {
    var context = NewContext(
      userArgs: ["-p", "ship it"],
      isYolo: true,
      model: "claude-opus-4-8");

    var command = _tool.BuildCommand(context);

    await Assert.That(command[0]).IsEqualTo("claude");
    await Assert.That(command).Contains("--dangerously-skip-permissions");
    await Assert.That(command).Contains("--model");
    await Assert.That(command).Contains("claude-opus-4-8");
    await Assert.That(command).Contains("-p");
    await Assert.That(command).Contains("ship it");
  }

  [Test]
  public async Task GetYoloModeFlags_ReturnsCorrectFlags()
  {
    var flags = _tool.GetYoloModeFlags();

    await Assert.That(flags).IsNotEmpty();
    await Assert.That(flags).Contains("--dangerously-skip-permissions");
  }

  [Test]
  public async Task GetInteractiveFlag_ReturnsNull()
  {
    var flag = _tool.GetInteractiveFlag();
    await Assert.That(flag).IsNull();
  }

  [Test]
  public async Task GetConfigDirName_ReturnsCorrectValue()
  {
    var dirName = _tool.GetConfigDirName();
    await Assert.That(dirName).IsEqualTo(".claude");
  }

  [Test]
  public async Task GetSessionDataPath_ReturnsNull()
  {
    var path = _tool.GetSessionDataPath();
    await Assert.That(path).IsNull();
  }

  [Test]
  public async Task GetHostConfigPath_UsesClaudeConfigPath()
  {
    var paths = new AppPaths
    {
      CurrentDirectory = "/tmp/project",
      UserHome = "/tmp/home",
      CopilotConfigPath = "/tmp/home/.config/copilot-cli-docker",
      ClaudeConfigPath = "/tmp/home/.claude",
      LocalConfigPath = "/tmp/project/.copilot_here",
      GlobalConfigPath = "/tmp/home/.config/copilot_here",
      ContainerWorkDir = "/work/tmp/project"
    };

    var path = _tool.GetHostConfigPath(paths);

    await Assert.That(path).IsEqualTo(paths.ClaudeConfigPath);
  }

  [Test]
  public async Task GetContainerConfigPath_ReturnsClaudeDirectory()
  {
    var path = _tool.GetContainerConfigPath();
    await Assert.That(path).IsEqualTo("/home/appuser/.claude");
  }

  [Test]
  public async Task GetRequiredDependencies_ContainsDockerWithoutGh()
  {
    var deps = _tool.GetRequiredDependencies();

    await Assert.That(deps).Contains("docker");
    await Assert.That(deps).DoesNotContain("gh");
  }

  [Test]
  public async Task GetAuthProvider_ReturnsNonNull()
  {
    await Assert.That(_tool.GetAuthProvider()).IsNotNull();
  }

  [Test]
  public async Task GetModelProvider_ReturnsNonNull()
  {
    await Assert.That(_tool.GetModelProvider()).IsNotNull();
  }

  [Test]
  public async Task SupportsModels_ReturnsTrue()
  {
    await Assert.That(_tool.SupportsModels).IsTrue();
  }

  [Test]
  public async Task ManagesOwnModelSelection_ReturnsTrue()
  {
    // Claude Code keeps its own model preference, so the shared model.conf must
    // not be auto-passed as --model.
    await Assert.That(_tool.ManagesOwnModelSelection).IsTrue();
  }

  [Test]
  public async Task GetAdditionalConfigMounts_WithClaudeJson_MountsIt()
  {
    var home = Directory.CreateTempSubdirectory("claude-home-");
    try
    {
      await File.WriteAllTextAsync(Path.Combine(home.FullName, ".claude.json"), "{}");
      var paths = PathsWithHome(home.FullName);

      var mounts = _tool.GetAdditionalConfigMounts(paths);

      await Assert.That(mounts.Count).IsEqualTo(1);
      await Assert.That(mounts[0].HostPath).IsEqualTo(Path.Combine(home.FullName, ".claude.json"));
      await Assert.That(mounts[0].ContainerPath).IsEqualTo("/home/appuser/.claude.json");
    }
    finally
    {
      home.Delete(recursive: true);
    }
  }

  [Test]
  public async Task GetAdditionalConfigMounts_WithoutClaudeJson_ReturnsEmpty()
  {
    var home = Directory.CreateTempSubdirectory("claude-home-");
    try
    {
      var mounts = _tool.GetAdditionalConfigMounts(PathsWithHome(home.FullName));
      await Assert.That(mounts).IsEmpty();
    }
    finally
    {
      home.Delete(recursive: true);
    }
  }

  private static AppPaths PathsWithHome(string home) => new()
  {
    CurrentDirectory = "/tmp/project",
    UserHome = home,
    CopilotConfigPath = Path.Combine(home, ".config", "copilot-cli-docker"),
    ClaudeConfigPath = Path.Combine(home, ".claude"),
    LocalConfigPath = "/tmp/project/.copilot_here",
    GlobalConfigPath = Path.Combine(home, ".config", "copilot_here"),
    ContainerWorkDir = "/work/tmp/project"
  };

  [Test]
  public async Task SupportsYoloMode_ReturnsTrue()
  {
    await Assert.That(_tool.SupportsYoloMode).IsTrue();
  }

  [Test]
  public async Task SupportsInteractiveMode_ReturnsTrue()
  {
    await Assert.That(_tool.SupportsInteractiveMode).IsTrue();
  }

  private static CommandContext NewContext(
    List<string>? userArgs = null,
    bool isYolo = false,
    string? model = null) => new()
  {
    UserArgs = userArgs ?? [],
    IsYolo = isYolo,
    IsInteractive = userArgs is null || userArgs.Count == 0,
    Model = model,
    ImageTag = "latest",
    Mounts = [],
    Environment = new Dictionary<string, string>()
  };
}
