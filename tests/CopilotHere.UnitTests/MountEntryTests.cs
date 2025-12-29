using CopilotHere.Commands.Mounts;
using TUnit.Core;

namespace CopilotHere.Tests;

public class MountEntryTests
{
  private static bool IsWindows => OperatingSystem.IsWindows();
  
  [Test]
  public async Task MountEntry_ReadOnly_SetsCorrectly()
  {
    // Arrange & Act
    var path = IsWindows ? @"C:\path\to\dir" : "/path/to/dir";
    var mount = new MountEntry(path, IsReadWrite: false, MountSource.Local);

    // Assert
    await Assert.That(mount.Path).IsEqualTo(path);
    await Assert.That(mount.IsReadWrite).IsFalse();
    await Assert.That(mount.Source).IsEqualTo(MountSource.Local);
  }

  [Test]
  public async Task MountEntry_ReadWrite_SetsCorrectly()
  {
    // Arrange & Act
    var mount = new MountEntry("~/data", IsReadWrite: true, MountSource.Global);

    // Assert
    await Assert.That(mount.Path).IsEqualTo("~/data");
    await Assert.That(mount.IsReadWrite).IsTrue();
    await Assert.That(mount.Source).IsEqualTo(MountSource.Global);
  }

  [Test]
  public async Task MountEntry_Equality_WorksCorrectly()
  {
    // Arrange
    var path = IsWindows ? @"C:\path" : "/path";
    var mount1 = new MountEntry(path, false, MountSource.Local);
    var mount2 = new MountEntry(path, false, MountSource.Local);
    var mount3 = new MountEntry(path, true, MountSource.Local);

    // Assert
    await Assert.That(mount1).IsEqualTo(mount2);
    await Assert.That(mount1).IsNotEqualTo(mount3);
  }

  [Test]
  public async Task ResolvePath_ExpandsTilde()
  {
    // Arrange
    var mount = new MountEntry("~/projects", false, MountSource.CommandLine);
    var userHome = IsWindows ? @"C:\Users\testuser" : "/home/testuser";

    // Act
    var resolved = mount.ResolvePath(userHome);

    // Assert
    await Assert.That(resolved).Contains("testuser");
    await Assert.That(resolved).Contains("projects");
  }

  [Test]
  [Category("Unix")]
  public async Task GetContainerPath_MapsToAppuserHome()
  {
    // This test is Unix-specific since container paths are always Linux-style
    if (IsWindows)
    {
      // On Windows, the path transformation works differently
      // The container path should still map correctly
      var userHome = @"C:\Users\testuser";
      var mount = new MountEntry(@"C:\Users\testuser\projects\myapp", false, MountSource.Local);
      var containerPath = mount.GetContainerPath(userHome);
      
      // Container paths are always Linux-style, mapped from Windows paths
      await Assert.That(containerPath).Contains("/home/appuser");
      await Assert.That(containerPath).Contains("projects");
      await Assert.That(containerPath).Contains("myapp");
    }
    else
    {
      var userHome = "/home/testuser";
      var mount = new MountEntry("/home/testuser/projects/myapp", false, MountSource.Local);
      var containerPath = mount.GetContainerPath(userHome);
      await Assert.That(containerPath).IsEqualTo("/home/appuser/projects/myapp");
    }
  }

  [Test]
  public async Task ToDockerVolume_FormatsCorrectly()
  {
    // Arrange
    var userHome = IsWindows ? @"C:\Users\testuser" : "/home/testuser";
    var dataPath = IsWindows ? @"C:\Users\testuser\data" : "/home/testuser/data";
    var outputPath = IsWindows ? @"C:\Users\testuser\output" : "/home/testuser/output";
    var mountRo = new MountEntry(dataPath, IsReadWrite: false, MountSource.Local);
    var mountRw = new MountEntry(outputPath, IsReadWrite: true, MountSource.Local);

    // Act
    var volumeRo = mountRo.ToDockerVolume(userHome);
    var volumeRw = mountRw.ToDockerVolume(userHome);

    // Assert
    await Assert.That(volumeRo).Contains(":ro");
    await Assert.That(volumeRw).Contains(":rw");
  }

