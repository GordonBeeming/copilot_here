# Add ARM64 Multi-Platform Support

**Date**: 2025-11-19  
**Issue**: [#9 - Add support for ARM architecture (linux/arm64)](https://github.com/GordonBeeming/copilot_here/issues/9)  
**Requested by**: @fabianlema

## Problem

Users running the Docker images on ARM-based machines (e.g., Apple Silicon M1/M2/M3) received warnings that the images were built for linux/amd64 only:

```
WARNING: The requested image's platform (linux/amd64) does not match 
the detected host platform (linux/arm64/v8) and no specific platform was requested
```

While the images work through emulation, this causes slower performance and unnecessary warnings. Native ARM64 support was needed.

## Solution

Updated the GitHub Actions workflow to build and publish multi-architecture Docker images supporting both `linux/amd64` and `linux/arm64` platforms using Docker Buildx.

## Changes Made

### Workflow Updates (`.github/workflows/publish.yml`)

1. **Added QEMU setup** (Step 3)
   - Added `docker/setup-qemu-action@v3` to enable cross-platform emulation during builds

2. **Added Buildx setup** (Step 4)
   - Added `docker/setup-buildx-action@v3` to enable multi-platform builds

3. **Updated base image build** (Step 9)
   - Added `platforms: linux/amd64,linux/arm64`
   - Changed `load: true` to `load: false` (can't load multi-platform images locally)
   - Added `outputs: type=image,push=false` for clarity

4. **Updated base image push** (Step 11)
   - Converted from shell script to `docker/build-push-action@v5`
   - Added `platforms: linux/amd64,linux/arm64`
   - Builds and pushes both architectures in one step

5. **Updated .NET image build** (Step 12)
   - Added `platforms: linux/amd64,linux/arm64`

6. **Updated Playwright+.NET image build** (Step 13)
   - Added `platforms: linux/amd64,linux/arm64`

7. **Renumbered all steps** to maintain sequential order (Steps 3-15)

### Dockerfile Updates (`Dockerfile`)

8. **Fixed PowerShell installation for ARM64**
   - AMD64: Uses Microsoft's APT repository (existing method)
   - ARM64: Downloads PowerShell 7.4.6 from GitHub releases and installs to `/opt/microsoft/powershell/7`
   - Architecture detection using `dpkg --print-architecture`
   - Creates symlink at `/usr/bin/pwsh` for both architectures

### Multi-Platform Build Process

The workflow now:
1. Builds all three image variants (base, dotnet, dotnet-playwright) for both platforms
2. Creates manifest lists that automatically select the correct platform
3. Publishes to `ghcr.io/gordonbeeming/copilot_here` with multi-arch support

## Testing

The changes will be tested when the workflow runs by:
- Building for both `linux/amd64` and `linux/arm64`
- Verifying manifest lists are created correctly
- Testing on ARM-based machines (Apple Silicon) to confirm native execution without warnings

## Benefits

- ✅ Native performance on ARM64 machines (Apple Silicon, ARM servers)
- ✅ No more platform mismatch warnings
- ✅ Automatic platform selection by Docker
- ✅ Maintains backward compatibility with AMD64
- ✅ All three image variants (base, dotnet, dotnet-playwright) support both architectures

## Notes

- Build times will increase slightly due to building for two platforms
- GitHub Actions runners use QEMU for ARM64 builds (cross-compilation)
- The workflow maintains the same caching strategy for both platforms
- No changes needed to child Dockerfiles (dotnet, playwright) - they inherit the multi-arch base
- PowerShell on ARM64 uses version 7.4.6 from GitHub releases (APT repo only has AMD64 packages)

## Build Failure & Fix

**Initial build failed** due to PowerShell installation failing on ARM64:
- Microsoft's APT repository doesn't provide PowerShell packages for ARM64/Debian
- Fixed by implementing architecture-specific installation:
  - AMD64: Continues to use APT repository
  - ARM64: Downloads and extracts PowerShell 7.4.6 tarball from GitHub releases
