using System.Diagnostics;

namespace CopilotHere.IntegrationTests;

/// <summary>
/// Thin wrapper around `docker` CLI invocations for integration tests. Wraps
/// process startup, captures stdout/stderr, and offers a couple of polled
/// helpers for waiting on container readiness.
///
/// We deliberately use the docker CLI rather than Docker.DotNet here because
/// the SUT is the broker, not Docker.DotNet — we want the harness as close to
/// what end users (and CI) actually invoke as possible.
/// </summary>
internal static class DockerHelper
{
  public sealed record CommandResult(int ExitCode, string Stdout, string Stderr)
  {
    public bool Succeeded => ExitCode == 0;
  }

  public static CommandResult Run(params string[] args) => RunWithTimeout(TimeSpan.FromMinutes(2), args);

  public static CommandResult RunWithTimeout(TimeSpan timeout, params string[] args)
  {
    var psi = new ProcessStartInfo("docker")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    foreach (var a in args) psi.ArgumentList.Add(a);

    using var process = Process.Start(psi)
      ?? throw new InvalidOperationException("Could not start docker CLI — is Docker installed?");

    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();

    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
    {
      try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
      throw new TimeoutException($"docker {string.Join(" ", args)} did not exit within {timeout.TotalSeconds}s");
    }

    return new CommandResult(process.ExitCode, stdout.Result, stderr.Result);
  }

  /// <summary>
  /// Removes the named container forcibly. Used for setup and teardown — never
  /// throws on failure (the container may not exist).
  /// </summary>
  public static void ForceRemove(string containerName)
  {
    try { Run("rm", "-f", containerName); } catch { /* best effort */ }
  }

  /// <summary>
  /// Returns true if the docker daemon is reachable. Used by IsDockerAvailable
  /// to gate live tests on environments without Docker (some CI runners,
  /// developer laptops in airplane mode, etc.).
  /// </summary>
  public static bool IsDockerAvailable()
  {
    try
    {
      var result = RunWithTimeout(TimeSpan.FromSeconds(5), "version", "--format", "{{.Server.Version}}");
      return result.Succeeded && !string.IsNullOrWhiteSpace(result.Stdout);
    }
    catch
    {
      return false;
    }
  }
}
