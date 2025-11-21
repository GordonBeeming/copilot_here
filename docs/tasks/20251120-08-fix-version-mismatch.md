# 2025-11-20 - Fix Version Mismatch

## Problem
The version numbers in `copilot_here.sh` and `copilot_here.ps1` were inconsistent. The headers were updated to `.15`, but the help text versions were lagging behind (`.12` and `.14` respectively).

## Solution
Updated all version references in both scripts to `2025-11-20.16` to ensure consistency.

## Changes
- Modified `copilot_here.sh`: Updated header and help text versions to `2025-11-20.16`.
- Modified `copilot_here.ps1`: Updated header and help text versions to `2025-11-20.16`.

## Testing
- [ ] Verify `copilot_here.sh --help` shows correct version
- [ ] Verify `copilot_here.ps1 -Help` shows correct version
