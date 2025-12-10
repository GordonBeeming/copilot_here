using CopilotHere.Infrastructure;

namespace CopilotHere.UnitTests;

public class DependencyCheckTests
{
  [Test]
  public async Task CheckAll_ReturnsResults()
  {
    // Act
    var results = DependencyCheck.CheckAll();

    // Assert
    await Assert.That(results).IsNotEmpty();
    await Assert.That(results.Count).IsEqualTo(3); // GitHub CLI, Docker, Docker Daemon
  }

  [Test]
  public async Task CheckAll_IncludesGitHubCli()
  {
    // Act
    var results = DependencyCheck.CheckAll();

    // Assert
    var ghResult = results.FirstOrDefault(r => r.Name.Contains("GitHub CLI"));
    await Assert.That(ghResult).IsNotNull();
  }

  [Test]
  public async Task CheckAll_IncludesDocker()
  {
    // Act
    var results = DependencyCheck.CheckAll();

    // Assert
    var dockerResult = results.FirstOrDefault(r => r.Name == "Docker");
    await Assert.That(dockerResult).IsNotNull();
  }

  [Test]
  public async Task CheckAll_IncludesDockerDaemon()
  {
    // Act
    var results = DependencyCheck.CheckAll();

    // Assert
    var daemonResult = results.FirstOrDefault(r => r.Name == "Docker Daemon");
    await Assert.That(daemonResult).IsNotNull();
  }

  [Test]
  public async Task DisplayResults_ReturnsTrue_WhenAllInstalled()
  {
    // Arrange
    var results = new List<DependencyCheck.DependencyResult>
    {
      new("Test Dependency", true, "1.0.0", null, null)
    };

    // Act
    var allSatisfied = DependencyCheck.DisplayResults(results);

    // Assert
    await Assert.That(allSatisfied).IsTrue();
  }

  [Test]
  public async Task DisplayResults_ReturnsFalse_WhenDependencyNotInstalled()
  {
    // Arrange
    var results = new List<DependencyCheck.DependencyResult>
    {
      new("Test Dependency", false, null, "Not found", "Install it")
    };

    // Act
    var allSatisfied = DependencyCheck.DisplayResults(results);

    // Assert
    await Assert.That(allSatisfied).IsFalse();
  }

  [Test]
  public async Task DisplayResults_ReturnsTrue_WhenAllSatisfied()
  {
    // Arrange
    var results = new List<DependencyCheck.DependencyResult>
    {
      new("Test Dependency", true, "1.0.0", null, null)
    };

    // Act
    var allSatisfied = DependencyCheck.DisplayResults(results);

    // Assert
    await Assert.That(allSatisfied).IsTrue();
  }

  [Test]
  public async Task DisplayResults_ReturnsFalse_WhenMixedResults()
  {
    // Arrange - mix of passed and failed dependencies
    var results = new List<DependencyCheck.DependencyResult>
    {
      new("Passed Dependency", true, "1.0.0", null, null),
      new("Failed Dependency", false, null, "Not found", "Install it")
    };

    // Act
    var allSatisfied = DependencyCheck.DisplayResults(results);

    // Assert
    await Assert.That(allSatisfied).IsFalse();
  }
}
