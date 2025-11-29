using CopilotHere.Commands.Mounts;
using TUnit.Core;

namespace CopilotHere.Tests;

public class MountEntryTests
{
  [Test]
  public async Task MountEntry_ReadOnly_SetsCorrectly()
  {
    // Arrange & Act
    var mount = new MountEntry("/path/to/dir", IsReadWrite: false, MountSource.Local);

    // Assert
    await Assert.That(mount.Path).IsEqualTo("/path/to/dir");
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
    var mount1 = new MountEntry("/path", false, MountSource.Local);
    var mount2 = new MountEntry("/path", false, MountSource.Local);
    var mount3 = new MountEntry("/path", true, MountSource.Local);

    // Assert
    await Assert.That(mount1).IsEqualTo(mount2);
    await Assert.That(mount1).IsNotEqualTo(mount3);
  }

  [Test]
  public async Task ResolvePath_ExpandsTilde()
  {
    // Arrange
    var mount = new MountEntry("~/projects", false, MountSource.CommandLine);
    var userHome = "/home/testuser";

    // Act
    var resolved = mount.ResolvePath(userHome);

    // Assert
    await Assert.That(resolved).Contains("/home/testuser");
    await Assert.That(resolved).Contains("projects");
  }

  [Test]
  public async Task GetContainerPath_MapsToAppuserHome()
  {
    // Arrange
    var userHome = "/home/testuser";
    var mount = new MountEntry("/home/testuser/projects/myapp", false, MountSource.Local);

    // Act
    var containerPath = mount.GetContainerPath(userHome);

    // Assert
    await Assert.That(containerPath).IsEqualTo("/home/appuser/projects/myapp");
  }

  [Test]
  public async Task ToDockerVolume_FormatsCorrectly()
  {
    // Arrange
    var userHome = "/home/testuser";
    var mountRo = new MountEntry("/home/testuser/data", IsReadWrite: false, MountSource.Local);
    var mountRw = new MountEntry("/home/testuser/output", IsReadWrite: true, MountSource.Local);

    // Act
    var volumeRo = mountRo.ToDockerVolume(userHome);
    var volumeRw = mountRw.ToDockerVolume(userHome);

    // Assert
    await Assert.That(volumeRo).Contains(":ro");
    await Assert.That(volumeRw).Contains(":rw");
  }
}
