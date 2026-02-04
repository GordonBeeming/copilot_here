using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Integration tests for tool configuration file reading/writing.
/// Tests the actual file I/O operations for tool.conf files.
/// </summary>
public class ToolConfigTests
{
  private string _tempDir = null!;
  private string _globalConfigDir = null!;
  private string _localConfigDir = null!;
  private string _legacyGlobalConfigDir = null!;
  private string _legacyLocalConfigDir = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);
    
    // Create directory structure matching real usage
    _globalConfigDir = Path.Combine(_tempDir, ".config", "cli_mate");
    _localConfigDir = Path.Combine(_tempDir, "project", ".cli_mate");
    _legacyGlobalConfigDir = Path.Combine(_tempDir, ".config", "copilot_here");
    _legacyLocalConfigDir = Path.Combine(_tempDir, "project", ".copilot_here");
    
    Directory.CreateDirectory(_globalConfigDir);
    Directory.CreateDirectory(_localConfigDir);
    Directory.CreateDirectory(_legacyGlobalConfigDir);
    Directory.CreateDirectory(_legacyLocalConfigDir);
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task WriteAndReadLocal_ToolConfig_RoundTrips()
  {
    // Arrange
    var toolConfigFile = Path.Combine(_localConfigDir, "tool.conf");
    
    // Act
    File.WriteAllText(toolConfigFile, "echo");
    var read = File.ReadAllText(toolConfigFile).Trim();
    
    // Assert
    await Assert.That(read).IsEqualTo("echo");
  }

  [Test]
  public async Task WriteAndReadGlobal_ToolConfig_RoundTrips()
  {
    // Arrange
    var toolConfigFile = Path.Combine(_globalConfigDir, "tool.conf");
    
    // Act
    File.WriteAllText(toolConfigFile, "github-copilot");
    var read = File.ReadAllText(toolConfigFile).Trim();
    
    // Assert
    await Assert.That(read).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task LocalToolConfig_TakesPriorityOverGlobal()
  {
    // Arrange
    var localToolFile = Path.Combine(_localConfigDir, "tool.conf");
    var globalToolFile = Path.Combine(_globalConfigDir, "tool.conf");
    
    File.WriteAllText(localToolFile, "echo");
    File.WriteAllText(globalToolFile, "github-copilot");
    
    // Act - read local first
    string? tool = null;
    if (File.Exists(localToolFile))
      tool = File.ReadAllText(localToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("echo");
  }

  [Test]
  public async Task NoLocalConfig_FallsBackToGlobal()
  {
    // Arrange
    var localToolFile = Path.Combine(_localConfigDir, "tool.conf");
    var globalToolFile = Path.Combine(_globalConfigDir, "tool.conf");
    
    File.WriteAllText(globalToolFile, "github-copilot");
    
    // Act - read local first, then global
    string? tool = null;
    if (File.Exists(localToolFile))
      tool = File.ReadAllText(localToolFile).Trim();
    else if (File.Exists(globalToolFile))
      tool = File.ReadAllText(globalToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task LegacyLocalConfig_StillWorks()
  {
    // Arrange
    var legacyToolFile = Path.Combine(_legacyLocalConfigDir, "tool.conf");
    File.WriteAllText(legacyToolFile, "echo");
    
    // Act
    var tool = File.ReadAllText(legacyToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("echo");
  }

  [Test]
  public async Task LegacyGlobalConfig_StillWorks()
  {
    // Arrange
    var legacyToolFile = Path.Combine(_legacyGlobalConfigDir, "tool.conf");
    File.WriteAllText(legacyToolFile, "github-copilot");
    
    // Act
    var tool = File.ReadAllText(legacyToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("github-copilot");
  }

  [Test]
  public async Task NewLocalConfig_TakesPriorityOverLegacyLocal()
  {
    // Arrange
    var newToolFile = Path.Combine(_localConfigDir, "tool.conf");
    var legacyToolFile = Path.Combine(_legacyLocalConfigDir, "tool.conf");
    
    File.WriteAllText(newToolFile, "echo");
    File.WriteAllText(legacyToolFile, "github-copilot");
    
    // Act - read new first
    string? tool = null;
    if (File.Exists(newToolFile))
      tool = File.ReadAllText(newToolFile).Trim();
    else if (File.Exists(legacyToolFile))
      tool = File.ReadAllText(legacyToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("echo");
  }

  [Test]
  public async Task NewGlobalConfig_TakesPriorityOverLegacyGlobal()
  {
    // Arrange
    var newToolFile = Path.Combine(_globalConfigDir, "tool.conf");
    var legacyToolFile = Path.Combine(_legacyGlobalConfigDir, "tool.conf");
    
    File.WriteAllText(newToolFile, "echo");
    File.WriteAllText(legacyToolFile, "github-copilot");
    
    // Act - read new first
    string? tool = null;
    if (File.Exists(newToolFile))
      tool = File.ReadAllText(newToolFile).Trim();
    else if (File.Exists(legacyToolFile))
      tool = File.ReadAllText(legacyToolFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("echo");
  }

  [Test]
  public async Task EmptyToolConfig_ReturnsEmptyString()
  {
    // Arrange
    var toolConfigFile = Path.Combine(_localConfigDir, "tool.conf");
    File.WriteAllText(toolConfigFile, "");
    
    // Act
    var tool = File.ReadAllText(toolConfigFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEmpty();
  }

  [Test]
  public async Task WhitespaceToolConfig_TrimmedToEmpty()
  {
    // Arrange
    var toolConfigFile = Path.Combine(_localConfigDir, "tool.conf");
    File.WriteAllText(toolConfigFile, "   \n\t  ");
    
    // Act
    var tool = File.ReadAllText(toolConfigFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEmpty();
  }

  [Test]
  public async Task ToolConfigWithWhitespace_TrimsCorrectly()
  {
    // Arrange
    var toolConfigFile = Path.Combine(_localConfigDir, "tool.conf");
    File.WriteAllText(toolConfigFile, "  echo  \n");
    
    // Act
    var tool = File.ReadAllText(toolConfigFile).Trim();
    
    // Assert
    await Assert.That(tool).IsEqualTo("echo");
  }

  [Test]
  public async Task ConfigFileReadValue_HandlesNonExistentFile()
  {
    // Arrange
    var nonExistentFile = Path.Combine(_localConfigDir, "nonexistent.conf");
    
    // Act
    var value = ConfigFile.ReadValue(nonExistentFile);
    
    // Assert
    await Assert.That(value).IsNull();
  }

  [Test]
  public async Task ConfigFileWriteValue_CreatesDirectoryIfNeeded()
  {
    // Arrange
    var newDir = Path.Combine(_tempDir, "newdir", "subdir");
    var configFile = Path.Combine(newDir, "tool.conf");
    
    // Act
    ConfigFile.WriteValue(configFile, "echo");
    
    // Assert
    await Assert.That(Directory.Exists(newDir)).IsTrue();
    await Assert.That(File.Exists(configFile)).IsTrue();
    await Assert.That(ConfigFile.ReadValue(configFile)).IsEqualTo("echo");
  }

  [Test]
  public async Task ConfigFileWriteValue_OverwritesExistingFile()
  {
    // Arrange
    var configFile = Path.Combine(_localConfigDir, "tool.conf");
    ConfigFile.WriteValue(configFile, "github-copilot");
    
    // Act
    ConfigFile.WriteValue(configFile, "echo");
    
    // Assert
    await Assert.That(ConfigFile.ReadValue(configFile)).IsEqualTo("echo");
  }

  [Test]
  public async Task ToolConfigPriority_CompleteScenario()
  {
    // Arrange - Set up all config files
    var newLocal = Path.Combine(_localConfigDir, "tool.conf");
    var legacyLocal = Path.Combine(_legacyLocalConfigDir, "tool.conf");
    var newGlobal = Path.Combine(_globalConfigDir, "tool.conf");
    var legacyGlobal = Path.Combine(_legacyGlobalConfigDir, "tool.conf");
    
    File.WriteAllText(newLocal, "local-new");
    File.WriteAllText(legacyLocal, "local-legacy");
    File.WriteAllText(newGlobal, "global-new");
    File.WriteAllText(legacyGlobal, "global-legacy");
    
    // Act - Simulate priority resolution
    string? tool = null;
    
    // Priority 1: New local
    if (File.Exists(newLocal))
      tool = File.ReadAllText(newLocal).Trim();
    
    // Priority 2: Legacy local
    if (string.IsNullOrWhiteSpace(tool) && File.Exists(legacyLocal))
      tool = File.ReadAllText(legacyLocal).Trim();
    
    // Priority 3: New global
    if (string.IsNullOrWhiteSpace(tool) && File.Exists(newGlobal))
      tool = File.ReadAllText(newGlobal).Trim();
    
    // Priority 4: Legacy global
    if (string.IsNullOrWhiteSpace(tool) && File.Exists(legacyGlobal))
      tool = File.ReadAllText(legacyGlobal).Trim();
    
    // Assert - Should pick new local
    await Assert.That(tool).IsEqualTo("local-new");
  }
}
