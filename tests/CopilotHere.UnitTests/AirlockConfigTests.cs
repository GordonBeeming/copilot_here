using System.Text.Json;
using CopilotHere.Commands.Airlock;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.Tests;

public class AirlockConfigTests
{
  private string _tempDir = null!;
  private AppPaths _paths = null!;

  [Before(Test)]
  public void Setup()
  {
    _tempDir = Path.Combine(Path.GetTempPath(), $"copilot_here_tests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(_tempDir);
    
    var globalConfigPath = Path.Combine(_tempDir, "global");
    var localConfigPath = Path.Combine(_tempDir, "local");
    Directory.CreateDirectory(globalConfigPath);
    Directory.CreateDirectory(localConfigPath);

    _paths = new AppPaths
    {
      UserHome = "/home/testuser",
      GlobalConfigPath = globalConfigPath,
      LocalConfigPath = localConfigPath,
      CurrentDirectory = _tempDir,
      ContainerWorkDir = "/work",
      CopilotConfigPath = Path.Combine(_tempDir, ".copilot")
    };
  }

  [After(Test)]
  public void Cleanup()
  {
    if (Directory.Exists(_tempDir))
      Directory.Delete(_tempDir, recursive: true);
  }

  [Test]
  public async Task Load_NoConfigs_ReturnsDisabled()
  {
    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsFalse();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Default);
    await Assert.That(config.RulesPath).IsNull();
  }

  [Test]
  public async Task Load_OnlyGlobal_ReturnsGlobal()
  {
    // Arrange
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var networkConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(globalRulesPath, networkConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Global);
    await Assert.That(config.RulesPath).IsEqualTo(globalRulesPath);
    await Assert.That(config.RulesSource).IsEqualTo(AirlockConfigSource.Global);
  }

