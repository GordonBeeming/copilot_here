using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class GitHubAuthTests
{
  [Test]
  public async Task RequiredScopes_ContainsCopilot()
  {
    // Assert
    await Assert.That(GitHubAuth.RequiredScopes).Contains("copilot");
  }

  [Test]
  public async Task RequiredScopes_ContainsReadPackages()
  {
    // Assert
    await Assert.That(GitHubAuth.RequiredScopes).Contains("read:packages");
  }

  [Test]
  public async Task ElevateTokenCommand_ContainsAllRequiredScopes()
  {
    // Act
    var command = GitHubAuth.ElevateTokenCommand;

    // Assert
    await Assert.That(command).Contains("gh auth refresh");
    await Assert.That(command).Contains("-h github.com");
    await Assert.That(command).Contains("copilot");
    await Assert.That(command).Contains("read:packages");
  }

  [Test]
  public async Task HasPrivilegedScopes_DetectsAdminOrg()
  {
    // Arrange
    var authOutput = "Token scopes: 'admin:org', 'repo'";

    // Act
    var result = GitHubAuth.HasPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(result).IsTrue();
  }

  [Test]
  public async Task HasPrivilegedScopes_DetectsAdminEnterprise()
  {
    // Arrange
    var authOutput = "Token scopes: 'admin:enterprise', 'copilot'";

    // Act
    var result = GitHubAuth.HasPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(result).IsTrue();
  }

  [Test]
  public async Task HasPrivilegedScopes_DetectsWritePackages()
  {
    // Arrange
    var authOutput = "Token scopes: 'write_packages', 'copilot'";

    // Act
    var result = GitHubAuth.HasPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(result).IsTrue();
  }

  [Test]
  public async Task HasPrivilegedScopes_DetectsDeleteRepo()
  {
    // Arrange
    var authOutput = "Token scopes: 'delete_repo', 'copilot'";

    // Act
    var result = GitHubAuth.HasPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(result).IsTrue();
  }

  [Test]
  public async Task HasPrivilegedScopes_ReturnsFalseForSafeScopes()
  {
    // Arrange
    var authOutput = "Token scopes: 'copilot', 'read:packages', 'repo'";

    // Act
    var result = GitHubAuth.HasPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(result).IsFalse();
  }

  [Test]
  public async Task GetPrivilegedScopes_ReturnsMatchingScopes()
  {
    // Arrange
    var authOutput = "Token scopes: 'admin:org', 'write_packages', 'copilot'";

    // Act
    var scopes = GitHubAuth.GetPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(scopes).Contains("admin:org");
    await Assert.That(scopes).Contains("write_packages");
  }

  [Test]
  public async Task GetPrivilegedScopes_ReturnsEmptyForSafeScopes()
  {
    // Arrange
    var authOutput = "Token scopes: 'copilot', 'read:packages'";

    // Act
    var scopes = GitHubAuth.GetPrivilegedScopes(authOutput);

    // Assert
    await Assert.That(scopes).IsEmpty();
  }
}
