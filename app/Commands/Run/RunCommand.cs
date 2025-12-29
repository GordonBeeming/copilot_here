using System.CommandLine;
using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Run;

/// <summary>
/// Main command that runs the Copilot CLI in a Docker container.
/// This is the default command when no subcommand is specified.
/// </summary>
public sealed class RunCommand : ICommand
{
  private readonly bool _isYolo;

  // === IMAGE SELECTION OPTIONS ===
  private readonly Option<bool> _dotnetOption;
  private readonly Option<bool> _dotnet8Option;
  private readonly Option<bool> _dotnet9Option;
  private readonly Option<bool> _dotnet10Option;
  private readonly Option<bool> _playwrightOption;
  private readonly Option<bool> _dotnetPlaywrightOption;
  private readonly Option<bool> _rustOption;
  private readonly Option<bool> _dotnetRustOption;

  // === MOUNT OPTIONS ===
  private readonly Option<string[]> _mountOption;
  private readonly Option<string[]> _mountRwOption;

  // === EXECUTION OPTIONS ===
  private readonly Option<bool> _noCleanupOption;
  private readonly Option<bool> _noPullOption;

  // === HELP OPTIONS ===
  private readonly Option<bool> _help2Option;

  // === SELF-UPDATE OPTIONS (handled by shell scripts, shown in help) ===
  private readonly Option<bool> _updateScriptsOption;

  // === SHELL INTEGRATION INSTALL (handled by native binary) ===
  private readonly Option<bool> _installShellsOption;

  // === YOLO MODE (handled in Program.cs but shown in help) ===
  private readonly Option<bool> _yoloOption;

  // === COPILOT PASSTHROUGH OPTIONS ===
  // These are passed directly to the Copilot CLI inside the container
  private readonly Option<string?> _promptOption;
  private readonly Option<string?> _modelOption;
  private readonly Option<bool> _continueOption;
  private readonly Option<string?> _resumeOption;
  private readonly Option<bool> _silentOption;
  private readonly Option<string?> _agentOption;
  private readonly Option<bool> _noColorOption;
  private readonly Option<string[]> _allowToolOption;
  private readonly Option<string[]> _denyToolOption;
  private readonly Option<string?> _streamOption;
  private readonly Option<string?> _logLevelOption;
  private readonly Option<bool> _screenReaderOption;
  private readonly Option<bool> _noCustomInstructionsOption;
  private readonly Option<string[]> _additionalMcpConfigOption;

  // For any additional args after -- separator
  private readonly Argument<string[]> _passthroughArgs;