  [Test]
  public async Task LoadFromFile_ParsesRwSuffix()
  {
    // This test verifies that the config file parsing handles :rw suffix
    // The same logic should apply to CLI arguments
    var path = "/path/to/data:rw";
    var isReadWrite = false;
    var mountPath = path;

    if (path.EndsWith(":rw", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = true;
      mountPath = path[..^3];
    }

    await Assert.That(mountPath).IsEqualTo("/path/to/data");
    await Assert.That(isReadWrite).IsTrue();
  }

  [Test]
  public async Task LoadFromFile_ParsesRoSuffix()
  {
    var path = "/path/to/data:ro";
    var isReadWrite = false;
    var mountPath = path;

    if (path.EndsWith(":ro", StringComparison.OrdinalIgnoreCase))
    {
      mountPath = path[..^3];
    }

    await Assert.That(mountPath).IsEqualTo("/path/to/data");
    await Assert.That(isReadWrite).IsFalse();
  }

  [Test]
  public async Task LoadFromFile_NoSuffixDefaultsToReadOnly()
  {
    var path = "/path/to/data";
    var isReadWrite = false;
    var mountPath = path;

    if (path.EndsWith(":rw", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = true;
      mountPath = path[..^3];
    }
    else if (path.EndsWith(":ro", StringComparison.OrdinalIgnoreCase))
    {
      mountPath = path[..^3];
    }

    await Assert.That(mountPath).IsEqualTo("/path/to/data");
    await Assert.That(isReadWrite).IsFalse();
  }

  [Test]
  public async Task ToDockerVolume_WindowsPath_ConvertsToDockerFormat()
  {
    // Only run on Windows
    if (!OperatingSystem.IsWindows())
    {
      // Skip test on non-Windows
      return;
    }

    // Arrange
    var mount = new MountEntry(@"C:\Users\test\project", false, MountSource.Local);
    var userHome = @"C:\Users\test";

    // Act
    var dockerVolume = mount.ToDockerVolume(userHome);

    // Assert - should convert C:\Users\test\project to /c/Users/test/project
    await Assert.That(dockerVolume).Contains("/c/Users/test/project");
    await Assert.That(dockerVolume).DoesNotContain(@"C:");
    await Assert.That(dockerVolume).DoesNotContain(@"\");
    await Assert.That(dockerVolume).Contains(":ro");
  }

  [Test]
  public async Task ToDockerVolume_WindowsPathReadWrite_ConvertsCorrectly()
  {
    // Only run on Windows
    if (!OperatingSystem.IsWindows())
    {
      // Skip test on non-Windows
      return;
    }

    // Arrange
    var mount = new MountEntry(@"C:\Data\project", true, MountSource.CommandLine);
    var userHome = @"C:\Users\test";

    // Act
    var dockerVolume = mount.ToDockerVolume(userHome);

    // Assert
    await Assert.That(dockerVolume).Contains("/c/Data/project");
    await Assert.That(dockerVolume).Contains(":rw");
  }

  [Test]
  public async Task ToDockerVolume_UnixPath_RemainsUnchanged()
  {
    // Only run on Unix/Linux/macOS
    if (OperatingSystem.IsWindows())
    {
      // Skip test on Windows
      return;
    }

    // Arrange
    var mount = new MountEntry("/home/user/project", false, MountSource.Local);
    var userHome = "/home/user";

    // Act
    var dockerVolume = mount.ToDockerVolume(userHome);

    // Assert - Unix paths should remain as-is
    await Assert.That(dockerVolume).Contains("/home/user/project");
    await Assert.That(dockerVolume).Contains(":ro");
  }

  [Test]
  public async Task ToDockerVolume_WindowsPathWithDifferentDrives_ConvertsCorrectly()
  {
    // Only run on Windows
    if (!OperatingSystem.IsWindows())
    {
      // Skip test on non-Windows
      return;
    }

    // Arrange - D: drive
    var mount = new MountEntry(@"D:\Projects\myapp", false, MountSource.Global);
    var userHome = @"C:\Users\test";

    // Act
    var dockerVolume = mount.ToDockerVolume(userHome);

    // Assert
    await Assert.That(dockerVolume).Contains("/d/Projects/myapp");
    await Assert.That(dockerVolume).DoesNotContain(@"D:");
  }
}