  [Test]
  public async Task Load_OnlyLocal_ReturnsLocal()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    var networkConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(localRulesPath, networkConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Local);
    await Assert.That(config.RulesPath).IsEqualTo(localRulesPath);
  }

  [Test]
  public async Task Load_BothGlobalAndLocal_LocalTakesPriority()
  {
    // Arrange
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var localRulesPath = _paths.GetLocalPath("network.json");
    
    // Global is enabled
    var globalConfig = NetworkConfig.CreateDefault(enabled: true);
    AirlockConfig.WriteNetworkConfig(globalRulesPath, globalConfig);
    
    // Local is disabled (should win)
    var localConfig = NetworkConfig.CreateDefault(enabled: false);
    AirlockConfig.WriteNetworkConfig(localRulesPath, localConfig);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert - Local should win
    await Assert.That(config.Enabled).IsFalse();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Local);
    await Assert.That(config.RulesPath).IsEqualTo(localRulesPath);
    await Assert.That(config.RulesSource).IsEqualTo(AirlockConfigSource.Local);
  }

  [Test]
  public async Task EnableLocal_CreatesFile()
  {
    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    var localRulesPath = _paths.GetLocalPath("network.json");
    await Assert.That(File.Exists(localRulesPath)).IsTrue();
    
    var networkConfig = AirlockConfig.ReadNetworkConfig(localRulesPath);
    await Assert.That(networkConfig).IsNotNull();
    await Assert.That(networkConfig!.Enabled).IsTrue();
  }

  [Test]
  public async Task EnableGlobal_CreatesFile()
  {
    // Act
    AirlockConfig.EnableGlobal(_paths);

    // Assert
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    await Assert.That(File.Exists(globalRulesPath)).IsTrue();
    
    var networkConfig = AirlockConfig.ReadNetworkConfig(globalRulesPath);
    await Assert.That(networkConfig).IsNotNull();
    await Assert.That(networkConfig!.Enabled).IsTrue();
  }

  [Test]
  public async Task DisableLocal_SetsEnabledFalse()
  {
    // Arrange
    AirlockConfig.EnableLocal(_paths);

    // Act
    AirlockConfig.DisableLocal(_paths);

    // Assert
    var localRulesPath = _paths.GetLocalPath("network.json");
    var networkConfig = AirlockConfig.ReadNetworkConfig(localRulesPath);
    await Assert.That(networkConfig!.Enabled).IsFalse();
  }

  [Test]
  public async Task DisableGlobal_SetsEnabledFalse()
  {
    // Arrange
    AirlockConfig.EnableGlobal(_paths);

    // Act
    AirlockConfig.DisableGlobal(_paths);

    // Assert
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    var networkConfig = AirlockConfig.ReadNetworkConfig(globalRulesPath);
    await Assert.That(networkConfig!.Enabled).IsFalse();
  }

  [Test]
  public async Task ReadNetworkConfig_NonexistentFile_ReturnsNull()
  {
    // Arrange
    var path = Path.Combine(_tempDir, "nonexistent.json");

    // Act
    var config = AirlockConfig.ReadNetworkConfig(path);

    // Assert
    await Assert.That(config).IsNull();
  }

  [Test]
  public async Task Toggle_KeepsUnknownKeysAndFormatting()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    const string original = """
      {
          "enabled": true,
          "mode": "monitor",
          "allowed_rules": [
              {
                  "host": "api.nuget.org",
                  "allowed_paths": ["/v3/*"],
                  "note": "a key copilot_here doesn't model"
              }
          ],
          "my_custom_top_level": { "a": 1 }
      }
      """;
    File.WriteAllText(localRulesPath, original);

    // Act
    AirlockConfig.DisableLocal(_paths);
    AirlockConfig.EnableLocal(_paths);

    // Assert - back to the original byte for byte
    await Assert.That(File.ReadAllText(localRulesPath)).IsEqualTo(original);
  }

  [Test]
  public async Task Toggle_KeepsComments()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    const string original = """
      {
        // nuget is needed for restore
        "enabled": true,
        "allowed_rules": [ { "host": "api.nuget.org", "allowed_paths": ["*"] } ]
      }
      """;
    File.WriteAllText(localRulesPath, original);

    // Act
    AirlockConfig.DisableLocal(_paths);

    // Assert
    await Assert.That(File.ReadAllText(localRulesPath))
      .IsEqualTo(original.Replace("\"enabled\": true", "\"enabled\": false"));
  }

  [Test]
  public async Task Toggle_IgnoresEnabledNestedInsideARule()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """
      {
        "allowed_rules": [
          { "host": "a.com", "enabled": false, "allowed_paths": ["*"] }
        ],
        "enabled": false
      }
      """);

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert - only the root flag flipped
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).Contains("\"host\": \"a.com\", \"enabled\": false");
    await Assert.That(updated).Contains("\"enabled\": true");
  }

  [Test]
  public async Task Toggle_InsertsEnabledWhenMissing()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """
      {
        "mode": "monitor",
        "allowed_rules": []
      }
      """);

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).Contains("\"enabled\": true");
    await Assert.That(updated).Contains("\"mode\": \"monitor\"");
    await Assert.That(AirlockConfig.Load(_paths).Enabled).IsTrue();
  }

  [Test]
  public async Task Toggle_MalformedJson_ThrowsAndLeavesFileAlone()
  {
    // Arrange
    var localRulesPath = _paths.GetLocalPath("network.json");
    const string broken = """{ "enabled": true, "allowed_rules": [ }""";
    File.WriteAllText(localRulesPath, broken);

    // Act & Assert
    await Assert.That(() => AirlockConfig.DisableLocal(_paths)).Throws<JsonException>();
    await Assert.That(File.ReadAllText(localRulesPath)).IsEqualTo(broken);
  }

  [Test]
  public async Task Toggle_EnabledHoldsObject_ThrowsShapeError_NotSyntaxError()
  {
    // Arrange - structurally valid JSON, but "enabled" holds the wrong shape.
    // Distinguishing this from a syntax error is what lets RunToggle's message
    // avoid the wrong claim that the file "isn't valid JSON".
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """{ "enabled": {} }""");

    // Act
    JsonException? caught = null;
    try
    {
      AirlockConfig.EnableLocal(_paths);
    }
    catch (JsonException ex)
    {
      caught = ex;
    }

    // Assert
    await Assert.That(caught).IsNotNull();
    await Assert.That(caught!.Message).Contains("must be a boolean");
  }

  [Test]
  public async Task DisableLocal_NoLocalFile_SeedsFromGlobalRules()
  {
    // Arrange - rules live globally only, with nothing in the project yet
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    File.WriteAllText(globalRulesPath, """
      {
        "enabled": true,
        "mode": "monitor",
        "allowed_rules": [
          { "host": "api.nuget.org", "allowed_paths": ["*"] },
          { "host": "registry.npmjs.org", "allowed_paths": ["*"] }
        ]
      }
      """);

    // Act
    var outcome = AirlockConfig.DisableLocal(_paths);

    // Assert - the global rules came across instead of being shadowed by an empty file
    await Assert.That(outcome).IsEqualTo(AirlockToggleOutcome.SeededFromGlobal);

    var local = AirlockConfig.ReadNetworkConfig(_paths.GetLocalPath("network.json"));
    await Assert.That(local!.Enabled).IsFalse();
    await Assert.That(local.Mode).IsEqualTo("monitor");
    await Assert.That(local.AllowedRules.Count).IsEqualTo(2);
    await Assert.That(local.AllowedRules[0].Host).IsEqualTo("api.nuget.org");
  }

  [Test]
  public async Task EnableLocal_NoLocalFile_SeedsFromGlobalRules()
  {
    // Arrange
    var globalRulesPath = _paths.GetGlobalPath("network.json");
    File.WriteAllText(globalRulesPath, """
      {
        "enabled": false,
        "allowed_rules": [ { "host": "api.nuget.org", "allowed_paths": ["*"] } ]
      }
      """);

    // Act
    var outcome = AirlockConfig.EnableLocal(_paths);

    // Assert
    await Assert.That(outcome).IsEqualTo(AirlockToggleOutcome.SeededFromGlobal);

    var config = AirlockConfig.Load(_paths);
    await Assert.That(config.Enabled).IsTrue();
    await Assert.That(config.EnabledSource).IsEqualTo(AirlockConfigSource.Local);

    var local = AirlockConfig.ReadNetworkConfig(_paths.GetLocalPath("network.json"));
    await Assert.That(local!.AllowedRules.Count).IsEqualTo(1);
  }

  [Test]
  public async Task DisableLocal_BrokenGlobalFile_CreatesNoLocalFile()
  {
    // Arrange
    File.WriteAllText(_paths.GetGlobalPath("network.json"), """{ "enabled": true, ]""");

    // Act & Assert - better to fail loudly than seed a project from a broken source
    await Assert.That(() => AirlockConfig.DisableLocal(_paths)).Throws<JsonException>();
    await Assert.That(File.Exists(_paths.GetLocalPath("network.json"))).IsFalse();
  }

  [Test]
  public async Task DisableLocal_NoConfigAnywhere_WritesDefault()
  {
    // Act
    var outcome = AirlockConfig.DisableLocal(_paths);

    // Assert
    await Assert.That(outcome).IsEqualTo(AirlockToggleOutcome.CreatedDefault);

    var local = AirlockConfig.ReadNetworkConfig(_paths.GetLocalPath("network.json"));
    await Assert.That(local!.Enabled).IsFalse();
    await Assert.That(local.AllowedRules.Count).IsEqualTo(0);
  }

  [Test]
  public async Task EnableLocal_ExistingLocalFile_ReportsUpdatedExisting()
  {
    // Arrange
    File.WriteAllText(_paths.GetLocalPath("network.json"), """{ "enabled": false }""");

    // Act
    var outcome = AirlockConfig.EnableLocal(_paths);

    // Assert
    await Assert.That(outcome).IsEqualTo(AirlockToggleOutcome.UpdatedExisting);
    await Assert.That(AirlockConfig.Load(_paths).Enabled).IsTrue();
  }

  [Test]
  public async Task Toggle_InsertsEnabledPreservesCommentInOtherwiseEmptyObject()
  {
    // Arrange - the root object has no real properties, only a comment
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """
      {
        // explanation
      }
      """);

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert - the comment survives alongside the inserted property, and the
    // result is well-formed strict JSON (no trailing comma left dangling
    // ahead of a comment with nothing else following it)
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).Contains("// explanation");
    await Assert.That(updated).Contains("\"enabled\": true");
    AssertWellFormedStrictJson(updated);
  }

  [Test]
  public async Task Toggle_InsertsEnabledOnEmptyObject_StaysWellFormed()
  {
    // Arrange - truly empty: nothing at all between the braces
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, "{}");

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).Contains("\"enabled\": true");
    AssertWellFormedStrictJson(updated);
  }

  [Test]
  public async Task Toggle_InsertsEnabledOnWhitespaceOnlyEmptyObject_StaysWellFormed()
  {
    // Arrange - empty object spread across lines, no comment, just whitespace.
    // This is the case that regressed: the insert has nothing following it,
    // so a trailing comma here would make the file strict-JSON-invalid even
    // though our own lenient reader tolerates it.
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, "{\n}");

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).Contains("\"enabled\": true");
    AssertWellFormedStrictJson(updated);
  }

  [Test]
  public async Task Toggle_InsertsEnabledOnSingleLineFile_StaysSingleLine()
  {
    // Arrange - a file with no newlines anywhere is deliberately single-line
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """{ "mode": "monitor" }""");

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert - stays on one line instead of being reformatted
    var updated = File.ReadAllText(localRulesPath);
    await Assert.That(updated).DoesNotContain("\n");
    await Assert.That(updated).Contains("\"enabled\": true");
    await Assert.That(updated).Contains("\"mode\": \"monitor\"");
    AssertWellFormedStrictJson(updated);
  }

  /// <summary>
  /// Parses with comments allowed but trailing commas rejected, so a splice that
  /// only happens to be readable by our own lenient reader still fails the test.
  /// </summary>
  private static void AssertWellFormedStrictJson(string json)
  {
    var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = false };
    using var _ = JsonDocument.Parse(json, options);
  }

  [Test]
  public async Task Toggle_DuplicateRootEnabled_UpdatesTheOccurrenceLoadWillRead()
  {
    // Arrange - a hand-edited duplicate key. System.Text.Json's deserializer
    // resolves duplicates to the last occurrence, so the splice must target
    // that one or Load() would disagree with what the toggle just reported.
    var localRulesPath = _paths.GetLocalPath("network.json");
    File.WriteAllText(localRulesPath, """{ "enabled": false, "enabled": false }""");

    // Act
    AirlockConfig.EnableLocal(_paths);

    // Assert
    await Assert.That(AirlockConfig.Load(_paths).Enabled).IsTrue();
  }

  [Test]
  public async Task Load_FileWithCommentsAndTrailingCommas_Reads()
  {
    // Arrange
    File.WriteAllText(_paths.GetLocalPath("network.json"), """
      {
        // hand-edited configs happen
        "enabled": true,
        "allowed_rules": [
          { "host": "api.nuget.org", "allowed_paths": ["*"] },
        ],
      }
      """);

    // Act
    var config = AirlockConfig.Load(_paths);

    // Assert
    await Assert.That(config.Enabled).IsTrue();
  }
}
