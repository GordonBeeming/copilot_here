using System.Diagnostics;
using System.Runtime.InteropServices;
using CopilotHere.Models;

class Program
{
  static async Task<int> Main(string[] args)
  {
    // ⚡ SPEED: minimal allocations
    var config = ParseArguments(args);

    // 2. Setup Context
    var currentDir = Directory.GetCurrentDirectory();
    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // Calculate container mapping (map host home to /home/appuser)
    string containerWorkDir;
    if (currentDir.StartsWith(userHome))
    {
      var relativePath = Path.GetRelativePath(userHome, currentDir).Replace("\\", "/");
      containerWorkDir = $"/home/appuser/{relativePath}";
    }
    else
    {
      // Fallback for paths outside user home (e.g. /tmp)
      containerWorkDir = currentDir;
    }

    // 3. Construct Docker Arguments
    // We use a List<string> to let Process.Start handle quoting/escaping automatically
    var dockerArgs = new List<string>
        {
            "run",
            "--rm",              // Cleanup container after exit
            "-it",               // Interactive TTY
            "-v", $"{currentDir}:{containerWorkDir}", // Mount current dir
            "-w", containerWorkDir,                   // Set work dir
            "-v", $"{Path.Combine(userHome, ".config/copilot-cli-docker")}:/home/appuser/.copilot", // Config persistence
            "-e", $"PUID={GetUserId()}", // Linux permissions mapping
            "-e", $"PGID={GetGroupId()}",
            "-e", $"GITHUB_TOKEN={GetGitHubToken()}"
        };

    // Add User Mounts
    foreach (var mount in config.ReadOnlyMounts)
      AddMount(dockerArgs, mount, "ro", userHome);

    foreach (var mount in config.ReadWriteMounts)
      AddMount(dockerArgs, mount, "rw", userHome);

    // Add Image
    dockerArgs.Add($"ghcr.io/gordonbeeming/copilot_here:{config.ImageTag}");

    // Add Entrypoint Command (The actual Copilot CLI command)
    dockerArgs.Add("copilot");

    // Pass through all remaining arguments to the inner tool
    dockerArgs.AddRange(config.PassthroughArgs);

    // 4. Execution
    Console.WriteLine($"🚀 Starting copilot_here ({config.ImageTag})...");

    if (!config.SkipPull)
    {
      // TODO: Implement the spinner logic here or in a helper
      // For now, simple fire-and-forget pull or synchronous if critical
    }

    return RunDockerProcess(dockerArgs);
  }

  private static RunConfig ParseArguments(string[] args)
  {
    var config = new RunConfig();
    var i = 0;

    while (i < args.Length)
    {
      var arg = args[i];

      // If we hit a command that belongs to copilot (not us), stop parsing
      // strict flags. Everything else is passthrough.
      if (!arg.StartsWith("-"))
      {
        config.PassthroughArgs.Add(arg);
        i++;
        continue;
      }

      switch (arg)
      {
        case "--dotnet":
        case "-d":
          config.ImageTag = "dotnet";
          break;
        case "--no-cleanup":
          config.SkipCleanup = true;
          break;
        case "--no-pull":
        case "--skip-pull":
          config.SkipPull = true;
          break;
        case "--mount":
          if (i + 1 < args.Length) config.ReadOnlyMounts.Add(args[++i]);
          break;
        case "--mount-rw":
          if (i + 1 < args.Length) config.ReadWriteMounts.Add(args[++i]);
          break;
        // Handle version/help specific to wrapper here
        default:
          // Unknown flag? Assume it belongs to the inner tool (e.g. -p, --model)
          config.PassthroughArgs.Add(arg);
          break;
      }
      i++;
    }
    return config;
  }

  private static void AddMount(List<string> args, string path, string mode, string home)
  {
    // Simple resolution logic
    var hostPath = path.Replace("~", home);
    hostPath = Path.GetFullPath(hostPath); // Resolves ../ and relative paths

    // Container path logic (mirroring shell script)
    var containerPath = hostPath.StartsWith(home)
        ? $"/home/appuser/{Path.GetRelativePath(home, hostPath).Replace("\\", "/")}"
        : hostPath;

    args.Add("-v");
    args.Add($"{hostPath}:{containerPath}:{mode}");
  }

  private static int RunDockerProcess(List<string> args)
  {
    // 5. The Signal Trap
    // This is crucial. When user hits Ctrl+C, we want Docker to handle it, not us.
    // We cancel the C# event so the process stays alive until Docker exits.
    Console.CancelKeyPress += (_, e) => e.Cancel = true;

    var startInfo = new ProcessStartInfo
    {
      FileName = "docker",
      UseShellExecute = false, // Required to inherit handles
      RedirectStandardInput = false, // Pass STDIN directly
      RedirectStandardOutput = false, // Pass STDOUT directly
      RedirectStandardError = false,  // Pass STDERR directly
      CreateNoWindow = false
    };

    foreach (var arg in args) startInfo.ArgumentList.Add(arg);

    using var process = Process.Start(startInfo);
    if (process is null)
    {
      Console.Error.WriteLine("❌ Failed to start Docker. Is it installed and in PATH?");
      return 1;
    }

    process.WaitForExit();
    return process.ExitCode;
  }

  private static string GetGitHubToken()
  {
    try
    {
      var info = new ProcessStartInfo("gh", "auth token")
      {
        RedirectStandardOutput = true,
        UseShellExecute = false
      };
      using var p = Process.Start(info);
      p?.WaitForExit();
      return p?.StandardOutput.ReadToEnd().Trim() ?? "";
    }
    catch
    {
      Console.WriteLine("⚠️ Could not retrieve GitHub Token. Are you logged in?");
      return "";
    }
  }

  private static string GetUserId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "1000"; // Default for Windows Docker
                                                                            // For Linux/Mac, you'd strictly want the actual ID, but 1000 is a safe fallback for 90% of devs
                                                                            // If strictly needed, we can run `id -u` via Process similar to GetGitHubToken
    return "1000";
  }

  private static string GetGroupId()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "1000";
    return "1000";
  }
}