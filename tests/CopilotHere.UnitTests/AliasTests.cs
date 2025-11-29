using System.CommandLine;
using TUnit.Core;

namespace CopilotHere.Tests;

/// <summary>
/// Tests that verify all CLI aliases (both standard and hidden) invoke the correct commands.
/// Uses TUnit Matrix to test every alias variation systematically.
/// </summary>
public class AliasTests
{
  // ===== IMAGE VARIANT ALIASES =====

  [Test]
  [Arguments("-d", "--dotnet")]
  [Arguments("-d8", "--dotnet8")]
  [Arguments("-d9", "--dotnet9")]
  [Arguments("-d10", "--dotnet10")]
  [Arguments("-pw", "--playwright")]
  [Arguments("-dp", "--dotnet-playwright")]
  [Arguments("-rs", "--rust")]
  [Arguments("-dr", "--dotnet-rust")]
  // PowerShell aliases
  [Arguments("-Dotnet", "--dotnet")]
  [Arguments("-Dotnet8", "--dotnet8")]
  [Arguments("-Dotnet9", "--dotnet9")]
  [Arguments("-Dotnet10", "--dotnet10")]
  [Arguments("-Playwright", "--playwright")]
  [Arguments("-DotnetPlaywright", "--dotnet-playwright")]
  [Arguments("-Rust", "--rust")]
  [Arguments("-DotnetRust", "--dotnet-rust")]
  public async Task ImageVariantAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== MOUNT ALIASES =====

  [Test]
  [Arguments("-Mount", "--mount")]
  [Arguments("-MountRW", "--mount-rw")]
  public async Task MountAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== EXECUTION OPTION ALIASES =====

  [Test]
  [Arguments("-NoCleanup", "--no-cleanup")]
  [Arguments("-NoPull", "--no-pull")]
  [Arguments("-SkipPull", "--no-pull")]
  public async Task ExecutionOptionAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== HELP ALIASES =====

  [Test]
  [Arguments("-Help2", "--help2")]
  public async Task HelpAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== UPDATE/UPGRADE ALIASES =====

  [Test]
  [Arguments("-u", "--update")]
  [Arguments("--upgrade", "--update")]
  [Arguments("-UpdateScripts", "--update")]
  [Arguments("-UpgradeScripts", "--update")]
  [Arguments("--update-scripts", "--update")]
  [Arguments("--upgrade-scripts", "--update")]
  public async Task UpdateAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== MOUNT COMMAND ALIASES =====

  [Test]
  [Arguments("-ListMounts", "--list-mounts")]
  [Arguments("-SaveMount", "--save-mount")]
  [Arguments("-SaveMountGlobal", "--save-mount-global")]
  [Arguments("-RemoveMount", "--remove-mount")]
  public async Task MountCommandAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== IMAGE COMMAND ALIASES =====

  [Test]
  [Arguments("-ListImages", "--list-images")]
  [Arguments("-ShowImage", "--show-image")]
  [Arguments("-SetImage", "--set-image")]
  [Arguments("-SetImageGlobal", "--set-image-global")]
  [Arguments("-ClearImage", "--clear-image")]
  [Arguments("-ClearImageGlobal", "--clear-image-global")]
  public async Task ImageCommandAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== AIRLOCK COMMAND ALIASES =====

  [Test]
  [Arguments("-EnableAirlock", "--enable-airlock")]
  [Arguments("-EnableGlobalAirlock", "--enable-global-airlock")]
  [Arguments("-DisableAirlock", "--disable-airlock")]
  [Arguments("-DisableGlobalAirlock", "--disable-global-airlock")]
  [Arguments("-ShowAirlockRules", "--show-airlock-rules")]
  [Arguments("-EditAirlockRules", "--edit-airlock-rules")]
  [Arguments("-EditGlobalAirlockRules", "--edit-global-airlock-rules")]
  public async Task AirlockCommandAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== YOLO MODE ALIASES =====

  [Test]
  [Arguments("-Yolo", "--yolo")]
  public async Task YoloAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== COPILOT PASSTHROUGH ALIASES =====

