# Model Configuration Feature

**Date**: 2026-01-05  
**Issue**: TBD (PBI created in `pbi.md`)

## Overview

Added model configuration management to copilot_here, allowing users to configure and persist their preferred AI model for GitHub Copilot CLI sessions. This follows the same pattern as image configuration.

## Problem Statement

Users had to pass `--model <model-id>` on every copilot_here invocation if they wanted to use a specific AI model. There was no way to save a preferred model for a project or globally, making it cumbersome to consistently use models like GPT-5 or Claude Sonnet 4.5.

## Solution

Implemented a complete model configuration system with the following commands:

### New Commands
- `--list-models` - Lists available models by parsing Copilot CLI error message (workaround approach)
- `--show-model` - Displays current model configuration (local, global, effective)
- `--set-model <model-id>` - Sets model in local config (`.copilot_here/model.conf`)
- `--set-model-global <model-id>` - Sets model in global config (`~/.config/copilot_here/model.conf`)
- `--clear-model` - Clears local model configuration
- `--clear-model-global` - Clears global model configuration

### Configuration Priority

The system follows standard configuration priority:
1. CLI argument: `--model <model-id>` (highest priority)
2. Local config: `.copilot_here/model.conf`
3. Global config: `~/.config/copilot_here/model.conf`
4. Default: GitHub Copilot CLI default (lowest priority)

### Available Models

The `--list-models` command uses a workaround to extract available models from the Copilot CLI error message.

**Implementation Approach:**
1. Run `copilot --model invalid-model-to-trigger-list` in container
2. Copilot CLI returns error listing valid model IDs
3. Parse error output using regex patterns to extract model IDs
4. Display parsed list to user

**Why this approach:**
- Copilot CLI doesn't have a dedicated model listing command
- The `/model` slash command is interactive only
- Error messages reliably list valid models
- Temporary solution until proper API is available

**Parsing Strategy:**
- Look for patterns like "valid values are: model1, model2"
- Extract quoted/backticked model IDs
- Filter out non-model text
- Return deduplicated list

**Fallback:**
- If parsing fails, shows instructions to use `/model` in interactive session
- Includes debug output when `COPILOT_HERE_DEBUG=1`

## Changes Made

### New Files Created

1. **`app/Commands/Model/_ModelConfig.cs`** - Model configuration class with Load/Save/Clear methods
2. **`app/Commands/Model/_ModelCommands.cs`** - Command router for model management
3. **`app/Commands/Model/ListModels.cs`** - Lists available models with descriptions
4. **`app/Commands/Model/ShowModel.cs`** - Shows current model configuration
5. **`app/Commands/Model/SetModel.cs`** - Sets local model configuration
6. **`app/Commands/Model/SetModelGlobal.cs`** - Sets global model configuration
7. **`app/Commands/Model/ClearModel.cs`** - Clears local model configuration
8. **`app/Commands/Model/ClearModelGlobal.cs`** - Clears global model configuration
9. **`tests/CopilotHere.UnitTests/ModelConfigTests.cs`** - Unit tests for ModelConfig class

### Modified Files

1. **`app/Program.cs`**
   - Added `using CopilotHere.Commands.Model;`
   - Registered `ModelCommands` in command list
   - Added PowerShell-style aliases for model commands

2. **`app/Infrastructure/AppContext.cs`**
   - Added `ModelConfig` property
   - Added model config loading in `Create()` method

3. **`app/Commands/Run/RunCommand.cs`**
   - Modified to load model from configuration
   - CLI `--model` flag now overrides configured model
   - Added debug logging for model source

4. **`README.md`**
   - Added "Model Management" section with full documentation
   - Included configuration priority explanation
   - Added example usage patterns

5. **Version Files** (all updated to 2026.01.05):
   - `copilot_here.sh` (lines 2 and 8)
   - `copilot_here.ps1` (lines 2 and 26)
   - `Directory.Build.props` (line 4)
   - `app/Infrastructure/BuildInfo.cs` (line 13)
   - `.github/copilot-instructions.md` (line 82)

## Testing

### Unit Tests
- ✅ All 290 unit tests pass (including 8 new ModelConfig tests)
- ✅ Tests verify configuration priority (CLI > Local > Global > Default)
- ✅ Tests verify save/load/clear operations for both local and global configs

### Manual Testing
```bash
# List available models (parses Copilot CLI error message)
copilot_here --list-models
# Output: Shows parsed list of model IDs from error message

# Show current configuration (empty initially)
copilot_here --show-model

# Set local model
copilot_here --set-model gpt-5

# Verify it was saved
copilot_here --show-model
# Output shows: Local config (.copilot_here/model.conf): gpt-5

# Clear local model
copilot_here --clear-model

# Verify it was cleared
copilot_here --show-model
# Output shows: Local config: (not set)
```

**Debug Mode:**
```bash
# Enable debug output to see parsing details
export COPILOT_HERE_DEBUG=1
copilot_here --list-models
```


## Implementation Pattern

Followed the exact same pattern as `Commands/Images/`:
- Configuration class with Load/Save/Clear methods
- Partial class pattern for organizing command implementations
- AppContext integration for automatic loading
- Priority-based configuration resolution
- Unit tests matching ImageConfigTests structure

## Benefits

- **Convenience**: Set model once, use everywhere
- **Flexibility**: Different models per project or global default
- **Consistency**: Same pattern as image configuration
- **Priority**: CLI flags still override for one-off changes
- **Testability**: Full unit test coverage

## Follow-up Items

- [ ] Create GitHub issue from `pbi.md`
- [ ] Consider adding model validation (ensure model ID exists in available list)
- [ ] Consider adding model aliases (e.g., `--set-model claude` → `claude-sonnet-4.5`)

## Implementation Notes

**`--list-models` Implementation (Error Parsing Workaround):**
- Runs `copilot --model invalid-model-to-trigger-list` in Docker container
- Copilot CLI returns error message listing valid models
- Parses stderr using multiple regex patterns to extract model IDs
- Displays parsed list or falls back to showing `/model` instructions
- Temporary solution - proper API would be better

**Why this approach:**
1. No dedicated `copilot config models` or similar command exists
2. The `/model` slash command is interactive only (no machine-readable output)
3. Error messages reliably contain valid model IDs
4. User feedback showed this is pragmatic until proper API available

**Added Infrastructure:**
- `DockerRunner.RunAndCapture()` - New method to capture stdout/stderr from Docker commands
- Regex-based parser with defensive fallbacks
- Debug logging for troubleshooting parsing issues

## Related Files

- PBI: `pbi.md`
- Documentation: `README.md` (Model Management section)
- Tests: `tests/CopilotHere.UnitTests/ModelConfigTests.cs`
