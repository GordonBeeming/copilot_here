using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles self-update functionality for the copilot_here binary.
/// </summary>
public static class SelfUpdater
{
  private const string ReleasesApiUrl = "https://api.github.com/repos/GordonBeeming/copilot_here/releases/latest";
  private const string RepoUrl = "https://github.com/GordonBeeming/copilot_here";

  /// <summary>
  /// Gets the current application version.
  /// </summary>
  public static string CurrentVersion
  {
    get
    {
      var assembly = typeof(SelfUpdater).Assembly;
      var version = assembly.GetName().Version;
      return version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "0.0.0";
    }
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
  /// Gets the latest version from GitHub releases.
  /// Looks for the cli-latest release which contains the version in its name.
  /// </summary>
  private static string? GetLatestVersion()
  {
    try
    {
      // First try the cli-latest release which has the version in the release name
      var startInfo = new ProcessStartInfo
      {
        FileName = "curl",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("-fsSL");
      startInfo.ArgumentList.Add("-H");
      startInfo.ArgumentList.Add("Accept: application/vnd.github+json");
      startInfo.ArgumentList.Add($"{ReleasesApiUrl.Replace("/latest", "")}/tags/cli-latest");

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var output = process.StandardOutput.ReadToEnd();
      process.WaitForExit();

      if (process.ExitCode != 0) return null;

      // The cli-latest release has the version in the "name" field: "CLI v0.1.0"
      // Look for "name": "CLI vX.Y.Z"
      var nameIndex = output.IndexOf("\"name\":", StringComparison.Ordinal);
      if (nameIndex >= 0)
      {
        var valueStart = output.IndexOf('"', nameIndex + 7);
        if (valueStart >= 0)
        {
          var valueEnd = output.IndexOf('"', valueStart + 1);
          if (valueEnd > valueStart)
          {
            var name = output.Substring(valueStart + 1, valueEnd - valueStart - 1);
            // Extract version from "CLI v0.1.0" or similar
            var vIndex = name.LastIndexOf('v');
            if (vIndex >= 0)
            {
              return name[(vIndex + 1)..].Trim();
            }
          }
        }
      }

      // Fallback: try to get version from tag_name (cli-vX.Y.Z-sha pattern)
      var tagIndex = output.IndexOf("\"tag_name\":", StringComparison.Ordinal);
      if (tagIndex >= 0)
      {
        var valueStart = output.IndexOf('"', tagIndex + 11);
        if (valueStart >= 0)
        {
          var valueEnd = output.IndexOf('"', valueStart + 1);
          if (valueEnd > valueStart)
          {
            var tagName = output.Substring(valueStart + 1, valueEnd - valueStart - 1);
            // Try to extract version from cli-v0.1.0-abc123 pattern
            if (tagName.StartsWith("cli-v", StringComparison.Ordinal))
            {
              var versionPart = tagName[5..]; // Remove "cli-v"
              var dashIndex = versionPart.IndexOf('-');
              if (dashIndex > 0)
              {
                return versionPart[..dashIndex];
              }
            }
            // Strip 'v' prefix if present
            return tagName.StartsWith('v') ? tagName[1..] : tagName;
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
  /// </summary>
  private static bool IsCurrentVersionLatest(string current, string latest)
  {
    // Parse versions as numeric components
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

  private static int[] ParseVersion(string version)
  {
    return version.Split('.', '-')
      .Select(part => int.TryParse(part, out var num) ? num : (int?)null)
      .Where(n => n.HasValue)
      .Select(n => n!.Value)
      .ToArray();
  }
}
