# SANDBOX_FLAGS Support Implementation

**Date:** 2025-12-05  
**Issue:** [#29 - Feature: sandbox flags](https://github.com/GordonBeeming/copilot_here/issues/29)

## Objective

Add support for the `SANDBOX_FLAGS` environment variable to allow users to pass custom Docker flags to the container, compatible with Gemini CLI convention.

## Problem Statement

Users requested the ability to customize Docker container behavior (networking, environment variables, capabilities, resource limits) without modifying the tool itself. This is particularly useful for:
- Custom network configurations (e.g., `--network host`, `--network my-custom-net`)
- Passing environment variables to scripts
- Setting resource limits
- Adding Linux capabilities

## Solution Approach

Implemented `SANDBOX_FLAGS` environment variable support using the same convention as Gemini CLI for interoperability.

### Key Design Decisions

1. **Single Environment Variable**: Use only `SANDBOX_FLAGS` (Gemini-compatible) instead of creating a copilot_here-specific variable
2. **Works in Both Modes**: Supports both normal Docker mode and Airlock proxy mode
3. **Airlock Network Handling**: In Airlock mode, `--network` flag modifies the proxy's external network while keeping the app container isolated
4. **Security Maintained**: App container in Airlock mode remains on internal network, cannot bypass proxy

### Architecture

**Normal Mode:**
```
SANDBOX_FLAGS → Parse → Inject into docker run command
```

**Airlock Mode:**
```
SANDBOX_FLAGS → Parse → Extract --network → Apply to proxy
                      → Filter out --network → Convert to YAML → Apply to app container
```

## Changes Made

### New Files

- **`app/Infrastructure/SandboxFlags.cs`** - Helper class for parsing and processing sandbox flags
  - `Parse()` - Parses `SANDBOX_FLAGS` env var into list of arguments
  - `ExtractNetwork()` - Extracts `--network` flag value
  - `FilterNetworkFlags()` - Removes network flags from list
  - `ToComposeYaml()` - Converts Docker flags to Docker Compose YAML format
  - `ShellSplit()` - Shell-style argument splitting with quote support

### Modified Files

- **`app/Commands/Run/RunCommand.cs`**
  - Updated `BuildDockerArgs()` to inject sandbox flags into Docker run command
  - Added `GenerateSessionId()` method for consistent container naming
  - Added `--name` flag to normal mode containers (pattern: `copilot_here-{sessionId}`)

- **`app/Infrastructure/AirlockRunner.cs`**
  - Updated `Run()` to parse sandbox flags and extract network configuration
  - Updated `GenerateComposeFile()` to accept external network and app flags
  - Added network YAML generation based on `--network` flag
  - Added `{{EXTRA_SANDBOX_FLAGS}}` placeholder handling

- **`app/Resources/docker-compose.airlock.yml.template`**
  - Added `{{NETWORKS}}` placeholder for dynamic network configuration
  - Added `{{EXTERNAL_NETWORK}}` placeholder for proxy's external network
  - Added `{{EXTRA_SANDBOX_FLAGS}}` placeholder for app container flags

- **`README.md`**
  - Added "Custom Docker Flags (SANDBOX_FLAGS)" section with examples
  - Documented supported flags and Airlock behavior

### Test Files

- **`tests/CopilotHere.UnitTests/SandboxFlagsTests.cs`** - C# unit tests for SandboxFlags class

## Testing Performed

- [x] Unit tests for flag parsing (C# - TUnit framework)
- [x] Network flag extraction tests
- [x] Flag filtering tests
- [x] Docker-to-Compose YAML conversion tests
- [x] All 232 tests passing
- [ ] Manual testing with real Docker scenarios (pending)

## Usage Examples

### Normal Mode

```bash
# Host networking
export SANDBOX_FLAGS="--network host"
copilot_here

# Custom environment variables
export SANDBOX_FLAGS="--env DEBUG=1 --env API_KEY=secret"
copilot_here

# Resource limits
export SANDBOX_FLAGS="--memory 2g --cpus 1.5"
copilot_here
```

### Airlock Mode

```bash
# Proxy connects to custom network, app stays isolated
docker network create my-services
SANDBOX_FLAGS="--network my-services" copilot_here --enable-airlock

# Host networking for proxy (access host services)
SANDBOX_FLAGS="--network host" copilot_here --enable-airlock
```

## Supported Flags

- ✅ `--network <name>` - Custom Docker network
- ✅ `--env <KEY=value>` - Environment variables
- ✅ `--cap-add <capability>` - Add Linux capabilities
- ✅ `--cap-drop <capability>` - Drop Linux capabilities
- ✅ `--memory <limit>` - Memory limit
- ✅ `--cpus <number>` - CPU limit
- ✅ `--ulimit <type>=<limit>` - Ulimits

## Security Considerations

- **Airlock mode maintains isolation**: App container stays on internal network even with `--network` flag
- **Network flag only affects proxy**: In Airlock mode, custom networks apply to proxy's external connection
- **No validation bypass**: App container cannot directly access custom networks
- **User responsibility**: In normal mode, users accept responsibility for security implications of custom flags

## Known Issues / Limitations

- Network specified in `SANDBOX_FLAGS` must exist before running (will fail if network doesn't exist)
- Some Docker flags may not translate to Docker Compose YAML (added as comments)
- Quote handling in complex arguments may need refinement based on real-world usage

## Follow-up Items

- [ ] Consider adding flag validation/warnings for potentially dangerous flags
- [ ] Monitor user feedback on which flags are most commonly used
- [ ] Consider adding shorthand syntax for common flag combinations
- [ ] Add troubleshooting section to docs for network-related issues

## References

- [Gemini CLI Sandbox Documentation](https://geminicli.com/docs/cli/sandbox/#custom-sandbox-flags)
- [GitHub Issue #29](https://github.com/GordonBeeming/copilot_here/issues/29)
