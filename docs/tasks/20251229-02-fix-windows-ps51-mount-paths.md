# Fix Windows PowerShell 5.1 Mount Path Issue

**Date:** 2025-12-29  
**Version:** 2025.12.29.10

## Problem

On Windows PowerShell 5.1 (not PowerShell Core), Docker volume mounts were failing with error:
```
docker: Error response from daemon: mount denied: the source path "\_code\\github\\gordonbeeming\\copilot_here\\default-airlock-rules.json:C:\_code\\github\\gordonbeeming\\copilot_here\\default-airlock-rules.json:ro" too many colons
```

The issue was that Windows paths with drive letters (e.g., `C:\path\to\file`) contain colons, and when used in Docker volume mount format (`hostPath:containerPath:mode`), Docker interprets this as having too many colons.

## Root Cause

The code was passing native Windows paths directly to Docker without converting them to Docker-compatible format. Docker on Windows expects paths in Unix-style format:
- Windows: `C:\Users\name\project`
- Docker: `/c/Users/name/project`

## Solution

Added a `ConvertToDockerPath()` helper method that:
1. Converts backslashes to forward slashes
2. On Windows, converts drive letter paths from `C:/path` to `/c/path`
3. On Unix/Linux, leaves paths unchanged

Applied this conversion in three locations:

### 1. `app/Commands/Mounts/_MountsConfig.cs`
- Updated `MountEntry.ToDockerVolume()` method
- Added private `ConvertToDockerPath()` helper

### 2. `app/Commands/Run/RunCommand.cs`
- Updated `BuildDockerArgs()` to convert current directory and copilot config paths
- Added private `ConvertToDockerPath()` helper

### 3. `app/Infrastructure/AirlockRunner.cs`
- Updated `GenerateComposeFile()` to convert all paths:
  - Extra mount paths
  - Logs directory path
  - Work directory
  - Copilot config path
  - Network config path
- Added private `ConvertToDockerPath()` helper

## Changes Made

- [x] Updated `MountEntry.ToDockerVolume()` in `_MountsConfig.cs`
- [x] Updated `BuildDockerArgs()` in `RunCommand.cs`
- [x] Updated `GenerateComposeFile()` in `AirlockRunner.cs`
- [x] Added `ConvertToDockerPath()` helper to all three files
- [x] Updated version to 2025.12.29.10 in all locations:
  - `copilot_here.sh`
  - `copilot_here.ps1`
  - `Directory.Build.props`
  - `app/Infrastructure/BuildInfo.cs`
- [x] Verified build succeeds
- [x] Verified all 278 unit tests pass

## Testing Performed

- Compilation: ✅ Build succeeded
- Unit Tests: ✅ All 278 tests passed
- Platform: Tested on Linux (will be tested on Windows by user)

## Impact

- Windows users on PowerShell 5.1 will now be able to use mount paths correctly
- Windows users on PowerShell Core (already working) continue to work
- Linux/macOS users unaffected (paths pass through unchanged)
- Both standard mode and Airlock mode are fixed

## Notes

The fix is compatible with both PowerShell 5.1 and PowerShell Core on Windows. The `OperatingSystem.IsWindows()` check ensures the conversion only happens on Windows platforms.
