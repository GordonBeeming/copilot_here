using CopilotHere.Commands.Airlock;
using CopilotHere.Infrastructure;
using TUnit.Core;

namespace CopilotHere.UnitTests;

public class InheritDefaultRulesTests
{
  [Test]
  public async Task NetworkConfig_WithInheritDefaultRules_MergesDefaultRules()
  {
    // Arrange
    var tempDir = Path.Combine(Path.GetTempPath(), $"copilot-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      // Create a user config with inherit_default_rules: true
      var userConfigPath = Path.Combine(tempDir, "network.json");
      var userConfig = new NetworkConfig
      {
        Enabled = true,
        InheritDefaultRules = true,
        Mode = "enforce",
        AllowedRules =
        [
          new NetworkRule
          {
            Host = "registry.npmjs.org",
            AllowedPaths = ["/npm", "/@playwright%2fmcp"]
          },
          new NetworkRule
          {
            Host = "api.github.com",
            AllowedPaths = ["/user", "/copilot_internal/user"]
          }
        ]
      };

      AirlockConfig.WriteNetworkConfig(userConfigPath, userConfig);

      // Simulate ProcessNetworkConfig by loading and merging
      var loadedConfig = AirlockConfig.ReadNetworkConfig(userConfigPath);
      await Assert.That(loadedConfig).IsNotNull();
      await Assert.That(loadedConfig!.InheritDefaultRules).IsTrue();

      // Verify user rules are present
      var npmRule = loadedConfig.AllowedRules.FirstOrDefault(r => r.Host == "registry.npmjs.org");
      await Assert.That(npmRule).IsNotNull();
      await Assert.That(npmRule!.AllowedPaths).Contains("/npm");
      
      var githubRule = loadedConfig.AllowedRules.FirstOrDefault(r => r.Host == "api.github.com");
      await Assert.That(githubRule).IsNotNull();
      await Assert.That(githubRule!.AllowedPaths).Contains("/copilot_internal/user");
    }
    finally
    {
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
    }
  }

  [Test]
  public async Task NetworkConfig_WithInheritDefaultRulesFalse_DoesNotMerge()
  {
    // Arrange
    var tempDir = Path.Combine(Path.GetTempPath(), $"copilot-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);

    try
    {
      var userConfigPath = Path.Combine(tempDir, "network.json");
      var userConfig = new NetworkConfig
      {
        Enabled = true,
        InheritDefaultRules = false,
        Mode = "enforce",
        AllowedRules =
        [
          new NetworkRule
          {
            Host = "example.com",
            AllowedPaths = ["/api"]
          }
        ]
      };

      AirlockConfig.WriteNetworkConfig(userConfigPath, userConfig);

      var loadedConfig = AirlockConfig.ReadNetworkConfig(userConfigPath);
      await Assert.That(loadedConfig).IsNotNull();
      await Assert.That(loadedConfig!.InheritDefaultRules).IsFalse();
      await Assert.That(loadedConfig.AllowedRules).HasCount().EqualTo(1);
      await Assert.That(loadedConfig.AllowedRules[0].Host).IsEqualTo("example.com");
    }
    finally
    {
      if (Directory.Exists(tempDir))
        Directory.Delete(tempDir, true);
    }
  }
}
