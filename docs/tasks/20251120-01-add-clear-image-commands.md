# 20251120-01-add-clear-image-commands

## Problem
Users had no way to clear the configured default image (local or global) once it was set, other than manually deleting the configuration files.

## Solution
Added new commands to both Bash/Zsh and PowerShell scripts to clear the image configuration.

### Changes Made
- Updated `copilot_here.sh`:
  - Added `__copilot_clear_image_config` function
  - Added `--clear-image` and `--clear-image-global` flags to `copilot_here` and `copilot_yolo` functions
  - Updated help text
  - Bumped version to `2025-11-20.14`
- Updated `copilot_here.ps1`:
  - Added `Clear-ImageConfig` function
  - Added `-ClearImage` and `-ClearImageGlobal` switches to `Copilot-Here` and `Copilot-Yolo` functions
  - Updated help text
  - Bumped version to `2025-11-20.14`
- Updated `README.md`:
  - Added documentation for the new commands in the "Image Management" section

## Testing
- [x] Verified script syntax
- [x] Verified help text updates
- [x] Verified version bumps
