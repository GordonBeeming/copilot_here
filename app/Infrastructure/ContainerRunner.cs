using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles container runtime process execution (Docker, Podman, OrbStack).
/// </summary>
public static class ContainerRunner
{
  private const string ImagePrefix = "ghcr.io/gordonbeeming/copilot_here";

  /// <summary>
  /// Gets the full image name for a given tag.
  /// </summary>
  public static string GetImageName(string tag) => $"{ImagePrefix}:{tag}";

  /// <summary>
  /// Runs a container command with the given arguments.
  /// </summary>
  public static int Run(ContainerRuntimeConfig runtimeConfig, IEnumerable<string> args)
  {
    // Intercept Ctrl+C to let container runtime handle it
    Console.CancelKeyPress += (_, e) => e.Cancel = true;

    var startInfo = new ProcessStartInfo
    {
      FileName = runtimeConfig.Runtime,
      UseShellExecute = false,
      RedirectStandardInput = false,
      RedirectStandardOutput = false,
      RedirectStandardError = false,
      CreateNoWindow = false
    };

    foreach (var arg in args)
      startInfo.ArgumentList.Add(arg);

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      Console.Error.WriteLine($"❌ Failed to start {runtimeConfig.RuntimeFlavor}. Is it installed and in PATH?");
      return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
  }

  /// <summary>
  /// Runs container runtime interactively with the given arguments, setting terminal title.
  /// </summary>
  public static int RunInteractive(ContainerRuntimeConfig runtimeConfig, IEnumerable<string> args, string? terminalTitle = null)
  {
    // Set terminal title if provided
    if (!string.IsNullOrEmpty(terminalTitle))
    {
      Console.Write($"\x1b]0;{terminalTitle}\x07");
    }

    try
    {
      return Run(runtimeConfig, args);
    }
    finally
    {
      // Reset terminal title
      if (!string.IsNullOrEmpty(terminalTitle))
      {
        Console.Write("\x1b]0;\x07");
      }
    }
  }

  /// <summary>
  /// Pulls a container image with progress output.
  /// </summary>
  public static bool PullImage(ContainerRuntimeConfig runtimeConfig, string imageName)
  {
    DebugLogger.Log($"PullImage called for: {imageName}");
    Console.WriteLine($"📥 Pulling image: {imageName}");

    var startInfo = new ProcessStartInfo
    {
      FileName = runtimeConfig.Runtime,
      UseShellExecute = false
    };
    startInfo.ArgumentList.Add("pull");
    startInfo.ArgumentList.Add(imageName);

    DebugLogger.Log($"Starting {runtimeConfig.Runtime} process: {runtimeConfig.Runtime} pull {imageName}");
    using var process = Process.Start(startInfo);
    if (process is null)
    {
      DebugLogger.Log($"Failed to start {runtimeConfig.Runtime} process");
      Console.WriteLine($"❌ Failed to start {runtimeConfig.RuntimeFlavor}");
      return false;
    }

    DebugLogger.Log($"{runtimeConfig.RuntimeFlavor} process started with PID: {process.Id}");
    DebugLogger.Log($"Waiting for {runtimeConfig.Runtime} process to exit...");
    process.WaitForExit();
    DebugLogger.Log($"{runtimeConfig.RuntimeFlavor} process exited with code: {process.ExitCode}");

    if (process.ExitCode == 0)
    {
      Console.WriteLine("✅ Image pulled successfully");
      return true;
    }

    Console.WriteLine("❌ Failed to pull image");
    return false;
  }

  /// <summary>
  /// Cleans up old copilot_here images older than 7 days.
  /// </summary>
  public static void CleanupOldImages(ContainerRuntimeConfig runtimeConfig, string keepImageName)
  {
    Console.WriteLine("🧹 Cleaning up old images (older than 7 days)...");

    try
    {
      // Get list of all copilot_here images
      var startInfo = new ProcessStartInfo
      {
        FileName = runtimeConfig.Runtime,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("images");
      startInfo.ArgumentList.Add("--no-trunc");
      startInfo.ArgumentList.Add(ImagePrefix);
      startInfo.ArgumentList.Add("--format");
      startInfo.ArgumentList.Add("{{.ID}}|{{.Repository}}:{{.Tag}}|{{.CreatedAt}}");

      using var listProcess = Process.Start(startInfo);
      if (listProcess is null) return;

      var output = listProcess.StandardOutput.ReadToEnd();
      listProcess.WaitForExit();

      if (string.IsNullOrWhiteSpace(output))
      {
        Console.WriteLine("  ✓ No images to clean up");
        return;
      }

      // Get the ID of the image to keep
      var keepImageId = GetImageId(runtimeConfig, keepImageName);
      var cutoffDate = DateTime.Now.AddDays(-7);
      var removedCount = 0;

      var imageLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('|'))
        .Where(parts => parts.Length >= 3)
        .Select(parts => new { ImageId = parts[0], ImageName = parts[1], CreatedAt = parts[2] })
        .Where(img => img.ImageId != keepImageId);

      foreach (var img in imageLines)
      {
        // Try to parse the date and check if older than 7 days
        if (TryParseDockerDate(img.CreatedAt, out var imageDate) && imageDate < cutoffDate)
        {
          if (RemoveImage(runtimeConfig, img.ImageId))
          {
            Console.WriteLine($"  🗑️  Removed: {img.ImageName}");
            removedCount++;
          }
        }
      }

      if (removedCount == 0)
        Console.WriteLine("  ✓ No old images to clean up");
      else
        Console.WriteLine($"  ✓ Cleaned up {removedCount} old image(s)");
    }
    catch
    {
      // Silently ignore cleanup errors
    }
  }

  private static string? GetImageId(ContainerRuntimeConfig runtimeConfig, string imageName)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = runtimeConfig.Runtime,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("inspect");
      startInfo.ArgumentList.Add("--format");
      startInfo.ArgumentList.Add("{{.Id}}");
      startInfo.ArgumentList.Add(imageName);

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var id = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();
      return process.ExitCode == 0 ? id : null;
    }
    catch
    {
      return null;
    }
  }

  private static bool RemoveImage(ContainerRuntimeConfig runtimeConfig, string imageId)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = runtimeConfig.Runtime,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };
      startInfo.ArgumentList.Add("rmi");
      startInfo.ArgumentList.Add("-f");
      startInfo.ArgumentList.Add(imageId);

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

  private static bool TryParseDockerDate(string dateStr, out DateTime result)
  {
    result = default;
    try
    {
      // Docker date format: "2025-01-28 12:34:56 +0000 UTC"
      var parts = dateStr.Split(' ');
      if (parts.Length >= 2 && DateTime.TryParse($"{parts[0]} {parts[1]}", out result))
      {
        return true;
      }
    }
    catch
    {
      // Date parsing failed - not critical, just skip this image
    }
    return false;
  }

  /// <summary>
  /// Runs a container command and captures stdout/stderr output.
  /// Returns (exitCode, stdout, stderr).
  /// </summary>
  public static (int exitCode, string stdout, string stderr) RunAndCapture(ContainerRuntimeConfig runtimeConfig, IEnumerable<string> args)
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = runtimeConfig.Runtime,
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };

    foreach (var arg in args)
      startInfo.ArgumentList.Add(arg);

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      return (1, "", $"Failed to start {runtimeConfig.RuntimeFlavor}");
    }

    var stdout = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();

    return (process.ExitCode, stdout, stderr);
  }
}