  public RunCommand(bool isYolo = false)
  {
    _isYolo = isYolo;

    // Note on aliases:
    // - Primary aliases use standard Unix conventions (--long)
    // - Short aliases like -d, -d8, -d9 are handled in Program.NormalizeArgs
    // - PowerShell-style aliases are also handled in Program.NormalizeArgs for backwards compatibility
    // - Descriptions show available short aliases in brackets

    _dotnetOption = new Option<bool>("--dotnet") { Description = "[-d] Use .NET image variant (all versions)" };

    _dotnet8Option = new Option<bool>("--dotnet8") { Description = "[-d8] Use .NET 8 image variant" };

    _dotnet9Option = new Option<bool>("--dotnet9") { Description = "[-d9] Use .NET 9 image variant" };

    _dotnet10Option = new Option<bool>("--dotnet10") { Description = "[-d10] Use .NET 10 image variant" };

    _playwrightOption = new Option<bool>("--playwright") { Description = "[-pw] Use Playwright image variant" };

    _dotnetPlaywrightOption = new Option<bool>("--dotnet-playwright") { Description = "[-dp] Use .NET + Playwright image variant" };

    _rustOption = new Option<bool>("--rust") { Description = "[-rs] Use Rust image variant" };

    _dotnetRustOption = new Option<bool>("--dotnet-rust") { Description = "[-dr] Use .NET + Rust image variant" };

    _mountOption = new Option<string[]>("--mount") { Description = "Mount additional directory (read-only)" };

    _mountRwOption = new Option<string[]>("--mount-rw") { Description = "Mount additional directory (read-write)" };

    _noCleanupOption = new Option<bool>("--no-cleanup") { Description = "Skip cleanup of unused Docker images" };

    _noPullOption = new Option<bool>("--no-pull") { Description = "Skip pulling the latest image" };
    _noPullOption.Aliases.Add("--skip-pull");

    _help2Option = new Option<bool>("--help2") { Description = "Show GitHub Copilot CLI native help" };

    // Update option - handled by shell scripts but shown in help
    _updateScriptsOption = new Option<bool>("--update") { Description = "[-u] Update binary and scripts (handled by shell wrapper)" };

    _installShellsOption = new Option<bool>("--install-shells") { Description = "Install shell integrations (bash/zsh/fish + PowerShell/cmd)" };

    // Yolo mode - adds --allow-all-tools and --allow-all-paths to Copilot
    // Note: This is handled in Program.cs for app name, but we add it here so it shows in --help
    _yoloOption = new Option<bool>("--yolo") { Description = "Enable YOLO mode (allow all tools and paths)" };

    // Copilot passthrough options
    _promptOption = new Option<string?>("--prompt") { Description = "[Copilot] Execute a prompt directly" };
    _promptOption.Aliases.Add("-p");
    _modelOption = new Option<string?>("--model") { Description = "[Copilot] Set AI model (claude-sonnet-4.5, gpt-5, etc.)" };
    _continueOption = new Option<bool>("--continue") { Description = "[Copilot] Resume most recent session" };
    _resumeOption = new Option<string?>("--resume") { Description = "[Copilot] Resume from a previous session [sessionId]", Arity = ArgumentArity.ZeroOrOne };
    _silentOption = new Option<bool>("--silent") { Description = "[Copilot] Output only agent response, useful for scripting with -p" };
    _silentOption.Aliases.Add("-s");
    _agentOption = new Option<string?>("--agent") { Description = "[Copilot] Specify a custom agent to use (only in prompt mode)" };
    _noColorOption = new Option<bool>("--no-color") { Description = "[Copilot] Disable all color output" };
    _allowToolOption = new Option<string[]>("--allow-tool") { Description = "[Copilot] Allow specific tools (can be used multiple times)" };
    _denyToolOption = new Option<string[]>("--deny-tool") { Description = "[Copilot] Deny specific tools (can be used multiple times)" };
    _streamOption = new Option<string?>("--stream") { Description = "[Copilot] Enable or disable streaming (on|off)" };
    _logLevelOption = new Option<string?>("--log-level") { Description = "[Copilot] Set log level (none|error|warning|info|debug|all)" };
    _screenReaderOption = new Option<bool>("--screen-reader") { Description = "[Copilot] Enable screen reader optimizations" };
    _noCustomInstructionsOption = new Option<bool>("--no-custom-instructions") { Description = "[Copilot] Disable loading custom instructions from AGENTS.md" };
    _additionalMcpConfigOption = new Option<string[]>("--additional-mcp-config") { Description = "[Copilot] Additional MCP servers config (JSON string or @filepath)" };

    // Passthrough args after -- separator
    _passthroughArgs = new Argument<string[]>("copilot-args")
    {
      Description = "Additional arguments passed to GitHub Copilot CLI",
      Arity = ArgumentArity.ZeroOrMore
    };
  }

