using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ShellIntegrationTests
{
  [Test]
  public async Task BuildCmdWrapper_UsesArgsSplatForForwarding()
  {
    // Act
    var wrapper = ShellIntegration.BuildCmdWrapper("copilot_yolo", "ignored");

    // Assert
    await Assert.That(wrapper).Contains("copilot_yolo @args");
    await Assert.That(wrapper).Contains(" -- %*");
  }

  [Test]
  public async Task BuildCmdWrapper_UsesArgsSplatForBothPwshAndWindowsPowerShell()
  {
    // Act
    var wrapper = ShellIntegration.BuildCmdWrapper("copilot_here", "ignored");

    // Assert
    var occurrences = wrapper.Split("copilot_here @args").Length - 1;
    await Assert.That(occurrences).IsEqualTo(2);
  }
}
