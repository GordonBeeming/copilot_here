using System.CommandLine;
using System.Diagnostics;
using CopilotHere.Infrastructure;
using AppCtx = CopilotHere.Infrastructure.AppContext;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetEditAirlockRulesCommand()
  {
    var command = new Command("--edit-airlock-rules", "Edit local Airlock rules in $EDITOR");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var rulesPath = AirlockConfig.GetLocalRulesPath(paths);
      var ctx = AppCtx.Create();
      OpenInEditor(rulesPath, ctx.ActiveTool);
      return 0;
    });
    return command;
  }

  private static void OpenInEditor(string filePath, ICliTool tool)
  {
    var editor = Environment.GetEnvironmentVariable("EDITOR")
              ?? Environment.GetEnvironmentVariable("VISUAL")
              ?? "vi";

    // Create file with default content if it doesn't exist
    if (!File.Exists(filePath))
    {
      var defaultContent = LoadDefaultRules(tool);
      File.WriteAllText(filePath, defaultContent);
    }

    try
    {
      var startInfo = new ProcessStartInfo(editor, filePath)
      {
        UseShellExecute = false
      };
      using var process = Process.Start(startInfo);
      process?.WaitForExit();
    }
    catch (Exception ex)
    {
      Console.WriteLine($"❌ Failed to open editor: {ex.Message}");
      Console.WriteLine($"   File location: {filePath}");
    }
  }

  private static string LoadDefaultRules(ICliTool tool)
  {
    var defaultPath = ResolveToolRulesPath(tool.GetDefaultNetworkRulesPath());
    if (!string.IsNullOrEmpty(defaultPath) && File.Exists(defaultPath))
    {
      return File.ReadAllText(defaultPath);
    }

    return """
      {
        "enabled": false,
        "rules": [
          { "action": "deny", "host": "*" }
        ]
      }
      """;
  }

  private static string? ResolveToolRulesPath(string configuredPath)
  {
    if (string.IsNullOrWhiteSpace(configuredPath))
      return null;

    if (Path.IsPathRooted(configuredPath))
      return configuredPath;

    var cwdPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
    if (File.Exists(cwdPath))
      return cwdPath;

    var appBasePath = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, configuredPath));
    if (File.Exists(appBasePath))
      return appBasePath;

    return null;
  }
}
