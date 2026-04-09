using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.DockerBroker;

public sealed partial class DockerBrokerCommands
{
  private static Command SetAddGlobalDockerBrokerImageCommand()
  {
    var command = new Command("--add-global-docker-broker-image",
      "Add an image glob pattern to the global docker broker allowed_images list");

    var patternArg = new Argument<string>("pattern")
    {
      Description = "Image glob pattern. Use '*' to match any sequence of characters."
    };
    command.Add(patternArg);

    command.SetAction(parseResult =>
    {
      var pattern = parseResult.GetValue(patternArg)!;
      var paths = AppPaths.Resolve();
      var added = DockerBrokerConfigLoader.AddImageGlobal(paths, pattern);
      if (added)
      {
        Console.WriteLine($"✅ Added trusted image pattern (global): {pattern}");
        Console.WriteLine($"   📁 Rules: {DockerBrokerConfigLoader.GetGlobalRulesPath(paths)}");
      }
      else
      {
        Console.WriteLine($"ℹ️  Pattern already present (global): {pattern}");
      }
      return 0;
    });
    return command;
  }
}
