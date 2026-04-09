namespace CopilotHere.IntegrationTests;

/// <summary>
/// Static gate for live-Docker integration tests. The integration test
/// project's tests are all guarded by an <c>if (!LiveDockerTest.ShouldRun) return;</c>
/// at the top so they no-op (and report passing) on environments without
/// Docker or without the opt-in env var.
///
/// Why a static helper instead of a TUnit attribute: TUnit's attribute API for
/// skip-on-condition is not stable across versions, and the rest of the
/// codebase already follows the same "guard at the top" convention (see
/// ContainerRuntimeConfigTests). One pattern beats two.
///
/// Enabling: set RUN_LIVE_DOCKER_TESTS=1 in the environment AND have a
/// reachable Docker daemon. CI sets the env var; locally a developer can
/// either set it explicitly or just live with the silent skips.
/// </summary>
internal static class LiveDockerTest
{
  private static readonly Lazy<bool> _shouldRun = new(() =>
  {
    var enabled = Environment.GetEnvironmentVariable("RUN_LIVE_DOCKER_TESTS");
    if (!string.Equals(enabled, "1", StringComparison.Ordinal) &&
        !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }
    return DockerHelper.IsDockerAvailable();
  });

  public static bool ShouldRun => _shouldRun.Value;
}
