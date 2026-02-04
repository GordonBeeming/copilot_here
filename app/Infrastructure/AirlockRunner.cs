using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CopilotHere.Commands.Mounts;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Manages Airlock proxy mode using Docker Compose.
/// </summary>
public static class AirlockRunner
{
  private const string ImagePrefix = "ghcr.io/gordonbeeming/copilot_here";

  /// <summary>
  /// Gets the embedded compose template from assembly resources.
  /// </summary>
  private static string? GetEmbeddedTemplate()
  {
    try
    {
      var assembly = Assembly.GetExecutingAssembly();
      var resourceName = "CopilotHere.Resources.docker-compose.airlock.yml.template";
      
      using var stream = assembly.GetManifestResourceStream(resourceName);
      if (stream is null) return null;
      
      using var reader = new StreamReader(stream);
      return reader.ReadToEnd();
    }
    catch
    {
      return null;
    }
  }

  /// <summary>
  /// Runs the Copilot CLI in Airlock mode with a proxy container.
  /// </summary>
  public static int Run(
    ContainerRuntimeConfig runtimeConfig,
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
    CleanupOrphanedResources(runtimeConfig);

    // Parse sandbox flags
    var sandboxFlags = SandboxFlags.Parse();
    var externalNetwork = SandboxFlags.ExtractNetwork(sandboxFlags) ?? runtimeConfig.DefaultNetworkName;
    var appFlags = SandboxFlags.FilterNetworkFlags(sandboxFlags);

    if (sandboxFlags.Count > 0)
    {
      DebugLogger.Log($"SANDBOX_FLAGS detected: {sandboxFlags.Count} flags");
      if (externalNetwork != runtimeConfig.DefaultNetworkName)
        DebugLogger.Log($"Using external network: {externalNetwork}");
    }

    var appImage = $"{ImagePrefix}:{imageTag}";
    var proxyImage = $"{ImagePrefix}:proxy";

    Console.WriteLine("🛡️  Starting in Airlock mode...");
    Console.WriteLine($"   App image: {appImage}");
    Console.WriteLine($"   Proxy image: {proxyImage}");
    Console.WriteLine($"   Network config: {rulesPath}");
    if (externalNetwork != runtimeConfig.DefaultNetworkName)
      Console.WriteLine($"   External network: {externalNetwork}");

    // Generate session ID for unique naming
    var sessionId = GenerateSessionId();
    var projectName = $"{SystemInfo.GetCurrentDirectoryName()}-{sessionId}".ToLowerInvariant();

    // Get compose template from embedded resource
    var templateContent = GetEmbeddedTemplate();
    if (templateContent is null)
    {
      Console.WriteLine("❌ Failed to load compose template");
      return 1;
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
      runtimeConfig, ctx, templateContent, projectName, appImage, proxyImage,
      processedConfigPath, externalNetwork, appFlags, mounts, copilotArgs, isYolo);

    if (composeFile is null)
    {
      Console.WriteLine("❌ Failed to generate compose file");
      return 1;
    }

    // Debug: show compose file location for troubleshooting
    if (Environment.GetEnvironmentVariable("COPILOT_HERE_DEBUG") == "1")
    {
      var content = File.ReadAllText(composeFile);
      Console.WriteLine($"   Compose file: {composeFile}");
      Console.WriteLine($"   File length: {content.Length} chars");
      Console.WriteLine("   --- BEGIN COMPOSE FILE ---");
      Console.WriteLine(content);
      Console.WriteLine("   --- END COMPOSE FILE ---");
    }

    try
    {
      // Set terminal title
      var titleEmoji = isYolo ? "🤖⚡️" : "🤖";
      var dirName = SystemInfo.GetCurrentDirectoryName();
      Console.Write($"\x1b]0;{titleEmoji} {dirName} 🛡️\x07");

      Console.WriteLine();

      // Start proxy in background
      if (!StartProxy(runtimeConfig, composeFile, projectName, ctx.Environment.GitHubToken))
      {
        Console.WriteLine("❌ Failed to start proxy container");
        return 1;
      }

      // Run app interactively
      var exitCode = RunApp(runtimeConfig, composeFile, projectName, ctx.Environment.GitHubToken);

      return exitCode;
    }
    finally
    {
      // Reset terminal title
      Console.Write("\x1b]0;\x07");

      // Cleanup
      Console.WriteLine();
      Console.WriteLine("🧹 Cleaning up airlock...");
      CleanupSession(runtimeConfig, projectName, composeFile, processedConfigPath);
    }
  }

