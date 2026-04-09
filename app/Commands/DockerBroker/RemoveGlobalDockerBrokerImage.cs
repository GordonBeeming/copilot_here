using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetRemoveGlobalDockerBrokerImageCommand()
  {
    var command = new Command("--remove-global-docker-broker-image",
      "Remove an image glob pattern from the global docker broker allowed_images list");

    var patternArg = new Argument<string>("pattern")
    {
      Description = "Image glob pattern to remove (must match exactly)"
    };
    command.Add(patternArg);

    command.SetAction(parseResult =>
    {
      var pattern = parseResult.GetValue(patternArg)!;
      var paths = AppPaths.Resolve();
      var removed = DockerBrokerConfigLoader.RemoveImageGlobal(paths, pattern);
      if (removed)
      {
        Console.WriteLine($"✅ Removed trusted image pattern (global): {pattern}");
      }
      else
      {
        Console.WriteLine($"ℹ️  Pattern not found in global config: {pattern}");
      }
      return 0;
    });
    return command;
  }
}
