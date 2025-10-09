# Multi-Image Docker Build Setup

**Date**: 2025-01-09  
**Task**: 20250109-01-docker-multi-image-setup

## Objective
Add support for building 2 additional Docker images with specific tags for specialized use cases:
1. Playwright image with Chromium for web testing
2. .NET image with SDK support for development

## Problem Statement
The repository only published a single base Docker image. There was a need for specialized variants that include:
- Web testing capabilities (Playwright + Chromium) for checking published work
- .NET SDK support (versions 8 and 9) for platform-specific development

These images needed to build from the same pipeline's base image using commit hash tags to ensure consistency.

## Solution Approach

### Image Build Chain
Created a sequential build process where each image builds on the previous one:
```
Base Image (node:20-slim + Copilot CLI)
    ↓ uses sha-<commit> tag
Playwright Image (+Playwright +Chromium)
    ↓ uses playwright-sha-<commit> tag
.NET Image (+.NET 8 SDK +.NET 9 SDK)
```

### Implementation
1. **Created Dockerfile.playwright**
   - Extends base image using commit hash tag
   - Installs Playwright system dependencies
   - Installs Playwright globally via npm
   - Installs Chromium browser with dependencies

2. **Created Dockerfile.dotnet**
   - Extends Playwright image using commit hash tag
   - Installs .NET SDK prerequisites
   - Installs .NET 8 SDK using dotnet-install.sh
   - Installs .NET 9 SDK using dotnet-install.sh
   - Sets up PATH and DOTNET_ROOT environment variables

3. **Updated GitHub Actions Workflow**
   - Added Step 9: Build and push Playwright image
   - Added Step 10: Build and push .NET image
   - Both steps only run when base image is pushed
   - Configured proper build args with commit hash tags
   - Set up registry caching for performance

## Changes Made

### New Files
- [x] `Dockerfile.playwright` - Playwright + Chromium image definition
- [x] `Dockerfile.dotnet` - .NET SDK image definition
- [x] `docs/docker-images.md` - Documentation for image variants
- [x] `.github/copilot-instructions.md` - Project-specific Copilot instructions
- [x] `docs/tasks/20250109-01-docker-multi-image-setup.md` - This task documentation

### Modified Files
- [x] `.github/workflows/publish.yml` - Added multi-image build steps

### Documentation Structure
- [x] Created `/docs` directory
- [x] Created `/docs/tasks` directory for task documentation
- [x] Created `/docs/tasks/images` directory for screenshots
- [x] Moved `DOCKER_IMAGES.md` to `docs/docker-images.md`
- [x] Created comprehensive Copilot instructions

## Image Tags Published

### Base Image
- `ghcr.io/gordonbeeming/copilot_here:latest`
- `ghcr.io/gordonbeeming/copilot_here:main`
- `ghcr.io/gordonbeeming/copilot_here:sha-<commit>`

### Playwright Image
- `ghcr.io/gordonbeeming/copilot_here:playwright`
- `ghcr.io/gordonbeeming/copilot_here:playwright-sha-<commit>`

### .NET Image
- `ghcr.io/gordonbeeming/copilot_here:dotnet`
- `ghcr.io/gordonbeeming/copilot_here:dotnet-sha-<commit>`

## Testing Performed

### Local Build Testing
- [x] Verified Dockerfile syntax
- [x] Confirmed workflow YAML is valid
- [x] Reviewed build arguments and tagging strategy

### Workflow Validation
- [x] Checked step dependencies
- [x] Verified conditional execution (only when push_needed is true)
- [x] Confirmed proper use of commit hash in build args
- [x] Validated cache configuration

## Technical Details

### Playwright Image
**Base**: `ghcr.io/gordonbeeming/copilot_here:${BASE_IMAGE_TAG}`

**Dependencies Installed**:
- libnss3, libnspr4, libatk1.0-0, libatk-bridge2.0-0
- libcups2, libdrm2, libdbus-1-3, libxkbcommon0
- libxcomposite1, libxdamage1, libxfixes3, libxrandr2
- libgbm1, libpango-1.0-0, libcairo2, libasound2
- libatspi2.0-0, libxshmfence1

**Tools Installed**:
- Playwright (latest via npm)
- Chromium (via `npx playwright install chromium --with-deps`)

### .NET Image
**Base**: `ghcr.io/gordonbeeming/copilot_here:${PLAYWRIGHT_IMAGE_TAG}`

**Prerequisites**:
- wget, ca-certificates

**SDKs Installed**:
- .NET 8 SDK (channel 8.0)
- .NET 9 SDK (channel 9.0)

**Installation Method**: Official dotnet-install.sh script
**Installation Path**: `/usr/share/dotnet`

## Workflow Integration

### Build Process
1. Base image builds and is tagged with commit SHA
2. If push is needed, base image is pushed to registry
3. Playwright image builds using `sha-<commit>` tag as base
4. Playwright image is pushed with `playwright` and `playwright-sha-<commit>` tags
5. .NET image builds using `playwright-sha-<commit>` tag as base
6. .NET image is pushed with `dotnet` and `dotnet-sha-<commit>` tags

### Conditional Execution
All image builds only execute when `steps.push_decision.outputs.push_needed == 'true'`, which occurs:
- On push events to main branch
- On schedule/manual triggers when base image has changed

### Caching Strategy
Each image variant uses registry caching:
- `cache-from`: Pulls from previous image version for faster builds
- `cache-to`: Stores cache inline for future builds

## Follow-up Items

### Completed Fixes
- [x] Fixed Docker cache preventing latest npm package detection (2025-01-09)
  - ~~Problem: Workflow used cached layers, so `npm install -g @github/copilot` never checked for new versions~~
  - ~~Solution: Added `no-cache: true` to base image build step~~
  - **Better Solution**: Fetch version from GitHub releases API and pass as build arg
    - New workflow step fetches latest version from `https://api.github.com/repos/github/copilot-cli/releases/latest`
    - Version is passed as `COPILOT_VERSION` build argument to Dockerfile
    - Cache is preserved when version hasn't changed (fast builds)
    - Cache is invalidated only when new version is released (automatic updates)
  - Impact: Nightly builds get latest version + fast cached builds when no updates

### Potential Improvements
- [ ] Consider adding health checks to Dockerfiles
- [ ] Add automated testing for image functionality
- [ ] Document image size differences
- [ ] Create shell function examples for each image variant in README

### Future Image Variants
The build structure now supports easily adding more variants:
- Could add Python variant
- Could add Go variant
- Could add other language-specific variants

## Notes
- All images maintain the same entrypoint and CMD from the base image
- User permission management (via entrypoint.sh) is inherited by all variants
- Each image is self-contained and can be used independently
- Commit hash tagging ensures consistency across all images from a single workflow run

## Documentation Organization
Created proper documentation structure following best practices:
- Standard GitHub files (README, LICENSE) remain in root
- All other documentation moved to `/docs`
- Task documentation in `/docs/tasks` with date-prefixed naming
- Image directory ready for screenshots in `/docs/tasks/images`
