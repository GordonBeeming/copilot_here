# Session Information Environment Variable

**Date:** 2025-12-29  
**Feature:** `COPILOT_HERE_SESSION_INFO` environment variable

## Overview

Added a new environment variable `COPILOT_HERE_SESSION_INFO` that contains JSON-formatted session information available inside copilot_here containers. This makes it easy for AI assistants and users to understand the environment they're working in without scrolling back through startup logs.

## Changes Made

### 1. New SessionInfo Infrastructure (`app/Infrastructure/SessionInfo.cs`)
- Created AOT-compatible JSON builder for session information
- Two methods:
  - `Generate()` - Basic session info for standard mode
  - `GenerateWithNetworkConfig()` - Extended info for Airlock mode with network rules details
- Manual JSON building to avoid reflection/dynamic code in Native AOT

### 2. Updated RunCommand
- Modified `BuildDockerArgs()` to accept `imageTag` parameter
- Generate session info JSON and pass as environment variable `-e COPILOT_HERE_SESSION_INFO=...`

### 3. Updated AirlockRunner
- Modified `GenerateComposeFile()` to include session info in compose template
- Adds `COPILOT_HERE_SESSION_INFO={{SESSION_INFO}}` to app container environment

### 4. Updated Docker Compose Template
- Added `COPILOT_HERE_SESSION_INFO={{SESSION_INFO}}` environment variable to app service

### 5. Added Session Info Helper Script
- Created `docker/session-info.sh` - shell script to display session info
- Installed as `/usr/local/bin/session-info` in base image
- Pretty-prints JSON with `jq` if available, fallback to plain JSON

### 6. Updated Base Dockerfile
- Copy `session-info.sh` to `/usr/local/bin/session-info`
- Make executable for easy access

## Usage

### View Session Information

Inside any copilot_here container:

```bash
# Pretty-printed (if jq available)
session-info

# Raw JSON
echo $COPILOT_HERE_SESSION_INFO

# Query specific fields with jq
echo $COPILOT_HERE_SESSION_INFO | jq .image.tag
echo $COPILOT_HERE_SESSION_INFO | jq .mounts
echo $COPILOT_HERE_SESSION_INFO | jq .airlock.network_config
```

### Session Info Structure

**Standard Mode:**
```json
{
  "copilot_here_version": "2025.12.29",
  "image": {
    "tag": "dotnet-rust",
    "full_name": "ghcr.io/gordonbeeming/copilot_here:dotnet-rust"
  },
  "mode": "yolo",
  "working_directory": "/work",
  "mounts": [
    {
      "host_path": "/Users/me/projects/myapp",
      "container_path": "/work",
      "mode": "rw",
      "source": "commandline"
    },
    {
      "host_path": "/Users/me/.gitconfig",
      "container_path": "/home/appuser/.gitconfig",
      "mode": "ro",
      "source": "global"
    }
  ],
  "airlock": {
    "enabled": false,
    "rules_path": "",
    "source": "none"
  }
}
```

**Airlock Mode (Extended):**
```json
{
  "copilot_here_version": "2025.12.29",
  "image": {
    "tag": "dotnet",
    "full_name": "ghcr.io/gordonbeeming/copilot_here:dotnet"
  },
  "mode": "standard",
  "working_directory": "/work",
  "mounts": [...],
  "airlock": {
    "enabled": true,
    "rules_path": "/tmp/copilot-network-config-abc123.json",
    "source": "local",
    "network_config": {
      "mode": "enforce",
      "logging_enabled": true,
      "rules_count": 42,
      "sample_domains": [
        "github.com",
        "api.github.com",
        "*.npmjs.org",
        "registry.yarnpkg.com",
        "*.nuget.org"
      ]
    }
  }
}
```

## Benefits

1. **Easy AI Context** - AI assistants can quickly reference environment details without scrolling
2. **No Filesystem Pollution** - Data stored in environment variable, not files
3. **Ephemeral** - Automatically cleaned when container stops
4. **Queryable** - Standard JSON format works with `jq` and other tools
5. **Airlock Transparency** - Network rules visible at a glance

## Implementation Notes

- **AOT-Compatible** - Manual JSON building avoids reflection/dynamic code
- **Escaping** - Proper JSON string escaping for paths with special characters
- **Non-Breaking** - Existing functionality unchanged, purely additive
- **Airlock-Aware** - Different info levels for standard vs. airlock modes

## Files Modified

- `app/Infrastructure/SessionInfo.cs` (new)
- `app/Commands/Run/RunCommand.cs`
- `app/Infrastructure/AirlockRunner.cs`
- `app/Resources/docker-compose.airlock.yml.template`
- `docker/Dockerfile.base`
- `docker/session-info.sh` (new)

## Testing Checklist

- [x] Build native binary without warnings
- [ ] Test standard mode: `copilot_here`
- [ ] Test YOLO mode: `copilot_yolo`
- [ ] Test Airlock mode with network config
- [ ] Verify `session-info` command works (after image rebuild)
- [ ] Verify `echo $COPILOT_HERE_SESSION_INFO | python3 -m json.tool` shows formatted JSON
- [ ] Test with different image variants
- [ ] Verify mounts appear with resolved (absolute) paths
- [ ] Test with special characters in paths

## Known Issues

- **Image Rebuild Required**: The `session-info` command won't be available until Docker images are rebuilt. Users can use `echo $COPILOT_HERE_SESSION_INFO | python3 -m json.tool` as a workaround.

## Follow-up Tasks

- [ ] Trigger Docker image rebuild in CI/CD
- [ ] Test in production after image deployment
- [ ] Update version numbers if needed
