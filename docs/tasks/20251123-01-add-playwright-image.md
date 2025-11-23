# 2025-11-23 - Add Playwright (Node) Image Variant

## Problem
Users who need browser automation (Playwright) but are working on Node.js applications currently have to use the `.NET + Playwright` image (`dotnet-playwright`). This image includes the full .NET SDKs (8, 9, 10), which adds unnecessary overhead and size for non-.NET projects.

## Solution
Created a new Docker image variant `playwright` that extends the base image (Node.js) with Playwright and its dependencies, without including the .NET SDKs.

## Changes
- **Dockerfiles**:
  - Renamed existing `Dockerfile.playwright` to `Dockerfile.dotnet-playwright` (to be explicit).
  - Created new `Dockerfile.playwright` for the Node.js + Playwright variant.
- **Workflow**:
  - Updated `.github/workflows/publish.yml` to build and publish the new `playwright` tag.
  - Updated the build step for `dotnet-playwright` to use the renamed Dockerfile.
- **Scripts**:
  - Updated `copilot_here.sh` and `copilot_here.ps1`:
    - Added `-pw` / `--playwright` flag.
    - Updated help text and image lists.
    - Bumped version to `2025-11-23`.
- **Documentation**:
  - Updated `README.md` to include the new image variant.
  - Updated the November updates blog post (external).

## Testing
- Verified script help text updates.
- Verified Dockerfile existence and naming.
- Verified workflow configuration.

## Checklist
- [x] Create `Dockerfile.playwright`
- [x] Rename old `Dockerfile.playwright` to `Dockerfile.dotnet-playwright`
- [x] Update GitHub Actions workflow
- [x] Update Bash script
- [x] Update PowerShell script
- [x] Update README
- [x] Update Blog Post
