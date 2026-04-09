using System.Reflection;
using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

/// <summary>
/// Loader and persistence helpers for the brokered Docker socket configuration.
/// Local config overrides global; global overrides the embedded default rules.
/// Mirrors the AirlockConfig pattern.
/// </summary>
public static class DockerBrokerConfigLoader
{
  private const string RulesFileName = "docker-broker.json";

  /// <summary>
  /// Loads the effective broker configuration for the current session.
  /// Resolution order: local file → global file → embedded default rules.
  /// When a user file has inherit_default_rules=true, missing endpoints from the
  /// embedded defaults are merged in (user entries take precedence by method+path).
  /// </summary>
  public static DockerBrokerConfig LoadEffective(AppPaths paths, out DockerBrokerConfigSource source)
  {
    var localPath = paths.GetLocalPath(RulesFileName);
    var globalPath = paths.GetGlobalPath(RulesFileName);

    DockerBrokerConfig? userConfig = null;
    source = DockerBrokerConfigSource.Default;

    if (File.Exists(localPath))
    {
      userConfig = ReadConfig(localPath);
      source = DockerBrokerConfigSource.Local;
    }
    else if (File.Exists(globalPath))
    {
      userConfig = ReadConfig(globalPath);
      source = DockerBrokerConfigSource.Global;
    }

    if (userConfig is null)
    {
      var defaults = LoadDefaultRules() ?? DockerBrokerConfig.CreateDefault(enabled: true);
      defaults.Enabled = true;
      return defaults;
    }

    if (userConfig.InheritDefaultRules)
    {
      var defaults = LoadDefaultRules();
      if (defaults is not null)
      {
        var existingKeys = new HashSet<string>(
          userConfig.AllowedEndpoints.Select(e => $"{e.Method.ToUpperInvariant()} {e.Path}"),
          StringComparer.Ordinal);

        foreach (var defaultEndpoint in defaults.AllowedEndpoints)
        {
          var key = $"{defaultEndpoint.Method.ToUpperInvariant()} {defaultEndpoint.Path}";
          if (!existingKeys.Contains(key))
          {
            userConfig.AllowedEndpoints.Add(defaultEndpoint);
          }
        }
      }
    }

    return userConfig;
  }

  /// <summary>
  /// Loads broker configuration ignoring sources — used to check the enabled flag
  /// without merging defaults. Mirrors AirlockConfig.Load semantics.
  /// </summary>
  public static (bool Enabled, string? RulesPath, DockerBrokerConfigSource Source) LoadEnabledFlag(AppPaths paths)
  {
    var localPath = paths.GetLocalPath(RulesFileName);
    var globalPath = paths.GetGlobalPath(RulesFileName);

    if (File.Exists(localPath))
    {
      var cfg = ReadConfig(localPath);
      return (cfg?.Enabled ?? false, localPath, DockerBrokerConfigSource.Local);
    }
    if (File.Exists(globalPath))
    {
      var cfg = ReadConfig(globalPath);
      return (cfg?.Enabled ?? false, globalPath, DockerBrokerConfigSource.Global);
    }
    return (false, null, DockerBrokerConfigSource.Default);
  }

  /// <summary>
  /// Reads a DockerBrokerConfig from a JSON file. Returns null if missing.
  /// </summary>
  public static DockerBrokerConfig? ReadConfig(string path)
  {
    if (!File.Exists(path))
      return null;

    using var stream = File.OpenRead(path);
    return JsonSerializer.Deserialize(stream, DockerBrokerConfigJsonContext.Default.DockerBrokerConfig);
  }

