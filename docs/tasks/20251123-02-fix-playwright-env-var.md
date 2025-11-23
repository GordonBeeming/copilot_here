# Fix Playwright Environment Variable

**Date:** 2025-11-23

## Problem
When using the Playwright Docker image, users (or the agent) were finding that Playwright would attempt to download browsers again, even though they were pre-installed in the image. This happened because the `PLAYWRIGHT_BROWSERS_PATH` environment variable was not set in the runtime environment, so local Playwright installations (e.g., in `node_modules`) would look in the default location or not know about the custom location used during build.

## Solution
Added `ENV PLAYWRIGHT_BROWSERS_PATH=/home/appuser/.cache/ms-playwright` to both `Dockerfile.playwright` and `Dockerfile.dotnet-playwright`. This ensures that any Playwright instance running in the container will automatically detect and use the pre-installed browsers.

## Changes
- Modified `Dockerfile.playwright`: Added `ENV PLAYWRIGHT_BROWSERS_PATH` and cleaned up `RUN` command.
- Modified `Dockerfile.dotnet-playwright`: Added `ENV PLAYWRIGHT_BROWSERS_PATH` and cleaned up `RUN` command.

## Testing
- Verified the Dockerfile syntax is correct.
- The change ensures the environment variable persists in the final image.
