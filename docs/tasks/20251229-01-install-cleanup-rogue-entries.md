# Fix Install Scripts to Clean Up Rogue Profile Entries on Reinstall

**Date**: 2025-12-29  
**Issue**: [#47](https://github.com/GordonBeeming/copilot_here/issues/47)

## Problem

When reinstalling copilot_here, the install scripts (`install.sh` and `install.ps1`) detected that marker blocks already existed and returned early with "already installed", skipping cleanup. This meant any manual `source ~/.copilot_here.sh` entries outside the marked boundaries persisted, leading to duplicate sourcing.

### Example of Rogue Entry
```bash
source ~/.copilot_here.sh  # <-- This stayed even after reinstall

# >>> copilot_here >>>
if [ -f "$HOME/.copilot_here.sh" ]; then
  source "$HOME/.copilot_here.sh"
fi
# <<< copilot_here <<<
```

## Solution

Modified both install scripts to **always clean up rogue entries**, even when markers already exist:

### Bash/Zsh (`install.sh`)
- Uses `awk` to extract the marked block and non-copilot_here lines
- Removes any lines containing `copilot_here.sh` outside markers
- Reconstructs profile with clean content
- Reports "cleaned up rogue entries" instead of "already installed"

### PowerShell (`install.ps1`)
- Extracts the marked block using string indices
- Uses regex to remove rogue entries from before/after blocks
- Reconstructs profile with only the marked block
- Reports "cleaned up rogue entries" instead of "already installed"

## Changes Made

### Files Modified
- [x] `install.sh` - Enhanced `update_profile()` function with awk-based cleanup
- [x] `install.ps1` - Enhanced `Update-ProfileFile` function with string manipulation cleanup
- [x] `copilot_here.sh` - Version bump to 2025.12.29.19
- [x] `copilot_here.ps1` - Version bump to 2025.12.29.19
- [x] `Directory.Build.props` - Version bump to 2025.12.29.19
- [x] `app/Infrastructure/BuildInfo.cs` - Version bump to 2025.12.29.19
- [x] `.github/copilot-instructions.md` - Version bump to 2025.12.29.19

## Testing Performed

Tested the awk logic with a sample profile containing rogue entries:
```bash
# Input
source ~/.copilot_here.sh

# >>> copilot_here >>>
if [ -f "$HOME/.copilot_here.sh" ]; then
  source "$HOME/.copilot_here.sh"
fi
# <<< copilot_here <<<

# Output (rogue entry removed)
# >>> copilot_here >>>
if [ -f "$HOME/.copilot_here.sh" ]; then
  source "$HOME/.copilot_here.sh"
fi
# <<< copilot_here <<<
```

## Expected Behavior After Fix

- Fresh install: Removes all copilot_here entries, adds marked block
- Reinstall: Preserves marked block, removes any rogue entries outside markers
- Clean profile files with only the standardized marked block
- No accumulation of manual entries over time

## Notes

- Both bash/zsh and PowerShell scripts now have identical cleanup behavior
- Version incremented to trigger automatic updates for existing users
- This ensures reinstalling always produces clean, predictable results
