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
    "dotnet-playwright",
    "rust",
    "dotnet-rust",
    "golang",
    "java"
  ];

  private static Command SetListImagesCommand()
  {
    var command = new Command("--list-images", "List all available Docker images");
    command.SetAction(_ =>
    {
      Console.WriteLine("📦 Available Images:");
      foreach (var tag in AvailableTags)
      {
        Console.WriteLine($"  • {tag}");
      }
      Console.WriteLine();
      Console.WriteLine("💡 Use --image <name> to run with any custom Docker image");
      return 0;
    });
    return command;
  }
}
