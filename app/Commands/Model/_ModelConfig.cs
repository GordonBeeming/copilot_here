using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

/// <summary>
/// Configuration for model selection.
/// Priority: CLI arg > Local config > Global config > Default (none)
/// </summary>
public sealed record ModelConfig
{
  /// <summary>The resolved model to use.</summary>
  public string? Model { get; init; }

  /// <summary>Source of the resolved model (for display purposes).</summary>
  public required ModelConfigSource Source { get; init; }

  /// <summary>Model from local config, if any.</summary>
  public string? LocalModel { get; init; }

  /// <summary>Model from global config, if any.</summary>
  public string? GlobalModel { get; init; }

  private const string ConfigFileName = "model.conf";
  private const string DefaultKeyword = "default";

  /// <summary>
  /// Loads model configuration from config files.
  /// Does NOT apply CLI overrides - that's done by the command handler.
  /// If config contains "default", treats it as null (use Copilot CLI default).
  /// </summary>
  public static ModelConfig Load(AppPaths paths)
  {
    var localModel = ConfigFile.ReadValue(paths.GetLocalPath(ConfigFileName));
    var globalModel = ConfigFile.ReadValue(paths.GetGlobalPath(ConfigFileName));
    
    // Normalize "default" keyword to null
    if (string.Equals(localModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
      localModel = null;
    if (string.Equals(globalModel, DefaultKeyword, StringComparison.OrdinalIgnoreCase))
      globalModel = null;

    // Determine effective model and source
    string? model;
    ModelConfigSource source;

    if (localModel is not null)
    {
      model = localModel;
      source = ModelConfigSource.Local;
    }
    else if (globalModel is not null)
    {
      model = globalModel;
      source = ModelConfigSource.Global;
    }
    else
    {
      model = null;
      source = ModelConfigSource.Default;
    }

    return new ModelConfig
    {
      Model = model,
      Source = source,
      LocalModel = localModel,
      GlobalModel = globalModel
    };
  }

  /// <summary>Saves model to local config.</summary>
  public static void SaveLocal(AppPaths paths, string model)
  {
    ConfigFile.WriteValue(paths.GetLocalPath(ConfigFileName), model);
  }

  /// <summary>Saves model to global config.</summary>
  public static void SaveGlobal(AppPaths paths, string model)
  {
    ConfigFile.WriteValue(paths.GetGlobalPath(ConfigFileName), model);
  }

  /// <summary>Clears local model config.</summary>
  public static bool ClearLocal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetLocalPath(ConfigFileName));
  }

  /// <summary>Clears global model config.</summary>
  public static bool ClearGlobal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetGlobalPath(ConfigFileName));
  }
}

public enum ModelConfigSource
{
  Default,
  Global,
  Local,
  CommandLine
}
