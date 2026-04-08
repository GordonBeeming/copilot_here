using System.CommandLine;
using CopilotHere.Commands;

namespace CopilotHere.Commands.DockerBroker;

/// <summary>
/// Commands for managing the brokered Docker socket (--dind feature).
/// Mirrors the AirlockCommands pattern.
/// </summary>
public sealed partial class DockerBrokerCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetEnableDockerBrokerCommand());
    root.Add(SetEnableGlobalDockerBrokerCommand());
    root.Add(SetDisableDockerBrokerCommand());
    root.Add(SetDisableGlobalDockerBrokerCommand());
    root.Add(SetShowDockerBrokerRulesCommand());
    root.Add(SetEditDockerBrokerRulesCommand());
    root.Add(SetEditGlobalDockerBrokerRulesCommand());
  }
}
