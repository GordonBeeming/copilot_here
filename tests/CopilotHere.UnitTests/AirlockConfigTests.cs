using CopilotHere.Commands.Airlock;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class AirlockConfigTests
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
  public async Task Load_NoConfigs_ReturnsDisabled()
  {
    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsFalse();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Default);
    await Assert.That(config.RulesPath).IsNull();
  }

  [Test]
  public async Task Load_OnlyGlobal_ReturnsGlobal()
  {
    // Arrange
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var networkConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(globalRulesPath, networkConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Global);
    await Assert.That(config.RulesPath).IsEqualTo(globalRulesPath);
    await Assert.That(config.RulesSource).IsEqualTo(AirlockConfigSource.Global);
  }

  [Test]
  public async Task Load_OnlyLocal_ReturnsLocal()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    var networkConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(localRulesPath, networkConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Local);
    await Assert.That(config.RulesPath).IsEqualTo(localRulesPath);
  }

  [Test]
  public async Task Load_BothGlobalAndLocal_LocalTakesPriority()
  {
    // Arrange
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var localRulesPath = _paths.GetLocalPath("network.json");
    
    // Global is enabled
    var globalConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(globalRulesPath, globalConfig);
    
    // Local is disabled (should win)
    var localConfig = NetworkConfig.CreateDefault(enabled: false);
    AirlockConfig.WriteNetworkConfig(localRulesPath, localConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert - Local should win
    await Assert.That(config.Enabled).IsFalse();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Local);
    await Assert.That(config.RulesPath).IsEqualTo(localRulesPath);
    await Assert.That(config.RulesSource).IsEqualTo(AirlockConfigSource.Local);
  }

  [Test]
  public async Task EnableLocal_CreatesFile()
  {
    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    var localRulesPath = _paths.GetLocalPath("network.json");
    await Assert.That(File.Exists(localRulesPath)).IsTrue();
    
    var networkConfig = AirlockConfig.ReadNetworkConfig(localRulesPath);
    await Assert.That(networkConfig).IsNotNull();
    await Assert.That(networkConfig!.Enabled).IsTrue();
  }

  [Test]
  public async Task EnableGlobal_CreatesFile()
  {
    // Act
    AirlockConfig.EnableGlobal(_paths);

    // Assert
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    await Assert.That(File.Exists(globalRulesPath)).IsTrue();
    
    var networkConfig = AirlockConfig.ReadNetworkConfig(globalRulesPath);
    await Assert.That(networkConfig).IsNotNull();
    await Assert.That(networkConfig!.Enabled).IsTrue();
  }

  [Test]
  public async Task DisableLocal_SetsEnabledFalse()
  {
    // Arrange
    AirlockConfig.EnableLocal(_paths);

    // Act
    AirlockConfig.DisableLocal(_paths);

    // Assert
    var localRulesPath = _paths.GetLocalPath("network.json");
    var networkConfig = AirlockConfig.ReadNetworkConfig(localRulesPath);
    await Assert.That(networkConfig!.Enabled).IsFalse();
  }

  [Test]
  public async Task DisableGlobal_SetsEnabledFalse()
  {
    // Arrange
    AirlockConfig.EnableGlobal(_paths);

    // Act
    AirlockConfig.DisableGlobal(_paths);

    // Assert
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var networkConfig = AirlockConfig.ReadNetworkConfig(globalRulesPath);
    await Assert.That(networkConfig!.Enabled).IsFalse();
  }

  [Test]
  public async Task ReadNetworkConfig_NonexistentFile_ReturnsNull()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "nonexistent.json");

    // Act
    var config = AirlockConfig.ReadNetworkConfig(path);

    // Assert
    await Assert.That(config).IsNull();
  }
}
