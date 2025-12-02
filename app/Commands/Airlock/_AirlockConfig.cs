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
      enabled = ReadEnabledFromJson(rulesPath);
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
  /// Reads the "enabled" field from a network.json file.
  /// </summary>
  private static bool ReadEnabledFromJson(string path)
  {
    try
    {
      var json = File.ReadAllText(path);
      // Simple parsing - look for "enabled": true or "enabled":true
      // This avoids adding a JSON library dependency
      var enabledPattern = "\"enabled\"";
      var idx = json.IndexOf(enabledPattern, StringComparison.OrdinalIgnoreCase);
      if (idx < 0) return false;

      // Find the value after the colon
      var colonIdx = json.IndexOf(':', idx + enabledPattern.Length);
      if (colonIdx < 0) return false;

      // Extract the value (skip whitespace)
      var valueStart = colonIdx + 1;
      while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
        valueStart++;

      // Check if it starts with 'true'
      return json.Length > valueStart + 3 &&
             json.Substring(valueStart, 4).Equals("true", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
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
    if (!File.Exists(path))
    {
      // Create a minimal config
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
      
      var json = enabled 
        ? "{\n  \"enabled\": true,\n  \"inherit_default_rules\": true,\n  \"mode\": \"enforce\",\n  \"allowed_rules\": []\n}\n"
        : "{\n  \"enabled\": false,\n  \"inherit_default_rules\": true,\n  \"mode\": \"enforce\",\n  \"allowed_rules\": []\n}\n";
      File.WriteAllText(path, json);
      return;
    }

    // Update existing file
    var content = File.ReadAllText(path);
    var enabledPattern = "\"enabled\"";
    var idx = content.IndexOf(enabledPattern, StringComparison.OrdinalIgnoreCase);
    
    if (idx >= 0)
    {
      // Find the value and replace it
      var colonIdx = content.IndexOf(':', idx + enabledPattern.Length);
      if (colonIdx >= 0)
      {
        var valueStart = colonIdx + 1;
        while (valueStart < content.Length && char.IsWhiteSpace(content[valueStart]))
          valueStart++;
        
        // Find end of value (true or false)
        var valueEnd = valueStart;
        while (valueEnd < content.Length && char.IsLetter(content[valueEnd]))
          valueEnd++;
        
        content = content[..valueStart] + (enabled ? "true" : "false") + content[valueEnd..];
        File.WriteAllText(path, content);
      }
    }
    else
    {
      // Insert enabled field at the start of the object
      var braceIdx = content.IndexOf('{');
      if (braceIdx >= 0)
      {
        content = content[..(braceIdx + 1)] + $"\n  \"enabled\": {(enabled ? "true" : "false")}," + content[(braceIdx + 1)..];
        File.WriteAllText(path, content);
      }
    }
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
