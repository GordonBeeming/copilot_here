using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class ToolRegistryTests
{
  [Test]
  public async Task Get_ValidTool_GitHubCopilot_ReturnsCorrectTool()
  {
    // Act
    var tool = ToolRegistry.Get("github-copilot");
    
    // Assert
    await Assert.That(tool).IsNotNull();
    await Assert.That(tool.Name).IsEqualTo("github-copilot");
    await Assert.That(tool.DisplayName).IsEqualTo("GitHub Copilot CLI");
  }
  
  [Test]
  public async Task Get_ValidTool_Echo_ReturnsCorrectTool()
  {
    // Act
    var tool = ToolRegistry.Get("echo");
    
    // Assert
    await Assert.That(tool).IsNotNull();
    await Assert.That(tool.Name).IsEqualTo("echo");
    await Assert.That(tool.DisplayName).IsEqualTo("Echo (Test Provider)");
  }
  
  [Test]
  public async Task Get_InvalidTool_ThrowsArgumentException()
  {
    // Act & Assert
    var exception = await Assert.That(() => ToolRegistry.Get("invalid-tool"))
      .Throws<ArgumentException>();
      
    await Assert.That(exception!.Message).Contains("Unknown tool: invalid-tool");
  }
  
  [Test]
  public async Task Get_InvalidTool_ErrorMessageIncludesAvailableTools()
  {
    // Act & Assert
    var exception = await Assert.That(() => ToolRegistry.Get("nonexistent"))
      .Throws<ArgumentException>();
      
    await Assert.That(exception!.Message).Contains("github-copilot");
    await Assert.That(exception.Message).Contains("echo");
  }
  
  [Test]
  public async Task GetDefault_ReturnsGitHubCopilot()
  {
    // Act
    var tool = ToolRegistry.GetDefault();
    
    // Assert
    await Assert.That(tool.Name).IsEqualTo("github-copilot");
  }
  
  [Test]
  public async Task Exists_ValidTool_GitHubCopilot_ReturnsTrue()
  {
    // Act
    var exists = ToolRegistry.Exists("github-copilot");
    
    // Assert
    await Assert.That(exists).IsTrue();
  }
  
  [Test]
  public async Task Exists_ValidTool_Echo_ReturnsTrue()
  {
    // Act
    var exists = ToolRegistry.Exists("echo");
    
    // Assert
    await Assert.That(exists).IsTrue();
  }
  
  [Test]
  public async Task Exists_InvalidTool_ReturnsFalse()
  {
    // Act
    var exists = ToolRegistry.Exists("invalid-tool");
    
    // Assert
    await Assert.That(exists).IsFalse();
  }
  
  [Test]
  public async Task Exists_EmptyString_ReturnsFalse()
  {
    // Act
    var exists = ToolRegistry.Exists("");
    
    // Assert
    await Assert.That(exists).IsFalse();
  }
  
  [Test]
  public async Task GetToolNames_ReturnsAllRegisteredTools()
  {
    // Act
    var names = ToolRegistry.GetToolNames().ToList();
    
    // Assert
    await Assert.That(names).IsNotEmpty();
    await Assert.That(names).Contains("github-copilot");
    await Assert.That(names).Contains("echo");
    await Assert.That(names.Count).IsGreaterThanOrEqualTo(2);
  }
  
  [Test]
  public async Task GetAll_ReturnsAllTools()
  {
    // Act
    var tools = ToolRegistry.GetAll().ToList();
    
    // Assert
    await Assert.That(tools).IsNotEmpty();
    await Assert.That(tools.Count).IsGreaterThanOrEqualTo(2);
    
    // Verify we can get instances
    var gitHubCopilot = tools.FirstOrDefault(t => t.Name == "github-copilot");
    var echo = tools.FirstOrDefault(t => t.Name == "echo");
    
    await Assert.That(gitHubCopilot).IsNotNull();
    await Assert.That(echo).IsNotNull();
  }
  
  [Test]
  public async Task GetAll_ToolsAreUnique()
  {
    // Act
    var tools = ToolRegistry.GetAll().ToList();
    var names = tools.Select(t => t.Name).ToList();
    
    // Assert - No duplicate names
    await Assert.That(names.Count).IsEqualTo(names.Distinct().Count());
  }
  
  [Test]
  public async Task Get_CalledMultipleTimes_ReturnsSameInstance()
  {
    // Act
    var tool1 = ToolRegistry.Get("github-copilot");
    var tool2 = ToolRegistry.Get("github-copilot");
    
    // Assert - Lazy<T> should return same instance
    await Assert.That(ReferenceEquals(tool1, tool2)).IsTrue();
  }
}
