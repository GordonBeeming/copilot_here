namespace CopilotHere.Infrastructure;

/// <summary>
/// Simple debug logger that outputs to stderr when COPILOT_HERE_DEBUG is set to 1 or true.
/// </summary>
public static class DebugLogger
{
  private static readonly bool IsDebugEnabled = IsDebugMode();

  private static bool IsDebugMode()
  {
    var debugVar = Environment.GetEnvironmentVariable("COPILOT_HERE_DEBUG");
    return debugVar == "1" || debugVar?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
  }

  /// <summary>
  /// Logs a debug message to stderr if COPILOT_HERE_DEBUG is set.
  /// </summary>
  public static void Log(string message)
  {
    if (IsDebugEnabled)
    {
      Console.Error.WriteLine($"[DEBUG] {message}");
    }
  }

  /// <summary>
  /// Logs a debug message with formatted arguments.
  /// </summary>
  public static void Log(string format, params object[] args)
  {
    if (IsDebugEnabled)
    {
      Console.Error.WriteLine($"[DEBUG] {string.Format(format, args)}");
    }
  }
}
