using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotHere.Commands.DockerBroker;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Phase 2 body inspection for Docker socket broker. Parses the JSON body of
/// `POST /containers/create` requests and applies a set of safety rules:
///
///   * Reject obviously dangerous container configurations:
///       - HostConfig.Privileged == true
///       - HostConfig.NetworkMode/PidMode/IpcMode/UsernsMode == "host"
///       - HostConfig.Binds containing forbidden host paths
///         (/, /etc, /root, /var, /usr, /bin, /sbin, /proc, /sys, the docker socket itself)
///       - HostConfig.CapAdd containing entries from the dangerous capability list
///
///   * In airlock + DinD mode, inject HostConfig.NetworkMode = "&lt;airlock-network&gt;"
///     so spawned sibling containers join the same internal-only network as the
///     workload. Without this, Testcontainers et al. spawn siblings on the
///     default bridge and the workload (which sits on `internal: true`) can't
///     reach them. With it, the workload connects to the sibling by Docker DNS
///     name and the airlock guarantees still hold.
///
/// The inspector treats the create request as a single JSON object: parse with
/// JsonNode (no source-generator since the schema is too dynamic), apply rules,
/// re-serialize, return the new bytes. The caller wraps that into a fresh
/// HTTP/1.1 request with an updated Content-Length.
///
/// Failures are conservative — if we can't parse the body we forward unchanged
/// rather than blocking valid Docker calls. The endpoint allowlist already
/// gates which paths are even reachable.
/// </summary>
public static class DockerBrokerBodyInspector
{
  /// <summary>
  /// Maximum body size we'll buffer for inspection. Container create payloads
  /// are typically a few KB. Anything beyond this we forward unchanged with a
  /// warning, on the theory that bypassing inspection is preferable to
  /// breaking legitimate workflows. Body inspection is the second line of
  /// defence — endpoint filtering is the first.
  /// </summary>
  public const int MaxInspectableBodyBytes = 2 * 1024 * 1024;

  /// <summary>
  /// Default list of host paths that container bind mounts MUST NOT target.
  /// These are paths whose contents would let a sibling container compromise
  /// the host or escape the airlock.
  /// </summary>
  private static readonly string[] ForbiddenBindHostPaths =
  [
    "/",
    "/etc",
    "/root",
    "/var",
    "/usr",
    "/bin",
    "/sbin",
    "/proc",
    "/sys",
    "/var/run/docker.sock",
    "/run/docker.sock",
  ];

  /// <summary>
  /// Default deny-list of Linux capabilities a sibling container should never request.
  /// </summary>
  private static readonly HashSet<string> DangerousCapabilities = new(StringComparer.OrdinalIgnoreCase)
  {
    "SYS_ADMIN",
    "SYS_MODULE",
    "SYS_PTRACE",
    "SYS_RAWIO",
    "SYS_BOOT",
    "MAC_ADMIN",
    "MAC_OVERRIDE",
    "DAC_READ_SEARCH",
    "NET_ADMIN",
    "AUDIT_CONTROL",
  };

  public sealed record InspectionResult(
    bool Allowed,
    string Reason,
    byte[]? RewrittenBody);

  /// <summary>
  /// Inspects (and optionally rewrites) the JSON body of a POST /containers/create
  /// request. Set <paramref name="siblingNetworkName"/> to inject NetworkMode for
  /// airlock-mode broker sessions; pass null in standard --dind mode.
  /// </summary>
  public static InspectionResult Inspect(byte[] body, string? siblingNetworkName) =>
    Inspect(body, siblingNetworkName, new DockerBrokerBodyInspectionConfig());

