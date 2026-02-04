using System.CommandLine;
using CopilotHere.Infrastructure;

namespace CopilotHere.Commands.Runtime;

public sealed partial class RuntimeCommands
{
  /// <summary>
  /// Sets the container runtime for the current project (local).
  /// </summary>
  private static Command CreateSetRuntimeCommand()
  {
    var nameArg = new Argument<string>("runtime") { Description = "Runtime name: 'docker', 'podman', or 'auto'" };
    var command = new Command("--set-runtime", "Set container runtime in local config")
    {
      nameArg
    };
    
    command.SetAction(parseResult =>
    {
      var runtimeName = parseResult.GetValue(nameArg)?.ToLowerInvariant();
      
      if (string.IsNullOrWhiteSpace(runtimeName))
      {
        Console.WriteLine("❌ Runtime name cannot be empty");
        return 1;
      }
      
      // Validate runtime
      if (runtimeName != "auto" && runtimeName != "docker" && runtimeName != "podman")
      {
        Console.WriteLine($"❌ Invalid runtime: {runtimeName}");
        Console.WriteLine();
        Console.WriteLine("Valid options:");
        Console.WriteLine("  • auto    - Auto-detect available runtime");
        Console.WriteLine("  • docker  - Use Docker or OrbStack");
        Console.WriteLine("  • podman  - Use Podman");
        return 1;
      }
      
      // If not auto, verify runtime is available
      if (runtimeName != "auto" && !ContainerRuntimeConfig.IsCommandAvailable(runtimeName))
      {
        Console.WriteLine($"❌ Runtime '{runtimeName}' is not installed or not in PATH");
        Console.WriteLine();
        Console.WriteLine("Available runtimes:");
        var available = ContainerRuntimeConfig.ListAvailable();
        if (available.Count == 0)
        {
          Console.WriteLine("  (none found - please install Docker or Podman)");
        }
        else
        {
          foreach (var runtime in available)
          {
            Console.WriteLine($"  • {runtime.Runtime} ({runtime.RuntimeFlavor})");
          }
        }
        return 1;
      }
      
      // Save to local config
      var paths = AppPaths.Resolve();
      
      try
      {
        ContainerRuntimeConfig.SaveLocal(paths, runtimeName);
        
        // Show what was configured
        ContainerRuntimeConfig? actualConfig = null;
        if (runtimeName == "auto")
        {
          var detectedRuntime = ContainerRuntimeConfig.AutoDetect();
          if (detectedRuntime != null)
          {
            actualConfig = ContainerRuntimeConfig.CreateConfig(detectedRuntime);
          }
        }
        else
        {
          actualConfig = ContainerRuntimeConfig.CreateConfig(runtimeName);
        }
        
        Console.WriteLine($"✅ Set local runtime to: {runtimeName}");
        if (actualConfig != null)
        {
          if (runtimeName == "auto")
          {
            Console.WriteLine($"   Detected: {actualConfig.RuntimeFlavor}");
          }
          else
          {
            Console.WriteLine($"   Using: {actualConfig.RuntimeFlavor}");
          }
        }
        Console.WriteLine($"   Config: .copilot_here/runtime.conf");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"❌ Failed to save config: {ex.Message}");
        return 1;
      }
      
      return 0;
    });
    
    return command;
  }

  /// <summary>
  /// Sets the container runtime globally.
  /// </summary>
  private static Command CreateSetRuntimeGlobalCommand()
  {
    var nameArg = new Argument<string>("runtime") { Description = "Runtime name: 'docker', 'podman', or 'auto'" };
    var command = new Command("--set-runtime-global", "Set container runtime in global config")
    {
      nameArg
    };
    
    command.SetAction(parseResult =>
    {
      var runtimeName = parseResult.GetValue(nameArg)?.ToLowerInvariant();
      
      if (string.IsNullOrWhiteSpace(runtimeName))
      {
        Console.WriteLine("❌ Runtime name cannot be empty");
        return 1;
      }
      
      // Validate runtime
      if (runtimeName != "auto" && runtimeName != "docker" && runtimeName != "podman")
      {
        Console.WriteLine($"❌ Invalid runtime: {runtimeName}");
        Console.WriteLine();
        Console.WriteLine("Valid options:");
        Console.WriteLine("  • auto    - Auto-detect available runtime");
        Console.WriteLine("  • docker  - Use Docker or OrbStack");
        Console.WriteLine("  • podman  - Use Podman");
        return 1;
      }
      
      // If not auto, verify runtime is available
      if (runtimeName != "auto" && !ContainerRuntimeConfig.IsCommandAvailable(runtimeName))
      {
        Console.WriteLine($"❌ Runtime '{runtimeName}' is not installed or not in PATH");
        Console.WriteLine();
        Console.WriteLine("Available runtimes:");
        var available = ContainerRuntimeConfig.ListAvailable();
        if (available.Count == 0)
        {
          Console.WriteLine("  (none found - please install Docker or Podman)");
        }
        else
        {
          foreach (var runtime in available)
          {
            Console.WriteLine($"  • {runtime.Runtime} ({runtime.RuntimeFlavor})");
          }
        }
        return 1;
      }
      
      // Save to global config
      var paths = AppPaths.Resolve();
      
      try
      {
        ContainerRuntimeConfig.SaveGlobal(paths, runtimeName);
        
        // Show what was configured
        ContainerRuntimeConfig? actualConfig = null;
        if (runtimeName == "auto")
        {
          var detectedRuntime = ContainerRuntimeConfig.AutoDetect();
          if (detectedRuntime != null)
          {
            actualConfig = ContainerRuntimeConfig.CreateConfig(detectedRuntime);
          }
        }
        else
        {
          actualConfig = ContainerRuntimeConfig.CreateConfig(runtimeName);
        }
        
        Console.WriteLine($"✅ Set global runtime to: {runtimeName}");
        if (actualConfig != null)
        {
          if (runtimeName == "auto")
          {
            Console.WriteLine($"   Detected: {actualConfig.RuntimeFlavor}");
          }
          else
          {
            Console.WriteLine($"   Using: {actualConfig.RuntimeFlavor}");
          }
        }
        Console.WriteLine($"   Config: ~/.config/copilot_here/runtime.conf");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"❌ Failed to save config: {ex.Message}");
        return 1;
      }
      
      return 0;
    });
    
    return command;
  }
}
