using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CopilotHere.Commands.Mounts;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Manages Airlock proxy mode using Docker Compose.
/// </summary>
public static class AirlockRunner
{
  private const string TemplateUrl = "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template";
  private const string ImagePrefix = "ghcr.io/gordonbeeming/copilot_here";

  /// <summary>
  /// Runs the Copilot CLI in Airlock mode with a proxy container.
  /// </summary>
  public static int Run(
    AppContext ctx,
    string imageTag,
    bool isYolo,
    List<MountEntry> mounts,
    List<string> copilotArgs)
  {
    var rulesPath = ctx.AirlockConfig.RulesPath;
    if (string.IsNullOrEmpty(rulesPath) || !File.Exists(rulesPath))
    {
      Console.WriteLine("❌ No Airlock rules file found. Create one with --enable-airlock");
      return 1;
    }

    // Cleanup orphaned networks/containers first
    CleanupOrphanedResources();

    var appImage = $"{ImagePrefix}:{imageTag}";
    var proxyImage = $"{ImagePrefix}:proxy";

    Console.WriteLine("🛡️  Starting in Airlock mode...");
    Console.WriteLine($"   App image: {appImage}");
    Console.WriteLine($"   Proxy image: {proxyImage}");
    Console.WriteLine($"   Network config: {rulesPath}");

    // Generate session ID for unique naming
    var sessionId = GenerateSessionId();
    var projectName = $"{SystemInfo.GetCurrentDirectoryName()}-{sessionId}".ToLowerInvariant();

    // Get or download compose template
    var templatePath = GetTemplatePath(ctx.Paths);
    if (!File.Exists(templatePath))
    {
      Console.WriteLine("📥 Downloading compose template...");
      if (!DownloadFile(TemplateUrl, templatePath))
      {
        Console.WriteLine("❌ Failed to download compose template");
        return 1;
      }
    }

    // Process network config with placeholders
    var processedConfigPath = ProcessNetworkConfig(rulesPath, ctx.Paths);
    if (processedConfigPath is null)
    {
      Console.WriteLine("❌ Failed to process network config");
      return 1;
    }

    // Setup logs directory if logging enabled
    SetupLogsDirectory(ctx.Paths, rulesPath);

    // Generate compose file
    var composeFile = GenerateComposeFile(
      ctx, templatePath, projectName, appImage, proxyImage,
      processedConfigPath, mounts, copilotArgs, isYolo);

    if (composeFile is null)
    {
      Console.WriteLine("❌ Failed to generate compose file");
      return 1;
    }

    try
    {
      // Set terminal title
      var titleEmoji = isYolo ? "🤖⚡️" : "🤖";
      var dirName = SystemInfo.GetCurrentDirectoryName();
      Console.Write($"\x1b]0;{titleEmoji} {dirName} 🛡️\x07");

      Console.WriteLine();

      // Start proxy in background
      if (!StartProxy(composeFile, projectName, ctx.Environment.GitHubToken))
      {
        Console.WriteLine("❌ Failed to start proxy container");
        return 1;
      }

      // Run app interactively
      var exitCode = RunApp(composeFile, projectName, ctx.Environment.GitHubToken);

      return exitCode;
    }
    finally
    {
      // Reset terminal title
      Console.Write("\x1b]0;\x07");

      // Cleanup
      Console.WriteLine();
      Console.WriteLine("🧹 Cleaning up airlock...");
      CleanupSession(projectName, composeFile, processedConfigPath);
    }
  }

  private static string GenerateSessionId()
  {
    var input = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
  }

  private static string GetTemplatePath(AppPaths paths)
  {
    return Path.Combine(paths.GlobalConfigPath, "docker-compose.airlock.yml.template");
  }

  private static bool DownloadFile(string url, string targetPath)
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
      startInfo.ArgumentList.Add(targetPath);

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

  private static string? ProcessNetworkConfig(string rulesPath, AppPaths paths)
  {
    try
    {
      var content = File.ReadAllText(rulesPath);

      // Get GitHub info for placeholder replacement
      var gitInfo = GitInfo.GetGitHubInfo();
      var owner = gitInfo?.Owner ?? "";
      var repo = gitInfo?.Repo ?? "";

      // Replace placeholders
      content = content.Replace("{{GITHUB_OWNER}}", owner);
      content = content.Replace("{{GITHUB_REPO}}", repo);

      // Write to temp file in config directory (Docker needs access)
      var tempDir = Path.Combine(paths.GlobalConfigPath, "tmp");
      Directory.CreateDirectory(tempDir);

      var tempFile = Path.Combine(tempDir, $"network-{DateTime.UtcNow.Ticks}.json");
      File.WriteAllText(tempFile, content);

      return tempFile;
    }
    catch
    {
      return null;
    }
  }

