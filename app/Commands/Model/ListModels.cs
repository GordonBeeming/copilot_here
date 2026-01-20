using System.CommandLine;
using System.Text.RegularExpressions;
using CopilotHere.Infrastructure;
using AppContext = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetListModelsCommand()
  {
    var command = new Command("--list-models", "List available AI models from GitHub Copilot CLI");
    command.SetAction(_ =>
    {
      Console.WriteLine("🤖 Fetching available models...");
      Console.WriteLine();
      
      var ctx = AppContext.Create();
      var imageTag = ctx.ImageConfig.Tag;
      var imageName = ctx.ActiveTool.GetImageName(imageTag);
      
      // Pull image if needed (quietly)
      if (!DockerRunner.PullImage(imageName))
      {
        Console.WriteLine("❌ Failed to pull Docker image");
        return 1;
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
      
      DebugLogger.Log("Running: docker run ... copilot --model invalid-model-to-trigger-list");
      var (exitCode, stdout, stderr) = DockerRunner.RunAndCapture(args);
      
      DebugLogger.Log($"Exit code: {exitCode}");
      DebugLogger.Log($"stderr: {stderr}");
      DebugLogger.Log($"stdout: {stdout}");
      
      // Parse the error message to extract model list
      var models = ParseModelListFromError(stderr);
      
      if (models.Count == 0)
      {
        // Fallback: show instructions if parsing fails
        Console.WriteLine("❌ Could not parse model list from Copilot CLI output");
        Console.WriteLine();
        Console.WriteLine("To see available models:");
        Console.WriteLine("  1. Run: copilot_here");
        Console.WriteLine("  2. Type: /model");
        Console.WriteLine();
        Console.WriteLine("Raw error output:");
        Console.WriteLine(stderr);
        return 1;
      }
      
      Console.WriteLine("Available models:");
      foreach (var model in models)
      {
        Console.WriteLine($"  • {model}");
      }
      Console.WriteLine();
      Console.WriteLine("💡 Set your preferred model:");
      Console.WriteLine("   copilot_here --set-model <model-id>       (local project)");
      Console.WriteLine("   copilot_here --set-model-global <model-id> (all projects)");
      
      return 0;
    });
    return command;
  }
  
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
