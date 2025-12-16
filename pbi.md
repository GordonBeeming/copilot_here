# Bug: Update prompt loop after successful update

**Created:** 2025-12-16T22:49:08.509Z

## Title
Update prompt loop: `yolo` repeatedly offers same update after successful update

## Summary
After accepting an update, running `yolo` again immediately re-prompts to update from the old version to the new version, creating an endless update loop.

## Environment
- OS: macOS (Apple Silicon / arm64)
- Shell: zsh (or bash)
- Command: `yolo` (calls `copilot_yolo`)
- Update channel: `cli-latest` release URL

## Steps to reproduce
1. Run `yolo`
2. When prompted `📢 Update available: <old> → <new>`, answer `y`
3. Update completes (e.g., “✅ Update complete! Reloading shell functions…”)
4. Run `yolo` again

## Expected behavior
After updating once, `yolo` runs without re-prompting (local script/binary should reflect the new version).

## Actual behavior
`yolo` prints a “reloading” message from the old version and then prompts to update again, e.g.:
- `🔄 Detected updated shell script (v<old>), reloading...`
- `📢 Update available: <old> → <new>`

## Example output
```text
❯ yolo
📢 Update available: 2025.12.15.3 → 2025.12.16.2
Would you like to update now? [y/N]: y
🔄 Updating copilot_here...

📥 Downloading latest binary...
📦 Downloading binary from: https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here-osx-arm64.tar.gz
✅ Binary installed to: /Users/gordonbeeming/.local/bin/copilot_here

📥 Downloading latest shell script...
✅ Update complete! Reloading shell functions...

❯ yolo
🔄 Detected updated shell script (v2025.12.15.3), reloading...
📢 Update available: 2025.12.15.3 → 2025.12.16.2
Would you like to update now? [y/N]: y
```

## Suspected cause
The wrapper compares “in-memory version” vs an on-disk script (`~/.copilot_here.sh` on macOS). The update path was sourcing a temporary downloaded script but **not persisting it** to the on-disk script path. On the next run, it reloads the stale file (old version), which re-triggers the update check.

## Impact
Users get stuck in a repeated update prompt loop; updates appear not to “stick”.

## Proposed fix
When updating, download the latest script and **write it to the on-disk script path used for reload/version checks** (e.g., `~/.copilot_here.sh` / `~/.copilot_here.ps1`), then source/dot-source that file.
