using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// GitHub Copilot CLI tool provider.
/// </summary>
public sealed class GitHubCopilotTool : ICliTool
{
  private readonly IAuthProvider _authProvider = new GitHubAuthProvider();
  private readonly IModelProvider _modelProvider = new GitHubCopilotModelProvider();

  public string Name => "github-copilot";
  
  public string DisplayName => "GitHub Copilot CLI";

  public string GetImageName(string tag)
  {
    // Image name format: ghcr.io/gordonbeeming/copilot_here:copilot-{variant}
    // Tag matches what users invoke: "copilot" (not "github-copilot")
    const string imagePrefix = "ghcr.io/gordonbeeming/copilot_here";
    var imageTag = string.IsNullOrEmpty(tag) ? "copilot-latest" : $"copilot-{tag}";
    return $"{imagePrefix}:{imageTag}";
  }

  public string GetDockerfile()
  {
    return "docker/tools/github-copilot/Dockerfile";
  }

  public List<string> BuildCommand(CommandContext context)
  {
    var args = new List<string> { "copilot" };

    // Add YOLO mode flags
    if (context.IsYolo)
    {
      args.Add("--allow-all-tools");
      args.Add("--allow-all-paths");
    }

    // Add model if specified
    if (!string.IsNullOrEmpty(context.Model))
    {
      args.Add("--model");
      args.Add(context.Model);
    }

    // Add user arguments
    args.AddRange(context.UserArgs);

    // If no args (interactive mode), add --banner
    // Check count excluding model args since model doesn't mean non-interactive
    var argsWithoutModel = args.Count;
    if (!string.IsNullOrEmpty(context.Model))
      argsWithoutModel -= 2; // Subtract --model and its value
    
    if (argsWithoutModel == 1 || (argsWithoutModel <= 3 && context.IsYolo))
    {
      args.Add("--banner");
    }

    return args;
  }

  public List<string> GetYoloModeFlags()
  {
    return ["--allow-all-tools", "--allow-all-paths"];
  }

  public string GetInteractiveFlag()
  {
    return "--banner";
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
    return ["docker", "gh"];
  }

  public string GetConfigDirName()
  {
    return ".copilot";
  }

  public string? GetSessionDataPath()
  {
    // GitHub Copilot stores session data in ~/.copilot
    return null;
  }

  public string GetDefaultNetworkRulesPath()
  {
    return "docker/tools/github-copilot/default-airlock-rules.json";
  }

  public bool SupportsModels => true;
  
  public bool SupportsYoloMode => true;
  
  public bool SupportsInteractiveMode => true;
}
