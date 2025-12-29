using CopilotHere.Commands.Images;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ImageConfigTests
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
    var config = ImageConfig.Load(_paths);

    // Assert
    await Assert.That(config.Tag).IsEqualTo("latest");
    await Assert.That(config.Source).IsEqualTo(ImageConfigSource.Default);
  }

  [Test]
  public async Task Load_OnlyGlobal_ReturnsGlobal()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetGlobalPath("image.conf"), "dotnet");

    // Act
    var config = ImageConfig.Load(_paths);

    // Assert
    await Assert.That(config.Tag).IsEqualTo("dotnet");
    await Assert.That(config.Source).IsEqualTo(ImageConfigSource.Global);
  }

  [Test]
  public async Task Load_OnlyLocal_ReturnsLocal()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetLocalPath("image.conf"), "playwright");

    // Act
    var config = ImageConfig.Load(_paths);

    // Assert
    await Assert.That(config.Tag).IsEqualTo("playwright");
    await Assert.That(config.Source).IsEqualTo(ImageConfigSource.Local);
  }

  [Test]
  public async Task Load_BothGlobalAndLocal_LocalTakesPriority()
  {
    // Arrange
    ConfigFile.WriteValue(_paths.GetGlobalPath("image.conf"), "dotnet");
    ConfigFile.WriteValue(_paths.GetLocalPath("image.conf"), "playwright");

    // Act
    var config = ImageConfig.Load(_paths);

    // Assert - Local should win
    await Assert.That(config.Tag).IsEqualTo("playwright");
    await Assert.That(config.Source).IsEqualTo(ImageConfigSource.Local);
    await Assert.That(config.GlobalTag).IsEqualTo("dotnet");
    await Assert.That(config.LocalTag).IsEqualTo("playwright");
  }

  [Test]
  public async Task SaveLocal_CreatesFile()
  {
    // Act
    ImageConfig.SaveLocal(_paths, "rust");

    // Assert
    var saved = ConfigFile.ReadValue(_paths.GetLocalPath("image.conf"));
    await Assert.That(saved).IsEqualTo("rust");
  }

  [Test]
  public async Task SaveGlobal_CreatesFile()
  {
    // Act
    ImageConfig.SaveGlobal(_paths, "dotnet-10");

    // Assert
    var saved = ConfigFile.ReadValue(_paths.GetGlobalPath("image.conf"));
    await Assert.That(saved).IsEqualTo("dotnet-10");
  }

  [Test]
  public async Task ClearLocal_RemovesFile()
  {
    // Arrange
    ImageConfig.SaveLocal(_paths, "playwright");

    // Act
    var result = ImageConfig.ClearLocal(_paths);

    // Assert
    await Assert.That(result).IsTrue();
    await Assert.That(File.Exists(_paths.GetLocalPath("image.conf"))).IsFalse();
  }

  [Test]
  public async Task ClearGlobal_RemovesFile()
  {
    // Arrange
    ImageConfig.SaveGlobal(_paths, "dotnet");

    // Act
    var result = ImageConfig.ClearGlobal(_paths);

    // Assert
    await Assert.That(result).IsTrue();
    await Assert.That(File.Exists(_paths.GetGlobalPath("image.conf"))).IsFalse();
  }
}
