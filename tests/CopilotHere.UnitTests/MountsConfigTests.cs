using CopilotHere.Commands.Mounts;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class MountsConfigTests
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
  public async Task Load_NoConfigs_ReturnsEmpty()
  {
    // Act
    var config = MountsConfig.Load(_paths);

    // Assert
    await Assert.That(config.GlobalMounts).HasCount().EqualTo(0);
    await Assert.That(config.LocalMounts).HasCount().EqualTo(0);
  }

  [Test]
  public async Task Load_OnlyGlobal_ReturnsGlobalMounts()
  {
    // Arrange
    var globalConfigPath = _paths.GetGlobalPath("mounts.conf");
    File.WriteAllText(globalConfigPath, "/global/path1\n/global/path2:rw\n");

    // Act
    var config = MountsConfig.Load(_paths);

    // Assert
    await Assert.That(config.GlobalMounts).HasCount().EqualTo(2);
    await Assert.That(config.GlobalMounts[0].HostPath).IsEqualTo("/global/path1");
    await Assert.That(config.GlobalMounts[0].IsReadWrite).IsFalse();
    await Assert.That(config.GlobalMounts[0].Source).IsEqualTo(MountSource.Global);
    await Assert.That(config.GlobalMounts[1].HostPath).IsEqualTo("/global/path2");
    await Assert.That(config.GlobalMounts[1].IsReadWrite).IsTrue();
  }

  [Test]
  public async Task Load_OnlyLocal_ReturnsLocalMounts()
  {
    // Arrange
    var localConfigPath = _paths.GetLocalPath("mounts.conf");
    File.WriteAllText(localConfigPath, "/local/path1\n/local/path2:ro\n");

    // Act
    var config = MountsConfig.Load(_paths);

    // Assert
    await Assert.That(config.LocalMounts).HasCount().EqualTo(2);
    await Assert.That(config.LocalMounts[0].HostPath).IsEqualTo("/local/path1");
    await Assert.That(config.LocalMounts[0].IsReadWrite).IsFalse();
    await Assert.That(config.LocalMounts[0].Source).IsEqualTo(MountSource.Local);
    await Assert.That(config.LocalMounts[1].HostPath).IsEqualTo("/local/path2");
    await Assert.That(config.LocalMounts[1].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Load_BothGlobalAndLocal_LoadsBothSeparately()
  {
    // Arrange
    File.WriteAllText(_paths.GetGlobalPath("mounts.conf"), "/global/path\n");
    File.WriteAllText(_paths.GetLocalPath("mounts.conf"), "/local/path\n");

    // Act
    var config = MountsConfig.Load(_paths);

    // Assert - Both should be loaded separately
    await Assert.That(config.GlobalMounts).HasCount().EqualTo(1);
    await Assert.That(config.LocalMounts).HasCount().EqualTo(1);
    await Assert.That(config.GlobalMounts[0].HostPath).IsEqualTo("/global/path");
    await Assert.That(config.LocalMounts[0].HostPath).IsEqualTo("/local/path");
  }

  [Test]
  public async Task SaveLocal_AddsMount()
  {
    // Act
    MountsConfig.SaveLocal(_paths, "/data", isReadWrite: true);

    // Assert
    var lines = ConfigFile.ReadLines(_paths.GetLocalPath("mounts.conf")).ToList();
    await Assert.That(lines).HasCount().EqualTo(1);
    await Assert.That(lines[0]).IsEqualTo("/data:rw");
  }

  [Test]
  public async Task SaveLocal_ReadOnly_NoSuffix()
  {
    // Act
    MountsConfig.SaveLocal(_paths, "/data", isReadWrite: false);

    // Assert
    var lines = ConfigFile.ReadLines(_paths.GetLocalPath("mounts.conf")).ToList();
    await Assert.That(lines).HasCount().EqualTo(1);
    await Assert.That(lines[0]).IsEqualTo("/data");
  }

  [Test]
  public async Task SaveGlobal_AddsMount()
  {
    // Act
    MountsConfig.SaveGlobal(_paths, "/shared", isReadWrite: true);

    // Assert
    var lines = ConfigFile.ReadLines(_paths.GetGlobalPath("mounts.conf")).ToList();
    await Assert.That(lines).HasCount().EqualTo(1);
    await Assert.That(lines[0]).IsEqualTo("/shared:rw");
  }

  [Test]
  public async Task Remove_RemovesFromBothConfigs()
  {
    // Arrange
    MountsConfig.SaveLocal(_paths, "/data", isReadWrite: false);
    MountsConfig.SaveGlobal(_paths, "/data", isReadWrite: false);

    // Act
    var result = MountsConfig.Remove(_paths, "/data");

    // Assert
    await Assert.That(result).IsTrue();
    var localLines = ConfigFile.ReadLines(_paths.GetLocalPath("mounts.conf")).ToList();
    var globalLines = ConfigFile.ReadLines(_paths.GetGlobalPath("mounts.conf")).ToList();
    await Assert.That(localLines).HasCount().EqualTo(0);
    await Assert.That(globalLines).HasCount().EqualTo(0);
  }

  [Test]
  public async Task ParsesCommentsAndEmptyLines()
  {
    // Arrange
    var configPath = _paths.GetGlobalPath("mounts.conf");
    File.WriteAllText(configPath, @"# Comment
/path1
   
/path2:rw
# Another comment
/path3:ro
");

    // Act
    var config = MountsConfig.Load(_paths);

    // Assert
    await Assert.That(config.GlobalMounts).HasCount().EqualTo(3);
    await Assert.That(config.GlobalMounts[0].HostPath).IsEqualTo("/path1");
    await Assert.That(config.GlobalMounts[1].HostPath).IsEqualTo("/path2");
    await Assert.That(config.GlobalMounts[1].IsReadWrite).IsTrue();
    await Assert.That(config.GlobalMounts[2].HostPath).IsEqualTo("/path3");
    await Assert.That(config.GlobalMounts[2].IsReadWrite).IsFalse();
  }
}
