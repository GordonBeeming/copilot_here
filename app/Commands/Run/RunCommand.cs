using System.CommandLine;
using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Model;
using CopilotHere.Commands.Mounts;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Run;

/// <summary>
/// Main command that runs the Copilot CLI in a Docker container.
/// This is the default command when no subcommand is specified.
/// </summary>
public sealed class RunCommand : ICommand
{
  private const string GitHubCopilotToolName = "github-copilot";
  private readonly bool _isYolo;

  // === TOOL SELECTION ===
  private readonly Option<string?> _toolOption;

  // === IMAGE SELECTION OPTIONS ===
  private readonly Option<bool> _dotnetOption;
  private readonly Option<bool> _dotnet8Option;
  private readonly Option<bool> _dotnet9Option;
  private readonly Option<bool> _dotnet10Option;
  private readonly Option<bool> _playwrightOption;
  private readonly Option<bool> _dotnetPlaywrightOption;
  private readonly Option<bool> _rustOption;
  private readonly Option<bool> _dotnetRustOption;
  private readonly Option<bool> _golangOption;

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

    _toolOption = new Option<string?>("--tool") { Description = "Override CLI tool (github-copilot, echo, etc.)" };

    _dotnetOption = new Option<bool>("--dotnet") { Description = "[-d] Use .NET image variant (all versions)" };

    _dotnet8Option = new Option<bool>("--dotnet8") { Description = "[-d8] Use .NET 8 image variant" };

    _dotnet9Option = new Option<bool>("--dotnet9") { Description = "[-d9] Use .NET 9 image variant" };

    _dotnet10Option = new Option<bool>("--dotnet10") { Description = "[-d10] Use .NET 10 image variant" };

    _playwrightOption = new Option<bool>("--playwright") { Description = "[-pw] Use Playwright image variant" };

    _dotnetPlaywrightOption = new Option<bool>("--dotnet-playwright") { Description = "[-dp] Use .NET + Playwright image variant" };

    _rustOption = new Option<bool>("--rust") { Description = "[-rs] Use Rust image variant" };

    _dotnetRustOption = new Option<bool>("--dotnet-rust") { Description = "[-dr] Use .NET + Rust image variant" };

    _golangOption = new Option<bool>("--golang") { Description = "[-go] Use Golang image variant" };

    _mountOption = new Option<string[]>("--mount") { Description = "Mount directory (read-only). Format: path or host:container" };

    _mountRwOption = new Option<string[]>("--mount-rw") { Description = "Mount directory (read-write). Format: path or host:container" };

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
    root.Add(_toolOption);
    root.Add(_dotnetOption);
    root.Add(_dotnet8Option);
    root.Add(_dotnet9Option);
    root.Add(_dotnet10Option);
    root.Add(_playwrightOption);
    root.Add(_dotnetPlaywrightOption);
    root.Add(_rustOption);
    root.Add(_dotnetRustOption);
    root.Add(_golangOption);
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
      var toolOverride = parseResult.GetValue(_toolOption);
      
      // Validate tool if specified
      if (!string.IsNullOrWhiteSpace(toolOverride) && !ToolRegistry.Exists(toolOverride))
      {
        Console.WriteLine($"❌ Unknown tool: {toolOverride}");
        Console.WriteLine();
        Console.WriteLine("Available tools:");
        foreach (var name in ToolRegistry.GetToolNames())
        {
          Console.WriteLine($"  • {name}");
        }
        return 1;
      }
      
      var ctx = Infrastructure.AppContext.Create(toolOverride);

      var dotnet = parseResult.GetValue(_dotnetOption);
      var dotnet8 = parseResult.GetValue(_dotnet8Option);
      var dotnet9 = parseResult.GetValue(_dotnet9Option);
      var dotnet10 = parseResult.GetValue(_dotnet10Option);
      var playwright = parseResult.GetValue(_playwrightOption);
      var dotnetPlaywright = parseResult.GetValue(_dotnetPlaywrightOption);
      var rust = parseResult.GetValue(_rustOption);
      var dotnetRust = parseResult.GetValue(_dotnetRustOption);
      var golang = parseResult.GetValue(_golangOption);
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
      var isGitHubCopilotTool = string.Equals(ctx.ActiveTool.Name, GitHubCopilotToolName, StringComparison.Ordinal);

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

