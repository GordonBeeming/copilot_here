using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Configuration for the Airlock network proxy.
/// The enabled flag is read from within the network.json file.
/// Local rules override global rules entirely (no merge).
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

  private const string RulesFileName = "network.json";

  /// <summary>
  /// Loads Airlock configuration from config files.
  /// The enabled flag is read from within the network.json file.
  /// </summary>
  public static AirlockConfig Load(AppPaths paths)
  {
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

    // Read enabled flag from the JSON file
    var enabled = false;
    if (rulesPath != null)
    {
      var config = ReadNetworkConfig(rulesPath);
      enabled = config?.Enabled ?? false;
    }

    return new AirlockConfig
    {
      Enabled = enabled,
      EnabledSource = rulesSource,
      RulesPath = rulesPath,
      RulesSource = rulesSource
    };
  }

  /// <summary>
  /// Reads a NetworkConfig from a JSON file.
  /// Returns null if the file doesn't exist.
  /// Throws JsonException if the file contains invalid JSON.
  /// </summary>
  internal static NetworkConfig? ReadNetworkConfig(string path)
  {
    if (!File.Exists(path))
      return null;

    using var stream = File.OpenRead(path);
    return JsonSerializer.Deserialize(stream, NetworkConfigJsonContext.Default.NetworkConfig);
  }

  /// <summary>
  /// Writes a NetworkConfig to a JSON file.
  /// </summary>
  internal static void WriteNetworkConfig(string path, NetworkConfig config)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var stream = File.Create(path);
    JsonSerializer.Serialize(stream, config, NetworkConfigJsonContext.Default.NetworkConfig);
  }

  /// <summary>Enables Airlock in local config by setting enabled:true in network.json.</summary>
  public static void EnableLocal(AppPaths paths)
  {
    SetEnabledInJson(paths.GetLocalPath(RulesFileName), true);
  }

  /// <summary>Enables Airlock in global config by setting enabled:true in network.json.</summary>
  public static void EnableGlobal(AppPaths paths)
  {
    SetEnabledInJson(paths.GetGlobalPath(RulesFileName), true);
  }

  /// <summary>Disables Airlock in local config by setting enabled:false in network.json.</summary>
  public static void DisableLocal(AppPaths paths)
  {
    SetEnabledInJson(paths.GetLocalPath(RulesFileName), false);
  }

  /// <summary>Disables Airlock in global config by setting enabled:false in network.json.</summary>
  public static void DisableGlobal(AppPaths paths)
  {
    SetEnabledInJson(paths.GetGlobalPath(RulesFileName), false);
  }

  /// <summary>Sets the enabled flag in a network.json file.</summary>
  private static void SetEnabledInJson(string path, bool enabled)
  {
    NetworkConfig config;

    if (File.Exists(path))
    {
      config = ReadNetworkConfig(path) ?? NetworkConfig.CreateDefault(enabled);
      config.Enabled = enabled;
    }
    else
    {
      config = NetworkConfig.CreateDefault(enabled);
    }

    WriteNetworkConfig(path, config);
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
