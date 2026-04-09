using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetAddDockerBrokerImageCommand()
  {
    var command = new Command("--add-docker-broker-image",
      "Add an image glob pattern to the local docker broker allowed_images list (e.g. 'mcr.microsoft.com/mssql/server:*')");

    var patternArg = new Argument<string>("pattern")
    {
      Description = "Image glob pattern. Use '*' to match any sequence of characters."
    };
    command.Add(patternArg);

    command.SetAction(parseResult =>
    {
      var pattern = parseResult.GetValue(patternArg)!;
      var paths = AppPaths.Resolve();
      var added = DockerBrokerConfigLoader.AddImageLocal(paths, pattern);
      if (added)
      {
        Console.WriteLine($"✅ Added trusted image pattern (local): {pattern}");
        Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetLocalRulesPath(paths)}");
      }
      else
      {
        Console.WriteLine($"ℹ️  Pattern already present (local): {pattern}");
      }
      return 0;
    });
    return command;
  }
}
