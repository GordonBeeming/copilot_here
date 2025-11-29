using CopilotHere.Infrastructure;

namespace CopilotHere.Tests;

public class ConfigFileTests
{
  private string _tempDir = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task ReadValue_EmptyFile_ReturnsNull()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "empty.conf");
    File.WriteAllText(path, "");

    // Act
    var result = ConfigFile.ReadValue(path);

    // Assert
    await Assert.That(result).IsNull();
  }

  [Test]
  public async Task ReadValue_WithContent_ReturnsTrimmmedValue()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "value.conf");
    File.WriteAllText(path, "  dotnet  \n");

    // Act
    var result = ConfigFile.ReadValue(path);

    // Assert
    await Assert.That(result).IsEqualTo("dotnet");
  }

  [Test]
  public async Task ReadValue_NonexistentFile_ReturnsNull()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "nonexistent.conf");

    // Act
    var result = ConfigFile.ReadValue(path);

    // Assert
    await Assert.That(result).IsNull();
  }

  [Test]
  public async Task WriteValue_CreatesFileWithValue()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "subdir", "value.conf");

    // Act
    ConfigFile.WriteValue(path, "playwright");

    // Assert
    await Assert.That(File.Exists(path)).IsTrue();
    await Assert.That(File.ReadAllText(path)).IsEqualTo("playwright");
  }

  [Test]
  public async Task Delete_RemovesFile()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "todelete.conf");
    File.WriteAllText(path, "content");

    // Act
    var result = ConfigFile.Delete(path);

    // Assert
    await Assert.That(result).IsTrue();
    await Assert.That(File.Exists(path)).IsFalse();
  }

  [Test]
  public async Task Delete_NonexistentFile_ReturnsFalse()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "nonexistent.conf");

    // Act
    var result = ConfigFile.Delete(path);

    // Assert
    await Assert.That(result).IsFalse();
  }

  [Test]
  public async Task ReadLines_SkipsCommentsAndEmptyLines()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "lines.conf");
    File.WriteAllText(path, "# Comment\n\n/valid/path\n   \n# Another comment\npath2\n");

    // Act
    var lines = ConfigFile.ReadLines(path).ToList();

    // Assert
    await Assert.That(lines).HasCount().EqualTo(2);
    await Assert.That(lines[0]).IsEqualTo("/valid/path");
    await Assert.That(lines[1]).IsEqualTo("path2");
  }

  [Test]
  public async Task ReadFlag_TrueValues_ReturnsTrue()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "flag.conf");

    foreach (var value in new[] { "true", "1", "yes", "enabled" })
    {
      File.WriteAllText(path, value);

      // Act
      var result = ConfigFile.ReadFlag(path);

      // Assert
      await Assert.That(result).IsTrue();
    }
  }

  [Test]
  public async Task ReadFlag_FalseValues_ReturnsFalse()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "flag.conf");
    File.WriteAllText(path, "false");

    // Act
    var result = ConfigFile.ReadFlag(path);

    // Assert
    await Assert.That(result).IsFalse();
  }

  [Test]
  public async Task WriteFlag_True_WritesTrue()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "flag.conf");

    // Act
    ConfigFile.WriteFlag(path, true);

    // Assert
    await Assert.That(File.ReadAllText(path)).IsEqualTo("true");
  }

  [Test]
  public async Task AppendLine_AddsLineToFile()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "append.conf");
    File.WriteAllText(path, "line1\n");

    // Act
    ConfigFile.AppendLine(path, "line2");

    // Assert
    var lines = File.ReadAllLines(path);
    await Assert.That(lines).HasCount().EqualTo(2);
    await Assert.That(lines[1]).IsEqualTo("line2");
  }
}
