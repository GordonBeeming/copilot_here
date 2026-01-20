using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Model;
using CopilotHere.Commands.Mounts;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Complete application context combining paths, environment, and loaded configs.
/// This is created once at startup and passed to all commands.
/// </summary>
public sealed record AppContext
{
  /// <summary>All resolved file paths.</summary>
  public required AppPaths Paths { get; init; }

  /// <summary>Runtime environment (auth, user IDs, etc.).</summary>
  public required AppEnvironment Environment { get; init; }

  /// <summary>The active CLI tool being used (e.g., GitHub Copilot, Echo).</summary>
  public required ICliTool ActiveTool { get; init; }

  /// <summary>Image configuration (loaded from config files).</summary>
  public required ImageConfig ImageConfig { get; init; }

  /// <summary>Model configuration (loaded from config files).</summary>
  public required ModelConfig ModelConfig { get; init; }

  /// <summary>Mounts configuration (loaded from config files).</summary>
  public required MountsConfig MountsConfig { get; init; }

  /// <summary>Airlock configuration (loaded from config files).</summary>
  public required AirlockConfig AirlockConfig { get; init; }

  /// <summary>Creates an AppContext with all state resolved and configs loaded.</summary>
  /// <param name="toolOverride">Optional tool name to override config (from CLI --tool argument)</param>
  public static AppContext Create(string? toolOverride = null)
  {
    var paths = AppPaths.Resolve();
    var environment = AppEnvironment.Resolve();

    // Determine which tool to use based on priority:
    // 1. CLI argument (--tool X)
    // 2. Local config (.cli_mate/tool.conf or .copilot_here/tool.conf)
    // 3. Global config (~/.config/cli_mate/tool.conf or ~/.config/copilot_here/tool.conf)
    // 4. Default (github-copilot)
    var toolName = toolOverride ?? GetToolFromConfig(paths) ?? "github-copilot";
    var tool = ToolRegistry.Exists(toolName) ? ToolRegistry.Get(toolName) : ToolRegistry.GetDefault();

    return new AppContext
    {
      Paths = paths,
      Environment = environment,
      ActiveTool = tool,
      ImageConfig = ImageConfig.Load(paths),
      ModelConfig = ModelConfig.Load(paths),
      MountsConfig = MountsConfig.Load(paths),
      AirlockConfig = AirlockConfig.Load(paths)
    };
  }

  private static string? GetToolFromConfig(AppPaths paths)
  {
    // Try local config first (.cli_mate/tool.conf)
    var localToolConfig = Path.Combine(paths.CurrentDirectory, ".cli_mate", "tool.conf");
    var tool = ConfigFile.ReadValue(localToolConfig);
    if (!string.IsNullOrWhiteSpace(tool) && ToolRegistry.Exists(tool)) return tool;

    // Try legacy local config (.copilot_here/tool.conf)
    var legacyLocalToolConfig = Path.Combine(paths.CurrentDirectory, ".copilot_here", "tool.conf");
    tool = ConfigFile.ReadValue(legacyLocalToolConfig);
    if (!string.IsNullOrWhiteSpace(tool) && ToolRegistry.Exists(tool)) return tool;

    // Try global config (~/.config/cli_mate/tool.conf)
    var globalToolConfig = Path.Combine(paths.UserHome, ".config", "cli_mate", "tool.conf");
    tool = ConfigFile.ReadValue(globalToolConfig);
    if (!string.IsNullOrWhiteSpace(tool) && ToolRegistry.Exists(tool)) return tool;

    // Try legacy global config (~/.config/copilot_here/tool.conf)
    var legacyGlobalToolConfig = paths.GetGlobalPath("tool.conf");
    tool = ConfigFile.ReadValue(legacyGlobalToolConfig);
    if (!string.IsNullOrWhiteSpace(tool) && ToolRegistry.Exists(tool)) return tool;

    return null;
  }
}
