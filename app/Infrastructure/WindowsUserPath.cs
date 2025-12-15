using Microsoft.Win32;

namespace CopilotHere.Infrastructure;

internal static class WindowsUserPath
{
  public static void EnsureUserPathContains(string directory)
  {
    try
    {
      var current = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
      if (ContainsPathEntry(current, directory))
      {
        return;
      }

      var newValue = string.IsNullOrWhiteSpace(current)
        ? directory
        : $"{directory};{current}";

      Environment.SetEnvironmentVariable("Path", newValue, EnvironmentVariableTarget.User);

      // Also update current process so subsequent commands in this session can use it.
      var processPath = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
      if (!ContainsPathEntry(processPath, directory))
      {
        Environment.SetEnvironmentVariable("Path", $"{directory};{processPath}");
      }
    }
    catch
    {
      // Best-effort; ignore failures (locked down environments, etc.)
    }
  }

  private static bool ContainsPathEntry(string pathValue, string directory)
  {
    var entries = pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return entries.Any(e => string.Equals(e.TrimEnd('\\'), directory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
  }
}
