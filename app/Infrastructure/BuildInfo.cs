namespace CopilotHere.Infrastructure;

/// <summary>
/// Provides build-time information for the application.
/// The BuildDate is stamped during CI/CD from the VERSION file via scripts/stamp-version.sh.
/// </summary>
public static class BuildInfo
{
  /// <summary>
  /// The build date in yyyy.MM.dd or yyyy.MM.dd.N format.
  /// This is replaced during build via MSBuild property.
  /// </summary>
  public const string BuildDate = "2026.04.29";
}
