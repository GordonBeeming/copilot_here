# Fix Workflow Image Tag Mismatch

**Date:** 2025-10-09  
**Issue:** GitHub Actions workflow failing when building Playwright variant image

## Problem Statement

The GitHub Actions workflow was failing at step 11 "Build and push Playwright image" with the following error:

```
ERROR: failed to build: failed to solve: ghcr.io/gordonbeeming/copilot_here:sha-e4cc1c4dd89220fe053bcfe51911f93319c044f8: failed to resolve source metadata for ghcr.io/gordonbeeming/copilot_here:sha-e4cc1c4dd89220fe053bcfe51911f93319c044f8: ghcr.io/gordonbeeming/copilot_here:sha-e4cc1c4dd89220fe053bcfe51911f93319c044f8: not found
```

**Workflow Run:** https://github.com/GordonBeeming/copilot_here/actions/runs/18372997964/job/52340446791

## Root Cause Analysis

The issue was a mismatch between the Docker image tag formats:

1. **Base Image Tags:** The `docker/metadata-action@v5` with `type=sha` creates tags with SHORT SHA format:
   - `main`
   - `latest`  
   - `sha-e4cc1c4` (7-character short SHA)

2. **Playwright Build Reference:** The workflow tried to build the Playwright image using:
   - `BASE_IMAGE_TAG=sha-${{ github.sha }}` 
   - This expands to the FULL SHA: `sha-e4cc1c4dd89220fe053bcfe51911f93319c044f8` (40 characters)

3. **Result:** The Playwright build step looked for a base image tag that didn't exist, causing the build to fail.

## Solution

Changed the variant image builds to use stable, existing tags instead of SHA-based tags:

### Before:
```yaml
# Playwright image
build-args: |
  BASE_IMAGE_TAG=sha-${{ github.sha }}  # Full SHA - doesn't exist!

# .NET image  
build-args: |
  PLAYWRIGHT_IMAGE_TAG=playwright-sha-${{ github.sha }}  # Full SHA - doesn't exist!
```

### After:
```yaml
# Playwright image
build-args: |
  BASE_IMAGE_TAG=latest  # Uses the just-pushed base image

# .NET image
build-args: |
  PLAYWRIGHT_IMAGE_TAG=playwright  # Uses the just-pushed Playwright image
```

## Changes Made

**File:** `.github/workflows/publish.yml`

- Line 121: Changed `BASE_IMAGE_TAG=sha-${{ github.sha }}` → `BASE_IMAGE_TAG=latest`
- Line 138: Changed `PLAYWRIGHT_IMAGE_TAG=playwright-sha-${{ github.sha }}` → `PLAYWRIGHT_IMAGE_TAG=playwright`

## Why This Works

1. **Sequential Build Order:** The workflow builds and pushes images in order:
   - Base image → Playwright image → .NET image

2. **Stable Tags:** Each image is tagged with stable names (`latest`, `playwright`) that the next build can reliably reference

3. **Traceability Maintained:** Each variant image still gets its own SHA-tagged version for traceability:
   - `playwright-sha-${{ github.sha }}`
   - `dotnet-sha-${{ github.sha }}`

## Benefits of This Approach

- ✅ **Simple and Reliable:** Uses straightforward tag names that always exist
- ✅ **No SHA Format Issues:** Avoids complexity of matching short vs. full SHA formats
- ✅ **Cache-Friendly:** Registry caching still works with stable tag names
- ✅ **Traceability:** Commit-specific tags are still created for all images

## Testing

The fix was validated by reviewing the workflow logic:
- Base image is always pushed with `latest` tag before Playwright build starts
- Playwright image is always pushed with `playwright` tag before .NET build starts
- Each variant can reliably reference the just-pushed stable tag

## Follow-up

None required. The fix is complete and ready for the next workflow run.
