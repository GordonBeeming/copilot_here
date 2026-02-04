using System.Text.RegularExpressions;
using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// Model provider for GitHub Copilot CLI.
/// Handles listing and validating AI models.
/// </summary>
public sealed partial class GitHubCopilotModelProvider : IModelProvider
{
  public Task<List<string>> ListAvailableModels(Infrastructure.AppContext ctx)
  {
    DebugLogger.Log("GitHubCopilotModelProvider.ListAvailableModels called");
    
    var imageTag = ctx.ImageConfig.Tag;
    var imageName = ctx.ActiveTool.GetImageName(imageTag);
    
    // Pull image if needed (quietly)
    if (!ContainerRunner.PullImage(ctx.RuntimeConfig, imageName))
    {
      DebugLogger.Log("Failed to pull image");
      return Task.FromResult(new List<string>());
    }
    
    // Run copilot with an invalid model to trigger error that lists valid models
    var args = new List<string>
    {
      "run",
      "--rm",
      "--env", $"GH_TOKEN={ctx.Environment.GitHubToken}",
      imageName,
      "copilot",  // Just "copilot", not "gh copilot"
      "--model", "invalid-model-to-trigger-list"
    };
    
    DebugLogger.Log($"Running: {ctx.RuntimeConfig.Runtime} run ... copilot --model invalid-model-to-trigger-list");
    var (exitCode, stdout, stderr) = ContainerRunner.RunAndCapture(ctx.RuntimeConfig, args);
    
    DebugLogger.Log($"Exit code: {exitCode}");
    DebugLogger.Log($"stderr: {stderr}");
    DebugLogger.Log($"stdout: {stdout}");
    
    // Parse the error message to extract model list
    return Task.FromResult(ParseModelListFromError(stderr));
  }

  public (bool isValid, string? error) ValidateModel(string model)
  {
    // For GitHub Copilot, we don't pre-validate models
    // Let the CLI validate them at runtime
    if (string.IsNullOrWhiteSpace(model))
    {
      return (false, "Model name cannot be empty");
    }
    
    return (true, null);
  }

  // === PRIVATE HELPER METHODS ===

  private static List<string> ParseModelListFromError(string errorOutput)
  {
    var models = new List<string>();
    
    // Look for "Allowed choices are model1, model2, model3."
    // Need to handle dots in model names (e.g., gpt-5.1)
    // Match everything after "Allowed choices are" until period followed by newline or end
    var match = Regex.Match(errorOutput, @"Allowed choices are\s+(.+?)\.(?:\s|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    if (match.Success)
    {
      var modelString = match.Groups[1].Value;
      DebugLogger.Log($"Captured model string: '{modelString}'");
      
      // Split by comma
      var parts = modelString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      foreach (var part in parts)
      {
        var cleaned = part.Trim();
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
          models.Add(cleaned);
          DebugLogger.Log($"Added model: '{cleaned}'");
        }
      }
      return models;
    }
    
    DebugLogger.Log("'Allowed choices are' pattern did not match");
    
    // Fallback: Look for patterns like "valid values are:" or "available models:"
    match = Regex.Match(errorOutput, @"(?:valid|available)[^:]*:\s*(.+)", RegexOptions.IgnoreCase);
    if (match.Success)
    {
      var modelString = match.Groups[1].Value;
      var parts = Regex.Split(modelString, @"[,;\n]");
      foreach (var part in parts)
      {
        var cleaned = part.Trim().Trim('"', '\'', '`', '.', ' ');
        if (!string.IsNullOrWhiteSpace(cleaned) && 
            !cleaned.Contains("and") && 
            !cleaned.Contains("or") &&
            cleaned.Length < 50)
        {
          models.Add(cleaned);
        }
      }
    }
    
    return models.Distinct().ToList();
  }
}
