using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class PathValidatorTests
{
  [Test]
  public async Task ResolvePath_ExpandsTilde()
  {
    // Arrange
    var path = "~/projects";
    var userHome = "/home/testuser";

    // Act
    var resolved = PathValidator.ResolvePath(path, userHome);

    // Assert
    await Assert.That(resolved).Contains("/home/testuser");
    await Assert.That(resolved).Contains("projects");
  }

  [Test]
  public async Task ResolvePath_HandlesAbsolutePath()
  {
    // Arrange
    var path = "/var/data";
    var userHome = "/home/testuser";

    // Act
    var resolved = PathValidator.ResolvePath(path, userHome);

    // Assert
    await Assert.That(resolved).IsEqualTo("/var/data");
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
