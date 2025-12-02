using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class PathValidatorTests
{
  private static bool IsWindows => OperatingSystem.IsWindows();
  
  [Test]
  public async Task ResolvePath_ExpandsTilde()
  {
    // Arrange
    var path = "~/projects";
    var userHome = IsWindows ? @"C:\Users\testuser" : "/home/testuser";

    // Act
    var resolved = PathValidator.ResolvePath(path, userHome);

    // Assert
    await Assert.That(resolved).Contains("testuser");
    await Assert.That(resolved).Contains("projects");
  }

  [Test]
  public async Task ResolvePath_HandlesAbsolutePath()
  {
    // Arrange
    var path = IsWindows ? @"C:\var\data" : "/var/data";
    var userHome = IsWindows ? @"C:\Users\testuser" : "/home/testuser";

    // Act
    var resolved = PathValidator.ResolvePath(path, userHome);

    // Assert
    var expected = IsWindows ? @"C:\var\data" : "/var/data";
    await Assert.That(resolved).IsEqualTo(expected);
  }

  [Test]
  public async Task WarnIfNotExists_ReturnsFalseForNonExistentPath()
  {
    // Arrange
    var nonExistentPath = "/this/path/does/not/exist/anywhere";

    // Act
    var result = PathValidator.WarnIfNotExists(nonExistentPath);

    // Assert
    await Assert.That(result).IsFalse();
  }

  [Test]
  public async Task WarnIfNotExists_ReturnsTrueForExistingPath()
  {
    // Arrange - use a path that definitely exists
    var existingPath = Environment.CurrentDirectory;

    // Act
    var result = PathValidator.WarnIfNotExists(existingPath);

    // Assert
    await Assert.That(result).IsTrue();
  }
}
