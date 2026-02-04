using System.Diagnostics;

namespace CopilotHere.Infrastructure;

/// <summary>
/// Configuration for container runtime detection and management.
/// Supports Docker, OrbStack, and Podman with auto-detection.
/// Priority: Local config > Global config > Auto-detect
/// </summary>
public sealed record ContainerRuntimeConfig
{
  /// <summary>The runtime to use: "docker" or "podman".</summary>
  public required string Runtime { get; init; }

  /// <summary>The flavor/variant of runtime: "Docker", "OrbStack", or "Podman".</summary>
  public required string RuntimeFlavor { get; init; }

  /// <summary>The compose command to use: "compose" (built-in) or "docker-compose"/"podman-compose" (external).</summary>
  public required string ComposeCommand { get; init; }

  /// <summary>Whether this runtime supports the airlock feature.</summary>
  public required bool SupportsAirlock { get; init; }

  /// <summary>Default network name for this runtime ("bridge" for Docker/OrbStack, "podman" for Podman).</summary>
  public required string DefaultNetworkName { get; init; }

  /// <summary>Source of the resolved runtime (for display purposes).</summary>
  public required RuntimeConfigSource Source { get; init; }

  /// <summary>Runtime from local config, if any.</summary>
  public string? LocalRuntime { get; init; }

  /// <summary>Runtime from global config, if any.</summary>
  public string? GlobalRuntime { get; init; }

  private const string ConfigFileName = "runtime.conf";

  /// <summary>
  /// Loads runtime configuration from config files or auto-detects.
  /// Does NOT apply CLI overrides - that's done by the command handler.
  /// </summary>
  public static ContainerRuntimeConfig Load(AppPaths paths)
  {
    var localRuntime = LoadFromConfig(paths.GetLocalPath(ConfigFileName));
    var globalRuntime = LoadFromConfig(paths.GetGlobalPath(ConfigFileName));

    // Determine effective runtime and source
    string runtime;
    RuntimeConfigSource source;

    if (localRuntime is not null)
    {
      runtime = localRuntime;
      source = RuntimeConfigSource.Local;
    }
    else if (globalRuntime is not null)
    {
      runtime = globalRuntime;
      source = RuntimeConfigSource.Global;
    }
    else
    {
      runtime = AutoDetect() ?? "docker"; // Fallback to docker
      source = RuntimeConfigSource.AutoDetected;
    }

    var config = CreateConfig(runtime);
    return config with
    {
      Source = source,
      LocalRuntime = localRuntime,
      GlobalRuntime = globalRuntime
    };
  }

  /// <summary>
  /// Reads runtime from config file.
  /// Normalizes "auto" to null (to trigger auto-detection).
  /// Normalizes other values to lowercase.
  /// </summary>
  public static string? LoadFromConfig(string path)
  {
    var value = ConfigFile.ReadValue(path);
    if (value is null) return null;

    var normalized = value.ToLowerInvariant();
    return normalized == "auto" ? null : normalized;
  }

  /// <summary>
  /// Auto-detects available container runtime.
  /// Tries Docker first (including OrbStack), then Podman.
  /// Returns null if no runtime is available.
  /// </summary>
  public static string? AutoDetect()
  {
    // Try Docker first (most common)
    if (IsCommandAvailable("docker"))
    {
      return "docker";
    }

    // Try Podman as fallback
    if (IsCommandAvailable("podman"))
    {
      return "podman";
    }

    return null;
  }

