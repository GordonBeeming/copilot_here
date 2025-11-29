using CopilotHere.Commands.Airlock;
using CopilotHere.Commands.Images;
using CopilotHere.Commands.Mounts;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Complete application context combining paths, environment, and loaded configs.
/// This is created once at startup and passed to all commands.
/// </summary>
public sealed record AppContext
{
  /// <summary>All resolved file paths.</summary>
  public required AppPaths Paths { get; init; }

  /// <summary>Runtime environment (auth, user IDs, etc.).</summary>
  public required AppEnvironment Environment { get; init; }

  /// <summary>Image configuration (loaded from config files).</summary>
  public required ImageConfig ImageConfig { get; init; }

  /// <summary>Mounts configuration (loaded from config files).</summary>
  public required MountsConfig MountsConfig { get; init; }

  /// <summary>Airlock configuration (loaded from config files).</summary>
  public required AirlockConfig AirlockConfig { get; init; }

  /// <summary>Creates an AppContext with all state resolved and configs loaded.</summary>
  public static AppContext Create()
  {
    var paths = AppPaths.Resolve();
    var environment = AppEnvironment.Resolve();

    return new AppContext
    {
      Paths = paths,
      Environment = environment,
      ImageConfig = ImageConfig.Load(paths),
      MountsConfig = MountsConfig.Load(paths),
      AirlockConfig = AirlockConfig.Load(paths)
    };
  }
}
