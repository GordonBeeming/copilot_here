using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Checks for required dependencies and displays their status.
/// </summary>
public static class DependencyCheck
{
  /// <summary>
  /// Result of a dependency check.
  /// </summary>
  public record DependencyResult(
    string Name,
    bool IsInstalled,
    string? Version,
    string? ErrorMessage,
    string? HelpMessage
  );

  /// <summary>
  /// Checks all required dependencies for the specified tool and returns their status.
  /// </summary>
  public static List<DependencyResult> CheckAll(ICliTool tool, ContainerRuntimeConfig runtimeConfig)
  {
    var results = new List<DependencyResult>();

    // Check container runtime and daemon first
    results.Add(CheckContainerRuntime(runtimeConfig));
    results.Add(CheckContainerRuntimeRunning(runtimeConfig));

    // Check tool-specific dependencies
    var toolDeps = tool.GetRequiredDependencies();
    foreach (var dep in toolDeps)
    {
      if (dep.Equals("docker", StringComparison.OrdinalIgnoreCase))
      {
        // Already checked (as container runtime)
        continue;
      }
      else if (dep.Equals("gh", StringComparison.OrdinalIgnoreCase))
      {
        results.Add(CheckGitHubCli(tool));
      }
      // Add more dependency checks here as needed
    }

    return results;
  }

  /// <summary>
  /// Displays dependency check results in a nice format.
  /// Only shows output if there are failures or debug mode is enabled.
  /// When showing output, displays all dependencies (both passed and failed).
  /// Returns true if all dependencies are satisfied, false otherwise.
  /// </summary>
  public static bool DisplayResults(List<DependencyResult> results)
  {
    var allSatisfied = true;
    var hasFailures = results.Any(r => !r.IsInstalled);
    var isDebugMode = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COPILOT_HERE_DEBUG"));

    // Only display if there are failures or in debug mode
    if (!hasFailures && !isDebugMode)
    {
      DebugLogger.Log("All dependencies satisfied (not displaying)");
      return true;
    }

    DebugLogger.Log($"Displaying dependency results (hasFailures: {hasFailures}, isDebugMode: {isDebugMode})");

    Console.WriteLine("\n📋 Dependency Check:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

    // Show all dependencies (both passed and failed) when displaying
    foreach (var result in results)
    {
      var status = result.IsInstalled ? "✅" : "❌";
      var versionInfo = result.Version != null ? $" ({result.Version})" : "";
      Console.WriteLine($"{status} {result.Name}{versionInfo}");

      if (!result.IsInstalled)
      {
        if (result.ErrorMessage != null)
        {
          Console.WriteLine($"   {result.ErrorMessage}");
        }
        allSatisfied = false;
      }

      if (result.HelpMessage != null)
      {
        Console.WriteLine($"   {result.HelpMessage}");
      }
    }

    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

    return allSatisfied;
  }

  /// <summary>
  /// Checks if GitHub CLI is installed and authenticated.
  /// </summary>
  private static DependencyResult CheckGitHubCli(ICliTool tool)
  {
    try
    {
      // Check if gh command exists
      var versionStartInfo = new ProcessStartInfo("gh", "--version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var versionProcess = Process.Start(versionStartInfo);
      if (versionProcess is null)
      {
        return new DependencyResult(
          "GitHub CLI (gh)",
          false,
          null,
          "Failed to run 'gh --version'",
          GetGitHubCliInstallHelp()
        );
      }

      var versionOutput = versionProcess.StandardOutput.ReadToEnd();
      versionProcess.WaitForExit();

      if (versionProcess.ExitCode != 0)
      {
        return new DependencyResult(
          "GitHub CLI (gh)",
          false,
          null,
          "GitHub CLI not found",
          GetGitHubCliInstallHelp()
        );
      }

      // Extract version from output (e.g., "gh version 2.40.1 (2024-01-09)")
      var version = ExtractGhVersion(versionOutput);

      // Check if authenticated
      var authStartInfo = new ProcessStartInfo("gh", "auth status")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var authProcess = Process.Start(authStartInfo);
      if (authProcess is null)
      {
        return new DependencyResult(
          "GitHub CLI (gh)",
          false,
          version,
          "Failed to check authentication status",
          GetGitHubAuthHelp(tool)
        );
      }

      authProcess.WaitForExit();

      if (authProcess.ExitCode != 0)
      {
        return new DependencyResult(
          "GitHub CLI (gh)",
          false,
          version,
          "Not authenticated",
          GetGitHubAuthHelp(tool)
        );
      }

      return new DependencyResult(
        "GitHub CLI (gh)",
        true,
        version,
        null,
        null
      );
    }
    catch (Exception ex)
    {
      return new DependencyResult(
        "GitHub CLI (gh)",
        false,
        null,
        $"Error checking GitHub CLI: {ex.Message}",
        GetGitHubCliInstallHelp()
      );
    }
  }

  /// <summary>
  /// Checks if the configured container runtime is installed.
  /// </summary>
  private static DependencyResult CheckContainerRuntime(ContainerRuntimeConfig runtimeConfig)
  {
    try
    {
      var startInfo = new ProcessStartInfo(runtimeConfig.Runtime, "--version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null)
      {
        return new DependencyResult(
          runtimeConfig.RuntimeFlavor,
          false,
          null,
          $"Failed to run '{runtimeConfig.Runtime} --version'",
          GetContainerRuntimeInstallHelp(runtimeConfig)
        );
      }

      var output = process.StandardOutput.ReadToEnd();
      process.WaitForExit();

      if (process.ExitCode != 0)
      {
        return new DependencyResult(
          runtimeConfig.RuntimeFlavor,
          false,
          null,
          $"{runtimeConfig.RuntimeFlavor} not found",
          GetContainerRuntimeInstallHelp(runtimeConfig)
        );
      }

      // Extract version from output
      var version = ExtractContainerRuntimeVersion(output, runtimeConfig.Runtime);

      return new DependencyResult(
        runtimeConfig.RuntimeFlavor,
        true,
        version,
        null,
        null
      );
    }
    catch (Exception ex)
    {
      return new DependencyResult(
        runtimeConfig.RuntimeFlavor,
        false,
        null,
        $"Error checking {runtimeConfig.RuntimeFlavor}: {ex.Message}",
        GetContainerRuntimeInstallHelp(runtimeConfig)
      );
    }
  }

  /// <summary>
  /// Checks if the container runtime daemon is running.
  /// </summary>
  private static DependencyResult CheckContainerRuntimeRunning(ContainerRuntimeConfig runtimeConfig)
  {
    try
    {
      var startInfo = new ProcessStartInfo(runtimeConfig.Runtime, "info")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null)
      {
        return new DependencyResult(
          $"{runtimeConfig.RuntimeFlavor} Daemon",
          false,
          null,
          $"Failed to run '{runtimeConfig.Runtime} info'",
          GetContainerRuntimeRunningHelp(runtimeConfig)
        );
      }

      process.WaitForExit();

      if (process.ExitCode != 0)
      {
        return new DependencyResult(
          $"{runtimeConfig.RuntimeFlavor} Daemon",
          false,
          null,
          $"{runtimeConfig.RuntimeFlavor} daemon not running",
          GetContainerRuntimeRunningHelp(runtimeConfig)
        );
      }

      return new DependencyResult(
        $"{runtimeConfig.RuntimeFlavor} Daemon",
        true,
        "Running",
        null,
        null
      );
    }
    catch (Exception ex)
    {
      return new DependencyResult(
        $"{runtimeConfig.RuntimeFlavor} Daemon",
        false,
        null,
        $"Error checking {runtimeConfig.RuntimeFlavor} daemon: {ex.Message}",
        GetContainerRuntimeRunningHelp(runtimeConfig)
      );
    }
  }

  /// <summary>
  /// Extracts GitHub CLI version from output.
  /// </summary>
  private static string? ExtractGhVersion(string output)
  {
    // Expected format: "gh version 2.40.1 (2024-01-09)"
    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    if (lines.Length == 0) return null;

    var firstLine = lines[0];
    var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 3 && parts[0] == "gh" && parts[1] == "version")
    {
      return parts[2];
    }

    return null;
  }

  /// <summary>
  /// Extracts container runtime version from output.
  /// </summary>
  private static string? ExtractContainerRuntimeVersion(string output, string runtime)
  {
    // Expected format: "Docker version 24.0.7, build afdd53b" or "podman version 4.5.0"
    var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    // Look for "version X.Y.Z" pattern
    for (int i = 0; i < parts.Length - 1; i++)
    {
      if (parts[i].Equals("version", StringComparison.OrdinalIgnoreCase))
      {
        // Remove trailing comma if present
        var version = parts[i + 1].TrimEnd(',');
        return version;
      }
    }

    return null;
  }

  /// <summary>
  /// Gets platform-specific help for installing GitHub CLI.
  /// </summary>
  private static string GetGitHubCliInstallHelp()
  {
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
      return "💡 Install: winget install -e --id GitHub.cli --source winget";
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
      return "💡 Install: brew install gh";
    }
    else // Linux
    {
      return "💡 Install: https://github.com/cli/cli/blob/trunk/docs/install_linux.md";
    }
  }

