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
  /// Gets the current application version from the shell script version format (YYYY.MM.DD).
  /// </summary>
  public static string CurrentVersion => GetVersionFromDate(BuildInfo.BuildDate);

  /// <summary>
  /// Converts a build date to version format.
  /// </summary>
  private static string GetVersionFromDate(string buildDate)
  {
    // BuildDate is in format "yyyy.MM.dd", use as-is for version
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
    Console.WriteLine("📥 Downloading update...");

    // Download to .update file
    var rid = GetRuntimeIdentifier();
    var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
    var archiveName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
      ? $"copilot_here-{rid}.zip" 
      : $"copilot_here-{rid}.tar.gz";
    var downloadUrl = $"{RepoUrl}/releases/download/cli-latest/{archiveName}";

    // Get the path to the current binary
    var currentBinaryPath = Environment.ProcessPath;
    if (string.IsNullOrEmpty(currentBinaryPath))
    {
      Console.WriteLine("❌ Could not determine current binary path");
      return 1;
    }

    var updatePath = currentBinaryPath + ".update";
    var tempArchive = Path.GetTempFileName();

    try
    {
      // Download archive
      if (!DownloadFile(downloadUrl, tempArchive))
      {
        Console.WriteLine("❌ Failed to download update");
        return 1;
      }

      // Extract binary from archive
      if (!ExtractBinary(tempArchive, updatePath, rid))
      {
        Console.WriteLine("❌ Failed to extract update");
        return 1;
      }

      // Make executable on Unix
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        MakeExecutable(updatePath);
      }

      Console.WriteLine("✅ Update downloaded successfully!");
      Console.WriteLine("   The update will be applied on next run.");
      return 0;
    }
    finally
    {
      // Clean up temp file
      try { File.Delete(tempArchive); } catch { }
    }
  }

  /// <summary>
  /// Downloads a file from URL to path.
  /// </summary>
  private static bool DownloadFile(string url, string path)
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
      startInfo.ArgumentList.Add(url);
      startInfo.ArgumentList.Add("-o");
      startInfo.ArgumentList.Add(path);

      using var process = Process.Start(startInfo);
      if (process is null) return false;

      process.WaitForExit();
      return process.ExitCode == 0;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Extracts the binary from a tar.gz or zip archive.
  /// </summary>
  private static bool ExtractBinary(string archivePath, string outputPath, string rid)
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        // Use PowerShell to extract zip on Windows
        var startInfo = new ProcessStartInfo
        {
          FileName = "powershell",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add($"Expand-Archive -Path '{archivePath}' -DestinationPath '{tempDir}' -Force; Move-Item -Path '{Path.Combine(tempDir, "copilot_here.exe")}' -Destination '{outputPath}' -Force; Remove-Item -Path '{tempDir}' -Recurse -Force");

        using var process = Process.Start(startInfo);
        if (process is null) return false;

        process.WaitForExit();
        return process.ExitCode == 0 && File.Exists(outputPath);
      }
      else
      {
        // Use tar to extract on Unix
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var startInfo = new ProcessStartInfo
        {
          FileName = "tar",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-xzf");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(tempDir);

        using var process = Process.Start(startInfo);
        if (process is null) return false;

        process.WaitForExit();
        if (process.ExitCode != 0) return false;

        var extractedBinary = Path.Combine(tempDir, "copilot_here");
        if (!File.Exists(extractedBinary)) return false;

        File.Move(extractedBinary, outputPath, overwrite: true);
        
        try { Directory.Delete(tempDir, recursive: true); } catch { }
        
        return true;
      }
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Makes a file executable on Unix.
  /// </summary>
  private static void MakeExecutable(string path)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = "chmod",
        UseShellExecute = false,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("+x");
      startInfo.ArgumentList.Add(path);

      using var process = Process.Start(startInfo);
      process?.WaitForExit();
    }
    catch { }
  }

  /// <summary>
  /// Gets the latest version from the shell script on GitHub.
  /// Parses the "# Version: YYYY.MM.DD" header from copilot_here.sh.
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

      // Parse "# Version: YYYY.MM.DD" from the script
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
  /// Handles date-based versions (YYYY.MM.DD or YYYY.MM.DD.N).
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
  /// Tries to compare versions as dates (YYYY.MM.DD or YYYY.MM.DD.N format).
  /// Binary versions may have .sha suffix which is ignored for comparison.
  /// </summary>
  private static bool TryCompareDateVersions(string current, string latest, out bool isCurrentLatest)
  {
    isCurrentLatest = false;

    // Parse versions: "2025.12.02", "2025.12.02.1", or "2025.12.02.abc123" (binary with sha)
    var currentParts = current.Split('.');
    var latestParts = latest.Split('.');

    // Need at least 3 parts for date (YYYY.MM.DD)
    if (currentParts.Length < 3 || latestParts.Length < 3)
      return false;

    // Try to parse the date parts
    if (!TryParseDateParts(currentParts, out var currentDate) ||
        !TryParseDateParts(latestParts, out var latestDate))
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

    // Same date - compare patch numbers (4th part if numeric)
    // Binary sha (non-numeric 4th part) is ignored, treated as patch 0
    var currentPatch = GetPatchNumber(currentParts);
    var latestPatch = GetPatchNumber(latestParts);

    isCurrentLatest = currentPatch >= latestPatch;
    return true;
  }

  /// <summary>
  /// Gets the patch number from version parts. Returns 0 if no numeric 4th part.
  /// </summary>
  private static int GetPatchNumber(string[] parts)
  {
    if (parts.Length > 3 && int.TryParse(parts[3], out var patch))
      return patch;
    return 0;
  }

  /// <summary>
  /// Tries to parse YYYY.MM.DD parts into a DateTime.
  /// </summary>
  private static bool TryParseDateParts(string[] parts, out DateTime date)
  {
    date = default;
    if (parts.Length < 3) return false;
    
    if (!int.TryParse(parts[0], out var year) ||
        !int.TryParse(parts[1], out var month) ||
        !int.TryParse(parts[2], out var day))
      return false;

    try
    {
      date = new DateTime(year, month, day);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static int[] ParseVersion(string version)
  {
    return version.Split('.')
      .Select(part => int.TryParse(part, out var num) ? num : (int?)null)
      .Where(n => n.HasValue)
      .Select(n => n!.Value)
      .ToArray();
  }
}
