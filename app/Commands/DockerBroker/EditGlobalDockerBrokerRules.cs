using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetEditGlobalDockerBrokerRulesCommand()
  {
    var command = new Command("--edit-global-docker-broker-rules", "Edit global Docker broker rules in $EDITOR");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var rulesPath = DockerBrokerConfigLoader.GetGlobalRulesPath(paths);
      OpenInEditor(rulesPath);
      return 0;
    });
    return command;
  }
}
