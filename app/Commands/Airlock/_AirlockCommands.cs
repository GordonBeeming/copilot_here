using System.CommandLine;
using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Commands for managing the Airlock network proxy.
/// </summary>
public sealed partial class AirlockCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetEnableAirlockCommand());
    root.Add(SetEnableGlobalAirlockCommand());
    root.Add(SetDisableAirlockCommand());
    root.Add(SetDisableGlobalAirlockCommand());
    root.Add(SetShowAirlockRulesCommand());
    root.Add(SetEditAirlockRulesCommand());
    root.Add(SetEditGlobalAirlockRulesCommand());
  }

  /// <summary>
  /// Runs an Airlock toggle and reports which file it wrote and how.
  /// </summary>
  /// <param name="toggle">Returns the outcome and the path it wrote.</param>
  private static int RunToggle(string successMessage, Func<AppPaths, (AirlockToggleOutcome Outcome, string Path)> toggle)
  {
    var paths = AppPaths.Resolve();

    try
    {
      var (outcome, path) = toggle(paths);

      Console.WriteLine(successMessage);
      Console.WriteLine($"   📁 Rules: {path}");

      if (outcome == AirlockToggleOutcome.SeededFromGlobal)
      {
        Console.WriteLine($"   ↳ seeded from global config ({paths.GetGlobalPath("network.json")})");
        Console.WriteLine("     Local config replaces global entirely, so its rules were copied across.");
      }

      return 0;
    }
    catch (JsonException ex)
    {
      Console.Error.WriteLine("❌ Could not update the Airlock config.");
      Console.Error.WriteLine($"   {ex.Message}");
      Console.Error.WriteLine("   No changes were written.");
      return 1;
    }
  }
}
