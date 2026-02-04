# Container Runtime Support

**Date**: 2026-02-04

## Overview

Added support for multiple container runtimes (Docker, Podman, OrbStack) with automatic detection and configuration management. Users can now choose their preferred container runtime or let the system auto-detect the best available option.

## Changes Made

### New Components

1. **`ContainerRuntimeConfig.cs`** - Core runtime configuration system
   - Auto-detection of Docker, Podman, and OrbStack
   - Configuration priority: CLI > Local > Global > Auto-detect
   - Runtime-specific settings (compose command, network name, airlock support)
   - Podman compose detection (built-in vs external)

2. **Runtime Commands** (`app/Commands/Runtime/`)
   - `--show-runtime` - Display current runtime configuration and available runtimes
   - `--list-runtimes` - List all available container runtimes on the system
   - `--set-runtime <runtime>` - Set runtime in local config (.copilot_here/runtime.conf)
   - `--set-runtime-global <runtime>` - Set runtime in global config (~/.config/copilot_here/runtime.conf)

3. **Configuration Files**
   - Local: `.copilot_here/runtime.conf`
   - Global: `~/.config/copilot_here/runtime.conf`
   - Values: `docker`, `podman`, or `auto` (for auto-detection)

### Updated Components

1. **`ContainerRunner.cs`** (renamed from `DockerRunner.cs`)
   - Now accepts `ContainerRuntimeConfig` parameter
   - Uses configured runtime instead of hardcoded "docker"
   - Updated all Docker-specific references to be runtime-agnostic

2. **`DependencyCheck.cs`**
   - Updated to check configured runtime instead of only Docker
   - Runtime-agnostic error messages and help text
   - Detects and reports specific runtime flavor (Docker, OrbStack, Podman)

3. **`AirlockRunner.cs`**
   - Updated to use configured runtime for compose commands
   - Supports both `docker compose` and `podman compose` syntax

4. **`RunCommand.cs`**
   - Displays runtime flavor in output: "🐳 Container runtime: Docker"
   - Passes runtime config to all container operations

5. **`AppContext.cs`**
   - Added `RuntimeConfig` property
   - Loads runtime configuration during context creation

## Supported Runtimes

### Docker
- **Command**: `docker`
- **Compose**: `docker compose` (built-in)
- **Network**: `bridge`
- **Airlock**: ✅ Supported

### OrbStack
- **Command**: `docker` (context: orbstack)
- **Compose**: `docker compose` (built-in)
- **Network**: `bridge`
- **Airlock**: ✅ Supported
- **Auto-detected**: When `docker context show` contains "orbstack"

### Podman
- **Command**: `podman`
- **Compose**: `podman compose` or `podman-compose` (auto-detected)
- **Network**: `podman`
- **Airlock**: ✅ Supported

## Configuration Priority

Following the project's configuration priority standard:

1. **CLI Arguments** (future: `--runtime docker/podman`)
2. **Local Config** (`.copilot_here/runtime.conf`)
3. **Global Config** (`~/.config/copilot_here/runtime.conf`)
4. **Auto-detect** (tries Docker first, then Podman)

## Usage Examples

### Show Current Runtime
```bash
copilot_here --show-runtime
```

Output:
```
🐳 Container Runtime: Docker
   Command: docker
   Version: Docker version 25.0.0, build xxx

   Source: 🌐 Global (~/.config/copilot_here/runtime.conf)
   🌐 Global config: docker

   Capabilities:
     • Compose command: docker compose
     • Airlock support: Yes
     • Default network: bridge

   Available runtimes:
   ▶ Docker (docker)
     Podman (podman)
```

### List Available Runtimes
```bash
copilot_here --list-runtimes
```

### Set Runtime Locally
```bash
# Use Podman for this project
copilot_here --set-runtime podman

# Use Docker for this project
copilot_here --set-runtime docker

# Use auto-detection for this project
copilot_here --set-runtime auto
```

### Set Runtime Globally
```bash
# Use Podman globally
copilot_here --set-runtime-global podman

# Use Docker globally
copilot_here --set-runtime-global docker
```

## Testing

### Unit Tests
- `ContainerRuntimeConfigTests.cs` - Configuration loading, priority, auto-detection
- `DependencyCheckTests.cs` - Updated to verify runtime-agnostic checks

### Manual Testing
- ✅ Docker on Linux (auto-detect)
- ✅ Docker on macOS (auto-detect)
- ✅ OrbStack on macOS (auto-detect and display)
- ✅ Podman on Linux (auto-detect and usage)
- ✅ Local config override
- ✅ Global config override
- ✅ Manual runtime switching

## Migration Notes

### For Users
- **No action required** - Auto-detection will continue to use Docker if available
- **Optional**: Set preferred runtime with `--set-runtime` or `--set-runtime-global`
- **Existing behavior**: Everything works the same by default

### For Code
- `DockerRunner` renamed to `ContainerRunner`
- All methods now require `ContainerRuntimeConfig` parameter
- Hardcoded "docker" strings replaced with `runtimeConfig.Runtime`
- References to "Docker" in messages updated to use `runtimeConfig.RuntimeFlavor`

## Future Enhancements

- [ ] CLI flag: `--runtime docker/podman` for one-time override
- [ ] Runtime-specific optimization settings
- [ ] Support for additional runtimes (nerdctl, containerd, etc.)
- [ ] Runtime health checks and diagnostics
- [ ] Per-image runtime preferences

## Breaking Changes

**None** - This is a backward-compatible addition. Existing installations will continue using Docker via auto-detection.

## Files Changed

- Added: `app/Infrastructure/ContainerRuntimeConfig.cs`
- Added: `app/Commands/Runtime/ListRuntimes.cs`
- Added: `app/Commands/Runtime/SetRuntime.cs`
- Added: `app/Commands/Runtime/ShowRuntime.cs`
- Added: `app/Commands/Runtime/_RuntimeCommands.cs`
- Added: `tests/CopilotHere.UnitTests/ContainerRuntimeConfigTests.cs`
- Renamed: `app/Infrastructure/DockerRunner.cs` → `ContainerRunner.cs`
- Modified: `app/Infrastructure/DependencyCheck.cs`
- Modified: `app/Infrastructure/AirlockRunner.cs`
- Modified: `app/Infrastructure/AppContext.cs`
- Modified: `app/Commands/Run/RunCommand.cs`
- Modified: `app/Commands/Model/ListModels.cs`
- Modified: `app/Tools/GitHubCopilotModelProvider.cs`
- Modified: `app/Program.cs`
- Modified: `tests/CopilotHere.UnitTests/DependencyCheckTests.cs`
- Modified: `dev-build.sh` (updated to handle config files)
