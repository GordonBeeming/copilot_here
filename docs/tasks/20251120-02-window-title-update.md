# 2025-11-20 - Window Title Update

## Objective
Update the startup scripts (`copilot_here.sh` and `copilot_here.ps1`) to change the terminal window title while running. This provides visual feedback on the current mode (Standard vs YOLO) and the active directory context.

## Solution
Implemented logic to set the terminal title at the start of execution and reset it upon exit.

### Key Changes
1.  **Emoji Indicators**:
    - Standard Mode: 🤖
    - YOLO Mode: 🤖⚡️
2.  **Title Format**: `{{ emoji }} DirectoryName` (e.g., `🤖⚡️ copilot_here`)
3.  **Shell Implementation**:
    - **Bash (`copilot_here.sh`)**: Used `printf "\033]0;%s\007"` for setting the title and `trap` to reset it on exit. Wrapped in a subshell to ensure clean trap handling.
    - **PowerShell (`copilot_here.ps1`)**: Set `$Host.UI.RawUI.WindowTitle` within a `try...finally` block to ensure the original title is restored. Added safety checks for `Split-Path`.

## Changes Made
- Modified `copilot_here.sh`: Added title setting logic and updated version to `2025-11-20.2`.
- Modified `copilot_here.ps1`: Added title setting logic and updated version to `2025-11-20.2`.
- Created `tests/integration/test_window_title.sh`: Integration test for Bash title setting.
- Created `tests/integration/test_window_title.ps1`: Integration test for PowerShell title setting.

## Testing
- **New Tests**: Created specific integration tests that mock the environment and verify the title output/setting.
    - `tests/integration/test_window_title.sh`: Passed.
    - `tests/integration/test_window_title.ps1`: Passed.
- **Regression Testing**: Ran existing test suites to ensure no regressions.
    - `tests/integration/test_bash.sh`: Passed.
    - `tests/integration/test_powershell.ps1`: Passed.

## Checklist
- [x] Update `copilot_here.sh`
- [x] Update `copilot_here.ps1`
- [x] Add Bash integration test
- [x] Add PowerShell integration test
- [x] Verify all tests pass
