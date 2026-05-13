using System.Net;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Chooses the IP the Docker broker's TCP listener binds to on non-Linux hosts.
/// Default is <see cref="IPAddress.Any"/> because Podman + gvproxy (Windows and
/// macOS Podman Machine) forwards host.docker.internal traffic to the host's
/// regular interface IP, not loopback — a 127.0.0.1-only listener is invisible
/// from inside the workload container. Docker Desktop's host.docker.internal
/// forwarding works with either bind, so Any is the safe cross-runtime default.
/// Users on untrusted networks who'd rather rely solely on the broker's rule
/// engine for safety can opt back into loopback via the env var below.
/// </summary>
internal static class BrokerBindResolver
{
  public const string BindLoopbackEnvVar = "COPILOT_HERE_BROKER_BIND_LOOPBACK";

  public static IPAddress ResolveTcpBindAddress(IReadOnlyDictionary<string, string?>? envOverride = null)
  {
    var raw = envOverride is null
      ? Environment.GetEnvironmentVariable(BindLoopbackEnvVar)
      : (envOverride.TryGetValue(BindLoopbackEnvVar, out var v) ? v : null);

    if (IsTruthy(raw))
    {
      return IPAddress.Loopback;
    }

    return IPAddress.Any;
  }

  private static bool IsTruthy(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw)) return false;
    return raw.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
  }
}
