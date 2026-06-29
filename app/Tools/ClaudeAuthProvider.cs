using System.Diagnostics;
using System.Text.Json;
using CopilotHere.Infrastructure;

namespace CopilotHere.Tools;

/// <summary>
/// Authentication provider for Claude Code.
///
/// The sandbox reuses your real host login so sessions can run long. Claude Code
/// reads OAuth credentials from ~/.claude/.credentials.json and refreshes them in
/// place using the stored refresh token. We mount the host ~/.claude into the
/// container, so on Linux that file is shared directly and refresh just works.
///
/// macOS keeps the login in the Keychain rather than a file, so a bind-mount
/// carries nothing on its own. We seed ~/.claude/.credentials.json from the
/// Keychain (full credentials, including the refresh token) so the container has
/// a self-refreshing copy. We deliberately do NOT also export
/// CLAUDE_CODE_OAUTH_TOKEN, because an explicit token would override the file and
/// disable refresh — capping sessions at the access token's short lifetime.
///
/// An explicit ANTHROPIC_API_KEY or CLAUDE_CODE_OAUTH_TOKEN in the environment
/// still wins, for anyone who prefers API-key billing or a long-lived
/// `claude setup-token`.
/// </summary>
public sealed class ClaudeAuthProvider : IAuthProvider
{
  private const string ApiKeyEnvVar = "ANTHROPIC_API_KEY";
  private const string OAuthTokenEnvVar = "CLAUDE_CODE_OAUTH_TOKEN";
  private const string KeychainService = "Claude Code-credentials";

  public (bool isValid, string? error) ValidateAuth()
  {
    DebugLogger.Log("ClaudeAuthProvider.ValidateAuth called");

    // Skip validation in test mode
    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_TEST_MODE")))
    {
      DebugLogger.Log("Test mode detected, skipping validation");
      return (true, null);
    }

    if (HasEnv(ApiKeyEnvVar))
    {
      DebugLogger.Log("ANTHROPIC_API_KEY present");
      return (true, null);
    }

    if (HasEnv(OAuthTokenEnvVar))
    {
      DebugLogger.Log("CLAUDE_CODE_OAUTH_TOKEN present");
      return (true, null);
    }

    if (EnsureHostCredentialsAvailable())
    {
      DebugLogger.Log("Host Claude Code credentials available for the sandbox");
      return (true, null);
    }

    return (false,
      "❌ Claude Code is not authenticated.\n" +
      "   Log in once on the host with `claude` (the sandbox reuses that login),\n" +
      "   or set ANTHROPIC_API_KEY / CLAUDE_CODE_OAUTH_TOKEN in your environment.");
  }

  public string GetToken()
  {
    return Environment.GetEnvironmentVariable(ApiKeyEnvVar)
           ?? Environment.GetEnvironmentVariable(OAuthTokenEnvVar)
           ?? string.Empty;
  }

  public string[] GetRequiredScopes()
  {
    // Claude Code has no scope concept like the gh token does.
    return [];
  }

  public string GetElevateTokenCommand()
  {
    return "claude  # run once on the host to log in, or set ANTHROPIC_API_KEY";
  }

  public Dictionary<string, string> GetEnvironmentVars()
  {
    var vars = new Dictionary<string, string>();

    // An explicit API key or OAuth token wins and passes straight through.
    var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
    if (!string.IsNullOrEmpty(apiKey))
    {
      vars[ApiKeyEnvVar] = apiKey;
      return vars;
    }

    var oauthEnv = Environment.GetEnvironmentVariable(OAuthTokenEnvVar);
    if (!string.IsNullOrEmpty(oauthEnv))
    {
      vars[OAuthTokenEnvVar] = oauthEnv;
      return vars;
    }

    // Otherwise make sure the mounted ~/.claude has the full credentials file so
    // the container can authenticate and refresh on its own. No token env var is
    // exported — that would shadow the file and stop refresh.
    EnsureHostCredentialsAvailable();
    return vars;
  }

  // === PRIVATE HELPER METHODS ===

  private static bool HasEnv(string name) =>
    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));

  private static string? CredentialsFilePath()
  {
    var userHome = Environment.GetEnvironmentVariable("HOME")
                   ?? Environment.GetEnvironmentVariable("USERPROFILE")
                   ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    return string.IsNullOrEmpty(userHome)
      ? null
      : Path.Combine(userHome, ".claude", ".credentials.json");
  }

  /// <summary>
  /// Ensures ~/.claude/.credentials.json holds usable credentials for the
  /// container.
  ///
  /// On Linux the host app uses that file directly, so it's already the shared,
  /// self-refreshing store — we just confirm it exists.
  ///
  /// On macOS the login lives in the Keychain, which the host app keeps fresh
  /// (including rotating the refresh token). We therefore re-seed the file from
  /// the Keychain on every run, not just once: a stale seed would carry a
  /// refresh token the host has already rotated away, and the container would
  /// fail with a 401. We only rewrite when the contents actually changed.
  /// Returns true when credentials are available to the sandbox.
  /// </summary>
  private static bool EnsureHostCredentialsAvailable()
  {
    var path = CredentialsFilePath();
    if (path is null)
    {
      return false;
    }

    if (!OperatingSystem.IsMacOS())
    {
      // Linux/Windows store the login in the file; if it's absent the user
      // hasn't logged in.
      return File.Exists(path);
    }

    var json = ReadKeychainCredentials();
    if (string.IsNullOrEmpty(json) || ExtractAccessToken(json) is null)
    {
      // Keychain unavailable (locked, no login) — fall back to any existing file.
      return File.Exists(path);
    }

    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      if (!File.Exists(path) || File.ReadAllText(path) != json)
      {
        File.WriteAllText(path, json);
        TryRestrictPermissions(path);
        DebugLogger.Log("Refreshed ~/.claude/.credentials.json from the macOS Keychain");
      }
      return true;
    }
    catch (Exception ex)
    {
      DebugLogger.Log($"Failed to write credentials file: {ex.Message}");
      // A previous good seed is better than nothing.
      return File.Exists(path);
    }
  }

  private static void TryRestrictPermissions(string path)
  {
    if (OperatingSystem.IsWindows())
    {
      return;
    }

    try
    {
      // Owner read/write only — the file holds an OAuth refresh token.
      File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    catch (Exception ex)
    {
      DebugLogger.Log($"Could not tighten credentials file permissions: {ex.Message}");
    }
  }

  private static string? ReadKeychainCredentials()
  {
    try
    {
      var startInfo = new ProcessStartInfo("security",
        $"find-generic-password -s \"{KeychainService}\" -w")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return null;

      var stdout = process.StandardOutput.ReadToEnd();
      process.StandardError.ReadToEnd();
      process.WaitForExit();

      return process.ExitCode == 0 ? stdout.Trim() : null;
    }
    catch (Exception ex)
    {
      DebugLogger.Log($"Keychain read failed: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  /// Both the Keychain payload and the Linux credentials file use the shape
  /// { "claudeAiOauth": { "accessToken": "...", "refreshToken": "...", ... } }.
  /// Used only to confirm the payload is the login we expect before writing it.
  /// </summary>
  private static string? ExtractAccessToken(string json)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
          && oauth.TryGetProperty("accessToken", out var token)
          && token.ValueKind == JsonValueKind.String)
      {
        return token.GetString();
      }
    }
    catch (JsonException ex)
    {
      DebugLogger.Log($"Credentials JSON parse failed: {ex.Message}");
    }

    return null;
  }
}
