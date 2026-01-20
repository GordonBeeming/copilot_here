using TUnit.Core;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Tests;

/// <summary>
/// Integration tests for AppContext tool loading with realistic scenarios.
/// Tests the complete priority chain: CLI arg > Local config > Global config > Default.
/// 
/// Note: These tests modify environment and working directory, so they manipulate
/// the actual file system to create realistic test scenarios.
/// </summary>
[NotInParallel]
public class AppContextToolLoadingTests
{
  private string _tempDir = null!;
  private string _originalWorkingDir = null!;
  private string _testProjectDir = null!;

  [Before(Test)]
  public void Setup()
  {
    _originalWorkingDir = Directory.GetCurrentDirectory();
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    _testProjectDir = Path.Combine(_tempDir, "project");
    
    Directory.CreateDirectory(_tempDir);
    Directory.CreateDirectory(_testProjectDir);
    
    // Set working directory to test project
    Directory.SetCurrentDirectory(_testProjectDir);
  }

  [After(Test)]
  public void Cleanup()
  {
    Directory.SetCurrentDirectory(_originalWorkingDir);
    
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
    
    // Clean up global config files created during tests
    CleanupGlobalToolConfig();
    CleanupLegacyGlobalToolConfig();
  }

  [Test]
  public async Task Create_NoConfigNoOverride_UsesDefaultGitHubCopilot()
  {
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert
    await Assert.That(ctx.ActiveTool).IsNotNull();
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task Create_WithToolOverride_UsesOverride()
  {
    // Arrange
    SetupLocalToolConfig("github-copilot");
    SetupGlobalToolConfig("github-copilot");
    
    // Act - override should win
    var ctx = AppContext.Create(toolOverride: "echo");
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_WithLocalConfig_UsesLocalConfig()
  {
    // Arrange
    SetupLocalToolConfig("echo");
    SetupGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_WithGlobalConfigOnly_UsesGlobalConfig()
  {
    // Arrange
    SetupGlobalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_ToolOverrideTakesPriorityOverLocalConfig()
  {
    // Arrange
    SetupLocalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: "echo");
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_ToolOverrideTakesPriorityOverGlobalConfig()
  {
    // Arrange
    SetupGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: "echo");
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_LocalConfigTakesPriorityOverGlobalConfig()
  {
    // Arrange
    SetupLocalToolConfig("echo");
    SetupGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Local should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_InvalidToolOverride_FallsBackToDefault()
  {
    // Act - invalid tool name
    var ctx = AppContext.Create(toolOverride: "nonexistent-tool");
    
    // Assert - Should fall back to default
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task Create_InvalidLocalConfig_FallsBackToGlobal()
  {
    // Arrange
    SetupLocalToolConfig("invalid-tool");
    SetupGlobalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Should skip invalid local and use global
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_InvalidGlobalConfig_FallsBackToDefault()
  {
    // Arrange
    SetupGlobalToolConfig("invalid-tool");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Should fall back to default
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task Create_LegacyLocalConfig_StillWorks()
  {
    // Arrange - Use old .copilot_here directory
    SetupLegacyLocalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_LegacyGlobalConfig_StillWorks()
  {
    // Arrange - Use old ~/.config/copilot_here directory
    SetupLegacyGlobalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_NewLocalConfigTakesPriorityOverLegacy()
  {
    // Arrange
    SetupLocalToolConfig("echo");
    SetupLegacyLocalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - New config should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_NewGlobalConfigTakesPriorityOverLegacy()
  {
    // Arrange
    SetupGlobalToolConfig("echo");
    SetupLegacyGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - New config should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_EmptyLocalConfig_FallsBackToGlobal()
  {
    // Arrange
    SetupLocalToolConfig("   "); // Whitespace only
    SetupGlobalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Should skip empty local and use global
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_CompletePriorityChain_OverrideWins()
  {
    // Arrange - Set up every level
    SetupLocalToolConfig("github-copilot");
    SetupLegacyLocalToolConfig("github-copilot");
    SetupGlobalToolConfig("github-copilot");
    SetupLegacyGlobalToolConfig("github-copilot");
    
    // Act - CLI override should win over everything
    var ctx = AppContext.Create(toolOverride: "echo");
    
    // Assert
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_CompletePriorityChain_NewLocalWins()
  {
    // Arrange - Set up every level except override
    SetupLocalToolConfig("echo");
    SetupLegacyLocalToolConfig("github-copilot");
    SetupGlobalToolConfig("github-copilot");
    SetupLegacyGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - New local should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_CompletePriorityChain_LegacyLocalWins()
  {
    // Arrange - Skip new local, set rest
    SetupLegacyLocalToolConfig("echo");
    SetupGlobalToolConfig("github-copilot");
    SetupLegacyGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Legacy local should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_CompletePriorityChain_NewGlobalWins()
  {
    // Arrange - Skip all local configs
    SetupGlobalToolConfig("echo");
    SetupLegacyGlobalToolConfig("github-copilot");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - New global should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_CompletePriorityChain_LegacyGlobalWins()
  {
    // Arrange - Only legacy global
    SetupLegacyGlobalToolConfig("echo");
    
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Legacy global should win
    await Assert.That(ctx.ActiveTool.Name).IsEqualTo("echo");
  }

  [Test]
  public async Task Create_LoadsAllOtherConfigs()
  {
    // Act
    var ctx = AppContext.Create(toolOverride: null);
    
    // Assert - Verify other configs are loaded
    await Assert.That(ctx.ImageConfig).IsNotNull();
    await Assert.That(ctx.ModelConfig).IsNotNull();
    await Assert.That(ctx.MountsConfig).IsNotNull();
    await Assert.That(ctx.AirlockConfig).IsNotNull();
    await Assert.That(ctx.Paths).IsNotNull();
    await Assert.That(ctx.Environment).IsNotNull();
  }

  [Test]
  public async Task Create_ToolHasCorrectProviders()
  {
    // Act
    var ctx = AppContext.Create(toolOverride: "github-copilot");
    
    // Assert
    await Assert.That(ctx.ActiveTool.GetAuthProvider()).IsNotNull();
    await Assert.That(ctx.ActiveTool.GetModelProvider()).IsNotNull();
  }

  [Test]
  public async Task Create_MultipleCallsWithSameOverride_ConsistentResults()
  {
    // Act
    var ctx1 = AppContext.Create(toolOverride: "echo");
    var ctx2 = AppContext.Create(toolOverride: "echo");
    
    // Assert - Should get same tool (though potentially different instances)
    await Assert.That(ctx1.ActiveTool.Name).IsEqualTo(ctx2.ActiveTool.Name);
    await Assert.That(ctx1.ActiveTool.Name).IsEqualTo("echo");
  }

  // Helper methods for setting up config files

  private void SetupLocalToolConfig(string toolName)
  {
    var configDir = Path.Combine(_testProjectDir, ".cli_mate");
    Directory.CreateDirectory(configDir);
    File.WriteAllText(Path.Combine(configDir, "tool.conf"), toolName);
  }

  private void SetupLegacyLocalToolConfig(string toolName)
  {
    var configDir = Path.Combine(_testProjectDir, ".copilot_here");
    Directory.CreateDirectory(configDir);
    File.WriteAllText(Path.Combine(configDir, "tool.conf"), toolName);
  }

  private void SetupGlobalToolConfig(string toolName)
  {
    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var configDir = Path.Combine(homeDir, ".config", "cli_mate");
    Directory.CreateDirectory(configDir);
    File.WriteAllText(Path.Combine(configDir, "tool.conf"), toolName);
  }

  private void SetupLegacyGlobalToolConfig(string toolName)
  {
    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var configDir = Path.Combine(homeDir, ".config", "copilot_here");
    Directory.CreateDirectory(configDir);
    File.WriteAllText(Path.Combine(configDir, "tool.conf"), toolName);
  }

  private void CleanupGlobalToolConfig()
  {
    try
    {
      var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      var toolConfigFile = Path.Combine(homeDir, ".config", "cli_mate", "tool.conf");
      if (File.Exists(toolConfigFile))
        File.Delete(toolConfigFile);
    }
    catch
    {
      // Ignore cleanup errors
    }
  }

  private void CleanupLegacyGlobalToolConfig()
  {
    try
    {
      var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
      var toolConfigFile = Path.Combine(homeDir, ".config", "copilot_here", "tool.conf");
      if (File.Exists(toolConfigFile))
        File.Delete(toolConfigFile);
    }
    catch
    {
      // Ignore cleanup errors
    }
  }
}
