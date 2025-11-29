namespace CopilotHere.Infrastructure;

/// <summary>
/// Generic utilities for reading/writing config files.
/// Each feature uses these primitives but implements its own merge logic.
/// </summary>
public static class ConfigFile
{
  /// <summary>
  /// Reads a single-value config file (e.g., image.conf containing just "dotnet").
  /// Returns null if file doesn't exist or is empty.
  /// </summary>
  public static string? ReadValue(string path)
  {
    if (!File.Exists(path)) return null;

    var content = File.ReadAllText(path).Trim();
    return string.IsNullOrEmpty(content) ? null : content;
  }

  /// <summary>
  /// Writes a single value to a config file, creating directories as needed.
  /// </summary>
  public static void WriteValue(string path, string value)
  {
    EnsureDirectory(path);
    File.WriteAllText(path, value);
  }

  /// <summary>
  /// Deletes a config file if it exists.
  /// </summary>
  public static bool Delete(string path)
  {
    if (!File.Exists(path)) return false;
    File.Delete(path);
    return true;
  }

  /// <summary>
  /// Reads a line-based config file, skipping comments (#) and empty lines.
  /// </summary>
  public static IEnumerable<string> ReadLines(string path)
  {
    if (!File.Exists(path)) yield break;

    foreach (var line in File.ReadAllLines(path))
    {
      var trimmed = line.Trim();
      if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
        yield return trimmed;
    }
  }

  /// <summary>
  /// Appends a line to a config file, creating it if needed.
  /// </summary>
  public static void AppendLine(string path, string line)
  {
    EnsureDirectory(path);
    File.AppendAllLines(path, [line]);
  }

  /// <summary>
  /// Reads a boolean flag file. Returns true if file exists and contains "true" or "1".
  /// </summary>
  public static bool ReadFlag(string path)
  {
    var value = ReadValue(path);
    return value is "true" or "1" or "yes" or "enabled";
  }

  /// <summary>
  /// Writes a boolean flag to a file.
  /// </summary>
  public static void WriteFlag(string path, bool value)
  {
    WriteValue(path, value ? "true" : "false");
  }

  private static void EnsureDirectory(string filePath)
  {
    var dir = Path.GetDirectoryName(filePath);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);
  }
}
