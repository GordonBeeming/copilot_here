using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Mounts;

/// <summary>
/// Configuration for additional mount paths.
/// Merge strategy: Global mounts + Local mounts + CLI mounts (all additive, no override)
/// </summary>
public sealed record MountsConfig
{
  /// <summary>Mounts from global config.</summary>
  public required IReadOnlyList<MountEntry> GlobalMounts { get; init; }

  /// <summary>Mounts from local config.</summary>
  public required IReadOnlyList<MountEntry> LocalMounts { get; init; }

  private const string ConfigFileName = "mounts.conf";

  /// <summary>
  /// Loads mount configuration from config files.
  /// CLI mounts are added by the command handler.
  /// </summary>
  public static MountsConfig Load(AppPaths paths)
  {
    return new MountsConfig
    {
      GlobalMounts = LoadFromFile(paths.GetGlobalPath(ConfigFileName), MountSource.Global),
      LocalMounts = LoadFromFile(paths.GetLocalPath(ConfigFileName), MountSource.Local)
    };
  }

  private static List<MountEntry> LoadFromFile(string path, MountSource source)
  {
    var mounts = new List<MountEntry>();

    foreach (var line in ConfigFile.ReadLines(path))
    {
      var isReadWrite = false;
      var mountPath = line;

      if (line.EndsWith(":rw", StringComparison.OrdinalIgnoreCase))
      {
        isReadWrite = true;
        mountPath = line[..^3];
      }
      else if (line.EndsWith(":ro", StringComparison.OrdinalIgnoreCase))
      {
        mountPath = line[..^3];
      }

      mounts.Add(new MountEntry(mountPath, isReadWrite, source));
    }

    return mounts;
  }

  /// <summary>Saves a mount to local config.</summary>
  public static void SaveLocal(AppPaths paths, string path, bool isReadWrite)
  {
    SaveMount(paths.GetLocalPath(ConfigFileName), path, isReadWrite);
  }

  /// <summary>Saves a mount to global config.</summary>
  public static void SaveGlobal(AppPaths paths, string path, bool isReadWrite)
  {
    SaveMount(paths.GetGlobalPath(ConfigFileName), path, isReadWrite);
  }

  private static void SaveMount(string configPath, string path, bool isReadWrite)
  {
    // Check for duplicates
    foreach (var existing in ConfigFile.ReadLines(configPath))
    {
      var existingPath = existing.TrimEnd(":ro".ToCharArray()).TrimEnd(":rw".ToCharArray());
      if (existingPath.Equals(path, StringComparison.OrdinalIgnoreCase))
      {
        Console.WriteLine($"⚠️  Mount already exists in config: {path}");
        return;
      }
    }

    var suffix = isReadWrite ? ":rw" : "";
    ConfigFile.AppendLine(configPath, $"{path}{suffix}");

    var isGlobal = configPath.Contains(".config/copilot_here") || configPath.Contains(".config\\copilot_here");
    if (isGlobal)
      Console.WriteLine($"✅ Saved to global config: {path}{suffix}");
    else
      Console.WriteLine($"✅ Saved to local config: {path}{suffix}");

    Console.WriteLine($"   Config file: {configPath}");
  }

  /// <summary>Removes a mount from all configs.</summary>
  public static bool Remove(AppPaths paths, string path)
  {
    var removedLocal = RemoveFromFile(paths.GetLocalPath(ConfigFileName), path);
    var removedGlobal = RemoveFromFile(paths.GetGlobalPath(ConfigFileName), path);
    return removedLocal || removedGlobal;
  }

  private static bool RemoveFromFile(string configPath, string pathToRemove)
  {
    if (!File.Exists(configPath)) return false;

    var lines = File.ReadAllLines(configPath);
    var newLines = new List<string>();
    var found = false;

    foreach (var line in lines)
    {
      var trimmed = line.Trim();
      var linePath = trimmed.TrimEnd(":ro".ToCharArray()).TrimEnd(":rw".ToCharArray());

      if (linePath.Equals(pathToRemove, StringComparison.OrdinalIgnoreCase))
      {
        found = true;
        continue;
      }

      newLines.Add(line);
    }

    if (found)
    {
      File.WriteAllLines(configPath, newLines);
    }

    return found;
  }
}

/// <summary>
/// A single mount entry with its path, mode, and source.
/// </summary>
public readonly record struct MountEntry(string Path, bool IsReadWrite, MountSource Source)
{
  /// <summary>
  /// Resolves ~ and relative paths to absolute paths, following symlinks.
  /// </summary>
  public string ResolvePath(string userHome)
  {
    return PathValidator.ResolvePath(Path, userHome);
  }

  /// <summary>
  /// Validates the mount path (checks existence, sensitive paths).
  /// Returns true if mount should proceed, false if cancelled.
  /// </summary>
  public bool Validate(string userHome)
  {
    var resolved = ResolvePath(userHome);

    // Warn if path doesn't exist
    PathValidator.WarnIfNotExists(resolved);

    // Check for sensitive paths and prompt for confirmation
    if (!PathValidator.ValidateSensitivePath(resolved, userHome))
    {
      return false;
    }

    return true;
  }

  /// <summary>Calculates the container path for this mount.</summary>
  public string GetContainerPath(string userHome)
  {
    var hostPath = ResolvePath(userHome);
    if (hostPath.StartsWith(userHome))
    {
      return $"/home/appuser/{System.IO.Path.GetRelativePath(userHome, hostPath).Replace("\\", "/")}";
    }
    
    // For paths outside user home, use /work prefix
    if (OperatingSystem.IsWindows() && hostPath.Length >= 2 && hostPath[1] == ':')
    {
      // Windows path outside home (e.g., C:\Data\...) - convert to /work/c/Data/...
      var driveLetter = char.ToLowerInvariant(hostPath[0]);
      var pathWithoutDrive = hostPath.Substring(2).Replace("\\", "/");
      return $"/work/{driveLetter}{pathWithoutDrive}";
    }
    
    // Unix path outside home - use /work prefix
    return $"/work{hostPath}";
  }

  /// <summary>Gets the Docker volume mount string.</summary>
  public string ToDockerVolume(string userHome)
  {
    var hostPath = ResolvePath(userHome);
    var dockerHostPath = ConvertToDockerPath(hostPath);
    var containerPath = GetContainerPath(userHome);
    var mode = IsReadWrite ? "rw" : "ro";
    return $"{dockerHostPath}:{containerPath}:{mode}";
  }

  /// <summary>
  /// Converts a Windows path to Docker-compatible format.
  /// On Windows: C:\path -> /c/path
  /// On Unix: /path -> /path (no change)
  /// </summary>
  private static string ConvertToDockerPath(string path)
  {
    // Convert backslashes to forward slashes
    var normalizedPath = path.Replace("\\", "/");
    
    // On Windows, convert drive letter paths (C:/ -> /c/)
    if (OperatingSystem.IsWindows() && 
        normalizedPath.Length >= 2 && 
        normalizedPath[1] == ':')
    {
      var driveLetter = char.ToLowerInvariant(normalizedPath[0]);
      var pathWithoutDrive = normalizedPath.Substring(2);
      return $"/{driveLetter}{pathWithoutDrive}";
    }
    
    return normalizedPath;
  }
}

public enum MountSource
{
  Global,
  Local,
  CommandLine
}