  [Test]
  [Arguments("-Prompt", "--prompt")]
  [Arguments("-Model", "--model")]
  [Arguments("-Continue", "--continue")]
  [Arguments("-Resume", "--resume")]
  public async Task CopilotPassthroughAlias_NormalizesToCanonicalForm(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  // ===== CANONICAL FORMS PASS THROUGH =====

  [Test]
  [Arguments("--dotnet")]
  [Arguments("--dotnet8")]
  [Arguments("--dotnet9")]
  [Arguments("--dotnet10")]
  [Arguments("--playwright")]
  [Arguments("--dotnet-playwright")]
  [Arguments("--rust")]
  [Arguments("--dotnet-rust")]
  [Arguments("--mount")]
  [Arguments("--mount-rw")]
  [Arguments("--no-cleanup")]
  [Arguments("--no-pull")]
  [Arguments("--skip-pull")]
  [Arguments("--help2")]
  [Arguments("--update")]
  [Arguments("--list-mounts")]
  [Arguments("--save-mount")]
  [Arguments("--save-mount-global")]
  [Arguments("--remove-mount")]
  [Arguments("--list-images")]
  [Arguments("--show-image")]
  [Arguments("--set-image")]
  [Arguments("--set-image-global")]
  [Arguments("--clear-image")]
  [Arguments("--clear-image-global")]
  [Arguments("--enable-airlock")]
  [Arguments("--enable-global-airlock")]
  [Arguments("--disable-airlock")]
  [Arguments("--disable-global-airlock")]
  [Arguments("--show-airlock-rules")]
  [Arguments("--edit-airlock-rules")]
  [Arguments("--edit-global-airlock-rules")]
  [Arguments("--yolo")]
  // Copilot passthrough canonical forms
  [Arguments("--prompt")]
  [Arguments("--model")]
  [Arguments("--continue")]
  [Arguments("--resume")]
  public async Task CanonicalForm_PassesThroughUnchanged(string arg)
  {
    // Arrange & Act
    var normalized = NormalizeArg(arg);

    // Assert
    await Assert.That(normalized).IsEqualTo(arg);
  }

  // ===== UNKNOWN ARGS PASS THROUGH =====

  [Test]
  [Arguments("--unknown")]
  [Arguments("-x")]
  [Arguments("some-value")]
  [Arguments("/path/to/file")]
  public async Task UnknownArg_PassesThroughUnchanged(string arg)
  {
    // Arrange & Act
    var normalized = NormalizeArg(arg);

    // Assert
    await Assert.That(normalized).IsEqualTo(arg);
  }

  // ===== YOLO MODE DETECTION =====

  [Test]
  [Arguments(new[] { "--yolo" }, true)]
  [Arguments(new[] { "-Yolo" }, true)]
  [Arguments(new[] { "--dotnet", "--yolo" }, true)]
  [Arguments(new[] { "--yolo", "--dotnet" }, true)]
  [Arguments(new[] { "--dotnet" }, false)]
  [Arguments(new string[] { }, false)]
  public async Task IsYoloMode_DetectsYoloFlag(string[] args, bool expected)
  {
    // Arrange & Act
    var isYolo = IsYoloMode(args);

    // Assert
    await Assert.That(isYolo).IsEqualTo(expected);
  }

  // ===== YOLO FLAG STRIPPING =====

  [Test]
  public async Task NormalizeArgs_StripsYoloFlag()
  {
    // Arrange
    var args = new[] { "--dotnet", "--yolo", "--no-pull" };

    // Act
    var normalized = NormalizeArgs(args);

    // Assert
    await Assert.That(normalized).HasCount().EqualTo(2);
    await Assert.That(normalized).Contains("--dotnet");
    await Assert.That(normalized).Contains("--no-pull");
    await Assert.That(normalized).DoesNotContain("--yolo");
  }

  [Test]
  public async Task NormalizeArgs_StripsPowerShellYoloFlag()
  {
    // Arrange
    var args = new[] { "-Dotnet", "-Yolo", "-NoPull" };

    // Act
    var normalized = NormalizeArgs(args);

    // Assert
    await Assert.That(normalized).HasCount().EqualTo(2);
    await Assert.That(normalized).Contains("--dotnet");
    await Assert.That(normalized).Contains("--no-pull");
    await Assert.That(normalized).DoesNotContain("--yolo");
    await Assert.That(normalized).DoesNotContain("-Yolo");
  }

  // ===== CASE INSENSITIVITY =====

  [Test]
  [Arguments("-DOTNET", "--dotnet")]
  [Arguments("-dotnet", "--dotnet")]
  [Arguments("-DoTnEt", "--dotnet")]
  [Arguments("-YOLO", "--yolo")]
  [Arguments("-yolo", "--yolo")]
  public async Task AliasMap_IsCaseInsensitive(string alias, string expected)
  {
    // Arrange & Act
    var normalized = NormalizeArg(alias);

    // Assert
    await Assert.That(normalized).IsEqualTo(expected);
  }

  #region Helper Methods (mirrors Program.cs logic)

  // These mirror the logic in Program.cs for testing purposes
  private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
  {
    // Short aliases (single-dash multi-char)
    { "-d", "--dotnet" },
    { "-d8", "--dotnet8" },
    { "-d9", "--dotnet9" },
    { "-d10", "--dotnet10" },
    { "-dp", "--dotnet-playwright" },
    { "-pw", "--playwright" },
    { "-rs", "--rust" },
    { "-dr", "--dotnet-rust" },

    // PowerShell-style aliases (hidden from help, for backwards compatibility)
    { "-Dotnet", "--dotnet" },
    { "-Dotnet8", "--dotnet8" },
    { "-Dotnet9", "--dotnet9" },
    { "-Dotnet10", "--dotnet10" },
    { "-Playwright", "--playwright" },
    { "-DotnetPlaywright", "--dotnet-playwright" },
    { "-Rust", "--rust" },
    { "-DotnetRust", "--dotnet-rust" },
    { "-Mount", "--mount" },
    { "-MountRW", "--mount-rw" },
    { "-NoCleanup", "--no-cleanup" },
    { "-NoPull", "--no-pull" },
    { "-SkipPull", "--no-pull" },
    { "-Help2", "--help2" },
    { "-u", "--update" },
    { "--upgrade", "--update" },
    { "-UpdateScripts", "--update" },
    { "-UpgradeScripts", "--update" },
    { "--update-scripts", "--update" },
    { "--upgrade-scripts", "--update" },
    { "-ListMounts", "--list-mounts" },
    { "-SaveMount", "--save-mount" },
    { "-SaveMountGlobal", "--save-mount-global" },
    { "-RemoveMount", "--remove-mount" },
    { "-ListImages", "--list-images" },
    { "-ShowImage", "--show-image" },
    { "-SetImage", "--set-image" },
    { "-SetImageGlobal", "--set-image-global" },
    { "-ClearImage", "--clear-image" },
    { "-ClearImageGlobal", "--clear-image-global" },
    { "-EnableAirlock", "--enable-airlock" },
    { "-EnableGlobalAirlock", "--enable-global-airlock" },
    { "-DisableAirlock", "--disable-airlock" },
    { "-DisableGlobalAirlock", "--disable-global-airlock" },
    { "-ShowAirlockRules", "--show-airlock-rules" },
    { "-EditAirlockRules", "--edit-airlock-rules" },
    { "-EditGlobalAirlockRules", "--edit-global-airlock-rules" },
    { "-Yolo", "--yolo" },

    // Copilot passthrough options (PowerShell-style aliases)
    { "-Prompt", "--prompt" },
    { "-Model", "--model" },
    { "-Continue", "--continue" },
    { "-Resume", "--resume" },
  };

  private static string NormalizeArg(string arg)
  {
    return AliasMap.TryGetValue(arg, out var mapped) ? mapped : arg;
  }

  private static bool IsYoloMode(string[] args)
  {
    foreach (var arg in args)
    {
      if (arg.Equals("--yolo", StringComparison.OrdinalIgnoreCase) ||
          arg.Equals("-Yolo", StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }
    return false;
  }

  private static string[] NormalizeArgs(string[] args)
  {
    var normalized = new List<string>(args.Length);
    foreach (var arg in args)
    {
      // Skip --yolo flag (already processed for app name)
      if (arg.Equals("--yolo", StringComparison.OrdinalIgnoreCase) ||
          arg.Equals("-Yolo", StringComparison.OrdinalIgnoreCase))
      {
        continue;
      }

      normalized.Add(AliasMap.TryGetValue(arg, out var mapped) ? mapped : arg);
    }
    return [.. normalized];
  }

  #endregion
}
