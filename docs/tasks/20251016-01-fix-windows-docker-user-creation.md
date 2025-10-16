# Fix Windows Docker Desktop User Creation Issue

**Date**: 2025-10-16  
**Type**: Bug Fix

## Problem

When running the Docker container on Windows Docker Desktop, users encountered the following error:

```
error: failed switching to "appuser": unable to find user appuser: no matching entries in passwd file
```

This occurred because the `useradd` command in the entrypoint script was failing silently (due to `|| true`), but the script continued to attempt switching to the non-existent user with `gosu appuser`.

## Root Cause

On Windows Docker Desktop, the user creation commands (`groupadd` and `useradd`) may fail for various reasons related to how Windows handles Linux containers. The script was suppressing these errors with `|| true` but didn't verify that the user was actually created before attempting to switch to it.

## Solution

Updated the `entrypoint.sh` script to:

1. Check if the `appuser` was successfully created using `id appuser`
2. If user creation failed, display a warning and run as root instead
3. Only attempt to switch to `appuser` if the user exists

This provides a graceful fallback that allows the container to run even when user creation fails, while still maintaining the security benefits of running as a non-root user on systems where it works correctly.

## Changes Made

### Modified Files

- `entrypoint.sh`: Added user existence check before attempting to switch users
- `README.md`: Restructured setup instructions to treat Windows as a first-class platform

### Code Changes

**entrypoint.sh:**
```bash
# Verify the user was created successfully
if ! id appuser >/dev/null 2>&1; then
    echo "Warning: Failed to create appuser, running as root" >&2
    mkdir -p /home/appuser/.copilot
    exec "$@"
fi
```

**README.md:**
- Reorganized setup instructions to show modes first (Safe vs YOLO), then platforms
- Added Windows PowerShell instructions alongside Linux/macOS bash instructions
- Grouped code in collapsible sections for better readability
- Made usage examples show both platforms side-by-side

## Testing

The fix should be tested on:

- [x] Linux Docker (should continue working as before)
- [ ] Windows Docker Desktop (should no longer fail with user creation error)
- [ ] macOS Docker Desktop (should continue working as before)

## Notes

- Running as root is not ideal from a security perspective, but it's acceptable for local development environments
- The warning message alerts users that user creation failed, in case they want to investigate
- This is a temporary fallback solution; future improvements could investigate why user creation fails on Windows and implement a more robust solution
- Windows is now treated as a first-class platform in the documentation with equal prominence
