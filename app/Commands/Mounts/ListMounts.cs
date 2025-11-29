using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Mounts;

public sealed partial class MountCommands
{
  private static Command SetListMountsCommand()
  {
    var command = new Command("--list-mounts", "Show all configured mounts");
    command.Aliases.Add("-ListMounts");
    command.SetAction(_ =>
    {
      var paths = AppPaths.Resolve();
      var config = MountsConfig.Load(paths);

      Console.WriteLine("Configured mounts:");
      Console.WriteLine();

      if (config.GlobalMounts.Count > 0)
      {
        Console.WriteLine("Global (~/.config/copilot_here/mounts.conf):");
        foreach (var mount in config.GlobalMounts)
        {
          var mode = mount.IsReadWrite ? "rw" : "ro";
          Console.WriteLine($"  {mount.Path} ({mode})");
        }
        Console.WriteLine();
      }

      if (config.LocalMounts.Count > 0)
      {
        Console.WriteLine("Local (.copilot_here/mounts.conf):");
        foreach (var mount in config.LocalMounts)
        {
          var mode = mount.IsReadWrite ? "rw" : "ro";
          Console.WriteLine($"  {mount.Path} ({mode})");
        }
        Console.WriteLine();
      }

      if (config.GlobalMounts.Count == 0 && config.LocalMounts.Count == 0)
      {
        Console.WriteLine("  No mounts configured.");
      }

      return 0;
    });
    return command;
  }
}
