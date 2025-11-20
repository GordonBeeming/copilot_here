# Dependency Version Arguments for Docker Builds

**Date:** 2025-11-20
**Description:** Updated Dockerfiles and GitHub Actions workflow to pass dependency versions as build arguments, ensuring rebuilds when dependencies change.

## Problem
Previously, the `.NET` and `Playwright` Docker images were only rebuilt when the base image changed or when the Dockerfile itself was modified. Dependencies like .NET SDKs and Playwright were installed using "latest" or channel-based scripts, which meant that if a new version of a dependency was released but the base image hadn't changed, the Docker build would use the cached layer with the old dependency version.

## Solution
1.  **Updated `Dockerfile.playwright`**:
    -   Added `ARG PLAYWRIGHT_VERSION=latest`.
    -   Updated installation to use the specified version: `npm install -g playwright@${PLAYWRIGHT_VERSION}`.

2.  **Updated `Dockerfile.dotnet`**:
    -   Added ARGs for .NET SDK versions: `DOTNET_SDK_8_VERSION`, `DOTNET_SDK_9_VERSION`, `DOTNET_SDK_10_VERSION`.
    -   Updated installation logic to use the specific version if provided, falling back to channel-based installation if not.

3.  **Updated `.github/workflows/publish.yml`**:
    -   Added a step to fetch the latest versions of Playwright and .NET SDKs (8, 9, 10) using `npm view` and `curl` + `jq`.
    -   Passed these versions as build arguments to the respective Docker build steps.
    -   Removed the conditional execution (`if: steps.push_decision.outputs.push_needed == 'true'`) for the .NET and Playwright image builds. This ensures that these images are always checked against the build arguments. If the versions have changed, the cache will be invalidated, and the image will be rebuilt and pushed. If versions haven't changed, Docker layer caching will be used.

## Changes
-   Modified `Dockerfile.playwright`
-   Modified `Dockerfile.dotnet`
-   Modified `.github/workflows/publish.yml`

## Testing
-   Verified that the workflow syntax is correct.
-   Verified that the version fetching commands work (simulated with `curl`).
-   The changes rely on Docker's build argument cache invalidation mechanism.
