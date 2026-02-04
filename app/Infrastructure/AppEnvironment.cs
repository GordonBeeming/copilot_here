namespace CopilotHere.Infrastructure;

/// <summary>
/// Runtime environment information (user IDs, terminal capabilities, etc.).
/// Separate from paths and config - this is system state.
/// </summary>
public sealed record AppEnvironment
{
  /// <summary>User ID for container permissions.</summary>
  public required string UserId { get; init; }

  /// <summary>Group ID for container permissions.</summary>
  public required string GroupId { get; init; }

  /// <summary>Whether the terminal supports emoji.</summary>
  public required bool SupportsEmoji { get; init; }

  /// <summary>Whether the terminal supports emoji variation selectors (U+FE0F).</summary>
  public required bool SupportsEmojiVariationSelectors { get; init; }

  /// <summary>Creates AppEnvironment with all runtime info resolved.</summary>
  public static AppEnvironment Resolve()
  {
    return new AppEnvironment
    {
      UserId = SystemInfo.GetUserId(),
      GroupId = SystemInfo.GetGroupId(),
      SupportsEmoji = SystemInfo.SupportsEmoji(),
      SupportsEmojiVariationSelectors = SystemInfo.SupportsEmojiVariationSelectors()
    };
  }
}
