using System.Text.Json.Serialization;

namespace CopilotHere.Commands.DockerBroker;

/// <summary>
/// Represents the docker-broker.json configuration file structure.
/// Mirrors the airlock NetworkConfig pattern.
/// </summary>
public sealed class DockerBrokerConfig
{
  [JsonPropertyName("enabled")]
  public bool Enabled { get; set; }

  [JsonPropertyName("enable_logging")]
  public bool EnableLogging { get; set; }

  [JsonPropertyName("inherit_default_rules")]
  public bool InheritDefaultRules { get; set; } = true;

  /// <summary>"enforce" blocks unmatched requests, "monitor" allows but logs.</summary>
  [JsonPropertyName("mode")]
  public string Mode { get; set; } = "enforce";

  [JsonPropertyName("allowed_endpoints")]
  public List<DockerBrokerEndpoint> AllowedEndpoints { get; set; } = [];

  /// <summary>
  /// Creates a default configuration with enabled set to the specified value.
  /// </summary>
  public static DockerBrokerConfig CreateDefault(bool enabled = false) => new()
  {
    Enabled = enabled,
    EnableLogging = false,
    InheritDefaultRules = true,
    Mode = "enforce",
    AllowedEndpoints = []
  };
}

/// <summary>
/// Represents a Docker API endpoint allowed through the broker.
/// Path matching is segment-aware: '*' matches a single path segment.
/// The leading '/v1.NN' API version prefix is stripped before matching.
/// </summary>
public sealed class DockerBrokerEndpoint
{
  [JsonPropertyName("method")]
  public string Method { get; set; } = string.Empty;

  [JsonPropertyName("path")]
  public string Path { get; set; } = string.Empty;
}

/// <summary>
/// JSON source generator context for AOT-compatible serialization.
/// </summary>
[JsonSourceGenerationOptions(
  WriteIndented = true,
  PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DockerBrokerConfig))]
[JsonSerializable(typeof(DockerBrokerEndpoint))]
[JsonSerializable(typeof(List<DockerBrokerEndpoint>))]
internal partial class DockerBrokerConfigJsonContext : JsonSerializerContext
{
}
