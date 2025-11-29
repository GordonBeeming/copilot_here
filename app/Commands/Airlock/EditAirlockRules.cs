using System.CommandLine;
using System.Diagnostics;
using CopilotHere.Infrastructure;

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
      OpenInEditor(rulesPath);
      return 0;
    });
    return command;
  }

  private static void OpenInEditor(string filePath)
  {
    var editor = Environment.GetEnvironmentVariable("EDITOR")
              ?? Environment.GetEnvironmentVariable("VISUAL")
              ?? "vi";

    // Create file with default content if it doesn't exist
    if (!File.Exists(filePath))
    {
      var defaultContent = """
        {
          "rules": [
            { "action": "allow", "host": "*.github.com" },
            { "action": "allow", "host": "*.githubusercontent.com" },
            { "action": "allow", "host": "api.githubcopilot.com" },
            { "action": "deny", "host": "*" }
          ]
        }
        """;
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
}
