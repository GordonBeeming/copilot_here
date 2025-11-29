using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Images;

/// <summary>
/// Configuration for image selection.
/// Priority: CLI arg > Local config > Global config > Default ("latest")
/// </summary>
public sealed record ImageConfig
{
  /// <summary>The resolved image tag to use.</summary>
  public required string Tag { get; init; }

  /// <summary>Source of the resolved tag (for display purposes).</summary>
  public required ImageConfigSource Source { get; init; }

  /// <summary>Tag from local config, if any.</summary>
  public string? LocalTag { get; init; }

  /// <summary>Tag from global config, if any.</summary>
  public string? GlobalTag { get; init; }

  private const string ConfigFileName = "image.conf";
  private const string DefaultTag = "latest";

  /// <summary>
  /// Loads image configuration from config files.
  /// Does NOT apply CLI overrides - that's done by the command handler.
  /// </summary>
  public static ImageConfig Load(AppPaths paths)
  {
    var localTag = ConfigFile.ReadValue(paths.GetLocalPath(ConfigFileName));
    var globalTag = ConfigFile.ReadValue(paths.GetGlobalPath(ConfigFileName));

    // Determine effective tag and source
    string tag;
    ImageConfigSource source;

    if (localTag is not null)
    {
      tag = localTag;
      source = ImageConfigSource.Local;
    }
    else if (globalTag is not null)
    {
      tag = globalTag;
      source = ImageConfigSource.Global;
    }
    else
    {
      tag = DefaultTag;
      source = ImageConfigSource.Default;
    }

    return new ImageConfig
    {
      Tag = tag,
      Source = source,
      LocalTag = localTag,
      GlobalTag = globalTag
    };
  }

  /// <summary>Saves image tag to local config.</summary>
  public static void SaveLocal(AppPaths paths, string tag)
  {
    ConfigFile.WriteValue(paths.GetLocalPath(ConfigFileName), tag);
  }

  /// <summary>Saves image tag to global config.</summary>
  public static void SaveGlobal(AppPaths paths, string tag)
  {
    ConfigFile.WriteValue(paths.GetGlobalPath(ConfigFileName), tag);
  }

  /// <summary>Clears local image config.</summary>
  public static bool ClearLocal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetLocalPath(ConfigFileName));
  }

  /// <summary>Clears global image config.</summary>
  public static bool ClearGlobal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetGlobalPath(ConfigFileName));
  }
}

public enum ImageConfigSource
{
  Default,
  Global,
  Local,
  CommandLine
}
