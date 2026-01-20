namespace CopilotHere.Infrastructure;

/// <summary>
/// Represents a CLI tool that can be used with cli_mate.
/// Implementations define how to interact with specific AI coding assistants.
/// </summary>
public interface ICliTool
{
    /// <summary>
    /// Unique identifier for the tool (e.g., "github-copilot", "opencode", "echo")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable display name (e.g., "GitHub Copilot CLI", "OpenCode")
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the Docker image name for this tool with the specified tag
    /// </summary>
    /// <param name="tag">Image tag (e.g., "latest", "dotnet", "rust")</param>
    /// <returns>Full image name (e.g., "ghcr.io/gordonbeeming/cli_mate-github-copilot:latest")</returns>
    string GetImageName(string tag);

    /// <summary>
    /// Gets the path to the Dockerfile for this tool
    /// </summary>
    /// <returns>Relative path to Dockerfile (e.g., "docker/github-copilot/Dockerfile")</returns>
    string GetDockerfile();

    /// <summary>
    /// Builds the command line arguments to execute inside the Docker container
    /// </summary>
    /// <param name="ctx">Command execution context with user arguments and configuration</param>
    /// <returns>List of command arguments to pass to Docker</returns>
    List<string> BuildCommand(CommandContext ctx);

    /// <summary>
    /// Gets the CLI flag for interactive mode (if supported)
    /// </summary>
    /// <returns>Flag string (e.g., "--banner") or null if not applicable</returns>
    string? GetInteractiveFlag();

    /// <summary>
    /// Gets the CLI flags for YOLO mode (unrestricted access)
    /// </summary>
    /// <returns>List of flags (e.g., ["--allow-all-tools", "--allow-all-paths"])</returns>
    List<string> GetYoloModeFlags();

    /// <summary>
    /// Gets the configuration directory name for this tool
    /// </summary>
    /// <returns>Directory name (e.g., "cli_mate", "opencode")</returns>
    string GetConfigDirName();

    /// <summary>
    /// Gets the path to the tool's session data directory (if applicable)
    /// </summary>
    /// <returns>Path to session data or null if not used</returns>
    string? GetSessionDataPath();

    /// <summary>
    /// Gets the authentication provider for this tool
    /// </summary>
    IAuthProvider GetAuthProvider();

    /// <summary>
    /// Gets the model provider for this tool
    /// </summary>
    IModelProvider GetModelProvider();

    /// <summary>
    /// Gets the list of required dependencies (besides Docker)
    /// </summary>
    /// <returns>Array of dependency names (e.g., ["docker", "gh"])</returns>
    string[] GetRequiredDependencies();

    /// <summary>
    /// Gets the path to the default network rules for Airlock mode
    /// </summary>
    /// <returns>Path to default-airlock-rules.json</returns>
    string GetDefaultNetworkRulesPath();

    /// <summary>
    /// Indicates if the tool supports model selection
    /// </summary>
    bool SupportsModels { get; }

    /// <summary>
    /// Indicates if the tool supports YOLO mode (unrestricted access)
    /// </summary>
    bool SupportsYoloMode { get; }

    /// <summary>
    /// Indicates if the tool supports interactive mode
    /// </summary>
    bool SupportsInteractiveMode { get; }
}

/// <summary>
/// Context information for building tool commands
/// </summary>
public record CommandContext
{
    public required List<string> UserArgs { get; init; }
    public required bool IsYolo { get; init; }
    public required bool IsInteractive { get; init; }
    public required string? Model { get; init; }
    public required string? ImageTag { get; init; }
    public required List<string> Mounts { get; init; }
    public required Dictionary<string, string> Environment { get; init; }
}
