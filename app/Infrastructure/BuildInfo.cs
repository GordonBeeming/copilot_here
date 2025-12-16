namespace CopilotHere.Infrastructure;

/// <summary>
/// Provides build-time information for the application.
/// The BuildDate is set during CI/CD from the shell script version.
/// </summary>
public static class BuildInfo
{
  /// <summary>
  /// The build date in yyyy.MM.dd or yyyy.MM.dd.N format.
  /// This is replaced during build via MSBuild property.
  /// </summary>
  public const string BuildDate = "2025.12.16.2";
}