  private static string GenerateSessionId()
  {
    var input = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
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
    ContainerRuntimeConfig runtimeConfig,
    AppContext ctx,
    string template,
    string projectName,
    string appImage,
    string proxyImage,
    string processedConfigPath,
    string externalNetwork,
    List<string> appSandboxFlags,
    List<MountEntry> mounts,
    List<string> copilotArgs,
    bool isYolo)
  {
    try
    {
      // Build extra mounts string
      var extraMounts = new StringBuilder();
      foreach (var mount in mounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        var containerPath = mount.GetContainerPath(ctx.Paths.UserHome);
        var resolvedPath = mount.ResolvePath(ctx.Paths.UserHome);
        var dockerPath = ConvertToDockerPath(resolvedPath);
        extraMounts.AppendLine($"      - {dockerPath}:{containerPath}:{mode}");
      }

      // Build logs mount if logging enabled
      var logsMount = "";
      var rulesContent = ctx.AirlockConfig.RulesPath is not null
        ? File.ReadAllText(ctx.AirlockConfig.RulesPath)
        : "";
      if (rulesContent.Contains("\"enable_logging\": true") || rulesContent.Contains("\"mode\": \"monitor\""))
      {
        var logsDir = Path.Combine(ctx.Paths.LocalConfigPath, "logs");
        var dockerLogsPath = ConvertToDockerPath(logsDir);
        logsMount = $"      - {dockerLogsPath}:/logs";
      }

      // Convert app sandbox flags to YAML
      var extraFlags = SandboxFlags.ToComposeYaml(appSandboxFlags);

      // Build networks section
      string networksYaml;
      if (externalNetwork == runtimeConfig.DefaultNetworkName)
      {
        networksYaml = $@"networks:
  airlock:
    internal: true
  {runtimeConfig.DefaultNetworkName}:";
      }
      else
      {
        networksYaml = $@"networks:
  airlock:
    internal: true
  {externalNetwork}:
    external: true";
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

      // Generate session info JSON for Airlock mode
      var sessionInfo = SessionInfo.GenerateWithNetworkConfig(
        ctx, 
        appImage.Split(':')[1], // Extract tag from full image name
        appImage, 
        mounts, 
        isYolo, 
        processedConfigPath);

      // Do substitutions
      var result = template
        .Replace("{{NETWORKS}}", networksYaml)
        .Replace("{{EXTERNAL_NETWORK}}", externalNetwork)
        .Replace("{{PROJECT_NAME}}", projectName)
        .Replace("{{APP_IMAGE}}", appImage)
        .Replace("{{PROXY_IMAGE}}", proxyImage)
        .Replace("{{WORK_DIR}}", ConvertToDockerPath(ctx.Paths.CurrentDirectory))
        .Replace("{{CONTAINER_WORK_DIR}}", ctx.Paths.ContainerWorkDir)
        .Replace("{{COPILOT_CONFIG}}", ConvertToDockerPath(ctx.Paths.CopilotConfigPath))
        .Replace("{{NETWORK_CONFIG}}", ConvertToDockerPath(processedConfigPath))
        .Replace("{{PUID}}", ctx.Environment.UserId.ToString())
        .Replace("{{PGID}}", ctx.Environment.GroupId.ToString())
        .Replace("{{SESSION_INFO}}", sessionInfo)
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
        else if (lines[i].Contains("{{EXTRA_SANDBOX_FLAGS}}"))
        {
          if (!string.IsNullOrEmpty(extraFlags))
            lines[i] = extraFlags.TrimEnd();
          else
            lines.RemoveAt(i);
        }
      }

      result = string.Join('\n', lines);

      // Write to temp file with .yml extension (required for Docker Compose)
      var tempFile = Path.Combine(Path.GetTempPath(), $"copilot-airlock-{Guid.NewGuid():N}.yml");
      File.WriteAllText(tempFile, result);
      return tempFile;
    }
    catch
    {
      return null;
    }
  }