  private static void SetupLogsDirectory(AppPaths paths, string rulesPath)
  {
    try
    {
      var content = File.ReadAllText(rulesPath);

      // Check if logging is enabled or monitor mode
      if (content.Contains("\"enable_logging\": true") || content.Contains("\"mode\": \"monitor\""))
      {
        var logsDir = Path.Combine(paths.LocalConfigPath, "logs");
        Directory.CreateDirectory(logsDir);

        var gitignorePath = Path.Combine(logsDir, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
          File.WriteAllText(gitignorePath, """
            # Ignore all log files - may contain sensitive information
            *
            !.gitignore
            """);
        }
      }
    }
    catch
    {
      // Non-fatal
    }
  }

  private static string? GenerateComposeFile(
    AppContext ctx,
    string templatePath,
    string projectName,
    string appImage,
    string proxyImage,
    string processedConfigPath,
    List<MountEntry> mounts,
    List<string> copilotArgs,
    bool isYolo)
  {
    try
    {
      var template = File.ReadAllText(templatePath);

      // Build extra mounts string
      var extraMounts = new StringBuilder();
      foreach (var mount in mounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        var containerPath = mount.GetContainerPath(ctx.Paths.UserHome);
        var resolvedPath = mount.ResolvePath(ctx.Paths.UserHome);
        extraMounts.AppendLine($"      - {resolvedPath}:{containerPath}:{mode}");
      }

      // Build logs mount if logging enabled
      var logsMount = "";
      var rulesContent = ctx.AirlockConfig.RulesPath is not null
        ? File.ReadAllText(ctx.AirlockConfig.RulesPath)
        : "";
      if (rulesContent.Contains("\"enable_logging\": true") || rulesContent.Contains("\"mode\": \"monitor\""))
      {
        var logsDir = Path.Combine(ctx.Paths.LocalConfigPath, "logs");
        logsMount = $"      - {logsDir}:/logs";
      }

      // Build copilot command
      var copilotCmd = new StringBuilder("[\"copilot\"");
      if (isYolo)
      {
        copilotCmd.Append(", \"--allow-all-tools\", \"--allow-all-paths\"");
        copilotCmd.Append($", \"--add-dir\", \"{ctx.Paths.ContainerWorkDir}\"");
      }

      if (copilotArgs.Count <= 1 || (copilotArgs.Count <= 3 && isYolo))
      {
        copilotCmd.Append(", \"--banner\"");
      }
      else
      {
        // Skip "copilot" at index 0
        for (var i = 1; i < copilotArgs.Count; i++)
        {
          var arg = copilotArgs[i].Replace("\"", "\\\"");
          copilotCmd.Append($", \"{arg}\"");
        }
      }
      copilotCmd.Append(']');

      // Do substitutions
      var result = template
        .Replace("{{PROJECT_NAME}}", projectName)
        .Replace("{{APP_IMAGE}}", appImage)
        .Replace("{{PROXY_IMAGE}}", proxyImage)
        .Replace("{{WORK_DIR}}", ctx.Paths.CurrentDirectory)
        .Replace("{{CONTAINER_WORK_DIR}}", ctx.Paths.ContainerWorkDir)
        .Replace("{{COPILOT_CONFIG}}", ctx.Paths.CopilotConfigPath)
        .Replace("{{NETWORK_CONFIG}}", processedConfigPath)
        .Replace("{{PUID}}", ctx.Environment.UserId.ToString())
        .Replace("{{PGID}}", ctx.Environment.GroupId.ToString())
        .Replace("{{COPILOT_ARGS}}", copilotCmd.ToString());

      // Handle multiline placeholders
      var lines = result.Split('\n').ToList();
      for (var i = lines.Count - 1; i >= 0; i--)
      {
        if (lines[i].Contains("{{EXTRA_MOUNTS}}"))
        {
          if (extraMounts.Length > 0)
            lines[i] = extraMounts.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{LOGS_MOUNT}}"))
        {
          if (!string.IsNullOrEmpty(logsMount))
            lines[i] = logsMount;
          else
            lines.RemoveAt(i);
        }
      }

      result = string.Join('\n', lines);

      // Write to temp file
      var tempFile = Path.GetTempFileName();
      File.WriteAllText(tempFile, result);
      return tempFile;
    }
    catch
    {
      return null;
    }
  }

  private static bool StartProxy(string composeFile, string projectName, string? token)
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

      startInfo.EnvironmentVariables["GITHUB_TOKEN"] = token ?? "";
      startInfo.EnvironmentVariables["COMPOSE_MENU"] = "0";

