using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// Model provider for Claude Code.
/// Claude Code accepts both short aliases (opus/sonnet/haiku) and fully-qualified
/// model IDs, and has no Copilot-style "pass an invalid model to list the valid
/// ones" probe, so the list is a curated static set rather than a container query.
/// </summary>
public sealed class ClaudeModelProvider : IModelProvider
{
  // Aliases resolve to Anthropic's current models; full IDs are accepted too.
  private static readonly List<string> KnownModels =
  [
    "opus",
    "sonnet",
    "haiku",
    "claude-opus-4-8",
    "claude-sonnet-4-6",
    "claude-haiku-4-5",
  ];

  public Task<List<string>> ListAvailableModels(Infrastructure.AppContext ctx)
  {
    DebugLogger.Log("ClaudeModelProvider.ListAvailableModels called");
    return Task.FromResult(new List<string>(KnownModels));
  }

  public (bool isValid, string? error) ValidateModel(string model)
  {
    // Don't pre-reject unknown IDs — Anthropic ships new models between releases,
    // and Claude Code validates the model itself at runtime. Only block empties.
    if (string.IsNullOrWhiteSpace(model))
    {
      return (false, "Model name cannot be empty");
    }

    return (true, null);
  }
}