  /// <summary>
  /// Gets help for authenticating with GitHub CLI.
  /// </summary>
  private static string GetGitHubAuthHelp(ICliTool tool)
  {
    var authProvider = tool.GetAuthProvider();
    var scopes = string.Join(",", authProvider.GetRequiredScopes());
    return $"💡 Authenticate: gh auth login -h github.com -s {scopes}";
  }

  /// <summary>
  /// Gets platform-specific help for installing the container runtime.
  /// </summary>
  private static string GetContainerRuntimeInstallHelp(ContainerRuntimeConfig runtimeConfig)
  {
    if (runtimeConfig.Runtime == "podman")
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        return "💡 Install: https://podman.io/docs/installation#windows";
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return "💡 Install: brew install podman";
      }
      else // Linux
      {
        return "💡 Install: https://podman.io/docs/installation#installing-on-linux";
      }
    }
    else // Docker
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        return "💡 Install: https://docs.docker.com/desktop/install/windows-install/";
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return "💡 Install: https://docs.docker.com/desktop/install/mac-install/ or brew install --cask orbstack";
      }
      else // Linux
      {
        return "💡 Install: https://docs.docker.com/engine/install/";
      }
    }
  }

  /// <summary>
  /// Gets help for starting the container runtime daemon.
  /// </summary>
  private static string GetContainerRuntimeRunningHelp(ContainerRuntimeConfig runtimeConfig)
  {
    if (runtimeConfig.Runtime == "podman")
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
          RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return "💡 Start Podman: podman machine start";
      }
      else // Linux
      {
        return "💡 Start Podman: sudo systemctl start podman";
      }
    }
    else // Docker
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
          RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        return runtimeConfig.RuntimeFlavor == "OrbStack" 
          ? "💡 Start OrbStack application" 
          : "💡 Start Docker Desktop application";
      }
      else // Linux
      {
        return "💡 Start Docker: sudo systemctl start docker";
      }
    }
  }
}