      // Validate capabilities before executing
      if (_isYolo && !ctx.ActiveTool.SupportsYoloMode)
      {
        Console.WriteLine($"❌ {ctx.ActiveTool.DisplayName} does not support YOLO mode");
        return 1;
      }

      var hasCopilotSpecificFlags = !string.IsNullOrEmpty(prompt)
        || continueSession
        || parseResult.GetResult(_resumeOption) is not null
        || silent
        || !string.IsNullOrEmpty(agent)
        || noColor
        || allowTools.Length > 0
        || denyTools.Length > 0
        || !string.IsNullOrEmpty(stream)
        || !string.IsNullOrEmpty(logLevel)
        || screenReader
        || noCustomInstructions
        || additionalMcpConfigs.Length > 0;

      if (!isGitHubCopilotTool && hasCopilotSpecificFlags)
      {
        Console.WriteLine($"❌ Copilot-specific passthrough flags are not supported for tool '{ctx.ActiveTool.Name}'");
        Console.WriteLine("   Use --tool github-copilot, or pass tool-native args after --");
        return 1;
      }

      DebugLogger.Log("Checking dependencies...");
      // Check dependencies
      var dependencyResults = DependencyCheck.CheckAll(ctx.ActiveTool, ctx.RuntimeConfig);
      var allDependenciesSatisfied = DependencyCheck.DisplayResults(dependencyResults);
      
      if (!allDependenciesSatisfied)
      {
        DebugLogger.Log("Dependency check failed");
        return 1;
      }
      DebugLogger.Log("All dependencies satisfied");

      DebugLogger.Log("Validating auth...");
      // Security check
      var authProvider = ctx.ActiveTool.GetAuthProvider();
      var (isValid, error) = authProvider.ValidateAuth();
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
      else if (golang) imageTag = "golang";

      var imageName = ctx.ActiveTool.GetImageName(imageTag);
      DebugLogger.Log($"Selected image: {imageName}");

      // Determine model (CLI overrides config)
      var effectiveModel = model ?? ctx.ModelConfig.Model;
      if (!string.IsNullOrEmpty(effectiveModel))
      {
        if (!ctx.ActiveTool.SupportsModels)
        {
          Console.WriteLine($"❌ {ctx.ActiveTool.DisplayName} does not support model selection");
          return 1;
        }

        DebugLogger.Log($"Using model: {effectiveModel} (source: {(model != null ? "CLI" : ctx.ModelConfig.Source.ToString())})");
      }

      // Build user arguments list (tool-specific passthrough options)
      var userArgs = new List<string>();
      
      // Handle --help2 (native help)
      if (help2)
      {
        userArgs.Add("--help");
      }
      else
      {
        if (isGitHubCopilotTool)
        {
          // Add Copilot passthrough options
          if (!string.IsNullOrEmpty(prompt))
          {
            userArgs.Add("--prompt");
            userArgs.Add(prompt);
          }
          if (continueSession)
          {
            userArgs.Add("--continue");
          }
          // Check if --resume was actually passed (even without a value)
          var resumeOptionResult = parseResult.GetResult(_resumeOption);
          if (resumeOptionResult != null)
          {
            userArgs.Add("--resume");
            if (!string.IsNullOrEmpty(resumeSession))
              userArgs.Add(resumeSession);
          }
          if (silent)
          {
            userArgs.Add("--silent");
          }
          if (!string.IsNullOrEmpty(agent))
          {
            userArgs.Add("--agent");
            userArgs.Add(agent);
          }
          if (noColor)
          {
            userArgs.Add("--no-color");
          }
          foreach (var tool in allowTools)
          {
            userArgs.Add("--allow-tool");
            userArgs.Add(tool);
          }
          foreach (var tool in denyTools)
          {
            userArgs.Add("--deny-tool");
            userArgs.Add(tool);
          }
          if (!string.IsNullOrEmpty(stream))
          {
            userArgs.Add("--stream");
            userArgs.Add(stream);
          }
          if (!string.IsNullOrEmpty(logLevel))
          {
            userArgs.Add("--log-level");
            userArgs.Add(logLevel);
          }
          if (screenReader)
          {
            userArgs.Add("--screen-reader");
          }
          if (noCustomInstructions)
          {
            userArgs.Add("--no-custom-instructions");
          }
          foreach (var mcpConfig in additionalMcpConfigs)
          {
            userArgs.Add("--additional-mcp-config");
            userArgs.Add(mcpConfig);
          }
        }

        userArgs.AddRange(passthroughArgs);
      }

