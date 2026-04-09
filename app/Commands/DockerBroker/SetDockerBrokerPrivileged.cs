using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  // Local: allow privileged siblings (flips body_inspection.reject_privileged to false)
  private static Command SetAllowPrivilegedDockerBrokerCommand()
  {
    var command = new Command("--allow-privileged-docker-broker",
      "Allow spawned siblings to request HostConfig.Privileged=true (local config)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.SetRejectPrivilegedLocal(paths, reject: false);
      Console.WriteLine("⚠️  Privileged sibling containers ALLOWED (local)");
      Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetLocalRulesPath(paths)}");
      Console.WriteLine("   Spawned containers can now request --privileged. Pair this with --add-docker-broker-image to limit blast radius.");
      return 0;
    });
    return command;
  }

  // Local: deny privileged siblings (default)
  private static Command SetDenyPrivilegedDockerBrokerCommand()
  {
    var command = new Command("--deny-privileged-docker-broker",
      "Reject any spawned sibling that requests HostConfig.Privileged=true (local config, default)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.SetRejectPrivilegedLocal(paths, reject: true);
      Console.WriteLine("✅ Privileged sibling containers DENIED (local)");
      Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetLocalRulesPath(paths)}");
      return 0;
    });
    return command;
  }

  // Global variants
  private static Command SetAllowPrivilegedGlobalDockerBrokerCommand()
  {
    var command = new Command("--allow-privileged-global-docker-broker",
      "Allow spawned siblings to request HostConfig.Privileged=true (global config)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.SetRejectPrivilegedGlobal(paths, reject: false);
      Console.WriteLine("⚠️  Privileged sibling containers ALLOWED (global)");
      Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetGlobalRulesPath(paths)}");
      return 0;
    });
    return command;
  }

  private static Command SetDenyPrivilegedGlobalDockerBrokerCommand()
  {
    var command = new Command("--deny-privileged-global-docker-broker",
      "Reject any spawned sibling that requests HostConfig.Privileged=true (global config, default)");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      DockerBrokerConfigLoader.SetRejectPrivilegedGlobal(paths, reject: true);
      Console.WriteLine("✅ Privileged sibling containers DENIED (global)");
      Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetGlobalRulesPath(paths)}");
      return 0;
    });
    return command;
  }
}
