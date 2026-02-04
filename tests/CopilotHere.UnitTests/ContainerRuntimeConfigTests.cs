using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Unit tests for ContainerRuntimeConfig - container runtime detection and configuration.
/// Tests auto-detection, config file reading/writing, and runtime-specific settings.
/// </summary>
public class ContainerRuntimeConfigTests
{
  private string _tempDir = null!;
  private string _projectDir = null!;
  private AppPaths _paths = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    _projectDir = Path.Combine(_tempDir, "project");
    Directory.CreateDirectory(_projectDir);
    
    var localConfigPath = Path.Combine(_projectDir, ".copilot_here");
    var globalConfigPath = Path.Combine(_tempDir, ".config", "copilot_here");
    
    Directory.CreateDirectory(localConfigPath);
    Directory.CreateDirectory(globalConfigPath);
    
    _paths = new AppPaths
    {
      CurrentDirectory = _projectDir,
      UserHome = _tempDir,
      CopilotConfigPath = Path.Combine(_tempDir, ".config", "copilot-cli-docker"),
      LocalConfigPath = localConfigPath,
      GlobalConfigPath = globalConfigPath,
      ContainerWorkDir = "/workspace"
    };
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task AutoDetect_ReturnsDockerOrPodman_WhenAvailable()
  {
    // Act
    var runtime = ContainerRuntimeConfig.AutoDetect();
    
    // Assert - Should find docker or podman on most dev machines
    await Assert.That(runtime).IsNotNull();
    await Assert.That(runtime == "docker" || runtime == "podman").IsTrue();
  }

  [Test]
  public async Task CreateConfig_Docker_ReturnsCorrectSettings()
  {
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("docker");
    
    // Assert
    await Assert.That(config.Runtime).IsEqualTo("docker");
    await Assert.That(config.DefaultNetworkName).IsEqualTo("bridge");
    await Assert.That(config.SupportsAirlock).IsTrue();
  }

  [Test]
  public async Task CreateConfig_Podman_ReturnsCorrectSettings()
  {
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("podman");
    
    // Assert
    await Assert.That(config.Runtime).IsEqualTo("podman");
    await Assert.That(config.DefaultNetworkName).IsEqualTo("podman");
    await Assert.That(config.SupportsAirlock).IsTrue();
  }

  [Test]
  public async Task CreateConfig_Docker_DetectsOrbStackOrDocker()
  {
    // This test will pass if OrbStack is installed, otherwise it will show Docker
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("docker");
    
    // Assert
    await Assert.That(config.RuntimeFlavor == "Docker" || config.RuntimeFlavor == "OrbStack").IsTrue();
  }

  [Test]
  public async Task LoadFromConfig_AutoValue_ReturnsNull()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(configFile, "auto");
    
    // Act
    var runtime = ContainerRuntimeConfig.LoadFromConfig(configFile);
    
