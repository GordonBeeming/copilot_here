using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles self-update functionality for the copilot_here binary.
/// </summary>
public static class SelfUpdater
{
  private const string RepoUrl = "https://github.com/GordonBeeming/copilot_here";
  private const string ShellScriptUrl = "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh";

  /// <summary>
  /// Gets the current application version from the shell script version format (YYYY-MM-DD).
  /// </summary>
  public static string CurrentVersion => GetVersionFromDate(BuildInfo.BuildDate);

  /// <summary>
  /// Converts a build date to version format.
  /// </summary>
  private static string GetVersionFromDate(string buildDate)
  {
    // BuildDate is in format "yyyy-MM-dd", use as-is for version
    return buildDate;
  }

  /// <summary>
  /// Gets the runtime identifier for the current platform.
  /// </summary>
  public static string GetRuntimeIdentifier()
  {
    var arch = RuntimeInformation.ProcessArchitecture switch
    {
      Architecture.X64 => "x64",
      Architecture.Arm64 => "arm64",
      _ => "x64"
    };

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      return $"win-{arch}";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      return $"osx-{arch}";
    return $"linux-{arch}";
  }

  /// <summary>
  /// Checks for updates and prompts the user to update if available.
  /// Returns exit code 0 on success or if no update needed, 1 on error.
  /// </summary>
  public static int CheckAndUpdate()
  {
    Console.WriteLine("🔄 Checking for updates...");
    Console.WriteLine($"   Current version: {CurrentVersion}");

    var latestVersion = GetLatestVersion();
    if (latestVersion is null)
    {
      Console.WriteLine("❌ Failed to check for updates. Check your network connection.");
      Console.WriteLine($"   You can manually download from: {RepoUrl}/releases");
      return 1;
    }

    Console.WriteLine($"   Latest version:  {latestVersion}");

    if (IsCurrentVersionLatest(CurrentVersion, latestVersion))
    {
      Console.WriteLine("✅ You are already running the latest version!");
      return 0;
    }

    Console.WriteLine();
    Console.WriteLine($"📦 A new version ({latestVersion}) is available!");
    Console.WriteLine();

    // Show download instructions based on platform
    var rid = GetRuntimeIdentifier();
    var binaryName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "copilot_here.exe" : "copilot_here";
    var downloadUrl = $"{RepoUrl}/releases/download/v{latestVersion}/{binaryName}-{rid}";

    Console.WriteLine("To update, download the new binary:");
    Console.WriteLine();

    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      Console.WriteLine($"  # PowerShell:");
      Console.WriteLine($"  Invoke-WebRequest -Uri \"{downloadUrl}\" -OutFile \"$env:USERPROFILE\\.local\\bin\\{binaryName}\"");
    }
    else
    {
      Console.WriteLine($"  # Using curl:");
      Console.WriteLine($"  curl -fsSL \"{downloadUrl}\" -o ~/.local/bin/{binaryName} && chmod +x ~/.local/bin/{binaryName}");
    }

    Console.WriteLine();
    Console.WriteLine($"Or visit: {RepoUrl}/releases/latest");

    return 0;
  }

  /// <summary>
  /// Gets the latest version from the shell script on GitHub.
  /// Parses the "# Version: YYYY-MM-DD" header from copilot_here.sh.
  /// </summary>
  private static string? GetLatestVersion()
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = "curl",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("-fsSL");
      startInfo.ArgumentList.Add(ShellScriptUrl);

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var output = process.StandardOutput.ReadToEnd();
      process.WaitForExit();

      if (process.ExitCode != 0) return null;

      // Parse "# Version: YYYY-MM-DD" from the script
      foreach (var line in output.Split('\n'))
      {
        if (line.StartsWith("# Version:", StringComparison.OrdinalIgnoreCase))
        {
          var version = line["# Version:".Length..].Trim();
          if (!string.IsNullOrEmpty(version))
          {
            return version;
          }
        }
      }

      return null;
    }
    catch
    {
      return null;
    }
  }

  /// <summary>
  /// Compares version strings to check if current is latest or newer.
  /// Handles date-based versions (YYYY-MM-DD or YYYY-MM-DD.N).
  /// </summary>
  private static bool IsCurrentVersionLatest(string current, string latest)
  {
    // Try to compare as dates first
    if (TryCompareDateVersions(current, latest, out var isLatest))
    {
      return isLatest;
    }

    // Fall back to numeric version comparison
    var currentParts = ParseVersion(current);
    var latestParts = ParseVersion(latest);

    // If we couldn't parse either version, assume update is needed
    if (currentParts.Length == 0 || latestParts.Length == 0)
    {
      return false;
    }

    for (var i = 0; i < Math.Max(currentParts.Length, latestParts.Length); i++)
    {
      var c = i < currentParts.Length ? currentParts[i] : 0;
      var l = i < latestParts.Length ? latestParts[i] : 0;

      if (c > l) return true;  // Current is newer
      if (c < l) return false; // Latest is newer
    }

    return true; // Equal
  }

  /// <summary>
  /// Tries to compare versions as dates (YYYY-MM-DD or YYYY-MM-DD.N format).
  /// </summary>
  private static bool TryCompareDateVersions(string current, string latest, out bool isCurrentLatest)
  {
    isCurrentLatest = false;

    // Parse current: "2025-12-02" or "2025-12-02.1"
    var currentParts = current.Split('.');
    var latestParts = latest.Split('.');

    // Try to parse the date part
    if (!TryParseDatePart(currentParts[0], out var currentDate) ||
        !TryParseDatePart(latestParts[0], out var latestDate))
    {
      return false;
    }

    // Compare dates
    var dateComparison = currentDate.CompareTo(latestDate);
    if (dateComparison > 0)
    {
      isCurrentLatest = true;
      return true;
    }
    if (dateComparison < 0)
    {
      isCurrentLatest = false;
      return true;
    }

    // Same date, compare patch numbers (e.g., .1, .2)
    var currentPatch = currentParts.Length > 1 && int.TryParse(currentParts[1], out var cp) ? cp : 0;
    var latestPatch = latestParts.Length > 1 && int.TryParse(latestParts[1], out var lp) ? lp : 0;

    isCurrentLatest = currentPatch >= latestPatch;
    return true;
  }

  /// <summary>
  /// Tries to parse a YYYY-MM-DD string into a DateTime.
  /// </summary>
  private static bool TryParseDatePart(string datePart, out DateTime date)
  {
    return DateTime.TryParseExact(datePart, "yyyy-MM-dd", 
      System.Globalization.CultureInfo.InvariantCulture,
      System.Globalization.DateTimeStyles.None, out date);
  }

  private static int[] ParseVersion(string version)
  {
    return version.Split('.', '-')
      .Select(part => int.TryParse(part, out var num) ? num : (int?)null)
      .Where(n => n.HasValue)
      .Select(n => n!.Value)
      .ToArray();
  }
}
