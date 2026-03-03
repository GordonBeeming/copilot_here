using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ContainerRunnerImageTests
{
  // === IsAbsoluteImageReference ===

  [Test]
  public async Task IsAbsoluteImageReference_WithSimpleTag_ReturnsFalse()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("latest")).IsFalse();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithDotnetTag_ReturnsFalse()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("dotnet-rust")).IsFalse();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithRegistryImage_ReturnsTrue()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("myregistry.io/myimage:v1")).IsTrue();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithDockerHubImage_ReturnsTrue()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("myuser/myimage:latest")).IsTrue();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithGhcrImage_ReturnsTrue()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("ghcr.io/myorg/myimage:v2")).IsTrue();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithLocalRegistryImage_ReturnsTrue()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("localhost:5000/myimage:dev")).IsTrue();
  }

  [Test]
  public async Task IsAbsoluteImageReference_WithNestedPath_ReturnsTrue()
  {
    await Assert.That(ContainerRunner.IsAbsoluteImageReference("registry.example.com/org/team/image:tag")).IsTrue();
  }

  // === GetImageName ===

  [Test]
  public async Task GetImageName_WithSimpleTag_ReturnsFullImageWithPrefix()
  {
    // Act
    var imageName = ContainerRunner.GetImageName("latest");

    // Assert
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:latest");
  }

  [Test]
  public async Task GetImageName_WithDotnetTag_ReturnsFullImageWithPrefix()
  {
    // Act
    var imageName = ContainerRunner.GetImageName("dotnet-rust");

    // Assert
    await Assert.That(imageName).IsEqualTo("ghcr.io/gordonbeeming/copilot_here:dotnet-rust");
  }

  [Test]
  public async Task GetImageName_WithAbsoluteImage_ReturnsAsIs()
  {
    // Act
    var imageName = ContainerRunner.GetImageName("myregistry.io/myimage:v1");

    // Assert
    await Assert.That(imageName).IsEqualTo("myregistry.io/myimage:v1");
  }

  [Test]
  public async Task GetImageName_WithDockerHubImage_ReturnsAsIs()
  {
    // Act
    var imageName = ContainerRunner.GetImageName("myuser/custom-copilot:latest");

    // Assert
    await Assert.That(imageName).IsEqualTo("myuser/custom-copilot:latest");
  }
}
