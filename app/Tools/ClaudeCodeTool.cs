using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// Claude Code CLI tool provider (Anthropic's @anthropic-ai/claude-code).
/// </summary>
public sealed class ClaudeCodeTool : ICliTool
{
  private readonly IAuthProvider _authProvider = new ClaudeAuthProvider();
  private readonly IModelProvider _modelProvider = new ClaudeModelProvider();

  public string Name => "claude";

  public string DisplayName => "Claude Code";

  public string GetImageName(string tag)
  {
    // If the tag is an absolute image reference (contains '/'), use it as-is
    if (ContainerRunner.IsAbsoluteImageReference(tag))
      return tag;

    // Image name format: ghcr.io/gordonbeeming/copilot_here:claude-{variant}
    // Tag matches what users invoke: "claude-dotnet", "claude-latest", etc.
    const string imagePrefix = "ghcr.io/gordonbeeming/copilot_here";
    var imageTag = string.IsNullOrEmpty(tag) ? "claude-latest" : $"claude-{tag}";
    return $"{imagePrefix}:{imageTag}";
  }

  public string GetDockerfile()
  {
    return "docker/tools/claude/Dockerfile";
  }

  public List<string> BuildCommand(CommandContext context)
  {
    var args = new List<string> { "claude" };

    // YOLO mode: skip all permission prompts inside the sandbox
    if (context.IsYolo)
    {
      args.Add("--dangerously-skip-permissions");
    }

    // Add model if specified (Claude Code accepts aliases like "opus" or full IDs)
    if (!string.IsNullOrEmpty(context.Model))
    {
      args.Add("--model");
      args.Add(context.Model);
    }

    // Add user arguments. With no further args, `claude` launches its
    // interactive TUI, so there is no separate interactive/banner flag to add.
    args.AddRange(context.UserArgs);

    return args;
  }

  public List<string> GetYoloModeFlags()
  {
    return ["--dangerously-skip-permissions"];
  }

  public string? GetInteractiveFlag()
  {
    // Claude Code is interactive by default when invoked with no prompt args.
    return null;
  }

  public IAuthProvider GetAuthProvider()
  {
    return _authProvider;
  }

  public IModelProvider GetModelProvider()
  {
    return _modelProvider;
  }

  public string[] GetRequiredDependencies()
  {
    // Auth flows through the mounted ~/.claude credentials and/or
    // ANTHROPIC_API_KEY, so the only host dependency is the container runtime.
    return ["docker"];
  }

  public string GetConfigDirName()
  {
    return ".claude";
  }

  public string? GetSessionDataPath()
  {
    return null;
  }

  public string GetHostConfigPath(AppPaths paths)
  {
    return paths.ClaudeConfigPath;
  }

  public string GetContainerConfigPath()
  {
    return "/home/appuser/.claude";
  }

  public IReadOnlyList<(string HostPath, string ContainerPath)> GetAdditionalConfigMounts(AppPaths paths)
  {
    // Claude Code's main config (model preference, MCP servers, settings) lives
    // in ~/.claude.json — a sibling of ~/.claude, not inside it — so it needs its
    // own mount. Skip it when absent so we never create a stray host file.
    var hostJson = Path.Combine(paths.UserHome, ".claude.json");
    if (File.Exists(hostJson))
    {
      return [(hostJson, "/home/appuser/.claude.json")];
    }

    return [];
  }

  public string GetDefaultNetworkRulesPath()
  {
    return "docker/tools/claude/default-airlock-rules.json";
  }

  public bool SupportsModels => true;

  // Claude Code persists its own model choice (its config + /model picker), so
  // don't push the shared model.conf at it — those values target Copilot.
  public bool ManagesOwnModelSelection => true;

  public bool SupportsYoloMode => true;

  public bool SupportsInteractiveMode => true;
}
