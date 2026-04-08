using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetDisableGlobalDockerBrokerCommand()
  {
    var command = new Command("--disable-global-docker-broker", "Disable the brokered Docker socket for global config");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.DisableGlobal(paths);
      Console.WriteLine("✅ Docker broker disabled (global)");
      return 0;
    });
    return command;
  }
}
