using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetDisableDockerBrokerCommand()
  {
    var command = new Command("--disable-docker-broker", "Disable the brokered Docker socket for local config");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.DisableLocal(paths);
      Console.WriteLine("✅ Docker broker disabled (local)");
      return 0;
    });
    return command;
  }
}