      // Build command context for the tool
      var commandContext = new CommandContext
      {
        UserArgs = userArgs,
        IsYolo = _isYolo,
        IsInteractive = userArgs.Count == 0 || (userArgs.Count == 1 && userArgs[0] == "--help"),
        Model = effectiveModel,
        ImageTag = imageTag,
        Mounts = [], // Will be populated later
        Environment = new Dictionary<string, string>()
      };

      if (commandContext.IsInteractive && !ctx.ActiveTool.SupportsInteractiveMode)
      {
        Console.WriteLine($"❌ {ctx.ActiveTool.DisplayName} does not support interactive mode");
        return 1;
      }

      // Use the tool to build the final command
      var toolCommand = ctx.ActiveTool.BuildCommand(commandContext);
      DebugLogger.Log($"Built command: {string.Join(" ", toolCommand)}");

      var supportsVariant = ctx.Environment.SupportsEmojiVariationSelectors;
      Console.WriteLine($"{Emoji.Rocket(supportsVariant)} Using image: {imageName}");
      Console.WriteLine($"🐳 Container runtime: {ctx.RuntimeConfig.RuntimeFlavor}");
      
      // Show model - always display even if using default
      if (!string.IsNullOrEmpty(effectiveModel))
      {
        var modelSourceIcon = model != null 
          ? Emoji.Tool(supportsVariant) // CLI flag
          : ctx.ModelConfig.Source == ModelConfigSource.Local 
            ? Emoji.Local(supportsVariant) // Local config
            : Emoji.Global(supportsVariant); // Global config
        Console.WriteLine($"{Emoji.Robot(supportsVariant)} Using model: {effectiveModel} {modelSourceIcon}");
      }
      else
      {
        Console.WriteLine($"{Emoji.Robot(supportsVariant)} Using model: default");
      }

      // Pull image unless skipped
      if (!noPull)
      {
        DebugLogger.Log("Pulling image...");
        if (!ContainerRunner.PullImage(ctx.RuntimeConfig, imageName))
        {
          DebugLogger.Log("Image pull failed");
          Console.WriteLine($"Error: Failed to pull image. Check {ctx.RuntimeConfig.RuntimeFlavor} setup and network.");
          return 1;
        }
        DebugLogger.Log("Image pull succeeded");
      }
      else
      {
        DebugLogger.Log("Skipping image pull (--no-pull flag)");
        Console.WriteLine($"{Emoji.Skip(supportsVariant)}  Skipping image pull");
      }

      // Cleanup old images unless skipped
      if (!noCleanup)
      {
        ContainerRunner.CleanupOldImages(ctx.RuntimeConfig, imageName);
      }
      else
      {
        Console.WriteLine($"{Emoji.Skip(supportsVariant)}  Skipping image cleanup");
      }

      // Collect all mounts (CLI first, then local, then global for priority)
      var allMounts = new List<MountEntry>();
      
