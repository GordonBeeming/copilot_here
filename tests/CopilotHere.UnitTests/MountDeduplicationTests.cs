using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class MountDeduplicationTests
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
  public async Task Deduplication_PrefersReadOnlyOverReadWrite_SameSource()
  {
    // Arrange
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data", IsReadWrite: true, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: false, MountSource.CommandLine)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Should keep read-only for security
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Deduplication_PrefersReadOnlyOverReadWrite_SameSourceReversed()
  {
    // Arrange - Read-only first, then read-write
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data", IsReadWrite: false, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: true, MountSource.CommandLine)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Should keep read-only for security
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Deduplication_CLIPriorityOverLocal_EvenIfLocalReadOnly()
  {
    // Arrange - CLI has read-write, Local has read-only
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data", IsReadWrite: true, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: false, MountSource.Local)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - CLI wins due to higher priority, even though it's read-write
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].Source).IsEqualTo(MountSource.CommandLine);
    await Assert.That(result[0].IsReadWrite).IsTrue();
  }

  [Test]
  public async Task Deduplication_LocalPriorityOverGlobal_RespectsSafety()
  {
    // Arrange - Local read-only, Global read-write
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data", IsReadWrite: false, MountSource.Local),
      new("/home/testuser/data", IsReadWrite: true, MountSource.Global)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Local wins (higher priority)
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].Source).IsEqualTo(MountSource.Local);
    await Assert.That(result[0].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Deduplication_HandlesTrailingSlashes()
  {
    // Arrange
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data/", IsReadWrite: false, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: true, MountSource.CommandLine)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Should treat as same path and keep read-only
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Deduplication_CaseInsensitive()
  {
    // Arrange
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/DATA", IsReadWrite: true, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: false, MountSource.CommandLine)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Should treat as same path and prefer read-only
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].IsReadWrite).IsFalse();
  }

  [Test]
  public async Task Deduplication_MultipleDifferentPaths()
  {
    // Arrange
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data1", IsReadWrite: true, MountSource.CommandLine),
      new("/home/testuser/data2", IsReadWrite: false, MountSource.CommandLine),
      new("/home/testuser/data1", IsReadWrite: false, MountSource.Local)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - Should keep both unique paths
    await Assert.That(result).HasCount().EqualTo(2);
  }

  [Test]
  public async Task Deduplication_MultipleSourcesSamePath_CLIWins()
  {
    // Arrange - All three sources have same path
    var mounts = new List<MountEntry>
    {
      new("/home/testuser/data", IsReadWrite: true, MountSource.CommandLine),
      new("/home/testuser/data", IsReadWrite: false, MountSource.Local),
      new("/home/testuser/data", IsReadWrite: true, MountSource.Global)
    };

    // Act
    var result = CallRemoveDuplicateMounts(mounts);

    // Assert - CLI wins (first in list, highest priority)
    await Assert.That(result).HasCount().EqualTo(1);
    await Assert.That(result[0].Source).IsEqualTo(MountSource.CommandLine);
  }

  // Helper method to call RemoveDuplicateMounts
  // (accessible via InternalsVisibleTo declared in app/CopilotHere.csproj)
  private List<MountEntry> CallRemoveDuplicateMounts(List<MountEntry> mounts)
  {
    return RunCommand.RemoveDuplicateMounts(mounts, _paths.UserHome);
  }
}