  private static bool StartProxy(ContainerRuntimeConfig runtimeConfig, string composeFile, string projectName, string? token)
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

      startInfo.EnvironmentVariables["GITHUB_TOKEN"] = token ?? "";
      startInfo.EnvironmentVariables["COMPOSE_MENU"] = "0";

      startInfo.ArgumentList.Add(runtimeConfig.ComposeCommand);
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

  private static int RunApp(ContainerRuntimeConfig runtimeConfig, string composeFile, string projectName, string? token)
  {
    // Let container runtime handle Ctrl+C
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

    startInfo.EnvironmentVariables["GITHUB_TOKEN"] = token ?? "";
    startInfo.EnvironmentVariables["COMPOSE_MENU"] = "0";

    startInfo.ArgumentList.Add(runtimeConfig.ComposeCommand);
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

  private static void CleanupSession(ContainerRuntimeConfig runtimeConfig, string projectName, string? composeFile, string? processedConfigPath)
  {
    try
    {
      // Stop and remove proxy container
      RunQuietCommand(runtimeConfig.Runtime, "stop", $"{projectName}-proxy");
      RunQuietCommand(runtimeConfig.Runtime, "rm", $"{projectName}-proxy");

      // Remove networks
      RunQuietCommand(runtimeConfig.Runtime, "network", "rm", $"{projectName}_airlock");
      RunQuietCommand(runtimeConfig.Runtime, "network", "rm", $"{projectName}_{runtimeConfig.DefaultNetworkName}");

      // Remove volume
      RunQuietCommand(runtimeConfig.Runtime, "volume", "rm", $"{projectName}_proxy-ca");

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

  private static void CleanupOrphanedResources(ContainerRuntimeConfig runtimeConfig)
  {
    try
    {
      // Get running containers
      var runningOutput = RunCommand(runtimeConfig.Runtime, "ps", "--format", "{{.Names}}");
      var runningContainers = new HashSet<string>(
        runningOutput?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? []);

      // Get all containers including stopped
      var allOutput = RunCommand(runtimeConfig.Runtime, "ps", "-a", "--format", "{{.Names}}");
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
          RunQuietCommand(runtimeConfig.Runtime, "rm", "-f", container);
          Console.WriteLine($"  🗑️  Removed orphaned proxy: {container}");
        }
      }

      // Find and remove orphaned networks
      var networksOutput = RunCommand(runtimeConfig.Runtime, "network", "ls", "--format", "{{.Name}}");
      var networks = networksOutput?.Split('\n', StringSplitOptions.RemoveEmptyEntries) ?? [];

      var copilotNetworks = networks.Where(n => n.EndsWith("_airlock") || n.EndsWith($"_{runtimeConfig.DefaultNetworkName}"));
      foreach (var network in copilotNetworks)
      {
        // Check if network has any containers
        var inspectOutput = RunCommand(runtimeConfig.Runtime, "network", "inspect", network, "--format", "{{len .Containers}}");
        if (inspectOutput?.Trim() == "0")
        {
          RunQuietCommand(runtimeConfig.Runtime, "network", "rm", network);
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

  /// <summary>
  /// Converts a Windows path to Docker-compatible format.
  /// On Windows: C:\path -> /c/path
  /// On Unix: /path -> /path (no change)
  /// </summary>
  private static string ConvertToDockerPath(string path)
  {
    // Convert backslashes to forward slashes
    var normalizedPath = path.Replace("\\", "/");
    
    // On Windows, convert drive letter paths (C:/ -> /c/)
    if (OperatingSystem.IsWindows() && 
        normalizedPath.Length >= 2 && 
        normalizedPath[1] == ':')
    {
      var driveLetter = char.ToLowerInvariant(normalizedPath[0]);
      var pathWithoutDrive = normalizedPath.Substring(2);
      return $"/{driveLetter}{pathWithoutDrive}";
    }
    
    return normalizedPath;
  }
}
