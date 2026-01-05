using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetSetModelCommand()
  {
    var command = new Command("--set-model", "Set default model in local config");

    var modelArg = new Argument<string>("model") { Description = "Model ID to set" };
    command.Add(modelArg);

    command.SetAction(parseResult =>
    {
      var model = parseResult.GetValue(modelArg)!;
      var paths = AppPaths.Resolve();
      ModelConfig.SaveLocal(paths, model);
      Console.WriteLine($"✅ Set local model: {model}");
      return 0;
    });
    return command;
  }
}
