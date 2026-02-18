using System.Text;

namespace CopilotHere.Tools;

/// <summary>
/// Echo provider - a test tool that displays configuration without executing any actual AI operations.
/// Perfect for validating the cli_mate configuration chassis.
/// </summary>
public class EchoTool : Infrastructure.ICliTool
{
    public string Name => "echo";
    public string DisplayName => "Echo (Test Provider)";

    public string GetImageName(string tag)
    {
        // Echo uses the same images as GitHub Copilot (copilot-* tags)
        // It doesn't need separate images - it just echoes config instead of running the tool
        const string imagePrefix = "ghcr.io/gordonbeeming/copilot_here";
        var imageTag = string.IsNullOrEmpty(tag) ? "copilot-latest" : $"copilot-{tag}";
        return $"{imagePrefix}:{imageTag}";
    }

    public string GetDockerfile()
    {
        return "docker/echo/Dockerfile";
    }

    public List<string> BuildCommand(Infrastructure.CommandContext ctx)
    {
        var args = new List<string> { "bash", "-c" };
        
        var echoScript = BuildEchoScript(ctx);
        args.Add(echoScript);
        
        return args;
    }

    private string BuildEchoScript(Infrastructure.CommandContext ctx)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("echo '═══════════════════════════════════════════════════════════'");
        sb.AppendLine("echo '🔊 ECHO PROVIDER - Configuration Debug'");
        sb.AppendLine("echo '═══════════════════════════════════════════════════════════'");
        sb.AppendLine("echo ''");
        sb.AppendLine("echo 'This shows what WOULD be executed without --tool echo:'");
        sb.AppendLine("echo ''");
        
        // Show the actual tool that would be used
        sb.AppendLine("echo '📦 DOCKER CONFIGURATION:'");
        sb.AppendLine($"echo '  Image: {GetImageName(ctx.ImageTag ?? "latest")}'");
        sb.AppendLine($"echo '  Tag: {ctx.ImageTag ?? "latest"}'");
        sb.AppendLine("echo ''");
        
        // Build what the actual GitHub Copilot command would be
        sb.AppendLine("echo '🤖 COMMAND THAT WOULD BE EXECUTED:'");
        var actualTool = new GitHubCopilotTool();
        var actualCommand = actualTool.BuildCommand(ctx);
        var commandStr = string.Join(" ", actualCommand.Select(arg => 
            arg.Contains(' ') || arg.Contains('\'') ? $"'{arg.Replace("'", "'\\''")}'" : arg));
        
        // Escape for bash echo
        commandStr = commandStr.Replace("'", "'\\''");
        sb.AppendLine($"echo '  {commandStr}'");
        sb.AppendLine("echo ''");
        
        // Show configuration flags
        sb.AppendLine("echo '⚙️  CONFIGURATION:'");
        sb.AppendLine($"echo '  YOLO Mode: {ctx.IsYolo}'");
        sb.AppendLine($"echo '  Interactive: {ctx.IsInteractive}'");
        sb.AppendLine($"echo '  Model: {ctx.Model ?? "(default)"}'");
        sb.AppendLine("echo ''");
        
        // Show user arguments
        sb.AppendLine("echo '📝 USER ARGUMENTS:'");
        var userArgs = string.Join(" ", ctx.UserArgs);
        if (!string.IsNullOrEmpty(userArgs))
        {
            // Escape single quotes for bash
            userArgs = userArgs.Replace("'", "'\\''");
            sb.AppendLine($"echo '  {userArgs}'");
        }
        else
        {
            sb.AppendLine("echo '  (none)'");
        }
        sb.AppendLine("echo ''");
        
        // Show mounts (from context)
        sb.AppendLine("echo '📂 MOUNTS:'");
        if (ctx.Mounts.Any())
        {
            foreach (var mount in ctx.Mounts.Take(5))
            {
                var escapedMount = mount.Replace("'", "'\\''");
                sb.AppendLine($"echo '  {escapedMount}'");
            }
            
            if (ctx.Mounts.Count > 5)
            {
                sb.AppendLine($"echo '  ... and {ctx.Mounts.Count - 5} more'");
            }
        }
        else
        {
            sb.AppendLine("echo '  (none configured)'");
        }
        sb.AppendLine("echo ''");
        
        // Show environment variables (limited for security)
        sb.AppendLine("echo '🔐 ENVIRONMENT VARIABLES:'");
        if (ctx.Environment.Any())
        {
            foreach (var env in ctx.Environment.Take(5))
            {
                var value = env.Key.Contains("TOKEN") || env.Key.Contains("SECRET") || env.Key.Contains("PASSWORD")
                    ? "***"
                    : env.Value;
                sb.AppendLine($"echo '  {env.Key}={value}'");
            }
            
            if (ctx.Environment.Count > 5)
            {
                sb.AppendLine($"echo '  ... and {ctx.Environment.Count - 5} more'");
            }
        }
        else
        {
            sb.AppendLine("echo '  (none)'");
        }
        
        sb.AppendLine("echo ''");
        sb.AppendLine("echo '═══════════════════════════════════════════════════════════'");
        sb.AppendLine("echo '💡 TIP: Remove --tool echo to execute the actual command'");
        sb.AppendLine("echo '═══════════════════════════════════════════════════════════'");
        
        return sb.ToString();
    }

    public string? GetInteractiveFlag()
    {
        return null; // Echo doesn't have an interactive flag
    }

    public List<string> GetYoloModeFlags()
    {
        return []; // Echo doesn't need special YOLO flags
    }

    public string GetConfigDirName()
    {
        return "cli_mate"; // Echo uses the same config directory
    }

    public string? GetSessionDataPath()
    {
        return null; // Echo doesn't persist session data
    }

    public string GetHostConfigPath(Infrastructure.AppPaths paths)
    {
        return paths.CopilotConfigPath;
    }

    public string GetContainerConfigPath()
    {
        return "/home/appuser/.copilot";
    }

    public Infrastructure.IAuthProvider GetAuthProvider()
    {
        return new EchoAuthProvider();
    }

    public Infrastructure.IModelProvider GetModelProvider()
    {
        return new EchoModelProvider();
    }

    public string[] GetRequiredDependencies()
    {
        return ["docker"]; // Only Docker is required for echo
    }

    public string GetDefaultNetworkRulesPath()
    {
        return "docker/echo/default-airlock-rules.json";
    }

    public bool SupportsModels => true;
    public bool SupportsYoloMode => true;
    public bool SupportsInteractiveMode => true;
}
