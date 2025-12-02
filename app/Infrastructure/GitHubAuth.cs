using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Handles GitHub CLI authentication operations.
/// </summary>
public static partial class GitHubAuth
{
  /// <summary>
  /// Required scopes for copilot_here to function.
  /// </summary>
  public static readonly string[] RequiredScopes = ["copilot", "read:packages"];

  /// <summary>
  /// The command to elevate token with required scopes.
  /// </summary>
  public static string ElevateTokenCommand => $"gh auth refresh -h github.com -s {string.Join(",", RequiredScopes)}";

  // Regex patterns for scope detection (compiled for AOT)
  // Matches privileged scopes like 'admin:org', 'manage_runners:org', 'write_packages', etc.
  [GeneratedRegex(@"'(admin:[^']+|manage_[^']+|write:public_key|delete_repo|write_packages|delete_packages)'", RegexOptions.IgnoreCase)]
  private static partial Regex PrivilegedScopesPattern();

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
  /// Gets the auth status output from gh CLI.
  /// </summary>
  public static string? GetAuthStatus()
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
      if (process is null) return null;

      // gh auth status outputs to stderr normally, but may vary by version
      // Read both to ensure we capture the scope information
      var stderr = process.StandardError.ReadToEnd();
      var stdout = process.StandardOutput.ReadToEnd();
      process.WaitForExit();

      // Combine both outputs - scopes could be in either
      return stderr + stdout;
    }
    catch
    {
      return null;
    }
  }

  /// <summary>
  /// Validates that the token has required scopes and warns about privileged scopes.
  /// Returns false if validation fails or user cancels due to privileged scopes.
  /// Skips validation if COPILOT_HERE_TEST_MODE environment variable is set.
  /// Set COPILOT_HERE_DEBUG=1 to see debug output for scope detection.
  /// </summary>
  public static (bool IsValid, string? Error) ValidateScopes()
  {
    // Skip validation in test mode
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_TEST_MODE")))
    {
      return (true, null);
    }

    var debug = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_DEBUG"));

    var output = GetAuthStatus();
    if (output is null)
      return (false, "Failed to run gh auth status");

    if (debug)
    {
      Console.WriteLine("[DEBUG] gh auth status output:");
      Console.WriteLine(output);
      Console.WriteLine("[DEBUG] End of output");
    }

    // Check for required scopes - gh auth status outputs scopes in format: 'scope1', 'scope2'
    var hasCopilotScope = output.Contains("'copilot'", StringComparison.Ordinal);
    var hasPackagesScope = output.Contains("'read:packages'", StringComparison.Ordinal);

    if (debug)
    {
      Console.WriteLine($"[DEBUG] hasCopilotScope: {hasCopilotScope}");
      Console.WriteLine($"[DEBUG] hasPackagesScope: {hasPackagesScope}");
    }

    if (!hasCopilotScope || !hasPackagesScope)
    {
      var missingScopes = new List<string>();
      if (!hasCopilotScope) missingScopes.Add("copilot");
      if (!hasPackagesScope) missingScopes.Add("read:packages");

      return (false, $"❌ Your gh token is missing the required scope(s): {string.Join(", ", missingScopes)}\n" +
                     $"Please run: {ElevateTokenCommand}");
    }

    // Warn about privileged scopes and require confirmation
    if (HasPrivilegedScopes(output))
    {
      Console.WriteLine("⚠️  Warning: Your GitHub token has highly privileged scopes (e.g., admin:org, admin:enterprise).");
      Console.Write("Are you sure you want to proceed with this token? [y/N]: ");

      var response = Console.ReadLine()?.Trim().ToLowerInvariant();
      if (response != "y" && response != "yes")
      {
        return (false, "Operation cancelled by user.");
      }
    }

    return (true, null);
  }

  /// <summary>
  /// Checks if the auth status output contains any privileged scopes.
  /// </summary>
  public static bool HasPrivilegedScopes(string authOutput)
  {
    return PrivilegedScopesPattern().IsMatch(authOutput);
  }

  /// <summary>
  /// Gets the list of privileged scopes found in the auth status.
  /// </summary>
  public static List<string> GetPrivilegedScopes(string authOutput)
  {
    var matches = PrivilegedScopesPattern().Matches(authOutput);
    return matches.Select(m => m.Groups[1].Value).Distinct().ToList();
  }
}
