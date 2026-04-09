using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetEditDockerBrokerRulesCommand()
  {
    var command = new Command("--edit-docker-broker-rules", "Edit local Docker broker rules in $EDITOR");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var rulesPath = DockerBrokerConfigLoader.GetLocalRulesPath(paths);
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

    if (!File.Exists(filePath))
    {
      var defaults = DockerBrokerConfigLoader.LoadDefaultRules() ?? DockerBrokerConfig.CreateDefault(enabled: true);
      DockerBrokerConfigLoader.WriteConfig(filePath, defaults);
    }

    try
    {
      var startInfo = new ProcessStartInfo(editor, filePath) { UseShellExecute = false };
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
