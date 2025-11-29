using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles GitHub CLI authentication operations.
/// </summary>
public static class GitHubAuth
{
  /// <summary>
  /// Retrieves the GitHub token using the gh CLI.
  /// </summary>
  public static string GetToken()
  {
    try
    {
      var startInfo = new ProcessStartInfo("gh", "auth token")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return string.Empty;

      var token = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();

      return process.ExitCode == 0 ? token : string.Empty;
    }
    catch
    {
      return string.Empty;
    }
  }

  /// <summary>
  /// Validates that the token has required scopes.
  /// </summary>
  public static (bool IsValid, string? Error) ValidateScopes()
  {
    try
    {
      var startInfo = new ProcessStartInfo("gh", "auth status")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null)
        return (false, "Failed to run gh auth status");

      var output = process.StandardError.ReadToEnd();
      process.WaitForExit();

      // Check for required scopes
      if (!output.Contains("'copilot'") || !output.Contains("'read:packages'"))
      {
        return (false, "Missing required scopes. Run: gh auth refresh -h github.com -s copilot,read:packages");
      }

      // Warn about privileged scopes
      if (output.Contains("'admin:") || output.Contains("'manage_") ||
          output.Contains("'write:public_key'") || output.Contains("'delete_repo'"))
      {
        Console.WriteLine("⚠️  Warning: Your GitHub token has highly privileged scopes.");
      }

      return (true, null);
    }
    catch (Exception ex)
    {
      return (false, $"Failed to validate auth: {ex.Message}");
    }
  }
}
