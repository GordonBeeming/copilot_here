namespace CopilotHere.Infrastructure;

/// <summary>
/// Provides authentication functionality for CLI tools
/// </summary>
public interface IAuthProvider
{
    /// <summary>
    /// Validates that authentication is properly configured and working
    /// </summary>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    (bool isValid, string? error) ValidateAuth();

    /// <summary>
    /// Gets the authentication token (if applicable)
    /// </summary>
    /// <returns>Authentication token or empty string if not applicable</returns>
    string GetToken();

    /// <summary>
    /// Gets the required authentication scopes
    /// </summary>
    /// <returns>Array of scope names (e.g., ["copilot", "read:packages"])</returns>
    string[] GetRequiredScopes();

    /// <summary>
    /// Gets the command to elevate/refresh the authentication token
    /// </summary>
    /// <returns>Shell command to run (e.g., "gh auth refresh -s copilot")</returns>
    string GetElevateTokenCommand();

    /// <summary>
    /// Gets environment variables needed for authentication
    /// </summary>
    /// <returns>Dictionary of environment variable names and values</returns>
    Dictionary<string, string> GetEnvironmentVars();
}
