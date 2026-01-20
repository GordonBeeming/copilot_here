namespace CopilotHere.Tools;

/// <summary>
/// Model provider for the Echo tool - returns mock models for testing
/// </summary>
public class EchoModelProvider : Infrastructure.IModelProvider
{
    private static readonly List<string> MockModels = new()
    {
        "echo-default",
        "echo-fast",
        "echo-balanced",
        "echo-powerful"
    };

    public Task<List<string>> ListAvailableModels(Infrastructure.AppContext ctx)
    {
        return Task.FromResult(MockModels);
    }

    public (bool isValid, string? error) ValidateModel(string model)
    {
        if (MockModels.Contains(model))
        {
            return (true, null);
        }

        return (false, $"Unknown echo model: {model}. Available: {string.Join(", ", MockModels)}");
    }
}