  public void Configure(RootCommand root)
  {
    root.Add(_dotnetOption);
    root.Add(_dotnet8Option);
    root.Add(_dotnet9Option);
    root.Add(_dotnet10Option);
    root.Add(_playwrightOption);
    root.Add(_dotnetPlaywrightOption);
    root.Add(_rustOption);
    root.Add(_dotnetRustOption);
    root.Add(_mountOption);
    root.Add(_mountRwOption);
    root.Add(_noCleanupOption);
    root.Add(_noPullOption);
    root.Add(_help2Option);
    root.Add(_updateScriptsOption);
    root.Add(_installShellsOption);
    root.Add(_yoloOption);

    // Copilot passthrough options
    root.Add(_promptOption);
    root.Add(_modelOption);
    root.Add(_continueOption);
    root.Add(_resumeOption);
    root.Add(_silentOption);
    root.Add(_agentOption);
    root.Add(_noColorOption);
    root.Add(_allowToolOption);
    root.Add(_denyToolOption);
    root.Add(_streamOption);
    root.Add(_logLevelOption);
    root.Add(_screenReaderOption);
    root.Add(_noCustomInstructionsOption);
    root.Add(_additionalMcpConfigOption);
    root.Add(_passthroughArgs);

    root.SetAction(parseResult =>
    {
      var ctx = Infrastructure.AppContext.Create();

      var dotnet = parseResult.GetValue(_dotnetOption);
      var dotnet8 = parseResult.GetValue(_dotnet8Option);
      var dotnet9 = parseResult.GetValue(_dotnet9Option);
      var dotnet10 = parseResult.GetValue(_dotnet10Option);
      var playwright = parseResult.GetValue(_playwrightOption);
      var dotnetPlaywright = parseResult.GetValue(_dotnetPlaywrightOption);
      var rust = parseResult.GetValue(_rustOption);
      var dotnetRust = parseResult.GetValue(_dotnetRustOption);
      var cliMountsRo = parseResult.GetValue(_mountOption) ?? [];
      var cliMountsRw = parseResult.GetValue(_mountRwOption) ?? [];
      var noCleanup = parseResult.GetValue(_noCleanupOption);
      var noPull = parseResult.GetValue(_noPullOption);
      var help2 = parseResult.GetValue(_help2Option);
      var updateScripts = parseResult.GetValue(_updateScriptsOption);
      var installShells = parseResult.GetValue(_installShellsOption);

      // Copilot passthrough options
      var prompt = parseResult.GetValue(_promptOption);
      var model = parseResult.GetValue(_modelOption);
      var continueSession = parseResult.GetValue(_continueOption);
      var resumeSession = parseResult.GetValue(_resumeOption);
      var silent = parseResult.GetValue(_silentOption);
      var agent = parseResult.GetValue(_agentOption);
      var noColor = parseResult.GetValue(_noColorOption);
      var allowTools = parseResult.GetValue(_allowToolOption) ?? [];
      var denyTools = parseResult.GetValue(_denyToolOption) ?? [];
      var stream = parseResult.GetValue(_streamOption);
      var logLevel = parseResult.GetValue(_logLevelOption);
      var screenReader = parseResult.GetValue(_screenReaderOption);
      var noCustomInstructions = parseResult.GetValue(_noCustomInstructionsOption);
      var additionalMcpConfigs = parseResult.GetValue(_additionalMcpConfigOption) ?? [];
      var passthroughArgs = parseResult.GetValue(_passthroughArgs) ?? [];

      // Handle --install-shells
      if (installShells)
      {
        DebugLogger.Log("--install-shells flag detected");
        return ShellIntegration.InstallAll();
      }

      // Handle --update - show message that it's handled by shell wrapper
      if (updateScripts)
      {
        DebugLogger.Log("--update flag detected (handled by shell wrapper)");
        Console.WriteLine("ℹ️  Update is handled by the shell wrapper (copilot_here function).");
        Console.WriteLine("   If running the binary directly, please use the shell function:");
        Console.WriteLine("");
        Console.WriteLine("   copilot_here --update");
        Console.WriteLine("");
        Console.WriteLine("   Or manually re-source the script to get updates.");
        return 0;
      }

      DebugLogger.Log("Checking dependencies...");
      // Check dependencies
      var dependencyResults = DependencyCheck.CheckAll();
      var allDependenciesSatisfied = DependencyCheck.DisplayResults(dependencyResults);
      
      if (!allDependenciesSatisfied)
      {
        DebugLogger.Log("Dependency check failed");
        return 1;
      }
      DebugLogger.Log("All dependencies satisfied");

      DebugLogger.Log("Validating GitHub auth scopes...");
      // Security check
      var (isValid, error) = GitHubAuth.ValidateScopes();
      if (!isValid)
      {
        DebugLogger.Log($"Auth validation failed: {error}");
        Console.WriteLine($"❌ {error}");
        return 1;
      }
      DebugLogger.Log("Auth validation passed");

      // Determine image tag (CLI overrides config)
      var imageTag = ctx.ImageConfig.Tag;

      if (dotnet) imageTag = "dotnet";
      else if (dotnet8) imageTag = "dotnet-8";
      else if (dotnet9) imageTag = "dotnet-9";
      else if (dotnet10) imageTag = "dotnet-10";
      else if (playwright) imageTag = "playwright";
      else if (dotnetPlaywright) imageTag = "dotnet-playwright";
      else if (rust) imageTag = "rust";
      else if (dotnetRust) imageTag = "dotnet-rust";

      var imageName = DockerRunner.GetImageName(imageTag);
      DebugLogger.Log($"Selected image: {imageName}");

      // Build copilot args list
      var copilotArgs = new List<string> { "copilot" };

      // Add YOLO mode flags
      if (_isYolo)
      {
        DebugLogger.Log("Adding YOLO mode flags");
        copilotArgs.Add("--allow-all-tools");
        copilotArgs.Add("--allow-all-paths");
      }

      // Handle --help2 (native copilot help)
      if (help2)
      {
        copilotArgs.Add("--help");
      }
      else
      {
        // Add passthrough options
        if (!string.IsNullOrEmpty(prompt))
        {
          copilotArgs.Add("--prompt");
          copilotArgs.Add(prompt);
        }
        if (!string.IsNullOrEmpty(model))
        {
          copilotArgs.Add("--model");
          copilotArgs.Add(model);
        }
        if (continueSession)
        {
          copilotArgs.Add("--continue");
        }
        // Check if --resume was actually passed (even without a value)
        var resumeOptionResult = parseResult.GetResult(_resumeOption);
        if (resumeOptionResult != null)
        {
          copilotArgs.Add("--resume");
          if (!string.IsNullOrEmpty(resumeSession))
            copilotArgs.Add(resumeSession);
        }
        if (silent)
        {
          copilotArgs.Add("--silent");
        }
        if (!string.IsNullOrEmpty(agent))
        {
          copilotArgs.Add("--agent");
          copilotArgs.Add(agent);
        }
        if (noColor)
        {
          copilotArgs.Add("--no-color");
        }
        foreach (var tool in allowTools)
        {
          copilotArgs.Add("--allow-tool");
          copilotArgs.Add(tool);
        }
        foreach (var tool in denyTools)
        {
          copilotArgs.Add("--deny-tool");
          copilotArgs.Add(tool);
        }
        if (!string.IsNullOrEmpty(stream))
        {
          copilotArgs.Add("--stream");
          copilotArgs.Add(stream);
        }
        if (!string.IsNullOrEmpty(logLevel))
        {
          copilotArgs.Add("--log-level");
          copilotArgs.Add(logLevel);
        }
        if (screenReader)
        {
          copilotArgs.Add("--screen-reader");
        }
        if (noCustomInstructions)
        {
          copilotArgs.Add("--no-custom-instructions");
        }
        foreach (var mcpConfig in additionalMcpConfigs)
        {
          copilotArgs.Add("--additional-mcp-config");
          copilotArgs.Add(mcpConfig);
        }
        copilotArgs.AddRange(passthroughArgs);

        // If no args (interactive mode), add --banner
        if (copilotArgs.Count == 1 || (copilotArgs.Count <= 3 && _isYolo))
        {
          copilotArgs.Add("--banner");
        }
      }

      var supportsVariant = ctx.Environment.SupportsEmojiVariationSelectors;
      Console.WriteLine($"{Emoji.Rocket(supportsVariant)} Using image: {imageName}");

      // Pull image unless skipped
      if (!noPull)
      {
        DebugLogger.Log("Pulling Docker image...");
        if (!DockerRunner.PullImage(imageName))
        {
          DebugLogger.Log("Docker image pull failed");
          Console.WriteLine("Error: Failed to pull Docker image. Check Docker setup and network.");
          return 1;
        }
        DebugLogger.Log("Docker image pull succeeded");
      }
      else
      {
        DebugLogger.Log("Skipping image pull (--no-pull flag)");
        Console.WriteLine($"{Emoji.Skip(supportsVariant)}  Skipping image pull");
      }

      // Cleanup old images unless skipped
      if (!noCleanup)
      {
        DockerRunner.CleanupOldImages(imageName);
      }
      else
      {
        Console.WriteLine($"{Emoji.Skip(supportsVariant)}  Skipping image cleanup");
      }

      // Collect all mounts (CLI first, then local, then global for priority)
      var allMounts = new List<MountEntry>();
      
      // Parse CLI mounts first (highest priority)
      foreach (var path in cliMountsRo)
        allMounts.Add(ParseCliMount(path, defaultReadWrite: false));
      foreach (var path in cliMountsRw)
        allMounts.Add(ParseCliMount(path, defaultReadWrite: true));
      
      // Add local mounts (medium priority)
      allMounts.AddRange(ctx.MountsConfig.LocalMounts);
      
      // Add global mounts last (lowest priority)
      allMounts.AddRange(ctx.MountsConfig.GlobalMounts);

      // Validate all mounts (check for sensitive paths, symlinks, existence)
      var validatedMounts = new List<MountEntry>();
      foreach (var mount in allMounts)
      {
        if (mount.Validate(ctx.Paths.UserHome))
        {
          validatedMounts.Add(mount);
        }
        else
        {
          // User cancelled due to sensitive path - skip this mount
          Console.WriteLine($"⏭️  Skipping mount: {mount.Path}");
        }
      }
      allMounts = validatedMounts;

      // Remove duplicates by comparing resolved container paths
      allMounts = RemoveDuplicateMounts(allMounts, ctx.Paths.UserHome);

      // Display mount info
      DisplayMounts(ctx, allMounts);

      // Display Airlock status and run in appropriate mode
      if (ctx.AirlockConfig.Enabled)
      {
        var sourceDisplay = ctx.AirlockConfig.EnabledSource switch
        {
          AirlockConfigSource.Local => "local (.copilot_here/network.json)",
          AirlockConfigSource.Global => "global (~/.config/copilot_here/network.json)",
          _ => "config"
        };
        Console.WriteLine($"🛡️  Airlock: enabled - {sourceDisplay}");

        // Run in Airlock mode with Docker Compose
        return AirlockRunner.Run(ctx, imageTag, _isYolo, allMounts, copilotArgs);
      }

      // Add directories for YOLO mode
      if (_isYolo)
      {
        // Add current dir and all mount paths to --add-dir
        copilotArgs.Add("--add-dir");
        copilotArgs.Add(ctx.Paths.ContainerWorkDir);

        foreach (var mount in allMounts)
        {
          copilotArgs.Add("--add-dir");
          copilotArgs.Add(mount.GetContainerPath(ctx.Paths.UserHome));
        }
      }

      // Build Docker args for standard mode
      var sessionId = GenerateSessionId();
      var containerName = $"copilot_here-{sessionId}";
      var dockerArgs = BuildDockerArgs(ctx, imageName, containerName, allMounts, copilotArgs, _isYolo, imageTag);

      // Set terminal title
      var titleEmoji = _isYolo ? "🤖⚡️" : "🤖";
      var dirName = SystemInfo.GetCurrentDirectoryName();
      var title = $"{titleEmoji} {dirName}";

      return DockerRunner.RunInteractive(dockerArgs, title);
    });
  }

