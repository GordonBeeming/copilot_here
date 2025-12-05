namespace CopilotHere.Infrastructure;

/// <summary>
/// Immutable record containing all resolved paths for the application.
/// This is the foundation - no config loading, just paths.
/// </summary>
public sealed record AppPaths
{
  /// <summary>Current working directory on the host.</summary>
  public required string CurrentDirectory { get; init; }

  /// <summary>User's home directory.</summary>
  public required string UserHome { get; init; }

  /// <summary>Path to copilot CLI config directory (~/.config/copilot-cli-docker).</summary>
  public required string CopilotConfigPath { get; init; }

  /// <summary>Path to local .copilot_here config directory.</summary>
  public required string LocalConfigPath { get; init; }

  /// <summary>Path to global ~/.config/copilot_here directory.</summary>
  public required string GlobalConfigPath { get; init; }

  /// <summary>Resolved container work directory path.</summary>
  public required string ContainerWorkDir { get; init; }

  /// <summary>Creates AppPaths with all paths resolved.</summary>
  public static AppPaths Resolve()
  {
    var currentDir = Directory.GetCurrentDirectory();
    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    var globalConfigPath = Path.Combine(userHome, ".config", "copilot_here");
    var copilotConfigPath = Path.Combine(userHome, ".config", "copilot-cli-docker");

    // Ensure config directories exist
    try 
    { 
      Directory.CreateDirectory(globalConfigPath); 
    } 
    catch (Exception ex)
    {
      Console.Error.WriteLine($"⚠️  Warning: Cannot create config directory: {globalConfigPath}");
      Console.Error.WriteLine($"   {ex.Message}");
      Console.Error.WriteLine($"   Global configuration features may not work.");
    }
    
    try 
    { 
      Directory.CreateDirectory(copilotConfigPath); 
    } 
    catch (Exception ex)
    {
      Console.Error.WriteLine($"⚠️  Warning: Cannot create Copilot config directory: {copilotConfigPath}");
      Console.Error.WriteLine($"   {ex.Message}");
      Console.Error.WriteLine($"   Copilot session data may not persist.");
    }

    // Calculate container work directory
    var containerWorkDir = currentDir.StartsWith(userHome)
      ? $"/home/appuser/{Path.GetRelativePath(userHome, currentDir).Replace("\\", "/")}"
      : currentDir;

    return new AppPaths
    {
      CurrentDirectory = currentDir,
      UserHome = userHome,
      CopilotConfigPath = copilotConfigPath,
      LocalConfigPath = Path.Combine(currentDir, ".copilot_here"),
      GlobalConfigPath = globalConfigPath,
      ContainerWorkDir = containerWorkDir
    };
  }

  /// <summary>Gets the local config file path for a given filename.</summary>
  public string GetLocalPath(string filename) => Path.Combine(LocalConfigPath, filename);

  /// <summary>Gets the global config file path for a given filename.</summary>
  public string GetGlobalPath(string filename) => Path.Combine(GlobalConfigPath, filename);
}
