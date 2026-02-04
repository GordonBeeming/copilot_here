using CopilotHere.Tools;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Central registry for all available CLI tools
/// </summary>
public static class ToolRegistry
{
    private static readonly Dictionary<string, Lazy<ICliTool>> _tools = new()
    {
        ["github-copilot"] = new Lazy<ICliTool>(() => new GitHubCopilotTool()),
        ["echo"] = new Lazy<ICliTool>(() => new EchoTool()),
    };

    /// <summary>
    /// Gets a tool by name
    /// </summary>
    /// <param name="name">Tool name (e.g., "github-copilot")</param>
    /// <returns>The CLI tool instance</returns>
    /// <exception cref="ArgumentException">Thrown if the tool name is not registered</exception>
    public static ICliTool Get(string name)
    {
        if (_tools.TryGetValue(name, out var lazyTool))
        {
            return lazyTool.Value;
        }

        var available = string.Join(", ", _tools.Keys);
        throw new ArgumentException(
            $"Unknown tool: {name}. Available tools: {available}",
            nameof(name)
        );
    }

    /// <summary>
    /// Gets the default tool (GitHub Copilot)
    /// </summary>
    public static ICliTool GetDefault()
    {
        return Get("github-copilot");
    }

    /// <summary>
    /// Gets all available tools
    /// </summary>
    public static IEnumerable<ICliTool> GetAll()
    {
        return _tools.Values.Select(lazy => lazy.Value);
    }

    /// <summary>
    /// Checks if a tool exists
    /// </summary>
    public static bool Exists(string name)
    {
        return _tools.ContainsKey(name);
    }

    /// <summary>
    /// Gets all registered tool names
    /// </summary>
    public static IEnumerable<string> GetToolNames()
    {
        return _tools.Keys;
    }
}
