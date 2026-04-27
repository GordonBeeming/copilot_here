using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CopilotHere.Commands.Airlock;
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
  internal static string? GetEmbeddedTemplate()
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
  /// Runs the CLI tool in Airlock mode with a proxy container.
  /// When <paramref name="broker"/> is non-null, the workload container is wired
  /// to talk to the host-side Docker socket broker for --dind support.
  /// </summary>
  public static int Run(
    ContainerRuntimeConfig runtimeConfig,
    AppContext ctx,
    string imageTag,
    bool isYolo,
    List<MountEntry> mounts,
    List<string> toolArgs,
    DockerSocketBroker? broker = null)
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

    var appImage = ContainerRunner.GetImageName(imageTag);
    // COPILOT_HERE_PROXY_IMAGE lets CI smoke tests point at an ephemeral
    // proxy-st:<sha> tag that was just built in a previous job. Falls back
    // to the canonical tag for normal use.
    var proxyImage = Environment.GetEnvironmentVariable("COPILOT_HERE_PROXY_IMAGE")
      is { Length: > 0 } overrideImage
      ? overrideImage
      : $"{ImagePrefix}:proxy";

    Console.WriteLine("🛡️  Starting in Airlock mode...");
    Console.WriteLine($"   App image: {appImage}");
    Console.WriteLine($"   Proxy image: {proxyImage}");
    Console.WriteLine($"   Network config: {rulesPath}");
    if (externalNetwork != runtimeConfig.DefaultNetworkName)
      Console.WriteLine($"   External network: {externalNetwork}");

    // Generate session ID for unique naming
    var sessionId = GenerateSessionId();
    var projectName = $"{SystemInfo.GetCurrentDirectoryName()}-{sessionId}".ToLowerInvariant();

    // Tell the broker which compose-network to inject as NetworkMode for any
    // siblings the workload spawns. Docker prefixes user-defined networks
    // with the compose project name, so the airlock network's real name is
    // "{projectName}_airlock". With this set, body inspection in
    // DockerSocketBroker will route Testcontainers / docker-run children onto
    // the same internal-only network as the workload, keeping the airlock
    // boundary intact and making the children reachable by Docker DNS.
    if (broker is not null)
    {
      broker.SiblingNetworkName = $"{projectName}_airlock";
    }

    // Get compose template from embedded resource
    var templateContent = GetEmbeddedTemplate();
    if (templateContent is null)
    {
      Console.WriteLine("❌ Failed to load compose template");
      return 1;
    }

    // Process network config with placeholders. The workload no longer
    // needs host.docker.internal in the airlock allowlist: it talks to
    // tcp://proxy:2375 directly, and the proxy container forwards to the
    // host broker via its own external network leg (see proxy-entrypoint.sh).
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
      processedConfigPath, externalNetwork, appFlags, mounts, toolArgs, isYolo, broker);

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
      if (!StartProxy(runtimeConfig, composeFile, projectName, ctx))
      {
        Console.WriteLine("❌ Failed to start proxy container");
        return 1;
      }

      // Run app interactively
      var exitCode = RunApp(runtimeConfig, composeFile, projectName, ctx);

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

  /// <summary>
  /// Gets the embedded default airlock rules from assembly resources.
  /// </summary>
  private static NetworkConfig? GetDefaultRules()
  {
    try
    {
      var assembly = Assembly.GetExecutingAssembly();
      var resourceName = "CopilotHere.Resources.github-copilot-default-airlock-rules.json";
      
      using var stream = assembly.GetManifestResourceStream(resourceName);
      if (stream is null) return null;
      
      using var reader = new StreamReader(stream);
      var json = reader.ReadToEnd();
      return System.Text.Json.JsonSerializer.Deserialize(json, Commands.Airlock.NetworkConfigJsonContext.Default.NetworkConfig);
    }
    catch
    {
      return null;
    }
  }

  private static string? ProcessNetworkConfig(string rulesPath, AppPaths paths)
  {
    try
    {
      // Load user's config
      var userConfig = Commands.Airlock.AirlockConfig.ReadNetworkConfig(rulesPath);
      if (userConfig is null)
        return null;

      // If inherit_default_rules is true, merge with default rules
      if (userConfig.InheritDefaultRules)
      {
        var defaultRules = GetDefaultRules();
        if (defaultRules is not null)
        {
          // Add default rules that don't conflict with user rules
          // Priority: user rules > default rules (user rules for same host override)
          var userHosts = new HashSet<string>(userConfig.AllowedRules.Select(r => r.Host), StringComparer.OrdinalIgnoreCase);

          foreach (var defaultRule in defaultRules.AllowedRules)
          {
            if (!userHosts.Contains(defaultRule.Host))
            {
              userConfig.AllowedRules.Add(defaultRule);
            }
          }
        }
      }

      // Get GitHub info for placeholder replacement
      var gitInfo = GitInfo.GetGitHubInfo();
      var owner = gitInfo?.Owner ?? "";
      var repo = gitInfo?.Repo ?? "";

      // Serialize to JSON
      var json = System.Text.Json.JsonSerializer.Serialize(userConfig, Commands.Airlock.NetworkConfigJsonContext.Default.NetworkConfig);

      // Replace placeholders in the JSON
      json = json.Replace("{{GITHUB_OWNER}}", owner);
      json = json.Replace("{{GITHUB_REPO}}", repo);

      // Write to temp file in config directory (Docker needs access)
      var tempDir = Path.Combine(paths.GlobalConfigPath, "tmp");
      Directory.CreateDirectory(tempDir);

      var tempFile = Path.Combine(tempDir, $"network-{DateTime.UtcNow.Ticks}.json");
      File.WriteAllText(tempFile, json);

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

  internal static string? GenerateComposeFile(
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
    List<string> toolArgs,
    bool isYolo,
    DockerSocketBroker? broker)
  {
    try
    {
      // Build extra mounts string
      var extraMounts = new StringBuilder();
      foreach (var mount in mounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        var containerPath = mount.GetContainerPath(ctx.Paths.UserHome);
        var resolvedPath = mount.ResolveHostPath(ctx.Paths.UserHome);
        var composePath = ConvertToComposePath(resolvedPath);
        extraMounts.AppendLine($"      - {composePath}:{containerPath}:{mode}");
      }

      // Build Docker broker substitutions for DinD on the airlock network.
      //
      // Two transport flavours depending on whether the host broker listens
      // on a Unix socket (Linux) or TCP loopback (macOS / Windows):
      //
      //   * Linux native: bind-mount the host-side UDS into the workload
      //     container at /var/run/docker.sock. Works on `internal: true`
      //     networks because UDS doesn't use IP routing at all — the socket
      //     file is local to the container's filesystem view.
      //
      //   * macOS / Windows (TCP loopback): the workload container can't
      //     reach the host gateway directly because the airlock network is
      //     internal-only. The airlock proxy container, however, IS already
      //     dual-homed on the airlock network AND the external network. So
      //     we extend the proxy image with socat (see proxy-entrypoint.sh)
      //     and ask it to forward TCP from inside the airlock network to
      //     the host's broker. Workload sets DOCKER_HOST=tcp://proxy:2375.
      //     The host broker still enforces every Docker API rule — this is
      //     just a layer-4 hop, no inspection.
      //
      // Keeping the bridge inside the proxy container (instead of a separate
      // sidecar service) matches the architectural rule that airlock-only
      // features should live in the airlock proxy image.
      //
      // All buffers below are empty when --dind is off.
      var brokerMount = new StringBuilder();
      var brokerEnv = new StringBuilder();
      var brokerExtraHosts = new StringBuilder();
      var proxyBrokerEnv = new StringBuilder();
      var proxyBrokerExtraHosts = new StringBuilder();
      if (broker is not null)
      {
        if (broker.UnixSocketPath is not null)
        {
          brokerMount.Append($"      - {broker.UnixSocketPath}:/var/run/docker.sock");
          brokerEnv.AppendLine("      - DOCKER_HOST=unix:///var/run/docker.sock");
        }
        else if (broker.BoundTcpEndpoint is not null)
        {
          var port = broker.BoundTcpEndpoint.Port;
          brokerEnv.AppendLine("      - DOCKER_HOST=tcp://proxy:2375");
          // Bypass the airlock HTTP proxy for the daemon connection. The
          // docker CLI itself doesn't apply HTTP_PROXY to the daemon socket,
          // but Docker.DotNet's ManagedHandler (Testcontainers .NET) might,
          // and the airlock proxy would reject `proxy:2375` outright since
          // it isn't in the host allowlist.
          brokerEnv.AppendLine("      - NO_PROXY=proxy,localhost,127.0.0.1");
          brokerEnv.AppendLine("      - no_proxy=proxy,localhost,127.0.0.1");
          // Tell the proxy container to spin up its socat bridge pointed at
          // the host broker, and give it a route to the host gateway.
          proxyBrokerEnv.AppendLine("    environment:");
          proxyBrokerEnv.Append($"      - BROKER_BRIDGE_TARGET=host.docker.internal:{port}");
          proxyBrokerExtraHosts.AppendLine("    extra_hosts:");
          proxyBrokerExtraHosts.Append("      - \"host.docker.internal:host-gateway\"");
        }
        brokerEnv.Append("      - TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal");
        brokerExtraHosts.Append("    extra_hosts:\n      - \"host.docker.internal:host-gateway\"");
      }

      // Build logs mount if logging enabled
      var logsMount = "";
      var rulesContent = ctx.AirlockConfig.RulesPath is not null
        ? File.ReadAllText(ctx.AirlockConfig.RulesPath)
        : "";
      if (rulesContent.Contains("\"enable_logging\": true") || rulesContent.Contains("\"mode\": \"monitor\""))
      {
        var logsDir = Path.Combine(ctx.Paths.LocalConfigPath, "logs");
        var composeLogsPath = ConvertToComposePath(logsDir);
        logsMount = $"      - {composeLogsPath}:/logs";
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

      // Build tool command - already built by the caller via ICliTool.BuildCommand()
      // The toolArgs already contains the complete command including:
      // - Tool name (e.g., "copilot", "bash")
      // - YOLO flags if applicable (e.g., "--allow-all-tools", "--allow-all-paths")
      // - Model if specified
      // - User arguments
      // - Interactive flag (e.g., "--banner") if applicable
      
      // Convert command to JSON array format for docker-compose
      var toolCmd = new StringBuilder("[");
      for (int i = 0; i < toolArgs.Count; i++)
      {
        if (i > 0) toolCmd.Append(", ");
        var arg = toolArgs[i].Replace("\"", "\\\"");
        toolCmd.Append($"\"{arg}\"");
      }
      toolCmd.Append(']');

      // Build auth environment variables from the active tool's auth provider
      var authEnvVars = new StringBuilder();
      foreach (var (key, value) in ctx.ActiveTool.GetAuthProvider().GetEnvironmentVars())
      {
        authEnvVars.AppendLine($"      - {key}=${{{key}}}");
      }

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
        .Replace("{{WORK_DIR}}", ConvertToComposePath(ctx.Paths.CurrentDirectory))
        .Replace("{{CONTAINER_WORK_DIR}}", ctx.Paths.ContainerWorkDir)
        .Replace("{{TOOL_CONFIG}}", ConvertToComposePath(ctx.ActiveTool.GetHostConfigPath(ctx.Paths)))
        .Replace("{{TOOL_CONFIG_CONTAINER_PATH}}", ctx.ActiveTool.GetContainerConfigPath())
        .Replace("{{NETWORK_CONFIG}}", ConvertToComposePath(processedConfigPath))
        .Replace("{{PUID}}", ctx.Environment.UserId.ToString())
        .Replace("{{PGID}}", ctx.Environment.GroupId.ToString())
        .Replace("{{SESSION_INFO}}", sessionInfo)
        .Replace("{{TOOL_ARGS}}", toolCmd.ToString());

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
        else if (lines[i].Contains("{{AUTH_ENV_VARS}}"))
        {
          if (authEnvVars.Length > 0)
            lines[i] = authEnvVars.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{DOCKER_BROKER_MOUNT}}"))
        {
          if (brokerMount.Length > 0)
            lines[i] = brokerMount.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{DOCKER_BROKER_ENV}}"))
        {
          if (brokerEnv.Length > 0)
            lines[i] = brokerEnv.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{DOCKER_BROKER_EXTRA_HOSTS}}"))
        {
          if (brokerExtraHosts.Length > 0)
            lines[i] = brokerExtraHosts.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{PROXY_BROKER_ENV}}"))
        {
          if (proxyBrokerEnv.Length > 0)
            lines[i] = proxyBrokerEnv.ToString().TrimEnd();
          else
            lines.RemoveAt(i);
        }
        else if (lines[i].Contains("{{PROXY_BROKER_EXTRA_HOSTS}}"))
        {
          if (proxyBrokerExtraHosts.Length > 0)
            lines[i] = proxyBrokerExtraHosts.ToString().TrimEnd();
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

  private static bool StartProxy(ContainerRuntimeConfig runtimeConfig, string composeFile, string projectName, AppContext ctx)
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

      // Get auth environment variables from the active tool's auth provider
      var authEnvVars = ctx.ActiveTool.GetAuthProvider().GetEnvironmentVars();
      foreach (var (key, value) in authEnvVars)
      {
        startInfo.EnvironmentVariables[key] = value;
      }
      
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

  private static int RunApp(ContainerRuntimeConfig runtimeConfig, string composeFile, string projectName, AppContext ctx)
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

    // Get auth environment variables from the active tool's auth provider
    var authEnvVars = ctx.ActiveTool.GetAuthProvider().GetEnvironmentVars();
    foreach (var (key, value) in authEnvVars)
    {
      startInfo.EnvironmentVariables[key] = value;
    }
    
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
  /// Converts a host path to a Docker Compose-compatible format.
  /// Compose passes the literal string to the daemon, so on Windows we keep the native
  /// drive letter (C:\foo -> C:/foo). The /c/foo form used by docker run -v is parsed by
  /// the CLI but treated as a Linux absolute path when it appears in a compose YAML —
  /// the daemon then fails with "mkdir /c: permission denied" (see #105).
  /// On Unix: /path -> /path (no change).
  /// </summary>
  internal static string ConvertToComposePath(string path)
  {
    return path.Replace("\\", "/");
  }
}