  /// <summary>
  /// Checks if a command is available by running --version.
  /// </summary>
  public static bool IsCommandAvailable(string command)
  {
    try
    {
      var startInfo = new ProcessStartInfo(command, "--version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return false;

      process.WaitForExit();
      return process.ExitCode == 0;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Checks if the current Docker context is OrbStack.
  /// </summary>
  public static bool IsOrbStack()
  {
    try
    {
      var startInfo = new ProcessStartInfo("docker", "context show")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return false;

      var output = process.StandardOutput.ReadToEnd().Trim().ToLowerInvariant();
      process.WaitForExit();

      return process.ExitCode == 0 && output.Contains("orbstack");
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// Detects Podman compose support.
  /// Returns "compose" for built-in support, "podman-compose" for external, or "compose" as fallback.
  /// </summary>
  public static string DetectPodmanCompose()
  {
    // Check if podman has built-in compose support (podman compose --version)
    try
    {
      var startInfo = new ProcessStartInfo("podman", "compose --version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return "compose";

      process.WaitForExit();
      if (process.ExitCode == 0)
      {
        return "compose"; // Built-in compose support
      }
    }
    catch
    {
      // Fall through to check external command
    }

    // Check for external podman-compose command
    if (IsCommandAvailable("podman-compose"))
    {
      return "podman-compose";
    }

    // Default to compose (may not work, but better than crashing)
    return "compose";
  }

  /// <summary>
  /// Creates a runtime configuration for the specified runtime.
  /// </summary>
  public static ContainerRuntimeConfig CreateConfig(string runtime)
  {
    var normalized = runtime.ToLowerInvariant();

    return normalized switch
    {
      "docker" => new ContainerRuntimeConfig
      {
        Runtime = "docker",
        RuntimeFlavor = IsOrbStack() ? "OrbStack" : "Docker",
        ComposeCommand = "compose",
        SupportsAirlock = true,
        DefaultNetworkName = "bridge",
        Source = RuntimeConfigSource.AutoDetected, // Will be overridden by Load()
        LocalRuntime = null,
        GlobalRuntime = null
      },
      "podman" => new ContainerRuntimeConfig
      {
        Runtime = "podman",
        RuntimeFlavor = "Podman",
        ComposeCommand = DetectPodmanCompose(),
        SupportsAirlock = true,
        DefaultNetworkName = "podman",
        Source = RuntimeConfigSource.AutoDetected, // Will be overridden by Load()
        LocalRuntime = null,
        GlobalRuntime = null
      },
      _ => throw new InvalidOperationException(
        $"Unknown container runtime: {runtime}. Supported values: docker, podman")
    };
  }

  /// <summary>Saves runtime to local config.</summary>
  public static void SaveLocal(AppPaths paths, string runtime)
  {
    ConfigFile.WriteValue(paths.GetLocalPath(ConfigFileName), runtime);
  }

  /// <summary>Saves runtime to global config.</summary>
  public static void SaveGlobal(AppPaths paths, string runtime)
  {
    ConfigFile.WriteValue(paths.GetGlobalPath(ConfigFileName), runtime);
  }

  /// <summary>Clears local runtime config.</summary>
  public static bool ClearLocal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetLocalPath(ConfigFileName));
  }

  /// <summary>Clears global runtime config.</summary>
  public static bool ClearGlobal(AppPaths paths)
  {
    return ConfigFile.Delete(paths.GetGlobalPath(ConfigFileName));
  }

  /// <summary>
  /// Returns a human-friendly display string for the runtime configuration.
  /// </summary>
  public string GetDisplayString()
  {
    var sourceLabel = Source switch
    {
      RuntimeConfigSource.Local => "local config",
      RuntimeConfigSource.Global => "global config",
      RuntimeConfigSource.AutoDetected => "auto-detected",
      RuntimeConfigSource.CommandLine => "CLI override",
      _ => "unknown"
    };

    return $"{RuntimeFlavor} ({sourceLabel})";
  }

  /// <summary>
  /// Gets the version string for the runtime.
  /// </summary>
  public string GetVersion()
  {
    try
    {
      var startInfo = new ProcessStartInfo(Runtime, "--version")
      {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using var process = Process.Start(startInfo);
      if (process is null) return "unknown";

      var output = process.StandardOutput.ReadToEnd().Trim();
      process.WaitForExit();

      return process.ExitCode == 0 ? output : "unknown";
    }
    catch
    {
      return "unknown";
    }
  }

  /// <summary>
  /// Lists all available container runtimes on the system.
  /// </summary>
  public static List<ContainerRuntimeConfig> ListAvailable()
  {
    var runtimes = new List<ContainerRuntimeConfig>();

    if (IsCommandAvailable("docker"))
    {
      runtimes.Add(CreateConfig("docker"));
    }

    if (IsCommandAvailable("podman"))
    {
      runtimes.Add(CreateConfig("podman"));
    }

    return runtimes;
  }
}

/// <summary>
/// Source of the runtime configuration.
/// </summary>
public enum RuntimeConfigSource
{
  AutoDetected,
  Global,
  Local,
  CommandLine
}
