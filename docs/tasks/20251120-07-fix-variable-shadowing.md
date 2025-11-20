# Fix Variable Shadowing in Cleanup Function

**Date**: 2025-11-20
**Type**: Bug Fix
**Version**: 2025-11-20.12

## Problem Statement

The `__copilot_cleanup_images` function was using a variable named `image_name` in its `read` loop.
This variable name collided with the `local image_name` defined in the calling function `__copilot_run`.
In Bash (dynamic scoping), the inner function was modifying the caller's variable.
When the `read` loop finished, `image_name` was left empty (or with the last value), clobbering the correct image name in `__copilot_run`.
This resulted in `docker run` receiving an empty string as the image name, causing the `docker: invalid reference format` error.

## Solution Approach

Rename the loop variables in `__copilot_cleanup_images` to be distinct and explicitly local to avoid shadowing/clobbering variables in the calling scope.
Also removed the temporary debug output.

## Changes Made

### Files Modified
- `copilot_here.sh` - Renamed `image_name` to `cleanup_image_name` in cleanup function.
- `copilot_here.ps1` - (No change needed as PowerShell uses lexical scoping, but checked for consistency).

### Key Implementation Details

#### Bash/Zsh (`__copilot_cleanup_images`)
Changed:
```bash
while IFS='|' read -r image_id image_name created_at; do
```
To:
```bash
while IFS='|' read -r cleanup_image_id cleanup_image_name cleanup_created_at; do
```
And updated usages inside the loop.

## Testing Performed

- [ ] Verified version updates in all files
- [ ] Checked bash script syntax
