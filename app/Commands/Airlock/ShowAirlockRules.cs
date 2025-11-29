using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetShowAirlockRulesCommand()
  {
    var command = new Command("--show-airlock-rules", "Show current Airlock proxy rules");
    command.Aliases.Add("-ShowAirlockRules");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = AirlockConfig.Load(paths);

      Console.WriteLine("Airlock configuration:");
      Console.WriteLine();
      Console.WriteLine($"  Enabled: {config.Enabled} (from {config.EnabledSource})");

      if (config.RulesPath is not null)
      {
        Console.WriteLine($"  Rules:   {config.RulesPath} (from {config.RulesSource})");
        Console.WriteLine();

        if (File.Exists(config.RulesPath))
        {
          Console.WriteLine("  Content:");
          var content = File.ReadAllText(config.RulesPath);
          foreach (var line in content.Split('\n'))
          {
            Console.WriteLine($"    {line}");
          }
        }
      }
      else
      {
        Console.WriteLine("  Rules:   Not configured");
      }
      return 0;
    });
    return command;
  }
}
