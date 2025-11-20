# Fix unknown/unknown OS/Arch Labels in Docker Packages

**Date**: 2025-11-20  
**Type**: Bug Fix

## Problem
One of the OS/Arch combinations was showing as "unknown/unknown" in the GitHub Container Registry for variant Docker images (dotnet and dotnet-playwright variants).

Reference: https://github.com/GordonBeeming/copilot_here/pkgs/container/copilot_here/581532018?tag=sha-d0088f5498e3bf1cc796bf5b8365ff078afaf82c

## Root Cause
The workflow used `docker/metadata-action@v5` only once for the base image (Step 6). When building variant images (dotnet and dotnet-playwright), the workflow was reusing the same metadata labels from the base image with `${{ steps.meta.outputs.labels }}`.

This approach caused issues because:
1. The metadata labels were associated with different tags (latest/sha-xxx vs dotnet/dotnet-sha-xxx)
2. The OCI image labels (org.opencontainers.image.*) were not properly aligned with each variant's specific tags
3. Docker registry used these mismatched labels to incorrectly identify some platforms as "unknown/unknown"

## Solution
Added separate metadata extraction steps for each image variant:

### Changes Made

#### 1. Base Image (No Change)
- Step 6: Extract metadata for base image
- Step 11: Build and push base image using `steps.meta.outputs.*`

#### 2. .NET Image (New Metadata)
- **Step 12** (NEW): Extract metadata for .NET image
  ```yaml
  - name: Extract metadata for .NET image
    id: meta-dotnet
    uses: docker/metadata-action@v5
    with:
      images: ghcr.io/${{ steps.repo.outputs.name }}
      tags: |
        type=raw,value=dotnet
        type=raw,value=dotnet-sha-${{ github.sha }}
  ```
- **Step 13**: Build and push .NET image
  - Changed from hardcoded tags to `${{ steps.meta-dotnet.outputs.tags }}`
  - Changed from reused labels to `${{ steps.meta-dotnet.outputs.labels }}`

#### 3. Playwright+.NET Image (New Metadata)
- **Step 14** (NEW): Extract metadata for Playwright+.NET image
  ```yaml
  - name: Extract metadata for Playwright+.NET image
    id: meta-playwright
    uses: docker/metadata-action@v5
    with:
      images: ghcr.io/${{ steps.repo.outputs.name }}
      tags: |
        type=raw,value=dotnet-playwright
        type=raw,value=dotnet-playwright-sha-${{ github.sha }}
  ```
- **Step 15**: Build and push Playwright+.NET image
  - Changed from hardcoded tags to `${{ steps.meta-playwright.outputs.tags }}`
  - Changed from reused labels to `${{ steps.meta-playwright.outputs.labels }}`

#### 4. Updated Step Numbers
- Updated remaining step numbers in comments (Steps 16-17) to reflect the new steps

## Files Modified
- `.github/workflows/publish.yml`

## Testing
- [x] Validated YAML syntax with `yamllint`
- [x] Validated GitHub Actions syntax with `actionlint`
- [x] Verified workflow structure logic
- [ ] Will be verified when workflow runs on merge to main

## Expected Outcome
All three image variants (base, dotnet, dotnet-playwright) will have proper OS/Arch labels in the GitHub Container Registry:
- linux/amd64
- linux/arm64

The "unknown/unknown" entry should no longer appear.

## Technical Details
The `docker/metadata-action` generates OCI-compliant labels including:
- `org.opencontainers.image.source`
- `org.opencontainers.image.version`
- `org.opencontainers.image.created`
- `org.opencontainers.image.revision`

These labels must match the specific tags being published. By generating metadata separately for each image variant with its specific tags, we ensure the labels are correctly associated with each image's manifest, allowing Docker registry to properly identify the platform information.
