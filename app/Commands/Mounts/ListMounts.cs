using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Mounts;

public sealed partial class MountCommands
{
  private static Command SetListMountsCommand()
  {
    var command = new Command("--list-mounts", "Show all configured mounts");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = MountsConfig.Load(paths);

      if (config.GlobalMounts.Count == 0 && config.LocalMounts.Count == 0)
      {
        Console.WriteLine("📂 No saved mounts configured");
        Console.WriteLine();
        Console.WriteLine("Add mounts with:");
        Console.WriteLine("  copilot_here --save-mount <path>         # Save to local config");
        Console.WriteLine("  copilot_here --save-mount-global <path>  # Save to global config");
        return 0;
      }

      Console.WriteLine("📂 Saved mounts:");

      foreach (var mount in config.GlobalMounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        Console.WriteLine($"  🌍 {mount.HostPath} ({mode})");
      }

      foreach (var mount in config.LocalMounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        Console.WriteLine($"  📍 {mount.HostPath} ({mode})");
      }

      Console.WriteLine();
      Console.WriteLine("Config files:");
      Console.WriteLine($"  Global: {paths.GetGlobalPath("mounts.conf")}");
      Console.WriteLine($"  Local:  {paths.GetLocalPath("mounts.conf")}");

      return 0;
    });
    return command;
  }
}
