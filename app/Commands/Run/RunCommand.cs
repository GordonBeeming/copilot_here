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

  // === SELF-UPDATE OPTIONS ===
  private readonly Option<bool> _updateScriptsOption;

  // === YOLO MODE (handled in Program.cs but shown in help) ===
  private readonly Option<bool> _yoloOption;

  // === COPILOT PASSTHROUGH OPTIONS ===
  // These are passed directly to the Copilot CLI inside the container
  private readonly Option<string?> _promptOption;
  private readonly Option<string?> _modelOption;
  private readonly Option<bool> _continueOption;
  private readonly Option<string?> _resumeOption;

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

    _updateScriptsOption = new Option<bool>("--update") { Description = "[-u] Update from GitHub repository" };

    // Yolo mode - adds --allow-all-tools and --allow-all-paths to Copilot
    // Note: This is handled in Program.cs for app name, but we add it here so it shows in --help
    _yoloOption = new Option<bool>("--yolo") { Description = "Enable YOLO mode (allow all tools and paths)" };

    // Copilot passthrough options
    _promptOption = new Option<string?>("--prompt") { Description = "Execute a prompt directly" };
    _promptOption.Aliases.Add("-p");
    _modelOption = new Option<string?>("--model") { Description = "Set AI model (claude-sonnet-4.5, gpt-5, etc.)" };
    _continueOption = new Option<bool>("--continue") { Description = "Resume most recent session" };
    _resumeOption = new Option<string?>("--resume") { Description = "Resume from a previous session [sessionId]", Arity = ArgumentArity.ZeroOrOne };

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
    root.Add(_yoloOption);

    // Copilot passthrough options
    root.Add(_promptOption);
    root.Add(_modelOption);
    root.Add(_continueOption);
    root.Add(_resumeOption);
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

      // Copilot passthrough options
      var prompt = parseResult.GetValue(_promptOption);
      var model = parseResult.GetValue(_modelOption);
      var continueSession = parseResult.GetValue(_continueOption);
      var resumeSession = parseResult.GetValue(_resumeOption);
      var passthroughArgs = parseResult.GetValue(_passthroughArgs) ?? [];

      // Handle --update
      if (updateScripts)
      {
        return SelfUpdater.CheckAndUpdate();
      }

      // Security check
      var (isValid, error) = GitHubAuth.ValidateScopes();
      if (!isValid)
      {
        Console.WriteLine($"❌ {error}");
        return 1;
      }

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

      // Build copilot args list
      var copilotArgs = new List<string> { "copilot" };

      // Add YOLO mode flags
      if (_isYolo)
      {
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
        if (resumeSession != null)
        {
          copilotArgs.Add("--resume");
          if (!string.IsNullOrEmpty(resumeSession))
            copilotArgs.Add(resumeSession);
        }
        copilotArgs.AddRange(passthroughArgs);

        // If no args (interactive mode), add --banner
        if (copilotArgs.Count == 1 || (copilotArgs.Count <= 3 && _isYolo))
        {
          copilotArgs.Add("--banner");
        }
      }

      Console.WriteLine($"🚀 Using image: {imageName}");

      // Pull image unless skipped
      if (!noPull)
      {
        if (!DockerRunner.PullImage(imageName))
        {
          Console.WriteLine("Error: Failed to pull Docker image. Check Docker setup and network.");
          return 1;
        }
      }
      else
      {
        Console.WriteLine("⏭️  Skipping image pull");
      }

      // Cleanup old images unless skipped
      if (!noCleanup)
      {
        DockerRunner.CleanupOldImages(imageName);
      }
      else
      {
        Console.WriteLine("⏭️  Skipping image cleanup");
      }

      // Collect all mounts
      var allMounts = new List<MountEntry>();
      allMounts.AddRange(ctx.MountsConfig.AllConfigMounts);
      foreach (var path in cliMountsRo)
        allMounts.Add(new MountEntry(path, IsReadWrite: false, MountSource.CommandLine));
      foreach (var path in cliMountsRw)
        allMounts.Add(new MountEntry(path, IsReadWrite: true, MountSource.CommandLine));

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
      var dockerArgs = BuildDockerArgs(ctx, imageName, allMounts, copilotArgs);

      // Set terminal title
      var titleEmoji = _isYolo ? "🤖⚡️" : "🤖";
      var dirName = SystemInfo.GetCurrentDirectoryName();
      var title = $"{titleEmoji} {dirName}";

      return DockerRunner.RunInteractive(dockerArgs, title);
    });
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
    List<MountEntry> mounts,
    List<string> copilotArgs)
  {
    var args = new List<string>
    {
      "run",
      "--rm",
      "-it",
      // Mount current directory
      "-v", $"{ctx.Paths.CurrentDirectory}:{ctx.Paths.ContainerWorkDir}",
      "-w", ctx.Paths.ContainerWorkDir,
      // Mount copilot config
      "-v", $"{ctx.Paths.CopilotConfigPath}:/home/appuser/.copilot",
      // Environment variables
      "-e", $"PUID={ctx.Environment.UserId}",
      "-e", $"PGID={ctx.Environment.GroupId}",
      "-e", $"GITHUB_TOKEN={ctx.Environment.GitHubToken}"
    };

    // Add additional mounts
    foreach (var mount in mounts)
    {
      args.Add("-v");
      args.Add(mount.ToDockerVolume(ctx.Paths.UserHome));
    }

    // Add image name
    args.Add(imageName);

    // Add copilot args
    args.AddRange(copilotArgs);

    return args;
  }
}
