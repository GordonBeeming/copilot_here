# Add read:packages Scope Requirement

**Date:** 2025-11-20
**Description:** Updated scripts and documentation to require `read:packages` scope for GitHub Container Registry access.

## Problem
Users were experiencing `denied: denied` errors when pulling the `copilot_here` Docker image from GHCR, even for public packages. This is because authenticated requests to GHCR require the `read:packages` scope, even if the package is public.

## Solution
Updated the `copilot_here` scripts (Bash and PowerShell) to:
1. Check if the user's token has the `read:packages` scope in addition to the `copilot` scope.
2. Update the error message to suggest the correct `gh auth refresh` command including both scopes.
3. Updated documentation to reflect this requirement.

## Changes
- Modified `copilot_here.sh`:
  - Updated version to `2025-11-20`
  - Added check for `read:packages` scope
  - Updated `gh auth refresh` command suggestion
- Modified `copilot_here.ps1`:
  - Updated version to `2025-11-20`
  - Added check for `read:packages` scope
  - Updated `gh auth refresh` command suggestion
- Modified `README.md`:
  - Updated Prerequisites section to mention `read:packages` scope
- Modified `.github/copilot-instructions.md`:
  - Updated current version to `2025-11-20`

## Testing
- Ran `./tests/run_all_tests.sh` to ensure no regressions in existing functionality.
- Verified that the scripts still pass syntax checks and logic tests.