  /// <summary>
  /// Inspects with explicit per-rule toggles. The broker passes its loaded
  /// <see cref="DockerBrokerBodyInspectionConfig"/> here so users can opt out
  /// of specific safety rules without disabling the rest.
  /// </summary>
  public static InspectionResult Inspect(byte[] body, string? siblingNetworkName, DockerBrokerBodyInspectionConfig policy)
  {
    if (body.Length == 0)
    {
      // Empty body — Docker rejects this anyway. Forward unchanged.
      return new InspectionResult(true, "empty body", null);
    }

    JsonNode? root;
    try
    {
      root = JsonNode.Parse(body);
    }
    catch (JsonException)
    {
      // Malformed JSON. Forward unchanged so the upstream daemon returns its
      // own 400 — that's a more useful error for users than a broker-level
      // "could not parse" message.
      return new InspectionResult(true, "unparseable body forwarded as-is", null);
    }

    if (root is not JsonObject obj)
    {
      return new InspectionResult(true, "non-object body forwarded as-is", null);
    }

    var hostConfig = obj["HostConfig"] as JsonObject;

    // ── Image allowlist (default-deny) ────────────────────────────────────
    // The allowed_images list is a strict whitelist. An empty list means
    // NO sibling containers may be spawned at all — the safe posture for an
    // AI agent is "name every image you trust, deny everything else". Users
    // who legitimately need to spawn things must enumerate them via
    // --add-docker-broker-image; otherwise every POST /containers/create is
    // refused.
    {
      var image = obj["Image"]?.GetValue<string?>() ?? "";
      var allowedImages = policy.AllowedImages ?? [];
      var matched = false;
      foreach (var pattern in allowedImages)
      {
        if (ImagePatternMatches(pattern, image))
        {
          matched = true;
          break;
        }
      }
      if (!matched)
      {
        var reason = allowedImages.Count == 0
          ? $"image \"{image}\" rejected: docker broker has no trusted images configured (allowed_images is empty)"
          : $"image \"{image}\" is not in docker broker allowed_images list";
        return new InspectionResult(false, reason, null);
      }
    }

    // ── Rejections ────────────────────────────────────────────────────────
    if (hostConfig is not null)
    {
      if (policy.RejectPrivileged && hostConfig["Privileged"]?.GetValue<bool?>() == true)
      {
        return new InspectionResult(false, "HostConfig.Privileged is denied by docker broker policy", null);
      }

      if (policy.RejectHostNamespaces)
      {
        foreach (var modeKey in new[] { "NetworkMode", "PidMode", "IpcMode", "UsernsMode" })
        {
          var mode = hostConfig[modeKey]?.GetValue<string?>();
          if (string.Equals(mode, "host", StringComparison.OrdinalIgnoreCase))
          {
            return new InspectionResult(false, $"HostConfig.{modeKey}=\"host\" is denied by docker broker policy", null);
          }
        }
      }

      if (policy.RejectForbiddenBinds && hostConfig["Binds"] is JsonArray binds)
      {
        foreach (var bindNode in binds)
        {
          var bind = bindNode?.GetValue<string?>();
          if (string.IsNullOrEmpty(bind)) continue;
          var hostPath = bind.Split(':')[0];
          if (IsForbiddenHostPath(hostPath))
          {
            return new InspectionResult(false, $"HostConfig.Binds entry \"{bind}\" targets a forbidden host path", null);
          }
        }
      }

      if (policy.RejectDangerousCapabilities && hostConfig["CapAdd"] is JsonArray capAdd)
      {
        foreach (var capNode in capAdd)
        {
          var cap = capNode?.GetValue<string?>();
          if (string.IsNullOrEmpty(cap)) continue;
          var stripped = cap.StartsWith("CAP_", StringComparison.OrdinalIgnoreCase) ? cap[4..] : cap;
          if (DangerousCapabilities.Contains(stripped))
          {
            return new InspectionResult(false, $"HostConfig.CapAdd entry \"{cap}\" is on the broker deny list", null);
          }
        }
      }
    }

    // ── Mutations ─────────────────────────────────────────────────────────
    bool mutated = false;

    // Inject NetworkMode for airlock-mode siblings. We only override the
    // default — if the caller already set an explicit non-host network we
    // leave it alone (they presumably know what they're doing). The "default"
    // and "bridge" cases are the ones we have to fix because they put the
    // sibling on a network the airlocked workload can't reach.
    if (!string.IsNullOrEmpty(siblingNetworkName))
    {
      hostConfig ??= new JsonObject();
      var existing = hostConfig["NetworkMode"]?.GetValue<string?>();
      if (string.IsNullOrEmpty(existing) ||
          string.Equals(existing, "default", StringComparison.OrdinalIgnoreCase) ||
          string.Equals(existing, "bridge", StringComparison.OrdinalIgnoreCase))
      {
        hostConfig["NetworkMode"] = siblingNetworkName;
        if (obj["HostConfig"] is null) obj["HostConfig"] = hostConfig;
        mutated = true;
      }

      // Also drop the top-level NetworkingConfig.EndpointsConfig if present —
      // Testcontainers sometimes sets it to "bridge" which Docker treats as
      // an explicit network attachment that conflicts with NetworkMode.
      if (obj["NetworkingConfig"] is JsonObject netCfg &&
          netCfg["EndpointsConfig"] is JsonObject endpoints &&
          endpoints.Count > 0)
      {
        netCfg["EndpointsConfig"] = new JsonObject();
        mutated = true;
      }
    }

    if (!mutated)
    {
      return new InspectionResult(true, "approved unchanged", null);
    }

    var rewritten = Encoding.UTF8.GetBytes(obj.ToJsonString());
    return new InspectionResult(true, "approved with NetworkMode injection", rewritten);
  }

  /// <summary>
  /// Glob-matches an image reference against a pattern. '*' matches any
  /// sequence of characters (including slashes and colons), so a single
  /// pattern can cover registry+repo+tag in one go. Matching is case-
  /// sensitive because Docker image references are case-sensitive.
  /// </summary>
  internal static bool ImagePatternMatches(string pattern, string image)
  {
    if (string.IsNullOrEmpty(pattern)) return false;
    if (image is null) image = "";

    // Fast path: literal match.
    if (!pattern.Contains('*'))
    {
      return string.Equals(pattern, image, StringComparison.Ordinal);
    }

    // Greedy backtracking glob match. Pattern length is bounded by the
    // user's config (small) so the worst-case cost is irrelevant.
    return GlobMatch(pattern.AsSpan(), 0, image.AsSpan(), 0);
  }

  private static bool GlobMatch(ReadOnlySpan<char> pattern, int pi, ReadOnlySpan<char> input, int ii)
  {
    while (pi < pattern.Length)
    {
      var c = pattern[pi];
      if (c == '*')
      {
        // Collapse consecutive stars.
        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        if (pi == pattern.Length) return true;
        for (var k = ii; k <= input.Length; k++)
        {
          if (GlobMatch(pattern, pi, input, k)) return true;
        }
        return false;
      }
      if (ii >= input.Length || input[ii] != c) return false;
      pi++;
      ii++;
    }
    return ii == input.Length;
  }

  private static bool IsForbiddenHostPath(string hostPath)
  {
    if (string.IsNullOrEmpty(hostPath)) return false;
    var normalized = hostPath.TrimEnd('/');
    if (normalized.Length == 0) normalized = "/";
    foreach (var forbidden in ForbiddenBindHostPaths)
    {
      if (string.Equals(normalized, forbidden, StringComparison.Ordinal))
        return true;
    }
    return false;
  }
}
