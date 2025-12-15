using System.CommandLine;
using CopilotHere.Commands;
using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;
using CopilotHere.Commands.Run;
using CopilotHere.Infrastructure;

class Program
{
  // Map of aliases to their canonical double-dash form.
  // This allows:
  // - Unconventional but user-friendly short aliases like -d, -d8, -d9, -d10
  // - PowerShell-style aliases (-PascalCase) for backwards compatibility without polluting help
  private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
  {
    // Short aliases (single-dash multi-char)
    { "-h", "--help" },
    { "-d", "--dotnet" },
    { "-d8", "--dotnet8" },
    { "-d9", "--dotnet9" },
    { "-d10", "--dotnet10" },
    { "-dp", "--dotnet-playwright" },
    { "-pw", "--playwright" },
    { "-rs", "--rust" },
    { "-dr", "--dotnet-rust" },

    // PowerShell-style aliases (hidden from help, for backwards compatibility)
    { "-Dotnet", "--dotnet" },
    { "-Dotnet8", "--dotnet8" },
    { "-Dotnet9", "--dotnet9" },
    { "-Dotnet10", "--dotnet10" },
    { "-Playwright", "--playwright" },
    { "-DotnetPlaywright", "--dotnet-playwright" },
    { "-Rust", "--rust" },
    { "-DotnetRust", "--dotnet-rust" },
    { "-Mount", "--mount" },
    { "-MountRW", "--mount-rw" },
    { "-NoCleanup", "--no-cleanup" },
    { "-NoPull", "--no-pull" },
    { "-SkipPull", "--no-pull" },
    { "-Help2", "--help2" },
    { "-u", "--update" },
    { "--upgrade", "--update" },
    { "-UpdateScripts", "--update" },
    { "-UpgradeScripts", "--update" },
    { "--update-scripts", "--update" },
    { "--upgrade-scripts", "--update" },
    { "-ListMounts", "--list-mounts" },
    { "-SaveMount", "--save-mount" },
    { "-SaveMountGlobal", "--save-mount-global" },
    { "-RemoveMount", "--remove-mount" },
    { "-ListImages", "--list-images" },
    { "-ShowImage", "--show-image" },
    { "-SetImage", "--set-image" },
    { "-SetImageGlobal", "--set-image-global" },
    { "-ClearImage", "--clear-image" },
    { "-ClearImageGlobal", "--clear-image-global" },
    { "-EnableAirlock", "--enable-airlock" },
    { "-EnableGlobalAirlock", "--enable-global-airlock" },
    { "-DisableAirlock", "--disable-airlock" },
    { "-DisableGlobalAirlock", "--disable-global-airlock" },
    { "-ShowAirlockRules", "--show-airlock-rules" },
    { "-EditAirlockRules", "--edit-airlock-rules" },
    { "-EditGlobalAirlockRules", "--edit-global-airlock-rules" },
    { "-Yolo", "--yolo" },

    // Copilot passthrough options (PowerShell-style aliases)
    { "-Prompt", "--prompt" },
    { "-Model", "--model" },
    { "-Continue", "--continue" },
    { "-Resume", "--resume" },
    { "-Silent", "--silent" },
    { "-Agent", "--agent" },
    { "-NoColor", "--no-color" },
    { "-AllowTool", "--allow-tool" },
    { "-DenyTool", "--deny-tool" },
    { "-Stream", "--stream" },
    { "-LogLevel", "--log-level" },
    { "-ScreenReader", "--screen-reader" },
    { "-NoCustomInstructions", "--no-custom-instructions" },
    { "-AdditionalMcpConfig", "--additional-mcp-config" },
  };

  static async Task<int> Main(string[] args)
  {
    DebugLogger.Log("=== Application started ===");
    DebugLogger.Log($"Args: {string.Join(" ", args)}");
    
    // Check for --yolo flag before normalizing (to determine app name)
    var isYolo = IsYoloMode(args);
    DebugLogger.Log($"YOLO mode: {isYolo}");
    
    var appName = isYolo ? "copilot_yolo" : GetAppName();
    DebugLogger.Log($"App name: {appName}");

    // Preprocess args to normalize aliases (short multi-char and PowerShell-style)
    args = NormalizeArgs(args);
    DebugLogger.Log($"Normalized args: {string.Join(" ", args)}");

    ShellIntegration.WarnIfMissing(appName, args);

    var rootCommand = new RootCommand($"{appName} - GitHub Copilot CLI in a secure Docker container");

    // Register all commands
    ICommand[] commands =
    [
      new RunCommand(isYolo),  // Main run command (default) - includes update option
      new MountCommands(),     // Mount management
      new ImageCommands(),     // Image management
      new AirlockCommands(),   // Airlock proxy
    ];

    foreach (var command in commands)
    {
      command.Configure(rootCommand);
    }

    DebugLogger.Log("Invoking command parser...");
    var exitCode = await rootCommand.Parse(args).InvokeAsync();
    DebugLogger.Log($"Application exiting with code: {exitCode}");
    return exitCode;
  }

  /// <summary>
  /// Checks if --yolo flag is present in args.
  /// </summary>
  private static bool IsYoloMode(string[] args)
  {
    return args.Any(arg =>
      arg.Equals("--yolo", StringComparison.OrdinalIgnoreCase) ||
      arg.Equals("-Yolo", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// Gets the application name from executable name.
  /// </summary>
  private static string GetAppName()
  {
    var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "copilot_here");
    return exeName;
  }

  /// <summary>
  /// Normalizes command-line args to support various alias styles.
  /// - Single-dash multi-char aliases like -d, -d8, -d9, -d10 for dotnet version selection
  /// - PowerShell-style aliases like -Dotnet, -Mount for backwards compatibility
  /// These are mapped to standard double-dash forms so they work but don't clutter help.
  /// </summary>
  private static string[] NormalizeArgs(string[] args)
  {
    var normalized = new List<string>(args.Length);
    foreach (var arg in args)
    {
      normalized.Add(AliasMap.TryGetValue(arg, out var mapped) ? mapped : arg);
    }
    return [.. normalized];
  }
}