using CopilotHere.Infrastructure;

namespace CopilotHere.Tests;

public class SystemInfoTests
{
  [Test]
  public async Task GetUserId_ReturnsNonEmptyString()
  {
    // Act
    var userId = SystemInfo.GetUserId();

    // Assert
    await Assert.That(userId).IsNotNull();
    await Assert.That(userId).IsNotEmpty();
  }

  [Test]
  public async Task GetGroupId_ReturnsNonEmptyString()
  {
    // Act
    var groupId = SystemInfo.GetGroupId();

    // Assert
    await Assert.That(groupId).IsNotNull();
    await Assert.That(groupId).IsNotEmpty();
  }

  [Test]
  public async Task SupportsEmoji_ReturnsBool()
  {
    // Act
    var result = SystemInfo.SupportsEmoji();

    // Assert - just verify it doesn't throw and returns a boolean
    await Assert.That(result).IsTypeOf<bool>();
  }
}
