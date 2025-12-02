using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles Docker process execution.
/// </summary>
public static class DockerRunner
{
  private const string ImagePrefix = "ghcr.io/gordonbeeming/copilot_here";

  /// <summary>
  /// Gets the full image name for a given tag.
  /// </summary>
  public static string GetImageName(string tag) => $"{ImagePrefix}:{tag}";

  /// <summary>
  /// Runs a Docker command with the given arguments.
  /// </summary>
  public static int Run(IEnumerable<string> args)
  {
    // Intercept Ctrl+C to let Docker handle it
    Console.CancelKeyPress += (_, e) => e.Cancel = true;

    var startInfo = new ProcessStartInfo
    {
      FileName = "docker",
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
      Console.Error.WriteLine("❌ Failed to start Docker. Is it installed and in PATH?");
      return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
  }

  /// <summary>
  /// Runs Docker interactively with the given arguments, setting terminal title.
  /// </summary>
  public static int RunInteractive(IEnumerable<string> args, string? terminalTitle = null)
  {
    // Set terminal title if provided
    if (!string.IsNullOrEmpty(terminalTitle))
    {
      Console.Write($"\x1b]0;{terminalTitle}\x07");
    }

    try
    {
      return Run(args);
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
  /// Pulls a Docker image with a spinner indicator.
  /// </summary>
  public static bool PullImage(string imageName)
  {
    Console.Write($"📥 Pulling image: {imageName}... ");

    var startInfo = new ProcessStartInfo
    {
      FileName = "docker",
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("pull");
    startInfo.ArgumentList.Add(imageName);

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      Console.WriteLine("❌");
      return false;
    }

    // Simple spinner while waiting
    var spinChars = new[] { '|', '/', '-', '\\' };
    var spinIndex = 0;

    while (!process.HasExited)
    {
      Console.Write(spinChars[spinIndex]);
      Console.Write('\b');
      spinIndex = (spinIndex + 1) % spinChars.Length;
      Thread.Sleep(100);
    }

    process.WaitForExit();

    if (process.ExitCode == 0)
    {
      Console.WriteLine("✅");
      return true;
    }

    Console.WriteLine("❌");
    return false;
  }

  /// <summary>
  /// Cleans up old copilot_here images older than 7 days.
  /// </summary>
  public static void CleanupOldImages(string keepImageName)
  {
    Console.WriteLine("🧹 Cleaning up old images (older than 7 days)...");

    try
    {
      // Get list of all copilot_here images
      var startInfo = new ProcessStartInfo
      {
        FileName = "docker",
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
      var keepImageId = GetImageId(keepImageName);
      var cutoffDate = DateTime.Now.AddDays(-7);
      var removedCount = 0;

      foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
      {
        var parts = line.Split('|');
        if (parts.Length < 3) continue;

        var imageId = parts[0];
        var imageName = parts[1];
        var createdAt = parts[2];

        // Skip the image we want to keep
        if (imageId == keepImageId) continue;

        // Try to parse the date and check if older than 7 days
        if (TryParseDockerDate(createdAt, out var imageDate) && imageDate < cutoffDate)
        {
          if (RemoveImage(imageId))
          {
            Console.WriteLine($"  🗑️  Removed: {imageName}");
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

  private static string? GetImageId(string imageName)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = "docker",
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

  private static bool RemoveImage(string imageId)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = "docker",
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
    catch { }
    return false;
  }
}
