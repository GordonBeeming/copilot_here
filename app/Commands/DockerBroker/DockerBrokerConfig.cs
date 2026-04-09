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
  /// Phase 2 body-inspection toggles for POST /containers/create. These let
  /// users opt out of individual safety rules when their workload legitimately
  /// needs them — e.g. Testcontainers .NET sometimes spawns siblings with
  /// Privileged=true. Defaults are conservative (everything ON), and users
  /// who hit a rule with a legitimate need can flip just that one toggle in
  /// their docker-broker.json without losing the others.
  /// </summary>
  [JsonPropertyName("body_inspection")]
  public DockerBrokerBodyInspectionConfig BodyInspection { get; set; } = new();

  /// <summary>
  /// Creates a default configuration with enabled set to the specified value.
  /// </summary>
  public static DockerBrokerConfig CreateDefault(bool enabled = false) => new()
  {
    Enabled = enabled,
    EnableLogging = false,
    InheritDefaultRules = true,
    Mode = "enforce",
    AllowedEndpoints = [],
    BodyInspection = new DockerBrokerBodyInspectionConfig()
  };
}

/// <summary>
/// Per-rule toggles for Phase 2 body inspection. All default to true: the
/// safe posture is to reject every dangerous flag and let users opt out
/// specific ones when they need them.
/// </summary>
public sealed class DockerBrokerBodyInspectionConfig
{
  [JsonPropertyName("reject_privileged")]
  public bool RejectPrivileged { get; set; } = true;

  [JsonPropertyName("reject_host_namespaces")]
  public bool RejectHostNamespaces { get; set; } = true;

  [JsonPropertyName("reject_forbidden_binds")]
  public bool RejectForbiddenBinds { get; set; } = true;

  [JsonPropertyName("reject_dangerous_capabilities")]
  public bool RejectDangerousCapabilities { get; set; } = true;

  /// <summary>
  /// When non-empty, the broker rejects any POST /containers/create whose
  /// Image field doesn't match one of these patterns. Glob syntax: '*'
  /// matches any sequence of characters (including slashes), so
  /// "mcr.microsoft.com/mssql/server:*" matches every tag of that image.
  ///
  /// Empty (default) disables image filtering — every image is allowed.
  /// Users who know exactly what their workload spawns can enumerate them
  /// here and get a meaningful additional layer of protection without
  /// having to inspect every PR's Dockerfile by hand.
  /// </summary>
  [JsonPropertyName("allowed_images")]
  public List<string> AllowedImages { get; set; } = [];
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
[JsonSerializable(typeof(DockerBrokerBodyInspectionConfig))]
[JsonSerializable(typeof(List<DockerBrokerEndpoint>))]
internal partial class DockerBrokerConfigJsonContext : JsonSerializerContext
{
}
