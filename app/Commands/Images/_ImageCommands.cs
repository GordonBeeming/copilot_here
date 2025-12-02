using System.CommandLine;

namespace CopilotHere.Commands.Images;

/// <summary>
/// Commands for managing Docker image configurations.
/// </summary>
public sealed partial class ImageCommands : ICommand
{
  public void Configure(RootCommand root)
  {
    root.Add(SetListImagesCommand());
    root.Add(SetShowImageCommand());
    root.Add(SetSetImageCommand());
    root.Add(SetSetImageGlobalCommand());
    root.Add(SetClearImageCommand());
    root.Add(SetClearImageGlobalCommand());
  }
}