      startInfo.ArgumentList.Add("compose");
      startInfo.ArgumentList.Add("-f");
      startInfo.ArgumentList.Add(composeFile);
      startInfo.ArgumentList.Add("-p");
      startInfo.ArgumentList.Add(projectName);
      startInfo.ArgumentList.Add("up");
      startInfo.ArgumentList.Add("-d");
      startInfo.ArgumentList.Add("proxy");

      using var process = Process.Start(startInfo);
      if (process is null) return false;

      var stderr = process.StandardError.ReadToEnd();
      process.WaitForExit();

      if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
      {
        Console.WriteLine($"   Error: {stderr.Trim()}");
      }

      return process.ExitCode == 0;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"   Error: {ex.Message}");
      return false;
    }
  }

  private static int RunApp(string composeFile, string projectName, string? token)
  {
    // Let Docker Compose handle Ctrl+C
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

    startInfo.EnvironmentVariables["GITHUB_TOKEN"] = token ?? "";
    startInfo.EnvironmentVariables["COMPOSE_MENU"] = "0";

    startInfo.ArgumentList.Add("compose");
    startInfo.ArgumentList.Add("-f");
    startInfo.ArgumentList.Add(composeFile);
    startInfo.ArgumentList.Add("-p");
    startInfo.ArgumentList.Add(projectName);
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("-i");
    startInfo.ArgumentList.Add("--rm");
    startInfo.ArgumentList.Add("app");

    using var process = Process.Start(startInfo);
    if (process is null) return 1;

    process.WaitForExit();
    return process.ExitCode;
  }

  private static void CleanupSession(string projectName, string? composeFile, string? processedConfigPath)
  {
    try
    {
      // Stop and remove proxy container
      RunQuietCommand("docker", "stop", $"{projectName}-proxy");
      RunQuietCommand("docker", "rm", $"{projectName}-proxy");

      // Remove networks
      RunQuietCommand("docker", "network", "rm", $"{projectName}_airlock");
      RunQuietCommand("docker", "network", "rm", $"{projectName}_bridge");

      // Remove volume
      RunQuietCommand("docker", "volume", "rm", $"{projectName}_proxy-ca");

      // Delete temp files
      if (composeFile is not null && File.Exists(composeFile))
        File.Delete(composeFile);
      if (processedConfigPath is not null && File.Exists(processedConfigPath))
        File.Delete(processedConfigPath);
    }
    catch
    {
      // Best effort cleanup
    }
  }

  private static void CleanupOrphanedResources()
  {
    try
    {
      // Get running containers
      var runningOutput = RunCommand("docker", "ps", "--format", "{{.Names}}");
      var runningContainers = new HashSet<string>(
        runningOutput?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? []);

      // Get all containers including stopped
      var allOutput = RunCommand("docker", "ps", "-a", "--format", "{{.Names}}");
      var allContainers = allOutput?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];

      // Find and remove orphaned proxy containers
      var proxyContainers = allContainers.Where(c => c.EndsWith("-proxy"));
      foreach (var container in proxyContainers)
      {
        var prefix = container[..^6]; // Remove "-proxy"
        var hasRunningApp = runningContainers.Any(c =>
          c.StartsWith($"{prefix}-app", StringComparison.OrdinalIgnoreCase));

        if (!hasRunningApp)
        {
          RunQuietCommand("docker", "rm", "-f", container);
          Console.WriteLine($"  🗑️  Removed orphaned proxy: {container}");
        }
      }

      // Find and remove orphaned networks
      var networksOutput = RunCommand("docker", "network", "ls", "--format", "{{.Name}}");
      var networks = networksOutput?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];

      var copilotNetworks = networks.Where(n => n.EndsWith("_airlock") || n.EndsWith("_bridge"));
      foreach (var network in copilotNetworks)
      {
        // Check if network has any containers
        var inspectOutput = RunCommand("docker", "network", "inspect", network, "--format", "{{len .Containers}}");
        if (inspectOutput?.Trim() == "0")
        {
          RunQuietCommand("docker", "network", "rm", network);
          Console.WriteLine($"  🗑️  Removed orphaned network: {network}");
        }
      }
    }
    catch
    {
      // Best effort cleanup
    }
  }

  private static string? RunCommand(params string[] args)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = args[0],
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      for (var i = 1; i < args.Length; i++)
        startInfo.ArgumentList.Add(args[i]);

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var output = process.StandardOutput.ReadToEnd();
      process.WaitForExit();

      return process.ExitCode == 0 ? output : null;
    }
    catch
    {
      return null;
    }
  }

  private static void RunQuietCommand(params string[] args)
  {
    try
    {
      var startInfo = new ProcessStartInfo
      {
        FileName = args[0],
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
      };

      for (var i = 1; i < args.Length; i++)
        startInfo.ArgumentList.Add(args[i]);

      using var process = Process.Start(startInfo);
      process?.WaitForExit();
    }
    catch
    {
      // Ignore
    }
  }
}
