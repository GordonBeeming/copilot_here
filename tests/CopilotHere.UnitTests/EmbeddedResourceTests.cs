using TUnit.Core;
using TUnit.Assertions.Extensions;

namespace CopilotHere.UnitTests;

public class EmbeddedResourceTests
{
  [Test]
  public async Task AirlockTemplate_IsEmbedded()
  {
    // The template should be embedded in the main assembly
    var assembly = System.Reflection.Assembly.Load("copilot_here");
    var resourceNames = assembly.GetManifestResourceNames();

    await Assert.That(resourceNames).Contains("CopilotHere.Resources.docker-compose.airlock.yml.template");
  }

  [Test]
  public async Task AirlockTemplate_IsValidYaml()
  {
    var assembly = System.Reflection.Assembly.Load("copilot_here");
    using var stream = assembly.GetManifestResourceStream("CopilotHere.Resources.docker-compose.airlock.yml.template");
    
    await Assert.That(stream).IsNotNull();
    
    using var reader = new StreamReader(stream!);
    var content = await reader.ReadToEndAsync();

    // Should start with comment
    await Assert.That(content.TrimStart().StartsWith("#")).IsTrue();
    
    // Should contain required YAML sections
    await Assert.That(content).Contains("networks:");
    await Assert.That(content).Contains("services:");
    await Assert.That(content).Contains("proxy:");
    await Assert.That(content).Contains("app:");
    
    // Should contain required placeholders
    await Assert.That(content).Contains("{{PROJECT_NAME}}");
    await Assert.That(content).Contains("{{APP_IMAGE}}");
    await Assert.That(content).Contains("{{PROXY_IMAGE}}");
    await Assert.That(content).Contains("{{WORK_DIR}}");
    await Assert.That(content).Contains("{{CONTAINER_WORK_DIR}}");
    await Assert.That(content).Contains("{{COPILOT_CONFIG}}");
    await Assert.That(content).Contains("{{NETWORK_CONFIG}}");
    await Assert.That(content).Contains("{{PUID}}");
    await Assert.That(content).Contains("{{PGID}}");
    await Assert.That(content).Contains("{{COPILOT_ARGS}}");
    await Assert.That(content).Contains("{{EXTRA_MOUNTS}}");
    await Assert.That(content).Contains("{{LOGS_MOUNT}}");
  }

  [Test]
  public async Task AirlockTemplate_PlaceholdersNotInComments()
  {
    // Ensure placeholders that get replaced with multi-line content
    // are not in comment lines (which would break YAML)
    var assembly = System.Reflection.Assembly.Load("copilot_here");
    using var stream = assembly.GetManifestResourceStream("CopilotHere.Resources.docker-compose.airlock.yml.template");
    using var reader = new StreamReader(stream!);
    var content = await reader.ReadToEndAsync();

    var lines = content.Split('\n');
    foreach (var line in lines)
    {
      var trimmed = line.TrimStart();
      if (trimmed.StartsWith("#"))
      {
        // Comment lines should not contain these placeholders
        // as they get replaced with multi-line content
        await Assert.That(trimmed.Contains("{{EXTRA_MOUNTS}}")).IsFalse();
        await Assert.That(trimmed.Contains("{{LOGS_MOUNT}}")).IsFalse();
      }
    }
  }
}
