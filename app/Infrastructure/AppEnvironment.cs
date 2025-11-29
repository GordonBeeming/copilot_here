namespace CopilotHere.Infrastructure;

/// <summary>
/// Runtime environment information (auth, user IDs, etc.).
/// Separate from paths and config - this is system state.
/// </summary>
public sealed record AppEnvironment
{
  /// <summary>GitHub authentication token.</summary>
  public required string GitHubToken { get; init; }

  /// <summary>User ID for container permissions.</summary>
  public required string UserId { get; init; }

  /// <summary>Group ID for container permissions.</summary>
  public required string GroupId { get; init; }

  /// <summary>Whether the terminal supports emoji.</summary>
  public required bool SupportsEmoji { get; init; }

  /// <summary>Creates AppEnvironment with all runtime info resolved.</summary>
  public static AppEnvironment Resolve()
  {
    return new AppEnvironment
    {
      GitHubToken = GitHubAuth.GetToken(),
      UserId = SystemInfo.GetUserId(),
      GroupId = SystemInfo.GetGroupId(),
      SupportsEmoji = SystemInfo.SupportsEmoji()
    };
  }
}
