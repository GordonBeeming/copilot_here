using System.Text.Json;
using System.Text.Json.Serialization;

namespace CopilotHere.Commands.Airlock;

/// <summary>
/// Represents the network.json configuration file structure.
/// </summary>
public sealed class NetworkConfig
{
  [JsonPropertyName("enabled")]
  public bool Enabled { get; set; }

  [JsonPropertyName("enable_logging")]
  public bool EnableLogging { get; set; }

  [JsonPropertyName("inherit_default_rules")]
  public bool InheritDefaultRules { get; set; } = true;

  [JsonPropertyName("mode")]
  public string Mode { get; set; } = "enforce";

  [JsonPropertyName("allowed_rules")]
  public List<NetworkRule> AllowedRules { get; set; } = [];

  /// <summary>
  /// Creates a default configuration with enabled set to the specified value.
  /// </summary>
  public static NetworkConfig CreateDefault(bool enabled = false) => new()
  {
    Enabled = enabled,
    EnableLogging = false,
    InheritDefaultRules = true,
    Mode = "enforce",
    AllowedRules = []
  };
}

/// <summary>
/// Represents a network rule in the configuration.
/// </summary>
public sealed class NetworkRule
{
  [JsonPropertyName("host")]
  public string Host { get; set; } = string.Empty;

  [JsonPropertyName("allowed_paths")]
  public List<string> AllowedPaths { get; set; } = [];
}

/// <summary>
/// JSON source generator context for AOT-compatible serialization.
/// </summary>
[JsonSourceGenerationOptions(
  WriteIndented = true,
  PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NetworkConfig))]
[JsonSerializable(typeof(NetworkRule))]
[JsonSerializable(typeof(List<NetworkRule>))]
internal partial class NetworkConfigJsonContext : JsonSerializerContext
{
}
