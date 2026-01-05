using CopilotHere.Commands.Model;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ModelConfigTests
{
  private string _tempDir = null!;
  private AppPaths _paths = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);
    
    var globalConfigPath = Path.Combine(_tempDir, "global");
    var localConfigPath = Path.Combine(_tempDir, "local");
    Directory.CreateDirectory(globalConfigPath);
    Directory.CreateDirectory(localConfigPath);

    _paths = new AppPaths
    {
      UserHome = "/home/testuser",
      GlobalConfigPath = globalConfigPath,
      LocalConfigPath = localConfigPath,
      CurrentDirectory = _tempDir,
      ContainerWorkDir = "/work",
      CopilotConfigPath = Path.Combine(_tempDir, ".copilot")
    };
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task Load_NoConfigs_ReturnsDefault()
  {
    // Act
    var config = ModelConfig.Load(_paths);

    // Assert
    await Assert.That(config.Model).IsNull();
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Default);
  }

  [Test]
  public async Task Load_OnlyGlobal_ReturnsGlobal()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetGlobalPath("model.conf"), "claude-sonnet-4.5");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert
    await Assert.That(config.Model).IsEqualTo("claude-sonnet-4.5");
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Global);
  }

  [Test]
  public async Task Load_OnlyLocal_ReturnsLocal()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetLocalPath("model.conf"), "gpt-5");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert
    await Assert.That(config.Model).IsEqualTo("gpt-5");
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Local);
  }

  [Test]
  public async Task Load_BothGlobalAndLocal_LocalTakesPriority()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetGlobalPath("model.conf"), "claude-haiku-4.5");
    ConfigFile.WriteValue(_paths.GetLocalPath("model.conf"), "gpt-5.2");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert - Local should win
    await Assert.That(config.Model).IsEqualTo("gpt-5.2");
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Local);
    await Assert.That(config.GlobalModel).IsEqualTo("claude-haiku-4.5");
    await Assert.That(config.LocalModel).IsEqualTo("gpt-5.2");
  }

  [Test]
  public async Task SaveLocal_CreatesFile()
  {
    // Act
    ModelConfig.SaveLocal(_paths, "claude-opus-4.5");

    // Assert
    var saved = ConfigFile.ReadValue(_paths.GetLocalPath("model.conf"));
    await Assert.That(saved).IsEqualTo("claude-opus-4.5");
  }

  [Test]
  public async Task SaveGlobal_CreatesFile()
  {
    // Act
    ModelConfig.SaveGlobal(_paths, "gpt-5.1-codex");

    // Assert
    var saved = ConfigFile.ReadValue(_paths.GetGlobalPath("model.conf"));
    await Assert.That(saved).IsEqualTo("gpt-5.1-codex");
  }

  [Test]
  public async Task ClearLocal_RemovesFile()
  {
    // Arrange
    ModelConfig.SaveLocal(_paths, "gemini-3-pro-preview");

    // Act
    var result = ModelConfig.ClearLocal(_paths);

    // Assert
    await Assert.That(result).IsTrue();
    await Assert.That(File.Exists(_paths.GetLocalPath("model.conf"))).IsFalse();
  }

  [Test]
  public async Task ClearGlobal_RemovesFile()
  {
    // Arrange
    ModelConfig.SaveGlobal(_paths, "gpt-4.1");

    // Act
    var result = ModelConfig.ClearGlobal(_paths);

    // Assert
    await Assert.That(result).IsTrue();
    await Assert.That(File.Exists(_paths.GetGlobalPath("model.conf"))).IsFalse();
  }

  [Test]
  public async Task Load_LocalSetToDefault_TreatsAsNull()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetLocalPath("model.conf"), "default");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert
    await Assert.That(config.Model).IsNull();
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Default);
  }

  [Test]
  public async Task Load_GlobalSetToDefault_TreatsAsNull()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetGlobalPath("model.conf"), "default");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert
    await Assert.That(config.Model).IsNull();
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Default);
  }

  [Test]
  public async Task Load_LocalDefaultAndGlobalHasModel_UsesGlobal()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetLocalPath("model.conf"), "default");
    ConfigFile.WriteValue(_paths.GetGlobalPath("model.conf"), "gpt-5");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert - Global should be used since local is "default" (null)
    await Assert.That(config.Model).IsEqualTo("gpt-5");
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Global);
  }

  [Test]
  public async Task Load_DefaultKeywordCaseInsensitive()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetLocalPath("model.conf"), "DEFAULT");
    ConfigFile.WriteValue(_paths.GetGlobalPath("model.conf"), "DeFaUlT");

    // Act
    var config = ModelConfig.Load(_paths);

    // Assert - Both should be treated as null
    await Assert.That(config.Model).IsNull();
    await Assert.That(config.Source).IsEqualTo(ModelConfigSource.Default);
  }
}
