using System.Diagnostics;
using System.Text.RegularExpressions;
using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// GitHub CLI authentication provider for GitHub Copilot.
/// Refactored from GitHubAuth.cs to implement IAuthProvider.
/// </summary>
public sealed partial class GitHubAuthProvider : IAuthProvider
{
  /// <summary>
  /// Required scopes for GitHub Copilot to function.
  /// </summary>
  private static readonly string[] _requiredScopes = ["copilot", "read:packages"];

  /// <summary>
  /// The command to elevate token with required scopes.
  /// </summary>
  private static string ElevateTokenCommand => $"gh auth refresh -h github.com -s {string.Join(",", _requiredScopes)}";

  // Regex patterns for scope detection (compiled for AOT)
  // Matches privileged scopes like 'admin:org', 'manage_runners:org', 'write_packages', etc.
  [GeneratedRegex(@"'(admin:[^']+|manage_[^']+|write:public_key|delete_repo|write_packages|delete_packages)'", RegexOptions.IgnoreCase)]
  private static partial Regex PrivilegedScopesPattern();

  public (bool isValid, string? error) ValidateAuth()
  {
    DebugLogger.Log("GitHubAuthProvider.ValidateAuth called");
    
    // Skip validation in test mode
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_TEST_MODE")))
    {
      DebugLogger.Log("Test mode detected, skipping validation");
      return (true, null);
    }

    var debug = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_DEBUG"));

    var output = GetAuthStatus();
    if (output is null)
    {
      DebugLogger.Log("GetAuthStatus returned null");
      return (false, "Failed to run gh auth status");
    }

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

      DebugLogger.Log($"Missing scopes: {string.Join(", ", missingScopes)}");
      return (false, $"❌ Your gh token is missing the required scope(s): {string.Join(", ", missingScopes)}\n" +
                     $"Please run: {ElevateTokenCommand}");
    }

    // Warn about privileged scopes and require confirmation
    if (HasPrivilegedScopes(output))
    {
      DebugLogger.Log("Privileged scopes detected, asking for confirmation");
      Console.WriteLine("⚠️  Warning: Your GitHub token has highly privileged scopes (e.g., admin:org, admin:enterprise).");
      Console.Write("Are you sure you want to proceed with this token? [y/N]: ");

      var response = Console.ReadLine()?.Trim().ToLowerInvariant();
      DebugLogger.Log($"User response: {response}");
      if (response != "y" && response != "yes")
      {
        DebugLogger.Log("User declined to proceed");
        return (false, "Operation cancelled by user.");
      }
    }

    DebugLogger.Log("Scope validation passed");
    return (true, null);
  }

  public string GetToken()
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

  public string[] GetRequiredScopes()
  {
    return _requiredScopes;
  }

  public string GetElevateTokenCommand()
  {
    return ElevateTokenCommand;
  }

  public Dictionary<string, string> GetEnvironmentVars()
  {
    var token = GetToken();
    return new Dictionary<string, string>
    {
      ["GITHUB_TOKEN"] = token,
      ["GH_TOKEN"] = token
    };
  }

  // === PRIVATE HELPER METHODS ===

  /// <summary>
  /// Gets the auth status output from gh CLI.
  /// </summary>
  private static string? GetAuthStatus()
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
  /// Checks if the auth status output contains any privileged scopes.
  /// </summary>
  private static bool HasPrivilegedScopes(string authOutput)
  {
    return PrivilegedScopesPattern().IsMatch(authOutput);
  }
}
