# Pre-run Update Check

**Date:** 2025-11-20
**Description:** Added a pre-run check to prompt users for updates if a newer version of the script is available.

## Problem
Users were not aware of updates to the `copilot_here` scripts unless they manually checked or ran the update command. The user requested a proactive check that prompts "y/n" before running.

## Solution
Implemented a `__copilot_check_for_updates` function in Bash and `Test-CopilotUpdate` function in PowerShell.
- Fetches the remote script version from GitHub.
- Compares it with the local version.
- If newer, prompts the user to update.
- If confirmed, calls the existing update logic.
- Refactored existing update logic into reusable functions (`__copilot_update_scripts` / `Update-CopilotScripts`).

## Changes
- Modified `copilot_here.sh`:
  - Added `__copilot_update_scripts` function.
  - Added `__copilot_check_for_updates` function.
  - Updated `copilot_here` and `copilot_yolo` to call the check before running.
- Modified `copilot_here.ps1`:
  - Added `Update-CopilotScripts` function.
  - Added `Test-CopilotUpdate` function.
  - Updated `Copilot-Here` and `Copilot-Yolo` to call the check before running.

## Testing
- Verified script syntax and structure.
- Logic ensures check is skipped in test mode (`COPILOT_HERE_TEST_MODE`).
- Timeout added to curl/web request to prevent hanging if offline (2 seconds).
