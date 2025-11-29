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

    // On Unix, try to get actual UID
    try
    {
      var uid = Environment.GetEnvironmentVariable("UID");
      if (!string.IsNullOrEmpty(uid)) return uid;
    }
    catch { }

    return "1000";
  }

  /// <summary>
  /// Gets the current group ID (for Linux container permissions).
  /// </summary>
  public static string GetGroupId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return "1000";

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
}
