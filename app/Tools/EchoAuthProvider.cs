namespace CopilotHere.Tools;

/// <summary>
/// Auth provider for the Echo tool - always succeeds (no auth required for testing)
/// </summary>
public class EchoAuthProvider : Infrastructure.IAuthProvider
{
    public (bool isValid, string? error) ValidateAuth()
    {
        // Echo doesn't require authentication
        return (true, null);
    }

    public string GetToken()
    {
        return "echo-test-token";
    }

    public string[] GetRequiredScopes()
    {
        return []; // No scopes required
    }

    public string GetElevateTokenCommand()
    {
        return ""; // No elevation needed
    }

    public Dictionary<string, string> GetEnvironmentVars()
    {
        return new Dictionary<string, string>
        {
            ["ECHO_MODE"] = "test"
        };
    }
}
