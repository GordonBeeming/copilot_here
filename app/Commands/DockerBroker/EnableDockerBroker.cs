using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetEnableDockerBrokerCommand()
  {
    var command = new Command("--enable-docker-broker", "Enable the brokered Docker socket with local rules (.copilot_here/docker-broker.json)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.EnableLocal(paths);
      Console.WriteLine("✅ Docker broker enabled (local) [BETA]");
      Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetLocalRulesPath(paths)}");
      return 0;
    });
    return command;
  }
}
