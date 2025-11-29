using System.CommandLine;

namespace CopilotHere.Commands.Images;

public sealed partial class ImageCommands
{
  private static readonly string[] AvailableTags =
  [
    "latest",
    "playwright",
    "dotnet",
    "dotnet-8",
    "dotnet-9",
    "dotnet-10",
    "dotnet-playwright"
  ];

  private static Command SetListImagesCommand()
  {
    var command = new Command("--list-images", "List all available Docker images");
    command.Aliases.Add("-ListImages");
    command.SetAction(_ =>
    {
      Console.WriteLine("Available images:");
      Console.WriteLine();
      foreach (var tag in AvailableTags)
      {
        Console.WriteLine($"  ghcr.io/gordonbeeming/copilot_here:{tag}");
      }
      return 0;
    });
    return command;
  }
}
