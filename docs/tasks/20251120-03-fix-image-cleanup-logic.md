# Fix Image Cleanup Logic for Old and Untagged Images

**Date**: 2025-11-20
**Type**: Bug Fix
**Version**: 2025-11-20.11

## Problem Statement

The image cleanup logic had several issues preventing it from effectively removing old images:
1. **Label Dependency**: It relied on the `project=copilot_here` label, which older images or locally built ones might lack.
2. **Untagged Images**: It explicitly filtered out `<none>` tagged images, but updated images often leave behind untagged "dangling" versions that need cleanup.
3. **Removal Failures**: Even when identified, `docker rmi` would fail to remove images referenced by stopped containers (common with ephemeral runs).
4. **Ambiguous References**: Attempting to remove untagged images by name (`repository:<none>`) is unreliable in Docker.

## Solution Approach

A comprehensive fix was implemented:
1. **Repository Filtering**: Switch to filtering by repository name (`ghcr.io/gordonbeeming/copilot_here`) instead of labels to catch all relevant images.
2. **Include Untagged**: Remove the filter excluding `<none>` tags to allow cleanup of dangling images.
3. **Force Removal**: Use `docker rmi -f` to remove images even if referenced by stopped containers (running containers are still protected by Docker).
4. **ID-Based Removal**: Switch to using unique **Image IDs** (SHA) for removal operations instead of names, ensuring precise targeting of specific images.
5. **Safety Check**: Explicitly resolve the ID of the "current" image (the one to keep) and ensure it is never removed, regardless of its tags.

## Changes Made

### Files Modified
- `copilot_here.sh` - Updated `__copilot_cleanup_images` function
- `copilot_here.ps1` - Updated `Remove-UnusedCopilotImages` function

### Key Implementation Details

#### Bash/Zsh (`__copilot_cleanup_images`)
- Resolve `keep_image_id` using `docker inspect`.
- Get all images using `docker images --no-trunc ...` to get full IDs.
- Iterate through images and compare IDs.
- Execute `docker rmi -f "$image_id"` for eligible old images.

#### PowerShell (`Remove-UnusedCopilotImages`)
- Resolve `keepImageId` using `docker inspect`.
- Get all images using `docker images --no-trunc ...` to get full IDs.
- Iterate through images and compare IDs.
- Execute `docker rmi -f $imageId` for eligible old images.

## Testing Performed

- [x] Verified version updates in all files
- [x] Checked bash script syntax
- [x] Checked PowerShell script syntax
- [x] Verified cleanup of old images (mock test)
- [x] Verified protection of current image