      // Parse CLI mounts first (highest priority)
      // Supports both simple paths and host:container format
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
          Console.WriteLine($"⏭️  Skipping mount: {mount.HostPath}");
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
        return AirlockRunner.Run(ctx.RuntimeConfig, ctx, imageTag, _isYolo, allMounts, toolCommand);
      }

      // Add directories for YOLO mode
      if (_isYolo)
      {
        // Add current dir and all mount paths to --add-dir
        toolCommand.Add("--add-dir");
        toolCommand.Add(ctx.Paths.ContainerWorkDir);

        foreach (var mount in allMounts)
        {
          toolCommand.Add("--add-dir");
          toolCommand.Add(mount.GetContainerPath(ctx.Paths.UserHome));
        }
      }

      // Build Docker args for standard mode
      var sessionId = GenerateSessionId();
      var containerName = $"copilot_here-{sessionId}";
      var dockerArgs = BuildDockerArgs(ctx, imageName, containerName, allMounts, toolCommand, _isYolo, imageTag, noPull);

      // Set terminal title
      var titleEmoji = _isYolo ? "🤖⚡️" : "🤖";
      var dirName = SystemInfo.GetCurrentDirectoryName();
      var title = $"{titleEmoji} {dirName}";

      return ContainerRunner.RunInteractive(ctx.RuntimeConfig, dockerArgs, title);
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
    List<string> toolCommand,
    bool isYolo,
    string imageTag,
    bool noPull)
  {
    // Generate session info JSON
    var sessionInfo = SessionInfo.Generate(ctx, imageTag, imageName, mounts, isYolo);
    var hostToolConfigPath = ctx.ActiveTool.GetHostConfigPath(ctx.Paths);
    var containerToolConfigPath = ctx.ActiveTool.GetContainerConfigPath();
    
    var args = new List<string>
    {
      "run",
      "--rm",
      "-it",
      "--name", containerName,
      // Mount current directory
      "-v", $"{ConvertToDockerPath(ctx.Paths.CurrentDirectory)}:{ctx.Paths.ContainerWorkDir}",
      "-w", ctx.Paths.ContainerWorkDir,
      // Mount active tool config
      "-v", $"{ConvertToDockerPath(hostToolConfigPath)}:{containerToolConfigPath}",
      // Environment variables
      "-e", $"PUID={ctx.Environment.UserId}",
      "-e", $"PGID={ctx.Environment.GroupId}",
      "-e", $"COPILOT_HERE_SESSION_INFO={sessionInfo}"
    };

    // Add auth environment variables from the active tool's auth provider
    foreach (var (key, value) in ctx.ActiveTool.GetAuthProvider().GetEnvironmentVars())
    {
      args.Add("-e");
      args.Add($"{key}={value}");
    }

    // Add --pull=never when --no-pull is specified
    // This prevents the container runtime from auto-pulling missing images
    // Both Docker (19.09+) and Podman support this flag
    if (noPull)
    {
      args.Insert(1, "--pull=never"); // Insert after "run"
    }

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

    // Add tool command
    args.AddRange(toolCommand);

    return args;
  }

  /// <summary>
  /// Parses a CLI mount path, handling both simple paths and host:container format.
  /// Format: "path", "path:rw", "path:ro", "host:container", "host:container:rw", "host:container:ro"
  /// </summary>
  internal static MountEntry ParseCliMount(string input, bool defaultReadWrite)
  {
    var isReadWrite = defaultReadWrite;
    var spec = input.Trim('\'', '"'); // Remove any surrounding quotes

    // Check for trailing :rw or :ro
    if (spec.EndsWith(":rw", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = true;
      spec = spec[..^3];
    }
    else if (spec.EndsWith(":ro", StringComparison.OrdinalIgnoreCase))
    {
      isReadWrite = false;
      spec = spec[..^3];
    }

    // Check if this is a host:container format
    var separatorIndex = FindBindSeparator(spec);
    
    if (separatorIndex == -1)
    {
      // Simple path format - auto-compute container path
      return new MountEntry(spec, isReadWrite, MountSource.CommandLine);
    }

    // host:container format
    var hostPath = spec[..separatorIndex];
    var containerPath = spec[(separatorIndex + 1)..];

    return new MountEntry(hostPath, containerPath, isReadWrite, MountSource.CommandLine);
  }

  /// <summary>
  /// Finds the separator between host and container paths in a bind spec.
  /// On Windows, skips the colon after drive letter (e.g., C:\path).
  /// </summary>
  private static int FindBindSeparator(string bindSpec)
  {
    var startIndex = 0;
    
    // On Windows, skip past drive letter colon (e.g., C:)
    if (OperatingSystem.IsWindows() && 
        bindSpec.Length >= 2 && 
        char.IsLetter(bindSpec[0]) && 
        bindSpec[1] == ':')
    {
      startIndex = 2;
    }

    return bindSpec.IndexOf(':', startIndex);
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
