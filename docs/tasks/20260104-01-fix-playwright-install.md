# 2026-01-04 - Fix Playwright Browser Installation

## Problem
Users reported that the agent still needs to install Playwright browsers manually when running tests, even when using the `playwright` image variant. This suggests that either the required browsers were missing (e.g., Firefox/WebKit) or there were permission issues preventing the agent from using or updating the installed browsers.

## Solution
Updated the Playwright Dockerfile to ensure all browsers are installed and the directory is writable by the non-root user.

### Changes
1. **Install All Browsers**: Changed `npx playwright install chromium --with-deps` to `npx playwright install --with-deps` in `docker/variants/Dockerfile.playwright`. This ensures Chromium, Firefox, and WebKit are all pre-installed.
2. **Fix Permissions**: Changed `chmod -R 755 /ms-playwright` to `chmod -R 777 /ms-playwright`. This allows the `appuser` (which the container runs as) to write to the browsers directory. This is critical if the user's project requires a different Playwright version than the one in the image, allowing the agent to download the matching browser version without permission errors.
3. **Version Bump**: Updated project version to `2026.01.04` across all files.

## Files Modified
- `docker/variants/Dockerfile.playwright`
- `copilot_here.sh`
- `copilot_here.ps1`
- `Directory.Build.props`
- `app/Infrastructure/BuildInfo.cs`
- `.github/copilot-instructions.md`

## Verification
- Verified Dockerfile syntax.
- Verified version numbers match in all 5 locations.
