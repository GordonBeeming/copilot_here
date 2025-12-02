using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

public sealed partial class AirlockCommands
{
  private static Command SetShowAirlockRulesCommand()
  {
    var command = new Command("--show-airlock-rules", "Show current Airlock proxy rules");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();

      Console.WriteLine("📋 Airlock Proxy Rules");
      Console.WriteLine("======================");
      Console.WriteLine();

      var defaultRulesPath = paths.GetGlobalPath("default-airlock-rules.json");
      if (File.Exists(defaultRulesPath))
      {
        Console.WriteLine("📦 Default Rules:");
        Console.WriteLine($"   {defaultRulesPath}");
        foreach (var line in File.ReadAllLines(defaultRulesPath))
        {
          Console.WriteLine($"   {line}");
        }
        Console.WriteLine();
      }
      else
      {
        Console.WriteLine("📦 Default Rules: Not found");
        Console.WriteLine();
      }

      var globalRulesPath = AirlockConfig.GetGlobalRulesPath(paths);
      if (File.Exists(globalRulesPath))
      {
        Console.WriteLine("🌍 Global Config:");
        Console.WriteLine($"   {globalRulesPath}");
        foreach (var line in File.ReadAllLines(globalRulesPath))
        {
          Console.WriteLine($"   {line}");
        }
        Console.WriteLine();
      }
      else
      {
        Console.WriteLine("🌍 Global Config: Not configured");
        Console.WriteLine();
      }

      var localRulesPath = AirlockConfig.GetLocalRulesPath(paths);
      if (File.Exists(localRulesPath))
      {
        Console.WriteLine("📁 Local Config:");
        Console.WriteLine($"   {localRulesPath}");
        foreach (var line in File.ReadAllLines(localRulesPath))
        {
          Console.WriteLine($"   {line}");
        }
        Console.WriteLine();
      }
      else
      {
        Console.WriteLine("📁 Local Config: Not configured");
        Console.WriteLine();
      }

      return 0;
    });
    return command;
  }
}
