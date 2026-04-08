using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetEnableGlobalDockerBrokerCommand()
  {
    var command = new Command("--enable-global-docker-broker", "Enable the brokered Docker socket with global rules (~/.config/copilot_here/docker-broker.json)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.EnableGlobal(paths);
      Console.WriteLine("✅ Docker broker enabled (global) [BETA]");
      Console.WriteLine($"   🌍 Rules: {DockerBrokerConfigLoader.GetGlobalRulesPath(paths)}");
      return 0;
    });
    return command;
  }
}