  private static string GenerateSessionId()
  {
    var input = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
    var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash)[..8].ToLowerInvariant();
  }

  private static void DisplayMounts(Infrastructure.AppContext ctx, List<MountEntry> allMounts)
  {
    if (allMounts.Count == 0) return;

    Console.WriteLine("📂 Mounts:");
    Console.WriteLine($"   📁 {ctx.Paths.ContainerWorkDir}");

    foreach (var mount in allMounts)
    {
      var mode = mount.IsReadWrite ? "rw" : "ro";
      var containerPath = mount.GetContainerPath(ctx.Paths.UserHome);
      var icon = mount.Source switch
      {
        MountSource.Global => ctx.Environment.SupportsEmoji ? "🌍" : "G:",
        MountSource.Local => ctx.Environment.SupportsEmoji ? "📍" : "L:",
        MountSource.CommandLine => ctx.Environment.SupportsEmoji ? "🔧" : "CLI:",
        _ => "  "
      };
      Console.WriteLine($"   {icon} {containerPath} ({mode})");
    }
  }

  private static List<string> BuildDockerArgs(
    Infrastructure.AppContext ctx,
    string imageName,
    string containerName,
    List<MountEntry> mounts,
    List<string> copilotArgs,
    bool isYolo,
    string imageTag)
  {
    // Generate session info JSON
    var sessionInfo = SessionInfo.Generate(ctx, imageTag, imageName, mounts, isYolo);
    
    var args = new List<string>
    {
      "run",
      "--rm",
      "-it",
      "--name", containerName,
      // Mount current directory
      "-v", $"{ctx.Paths.CurrentDirectory}:{ctx.Paths.ContainerWorkDir}",
      "-w", ctx.Paths.ContainerWorkDir,
      // Mount copilot config
      "-v", $"{ctx.Paths.CopilotConfigPath}:/home/appuser/.copilot",
      // Environment variables
      "-e", $"PUID={ctx.Environment.UserId}",
      "-e", $"PGID={ctx.Environment.GroupId}",
      "-e", $"GITHUB_TOKEN={ctx.Environment.GitHubToken}",
      "-e", $"COPILOT_HERE_SESSION_INFO={sessionInfo}"
    };

    // Add additional mounts
    foreach (var mount in mounts)
    {
      args.Add("-v");
      args.Add(mount.ToDockerVolume(ctx.Paths.UserHome));
    }

    // Add sandbox flags from SANDBOX_FLAGS environment variable
    var sandboxFlags = SandboxFlags.Parse();
    if (sandboxFlags.Count > 0)
    {
      DebugLogger.Log($"Adding {sandboxFlags.Count} sandbox flags from SANDBOX_FLAGS");
      args.AddRange(sandboxFlags);
    }

    // Add image name
    args.Add(imageName);

    // Add copilot args
    args.AddRange(copilotArgs);

    return args;
  }

  /// <summary>
  /// Parses a CLI mount path, handling :rw/:ro suffix.
  /// Format: "path" or "path:rw" or "path:ro"
  /// </summary>
  private static MountEntry ParseCliMount(string input, bool defaultReadWrite)
  {
    var isReadWrite = defaultReadWrite;
    var mountPath = input;

    if (input.EndsWith(":rw", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = true;
      mountPath = input[..^3];
    }
    else if (input.EndsWith(":ro", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = false;
      mountPath = input[..^3];
    }

    return new MountEntry(mountPath, isReadWrite, MountSource.CommandLine);
  }

  /// <summary>
  /// Removes duplicate mounts by comparing normalized container paths.
  /// Keeps the first occurrence (CLI > Local > Global priority).
  /// Prefers read-only over read-write for security when same source priority.
  /// Internal for testing via InternalsVisibleTo.
  /// </summary>
  internal static List<MountEntry> RemoveDuplicateMounts(List<MountEntry> mounts, string userHome)
  {
    var seen = new Dictionary<string, MountEntry>(StringComparer.OrdinalIgnoreCase);
    
    foreach (var mount in mounts)
    {
      var containerPath = NormalizePath(mount.GetContainerPath(userHome));
      
      if (!seen.TryGetValue(containerPath, out var existing))
      {
        // First occurrence - add it
        seen[containerPath] = mount;
      }
      else if (mount.Source == existing.Source && !mount.IsReadWrite && existing.IsReadWrite)
      {
        // Same source priority: prefer read-only over read-write for security
        seen[containerPath] = mount;
      }
      // Otherwise keep the first (higher priority source)
    }

    return [.. seen.Values];
  }

  /// <summary>
  /// Normalizes a path by removing trailing slashes and ensuring consistent format.
  /// Internal for testing via InternalsVisibleTo.
  /// </summary>
  internal static string NormalizePath(string path)
  {
    if (string.IsNullOrEmpty(path)) return path;
    
    // Remove trailing slashes/backslashes
    return path.TrimEnd('/', '\\');
  }
}
