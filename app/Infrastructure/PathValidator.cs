namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles path validation, symlink resolution, and sensitive path warnings.
/// </summary>
public static class PathValidator
{
  private static readonly string[] SensitivePaths =
  [
    "/",
    "/etc",
    "/root",
    "~/.ssh"
  ];

  /// <summary>
  /// Resolves a path, following symlinks and expanding ~ to home directory.
  /// </summary>
  public static string ResolvePath(string path, string userHome)
  {
    // Expand tilde
    var expanded = path.Replace("~", userHome);

    // Get absolute path
    var absolute = Path.GetFullPath(expanded);

    // Follow symlinks if the path exists
    if (File.Exists(absolute) || Directory.Exists(absolute))
    {
      var resolved = ResolveSymlink(absolute);
      if (resolved != absolute)
      {
        Console.WriteLine($"🔗 Following symlink: {path} → {resolved}");
        return resolved;
      }
    }

    return absolute;
  }

  /// <summary>
  /// Checks if a path is sensitive and prompts for confirmation if so.
  /// Returns true if the operation should proceed, false if cancelled.
  /// </summary>
  public static bool ValidateSensitivePath(string resolvedPath, string userHome)
  {
    // Expand ~ in sensitive paths for comparison
    var matchingSensitivePath = SensitivePaths
      .Select(p => p.Replace("~", userHome))
      .FirstOrDefault(expandedSensitive =>
        resolvedPath.Equals(expandedSensitive, StringComparison.OrdinalIgnoreCase) ||
        resolvedPath.StartsWith(expandedSensitive + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    if (matchingSensitivePath is null)
      return true;

    Console.WriteLine($"⚠️  Warning: Mounting sensitive system path: {resolvedPath}");
    Console.Write("Are you sure you want to mount this sensitive path? [y/N]: ");

    var response = Console.ReadLine()?.Trim().ToLowerInvariant();
    if (response != "y" && response != "yes")
    {
      Console.WriteLine("Operation cancelled by user.");
      return false;
    }

    return true;
  }

  /// <summary>
  /// Validates a path exists and warns if it doesn't.
  /// </summary>
  public static bool WarnIfNotExists(string resolvedPath)
  {
    if (!File.Exists(resolvedPath) && !Directory.Exists(resolvedPath))
    {
      Console.WriteLine($"⚠️  Warning: Path does not exist: {resolvedPath}");
      return false;
    }

    return true;
  }

  /// <summary>
  /// Resolves symlinks to their target path (cross-platform).
  /// </summary>
  private static string ResolveSymlink(string path)
  {
    try
    {
      var fileInfo = new FileInfo(path);
      if (fileInfo.LinkTarget != null)
      {
        // It's a symlink - resolve it
        var targetPath = fileInfo.LinkTarget;

        // If target is relative, make it absolute
        if (!Path.IsPathRooted(targetPath))
        {
          var directory = Path.GetDirectoryName(path) ?? "";
          targetPath = Path.GetFullPath(Path.Combine(directory, targetPath));
        }

        // Recursively resolve in case of symlink chains
        return ResolveSymlink(targetPath);
      }

      // Also check if it's a directory symlink
      var dirInfo = new DirectoryInfo(path);
      if (dirInfo.Exists && dirInfo.LinkTarget != null)
      {
        var targetPath = dirInfo.LinkTarget;

        if (!Path.IsPathRooted(targetPath))
        {
          var parentDir = Path.GetDirectoryName(path) ?? "";
          targetPath = Path.GetFullPath(Path.Combine(parentDir, targetPath));
        }

        return ResolveSymlink(targetPath);
      }
    }
    catch
    {
      // If we can't resolve the symlink, return the original path
    }

    return path;
  }
}
