using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Configuration for the Airlock network proxy.
/// Priority: Local enabled flag > Global enabled flag
/// Rules: Local rules override global rules entirely (no merge)
/// </summary>
public sealed record AirlockConfig
{
  /// <summary>Whether Airlock is enabled.</summary>
  public required bool Enabled { get; init; }

  /// <summary>Source of the enabled flag.</summary>
  public required AirlockConfigSource EnabledSource { get; init; }

  /// <summary>Path to the active rules file (local or global).</summary>
  public string? RulesPath { get; init; }

  /// <summary>Source of the rules file.</summary>
  public AirlockConfigSource RulesSource { get; init; }

  private const string EnabledFileName = "airlock.enabled";
  private const string RulesFileName = "network.json";

  /// <summary>
  /// Loads Airlock configuration from config files.
  /// </summary>
  public static AirlockConfig Load(AppPaths paths)
  {
    // Check enabled flags (local takes precedence)
    var localEnabled = ConfigFile.ReadFlag(paths.GetLocalPath(EnabledFileName));
    var globalEnabled = ConfigFile.ReadFlag(paths.GetGlobalPath(EnabledFileName));

    bool enabled;
    AirlockConfigSource enabledSource;

    if (File.Exists(paths.GetLocalPath(EnabledFileName)))
    {
      enabled = localEnabled;
      enabledSource = AirlockConfigSource.Local;
    }
    else if (File.Exists(paths.GetGlobalPath(EnabledFileName)))
    {
      enabled = globalEnabled;
      enabledSource = AirlockConfigSource.Global;
    }
    else
    {
      enabled = false;
      enabledSource = AirlockConfigSource.Default;
    }

    // Find rules file (local overrides global entirely)
    string? rulesPath = null;
    var rulesSource = AirlockConfigSource.Default;

    var localRulesPath = paths.GetLocalPath(RulesFileName);
    var globalRulesPath = paths.GetGlobalPath(RulesFileName);

    if (File.Exists(localRulesPath))
    {
      rulesPath = localRulesPath;
      rulesSource = AirlockConfigSource.Local;
    }
    else if (File.Exists(globalRulesPath))
    {
      rulesPath = globalRulesPath;
      rulesSource = AirlockConfigSource.Global;
    }

    return new AirlockConfig
    {
      Enabled = enabled,
      EnabledSource = enabledSource,
      RulesPath = rulesPath,
      RulesSource = rulesSource
    };
  }

  /// <summary>Enables Airlock in local config.</summary>
  public static void EnableLocal(AppPaths paths)
  {
    ConfigFile.WriteFlag(paths.GetLocalPath(EnabledFileName), true);
  }

  /// <summary>Enables Airlock in global config.</summary>
  public static void EnableGlobal(AppPaths paths)
  {
    ConfigFile.WriteFlag(paths.GetGlobalPath(EnabledFileName), true);
  }

  /// <summary>Disables Airlock in local config.</summary>
  public static void DisableLocal(AppPaths paths)
  {
    ConfigFile.WriteFlag(paths.GetLocalPath(EnabledFileName), false);
  }

  /// <summary>Disables Airlock in global config.</summary>
  public static void DisableGlobal(AppPaths paths)
  {
    ConfigFile.WriteFlag(paths.GetGlobalPath(EnabledFileName), false);
  }

  /// <summary>Gets the path to the local rules file (creates dir if needed).</summary>
  public static string GetLocalRulesPath(AppPaths paths)
  {
    var path = paths.GetLocalPath(RulesFileName);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    return path;
  }

  /// <summary>Gets the path to the global rules file (creates dir if needed).</summary>
  public static string GetGlobalRulesPath(AppPaths paths)
  {
    var path = paths.GetGlobalPath(RulesFileName);
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
    return path;
  }
}

public enum AirlockConfigSource
{
  Default,
  Global,
  Local
}