  /// <summary>
  /// Writes a DockerBrokerConfig to a JSON file, creating the directory if needed.
  /// </summary>
  public static void WriteConfig(string path, DockerBrokerConfig config)
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var stream = File.Create(path);
    JsonSerializer.Serialize(stream, config, DockerBrokerConfigJsonContext.Default.DockerBrokerConfig);
  }

  /// <summary>Loads the embedded default broker rules.</summary>
  public static DockerBrokerConfig? LoadDefaultRules()
  {
    try
    {
      var assembly = Assembly.GetExecutingAssembly();
      const string resourceName = "CopilotHere.Resources.default-docker-broker-rules.json";

      using var stream = assembly.GetManifestResourceStream(resourceName);
      if (stream is null) return null;

      return JsonSerializer.Deserialize(stream, DockerBrokerConfigJsonContext.Default.DockerBrokerConfig);
    }
    catch
    {
      return null;
    }
  }

  /// <summary>Enables broker in local config (creates file from defaults if missing).</summary>
  public static void EnableLocal(AppPaths paths) => SetEnabledInJson(paths.GetLocalPath(RulesFileName), true);

  /// <summary>Enables broker in global config (creates file from defaults if missing).</summary>
  public static void EnableGlobal(AppPaths paths) => SetEnabledInJson(paths.GetGlobalPath(RulesFileName), true);

  /// <summary>Disables broker in local config.</summary>
  public static void DisableLocal(AppPaths paths) => SetEnabledInJson(paths.GetLocalPath(RulesFileName), false);

  /// <summary>Disables broker in global config.</summary>
  public static void DisableGlobal(AppPaths paths) => SetEnabledInJson(paths.GetGlobalPath(RulesFileName), false);

  /// <summary>
  /// Adds an image glob pattern to the local config's allowed_images list.
  /// Idempotent — duplicates are not added. Creates the file from the clean
  /// overlay shape (empty endpoints, inherit defaults) if it doesn't exist.
  /// </summary>
  public static bool AddImageLocal(AppPaths paths, string pattern) =>
    AddImageInJson(paths.GetLocalPath(RulesFileName), pattern);

  /// <summary>Same as <see cref="AddImageLocal"/> but for the global config file.</summary>
  public static bool AddImageGlobal(AppPaths paths, string pattern) =>
    AddImageInJson(paths.GetGlobalPath(RulesFileName), pattern);

  /// <summary>Removes an image glob pattern from the local config's allowed_images list.</summary>
  public static bool RemoveImageLocal(AppPaths paths, string pattern) =>
    RemoveImageInJson(paths.GetLocalPath(RulesFileName), pattern);

  /// <summary>Removes an image glob pattern from the global config's allowed_images list.</summary>
  public static bool RemoveImageGlobal(AppPaths paths, string pattern) =>
    RemoveImageInJson(paths.GetGlobalPath(RulesFileName), pattern);

  /// <summary>
  /// Sets the body_inspection.reject_privileged toggle on the local config.
  /// Creates the file as a clean overlay if it doesn't exist.
  /// </summary>
  public static void SetRejectPrivilegedLocal(AppPaths paths, bool reject) =>
    SetRejectPrivilegedInJson(paths.GetLocalPath(RulesFileName), reject);

  /// <summary>Same as <see cref="SetRejectPrivilegedLocal"/> for the global config file.</summary>
  public static void SetRejectPrivilegedGlobal(AppPaths paths, bool reject) =>
    SetRejectPrivilegedInJson(paths.GetGlobalPath(RulesFileName), reject);

  private static DockerBrokerConfig LoadOrCreateOverlay(string path)
  {
    if (File.Exists(path))
    {
      return ReadConfig(path) ?? CreateOverlay(enabled: true);
    }
    return CreateOverlay(enabled: true);
  }

  private static DockerBrokerConfig CreateOverlay(bool enabled) => new()
  {
    Enabled = enabled,
    EnableLogging = false,
    InheritDefaultRules = true,
    Mode = "enforce",
    AllowedEndpoints = [],
    BodyInspection = new DockerBrokerBodyInspectionConfig()
  };

  private static bool AddImageInJson(string path, string pattern)
  {
    var config = LoadOrCreateOverlay(path);
    config.BodyInspection ??= new DockerBrokerBodyInspectionConfig();
    if (config.BodyInspection.AllowedImages.Contains(pattern, StringComparer.Ordinal))
    {
      return false;
    }
    config.BodyInspection.AllowedImages.Add(pattern);
    WriteConfig(path, config);
    return true;
  }

  private static bool RemoveImageInJson(string path, string pattern)
  {
    if (!File.Exists(path)) return false;
    var config = ReadConfig(path);
    if (config?.BodyInspection?.AllowedImages is null) return false;
    var removed = config.BodyInspection.AllowedImages.RemoveAll(p => string.Equals(p, pattern, StringComparison.Ordinal));
    if (removed > 0)
    {
      WriteConfig(path, config);
      return true;
    }
    return false;
  }

  private static void SetRejectPrivilegedInJson(string path, bool reject)
  {
    var config = LoadOrCreateOverlay(path);
    config.BodyInspection ??= new DockerBrokerBodyInspectionConfig();
    config.BodyInspection.RejectPrivileged = reject;
    WriteConfig(path, config);
  }

  private static void SetEnabledInJson(string path, bool enabled)
  {
    DockerBrokerConfig config;
    if (File.Exists(path))
    {
      config = ReadConfig(path) ?? DockerBrokerConfig.CreateDefault(enabled);
      config.Enabled = enabled;
    }
    else
    {
      // Seed a clean overlay: empty allowed_endpoints + inherit_default_rules=true.
      // The user gets a minimal file they can extend without having to delete
      // every default they don't want to maintain. The embedded defaults still
      // apply at runtime via inheritance.
      config = new DockerBrokerConfig
      {
        Enabled = enabled,
        EnableLogging = false,
        InheritDefaultRules = true,
        Mode = "enforce",
        AllowedEndpoints = []
      };
    }

    WriteConfig(path, config);
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

public enum DockerBrokerConfigSource
{
  Default,
  Global,
  Local
}
