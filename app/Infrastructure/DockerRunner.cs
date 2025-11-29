using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles Docker process execution.
/// </summary>
public static class DockerRunner
{
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
}
