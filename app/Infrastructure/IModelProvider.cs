namespace CopilotHere.Infrastructure;

/// <summary>
/// Provides model listing and validation functionality for CLI tools
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// Lists all available models for this tool
    /// </summary>
    /// <param name="ctx">Application context</param>
    /// <returns>List of available model names</returns>
    Task<List<string>> ListAvailableModels(AppContext ctx);

    /// <summary>
    /// Validates that a model name is valid for this tool
    /// </summary>
    /// <param name="model">Model name to validate</param>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    (bool isValid, string? error) ValidateModel(string model);
}
