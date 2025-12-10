using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopilotHere.Infrastructure;

/// <summary>
/// System information utilities for cross-platform support.
/// </summary>
public static class SystemInfo
{
  /// <summary>
  /// Gets the current user ID (for Linux container permissions).
  /// </summary>
  public static string GetUserId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "1000";

    // Try environment variable first
    var uid = Environment.GetEnvironmentVariable("UID");
    if (!string.IsNullOrEmpty(uid))
      return uid;

    // Run 'id -u' to get actual UID
    try
    {
      var startInfo = new ProcessStartInfo("id", "-u")
      {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };
      using var process = Process.Start(startInfo);
      if (process != null)
      {
        var result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
          return result;
      }
    }
    catch
    {
      // Process execution failed - fall back to default UID
    }

    return "1000";
  }

  /// <summary>
  /// Gets the current group ID (for Linux container permissions).
  /// </summary>
  public static string GetGroupId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "1000";

    // Run 'id -g' to get actual GID
    try
    {
      var startInfo = new ProcessStartInfo("id", "-g")
      {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };
      using var process = Process.Start(startInfo);
      if (process != null)
      {
        var result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        if (process.ExitCode == 0 && !string.IsNullOrEmpty(result))
          return result;
      }
    }
    catch
    {
      // Process execution failed - fall back to default GID
    }

    return "1000";
  }

  /// <summary>
  /// Checks if the terminal supports emoji display.
  /// </summary>
  public static bool SupportsEmoji()
  {
    var lang = Environment.GetEnvironmentVariable("LANG") ?? "";
    var term = Environment.GetEnvironmentVariable("TERM") ?? "";

    return lang.Contains("UTF-8", StringComparison.OrdinalIgnoreCase) &&
           !term.Equals("dumb", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Checks if the terminal supports emoji variation selectors (U+FE0F).
  /// Windows Terminal has issues with variation selectors, while macOS/Linux terminals handle them fine.
  /// </summary>
  public static bool SupportsEmojiVariationSelectors()
  {
    // Check environment variable override first
    var envVar = Environment.GetEnvironmentVariable("COPILOT_HERE_EMOJI_VARIANT");
    if (!string.IsNullOrEmpty(envVar))
    {
      return envVar.Equals("full", StringComparison.OrdinalIgnoreCase);
    }

    // Windows Terminal struggles with variation selectors
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return false;
    }

    // macOS and Linux terminals typically handle them well
    return SupportsEmoji();
  }

  /// <summary>
  /// Gets the current directory name for use in terminal titles.
  /// </summary>
  public static string GetCurrentDirectoryName()
  {
    return Path.GetFileName(Directory.GetCurrentDirectory()) ?? "copilot_here";
  }
}
