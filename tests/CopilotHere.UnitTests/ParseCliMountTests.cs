using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;

namespace CopilotHere.Tests;

public class ParseCliMountTests
{
  [Test]
  public async Task ParseCliMount_SimplePath_DefaultReadOnly()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/path/to/dir", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
    await Assert.That(mount.IsReadWrite).IsFalse();
    await Assert.That(mount.Source).IsEqualTo(MountSource.CommandLine);
  }

  [Test]
  public async Task ParseCliMount_WithQuotes_TrimsQuotes()
  {
    // Act
    var mount = RunCommand.ParseCliMount("'/path/to/dir'", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
  }

  [Test]
  public async Task ParseCliMount_WithDoubleQuotes_TrimsQuotes()
  {
    // Act
    var mount = RunCommand.ParseCliMount("\"/path/to/dir\"", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
  }

  [Test]
  public async Task ParseCliMount_WithQuotesAndReadOnly_CorrectlyParsesReadOnly()
  {
    // Act
    var mount = RunCommand.ParseCliMount("'/path/to/dir:ro'", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
    await Assert.That(mount.IsReadWrite).IsFalse();
  }

  [Test]
  public async Task ParseCliMount_WithQuotesAndReadWrite_CorrectlyParsesReadWrite()
  {
    // Act
    var mount = RunCommand.ParseCliMount("'/path/to/dir:rw'", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
    await Assert.That(mount.IsReadWrite).IsTrue();
  }

  [Test]
  public async Task ParseCliMount_ReadOnlySuffix_OverridesDefault()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/path/to/dir:ro", defaultReadWrite: true);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
    await Assert.That(mount.IsReadWrite).IsFalse();
  }

  [Test]
  public async Task ParseCliMount_ReadWriteSuffix_OverridesDefault()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/path/to/dir:rw", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/path/to/dir");
    await Assert.That(mount.IsReadWrite).IsTrue();
  }

  [Test]
  public async Task ParseCliMount_HostContainerFormat_ParsesBothPaths()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/host/path:/container/path", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/host/path");
    await Assert.That(mount.ContainerPath).IsEqualTo("/container/path");
    await Assert.That(mount.IsReadWrite).IsFalse();
  }

  [Test]
  public async Task ParseCliMount_HostContainerWithReadOnly_ParsesCorrectly()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/host/path:/container/path:ro", defaultReadWrite: true);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/host/path");
    await Assert.That(mount.ContainerPath).IsEqualTo("/container/path");
    await Assert.That(mount.IsReadWrite).IsFalse();
  }

  [Test]
  public async Task ParseCliMount_HostContainerWithReadWrite_ParsesCorrectly()
  {
    // Act
    var mount = RunCommand.ParseCliMount("/host/path:/container/path:rw", defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/host/path");
    await Assert.That(mount.ContainerPath).IsEqualTo("/container/path");
    await Assert.That(mount.IsReadWrite).IsTrue();
  }

  [Test]
  public async Task ParseCliMount_QuotedHostContainerWithReadWrite_ParsesCorrectly()
  {
    // Arrange - This is the critical test case: quotes around "path:rw"
    var input = "'/host/path:/container/path:rw'";

    // Act
    var mount = RunCommand.ParseCliMount(input, defaultReadWrite: false);

    // Assert
    await Assert.That(mount.HostPath).IsEqualTo("/host/path");
    await Assert.That(mount.ContainerPath).IsEqualTo("/container/path");
    await Assert.That(mount.IsReadWrite).IsTrue();
  }

  [Test]
  public async Task ParseCliMount_CaseInsensitive_ReadOnly()
  {
    // Act
    var mount1 = RunCommand.ParseCliMount("/path:RO", defaultReadWrite: true);
    var mount2 = RunCommand.ParseCliMount("/path:Ro", defaultReadWrite: true);

    // Assert
    await Assert.That(mount1.IsReadWrite).IsFalse();
    await Assert.That(mount2.IsReadWrite).IsFalse();
  }

  [Test]
  public async Task ParseCliMount_CaseInsensitive_ReadWrite()
  {
    // Act
    var mount1 = RunCommand.ParseCliMount("/path:RW", defaultReadWrite: false);
    var mount2 = RunCommand.ParseCliMount("/path:Rw", defaultReadWrite: false);

    // Assert
    await Assert.That(mount1.IsReadWrite).IsTrue();
    await Assert.That(mount2.IsReadWrite).IsTrue();
  }
}