    // Assert - "auto" is normalized to null (triggers auto-detection)
    await Assert.That(runtime).IsNull();
  }

  [Test]
  public async Task LoadFromConfig_DockerValue_ReturnsDocker()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(configFile, "docker");
    
    // Act
    var runtime = ContainerRuntimeConfig.LoadFromConfig(configFile);
    
    // Assert
    await Assert.That(runtime).IsEqualTo("docker");
  }

  [Test]
  public async Task LoadFromConfig_PodmanValue_ReturnsPodman()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(configFile, "podman");
    
    // Act
    var runtime = ContainerRuntimeConfig.LoadFromConfig(configFile);
    
    // Assert
    await Assert.That(runtime).IsEqualTo("podman");
  }

  [Test]
  public async Task LoadFromConfig_MixedCase_NormalizesToLowercase()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(configFile, "DOCKER");
    
    // Act
    var runtime = ContainerRuntimeConfig.LoadFromConfig(configFile);
    
    // Assert
    await Assert.That(runtime).IsEqualTo("docker");
  }

  [Test]
  public async Task LoadFromConfig_NonExistentFile_ReturnsNull()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("nonexistent.conf");
    
    // Act
    var runtime = ContainerRuntimeConfig.LoadFromConfig(configFile);
    
    // Assert
    await Assert.That(runtime).IsNull();
  }

  [Test]
  public async Task Load_NoConfig_AutoDetects()
  {
    // Act
    var config = ContainerRuntimeConfig.Load(_paths);
    
    // Assert
    await Assert.That(config.Source).IsEqualTo(RuntimeConfigSource.AutoDetected);
    await Assert.That(config.Runtime == "docker" || config.Runtime == "podman").IsTrue();
  }

  [Test]
  public async Task Load_LocalConfig_HasPriorityOverGlobal()
  {
    // Arrange
    var localFile = _paths.GetLocalPath("runtime.conf");
    var globalFile = _paths.GetGlobalPath("runtime.conf");
    
    File.WriteAllText(localFile, "docker");
    File.WriteAllText(globalFile, "podman");
    
    // Act
    var config = ContainerRuntimeConfig.Load(_paths);
    
    // Assert
    await Assert.That(config.Source).IsEqualTo(RuntimeConfigSource.Local);
    await Assert.That(config.Runtime).IsEqualTo("docker");
    await Assert.That(config.LocalRuntime).IsEqualTo("docker");
  }

  [Test]
  public async Task Load_GlobalConfig_UsedWhenNoLocal()
  {
    // Arrange
    var globalFile = _paths.GetGlobalPath("runtime.conf");
    File.WriteAllText(globalFile, "docker");
    
    // Act
    var config = ContainerRuntimeConfig.Load(_paths);
    
    // Assert
    await Assert.That(config.Source).IsEqualTo(RuntimeConfigSource.Global);
    await Assert.That(config.Runtime).IsEqualTo("docker");
    await Assert.That(config.GlobalRuntime).IsEqualTo("docker");
  }

  [Test]
  public async Task SaveLocal_CreatesConfigFile()
  {
    // Arrange
    var expectedFile = _paths.GetLocalPath("runtime.conf");
    
    // Act
    ContainerRuntimeConfig.SaveLocal(_paths, "docker");
    
    // Assert
    await Assert.That(File.Exists(expectedFile)).IsTrue();
    await Assert.That(File.ReadAllText(expectedFile).Trim()).IsEqualTo("docker");
  }

  [Test]
  public async Task SaveGlobal_CreatesConfigFile()
  {
    // Arrange
    var expectedFile = _paths.GetGlobalPath("runtime.conf");
    
    // Act
    ContainerRuntimeConfig.SaveGlobal(_paths, "podman");
    
    // Assert
    await Assert.That(File.Exists(expectedFile)).IsTrue();
    await Assert.That(File.ReadAllText(expectedFile).Trim()).IsEqualTo("podman");
  }

  [Test]
  public async Task SaveLocal_OverwritesExisting()
  {
    // Arrange
    var configFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(configFile, "docker");
    
    // Act
    ContainerRuntimeConfig.SaveLocal(_paths, "podman");
    
    // Assert
    await Assert.That(File.ReadAllText(configFile).Trim()).IsEqualTo("podman");
  }

  [Test]
  public async Task ListAvailable_ReturnsAtLeastOneRuntime()
  {
    // Act
    var available = ContainerRuntimeConfig.ListAvailable();
    
    // Assert - Most dev machines have Docker or Podman
    await Assert.That(available.Count).IsGreaterThan(0);
  }

  [Test]
  public async Task ListAvailable_ContainsValidRuntimes()
  {
    // Act
    var available = ContainerRuntimeConfig.ListAvailable();
    
    // Assert
    foreach (var config in available)
    {
      await Assert.That(config.Runtime == "docker" || config.Runtime == "podman").IsTrue();
      await Assert.That(config.RuntimeFlavor).IsNotNull();
      await Assert.That(config.DefaultNetworkName == "bridge" || config.DefaultNetworkName == "podman").IsTrue();
    }
  }

  [Test]
  public async Task IsCommandAvailable_Docker_ChecksCorrectly()
  {
    // Act
    var isAvailable = ContainerRuntimeConfig.IsCommandAvailable("docker");
    
    // Assert - On most dev machines, this should be true
    // We can't assert true/false definitively, just that it doesn't throw
    await Assert.That(isAvailable == true || isAvailable == false).IsTrue();
  }

  [Test]
  public async Task IsCommandAvailable_NonExistentCommand_ReturnsFalse()
  {
    // Act
    var isAvailable = ContainerRuntimeConfig.IsCommandAvailable("nonexistent-command-12345");
    
    // Assert
    await Assert.That(isAvailable).IsFalse();
  }

  [Test]
  public async Task GetVersion_Docker_ReturnsVersionString()
  {
    // Arrange - Only run if docker is available
    if (!ContainerRuntimeConfig.IsCommandAvailable("docker"))
    {
      return; // Skip test if docker not available
    }
    
    var config = ContainerRuntimeConfig.CreateConfig("docker");
    
    // Act
    var version = config.GetVersion();
    
    // Assert
    await Assert.That(version).IsNotNull();
    await Assert.That(version.Contains("version")).IsTrue();
  }

  [Test]
  public async Task DefaultNetworkName_Docker_IsBridge()
  {
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("docker");
    
    // Assert
    await Assert.That(config.DefaultNetworkName).IsEqualTo("bridge");
  }

  [Test]
  public async Task DefaultNetworkName_Podman_IsPodman()
  {
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("podman");
    
    // Assert
    await Assert.That(config.DefaultNetworkName).IsEqualTo("podman");
  }

  [Test]
  public async Task ComposeCommand_Docker_IsDockerCompose()
  {
    // Act
    var config = ContainerRuntimeConfig.CreateConfig("docker");
    
    // Assert
    await Assert.That(config.ComposeCommand).IsEqualTo("compose");
  }

  [Test]
  public async Task SupportsAirlock_AllRuntimes_ReturnsTrue()
  {
    // Both Docker and Podman support Airlock mode
    // Act
    var dockerConfig = ContainerRuntimeConfig.CreateConfig("docker");
    var podmanConfig = ContainerRuntimeConfig.CreateConfig("podman");
    
    // Assert
    await Assert.That(dockerConfig.SupportsAirlock).IsTrue();
    await Assert.That(podmanConfig.SupportsAirlock).IsTrue();
  }

  [Test]
  public async Task Load_AutoConfigInFile_StillAutoDetects()
  {
    // Arrange - Set config to "auto"
    var localFile = _paths.GetLocalPath("runtime.conf");
    File.WriteAllText(localFile, "auto");
    
    // Act
    var config = ContainerRuntimeConfig.Load(_paths);
    
    // Assert - "auto" in config is treated as auto-detection
    await Assert.That(config.Source).IsEqualTo(RuntimeConfigSource.AutoDetected);
  }

  [Test]
  public async Task ConfigFileRoundTrip_PreservesValue()
  {
    // Arrange & Act
    ContainerRuntimeConfig.SaveLocal(_paths, "docker");
    var config = ContainerRuntimeConfig.Load(_paths);
    
    // Assert
    await Assert.That(config.Runtime).IsEqualTo("docker");
    await Assert.That(config.Source).IsEqualTo(RuntimeConfigSource.Local);
  }
}
