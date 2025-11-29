using System.CommandLine;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Run;

/// <summary>
/// Main command that runs the Copilot CLI in a Docker container.
/// This is the default command when no subcommand is specified.
/// </summary>
public sealed class RunCommand : ICommand
{
  private readonly bool _isYolo;

  // === IMAGE SELECTION OPTIONS ===
  private readonly Option<bool> _dotnetOption;
  private readonly Option<bool> _dotnet8Option;
  private readonly Option<bool> _dotnet9Option;
  private readonly Option<bool> _dotnet10Option;
  private readonly Option<bool> _playwrightOption;
  private readonly Option<bool> _dotnetPlaywrightOption;
  private readonly Option<bool> _rustOption;
  private readonly Option<bool> _dotnetRustOption;

  // === MOUNT OPTIONS ===
  private readonly Option<string[]> _mountOption;
  private readonly Option<string[]> _mountRwOption;

  // === EXECUTION OPTIONS ===
  private readonly Option<bool> _noCleanupOption;
  private readonly Option<bool> _noPullOption;

  // === HELP OPTIONS ===
  private readonly Option<bool> _help2Option;

  // === SELF-UPDATE OPTIONS ===
  private readonly Option<bool> _updateScriptsOption;

  public RunCommand(bool isYolo = false)
  {
    _isYolo = isYolo;

    // Note on aliases:
    // - Primary aliases use standard Unix conventions (--long)
    // - Short aliases like -d, -d8, -d9 are handled in Program.NormalizeArgs
    // - PowerShell-style aliases are also handled in Program.NormalizeArgs for backwards compatibility
    // - Descriptions show available short aliases in brackets

    _dotnetOption = new Option<bool>("--dotnet") { Description = "[-d] Use .NET image variant (all versions)" };

    _dotnet8Option = new Option<bool>("--dotnet8") { Description = "[-d8] Use .NET 8 image variant" };

    _dotnet9Option = new Option<bool>("--dotnet9") { Description = "[-d9] Use .NET 9 image variant" };

    _dotnet10Option = new Option<bool>("--dotnet10") { Description = "[-d10] Use .NET 10 image variant" };

    _playwrightOption = new Option<bool>("--playwright") { Description = "[-pw] Use Playwright image variant" };

    _dotnetPlaywrightOption = new Option<bool>("--dotnet-playwright") { Description = "[-dp] Use .NET + Playwright image variant" };

    _rustOption = new Option<bool>("--rust") { Description = "[-rs] Use Rust image variant" };

    _dotnetRustOption = new Option<bool>("--dotnet-rust") { Description = "[-dr] Use .NET + Rust image variant" };

    _mountOption = new Option<string[]>("--mount") { Description = "Mount additional directory (read-only)" };

    _mountRwOption = new Option<string[]>("--mount-rw") { Description = "Mount additional directory (read-write)" };

    _noCleanupOption = new Option<bool>("--no-cleanup") { Description = "Skip cleanup of unused Docker images" };

    _noPullOption = new Option<bool>("--no-pull") { Description = "Skip pulling the latest image" };
    _noPullOption.Aliases.Add("--skip-pull");

    _help2Option = new Option<bool>("--help2") { Description = "Show GitHub Copilot CLI native help" };

    _updateScriptsOption = new Option<bool>("--update") { Description = "[-u] Update from GitHub repository" };
  }

  public void Configure(RootCommand root)
  {
    root.Add(_dotnetOption);
    root.Add(_dotnet8Option);
    root.Add(_dotnet9Option);
    root.Add(_dotnet10Option);
    root.Add(_playwrightOption);
    root.Add(_dotnetPlaywrightOption);
    root.Add(_rustOption);
    root.Add(_dotnetRustOption);
    root.Add(_mountOption);
    root.Add(_mountRwOption);
    root.Add(_noCleanupOption);
    root.Add(_noPullOption);
    root.Add(_help2Option);
    root.Add(_updateScriptsOption);

    root.SetAction(parseResult =>
    {
      var ctx = Infrastructure.AppContext.Create();

      var dotnet = parseResult.GetValue(_dotnetOption);
      var dotnet8 = parseResult.GetValue(_dotnet8Option);
      var dotnet9 = parseResult.GetValue(_dotnet9Option);
      var dotnet10 = parseResult.GetValue(_dotnet10Option);
      var playwright = parseResult.GetValue(_playwrightOption);
      var dotnetPlaywright = parseResult.GetValue(_dotnetPlaywrightOption);
      var rust = parseResult.GetValue(_rustOption);
      var dotnetRust = parseResult.GetValue(_dotnetRustOption);
      var cliMountsRo = parseResult.GetValue(_mountOption) ?? [];
      var cliMountsRw = parseResult.GetValue(_mountRwOption) ?? [];
      var noCleanup = parseResult.GetValue(_noCleanupOption);
      var noPull = parseResult.GetValue(_noPullOption);
      var help2 = parseResult.GetValue(_help2Option);
      var updateScripts = parseResult.GetValue(_updateScriptsOption);

      // Handle --help2 (native copilot help)
      if (help2)
      {
        Console.WriteLine("coming soon - help2 (native copilot help)");
        return 0;
      }

      // Handle --update-scripts
      if (updateScripts)
      {
        Console.WriteLine("coming soon - update scripts");
        return 0;
      }

      // Determine image tag (CLI overrides config)
      var imageTag = ctx.ImageConfig.Tag;
      var imageSource = ctx.ImageConfig.Source;

      if (dotnet) { imageTag = "dotnet"; imageSource = ImageConfigSource.CommandLine; }
      else if (dotnet8) { imageTag = "dotnet-8"; imageSource = ImageConfigSource.CommandLine; }
      else if (dotnet9) { imageTag = "dotnet-9"; imageSource = ImageConfigSource.CommandLine; }
      else if (dotnet10) { imageTag = "dotnet-10"; imageSource = ImageConfigSource.CommandLine; }
      else if (playwright) { imageTag = "playwright"; imageSource = ImageConfigSource.CommandLine; }
      else if (dotnetPlaywright) { imageTag = "dotnet-playwright"; imageSource = ImageConfigSource.CommandLine; }
      else if (rust) { imageTag = "rust"; imageSource = ImageConfigSource.CommandLine; }
      else if (dotnetRust) { imageTag = "dotnet-rust"; imageSource = ImageConfigSource.CommandLine; }

      // Collect all mounts: config mounts + CLI mounts
      var allMounts = new List<MountEntry>();

      // Add config mounts (global + local)
      allMounts.AddRange(ctx.MountsConfig.AllConfigMounts);

      // Add CLI mounts
      foreach (var path in cliMountsRo)
        allMounts.Add(new MountEntry(path, IsReadWrite: false, MountSource.CommandLine));
      foreach (var path in cliMountsRw)
        allMounts.Add(new MountEntry(path, IsReadWrite: true, MountSource.CommandLine));

      Console.WriteLine("coming soon - run copilot");
      Console.WriteLine($"  Image: {imageTag} (from {imageSource})");
      Console.WriteLine($"  Mounts: {allMounts.Count} total");
      foreach (var mount in allMounts)
      {
        var mode = mount.IsReadWrite ? "rw" : "ro";
        Console.WriteLine($"    [{mount.Source}] {mount.Path} ({mode})");
      }
      Console.WriteLine($"  Airlock: {(ctx.AirlockConfig.Enabled ? "enabled" : "disabled")} (from {ctx.AirlockConfig.EnabledSource})");
      Console.WriteLine($"  Skip cleanup: {noCleanup}");
      Console.WriteLine($"  Skip pull: {noPull}");

      return 0;
    });
  }
}
