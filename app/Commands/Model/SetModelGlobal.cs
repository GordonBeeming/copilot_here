using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Model;

public sealed partial class ModelCommands
{
  private static Command SetSetModelGlobalCommand()
  {
    var command = new Command("--set-model-global", "Set default model in global config");

    var modelArg = new Argument<string>("model") { Description = "Model ID to set" };
    command.Add(modelArg);

    command.SetAction(parseResult =>
    {
      var model = parseResult.GetValue(modelArg)!;
      var paths = AppPaths.Resolve();
      ModelConfig.SaveGlobal(paths, model);
      Console.WriteLine($"✅ Set global model: {model}");
      return 0;
    });
    return command;
  }
}
