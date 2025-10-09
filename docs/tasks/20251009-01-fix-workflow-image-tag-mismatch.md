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

## Solution Evolution

### Initial Fix (Commits 8381311, 7158aee)
Changed the variant image builds to use stable tags (`latest`, `playwright`) instead of SHA-based references. This worked but didn't address the need for unique tags during concurrent workflow runs.

### Final Solution (Commit 8377015)
Based on user feedback, updated the workflow to use SHA-based tags to prevent conflicts during concurrent runs:

**Step 9 - Push base image with full SHA tag:**
```yaml
- name: Push image to registry
  if: steps.push_decision.outputs.push_needed == 'true'
  run: |
    docker push --all-tags ghcr.io/${{ steps.repo.outputs.name }}
    # Tag with full SHA for variant builds to reference
    docker tag ghcr.io/${{ steps.repo.outputs.name }}:latest ghcr.io/${{ steps.repo.outputs.name }}:sha-${{ github.sha }}
    docker push ghcr.io/${{ steps.repo.outputs.name }}:sha-${{ github.sha }}
```

**Step 10 - Playwright image references full SHA:**
```yaml
build-args: |
  BASE_IMAGE_TAG=sha-${{ github.sha }}  # Now this tag exists!
```

**Step 11 - .NET image references Playwright's full SHA:**
```yaml
build-args: |
  PLAYWRIGHT_IMAGE_TAG=playwright-sha-${{ github.sha }}  # References the variant's SHA tag
```

## Changes Made

**File:** `.github/workflows/publish.yml`

### Initial Fix (Commits 8381311, 7158aee):
- Line 121: Changed `BASE_IMAGE_TAG=sha-${{ github.sha }}` → `BASE_IMAGE_TAG=latest`
- Line 138: Changed `PLAYWRIGHT_IMAGE_TAG=playwright-sha-${{ github.sha }}` → `PLAYWRIGHT_IMAGE_TAG=playwright`

### Final Update (Commit 8377015):
- Line 111-115: Added multi-line run command to tag and push base image with full SHA
- Line 126: Changed `BASE_IMAGE_TAG=latest` → `BASE_IMAGE_TAG=sha-${{ github.sha }}`
- Line 143: Changed `PLAYWRIGHT_IMAGE_TAG=playwright` → `PLAYWRIGHT_IMAGE_TAG=playwright-sha-${{ github.sha }}`

## Why This Works

1. **Full SHA Tag Created:** After pushing the base image with metadata tags, we explicitly tag it with the full SHA and push that tag
2. **Unique References:** Each workflow run uses its own commit SHA, ensuring no conflicts between concurrent runs
3. **Sequential Dependencies:** Variant images reference the SHA-specific tags from previous steps
4. **Traceability:** All images maintain commit-specific tags for full traceability

## Benefits of This Approach

- ✅ **Concurrent-Safe:** Multiple workflow runs can execute simultaneously without tag conflicts
- ✅ **Traceable:** Every image variant is tagged with its source commit SHA
- ✅ **Reliable:** Tags are created immediately before they're needed
- ✅ **No Format Issues:** Uses full SHA consistently across all build steps

## Testing

The fix was validated by:
- Reviewing the workflow logic to ensure tags are created before being referenced
- Verifying that each SHA-based tag is unique to the workflow run
- Confirming sequential dependency chain works correctly

## Follow-up

None required. The fix is complete and addresses both the original issue and the concurrent execution requirement.
