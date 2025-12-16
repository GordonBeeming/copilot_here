# Troubleshooting Guide

## Debug Mode

If `copilot_here` or `copilot_yolo` is not working as expected (e.g., terminal shows "kill" or nothing happens), you can enable debug mode to see detailed logging of what's happening.

### Enable Debug Mode

**Bash/Zsh:**
```bash
export COPILOT_HERE_DEBUG=1
copilot_yolo
```

**PowerShell:**
```powershell
$env:COPILOT_HERE_DEBUG = "1"
copilot_yolo
```

### What Debug Mode Shows

Debug mode adds detailed logging at key points:

1. **Shell wrapper logging** (stderr):
   - Function called and arguments
   - Update check status
   - Binary location check
   - Binary execution command
   - Exit code from binary

2. **Binary logging** (stderr):
   - Application startup
   - Argument parsing
   - YOLO mode detection
   - GitHub auth validation
   - Image selection
   - Docker pull process
   - Process IDs and exit codes

### Example Debug Output

```
[DEBUG] === copilot_yolo called with args: -p echo hello
[DEBUG] Checking for updates...
[DEBUG] Could not fetch remote version (offline or timeout)
[DEBUG] Ensuring binary is installed...
[DEBUG] Binary found
[DEBUG] Executing binary in YOLO mode: /Users/you/.local/bin/copilot_here --yolo -p echo hello
[DEBUG] === Application started ===
[DEBUG] Args: --yolo -p echo hello
[DEBUG] YOLO mode: true
[DEBUG] App name: copilot_yolo
[DEBUG] Normalized args: --yolo -p echo hello
[DEBUG] Invoking command parser...
[DEBUG] Validating GitHub auth scopes...
[DEBUG] ValidateScopes called
[DEBUG] Scope validation passed
[DEBUG] Selected image: ghcr.io/gordonbeeming/copilot_here:latest
[DEBUG] Adding YOLO mode flags
[DEBUG] Pulling Docker image...
[DEBUG] PullImage called for: ghcr.io/gordonbeeming/copilot_here:latest
[DEBUG] Starting docker process: docker pull ghcr.io/gordonbeeming/copilot_here:latest
[DEBUG] Docker process started with PID: 12345
[DEBUG] Waiting for docker process to exit...
```

### Common Issues

#### 1. Terminal Shows "kill" or Nothing Happens

**Symptoms:**
- Running `copilot_yolo` shows no output
- Terminal immediately shows "kill" or exits
- No error messages displayed

**Diagnosis with Debug Mode:**
```bash
export COPILOT_HERE_DEBUG=1
copilot_yolo
```

Look for where the output stops:
- **Stops at "Checking for updates"**: Network issue or timeout
- **Stops at "Validating GitHub auth scopes"**: `gh` CLI issue or token problem
- **Stops at "Privileged scopes detected"**: Waiting for user input (shouldn't happen in YOLO mode)
- **Stops at "Starting docker process"**: Docker not responding or not installed

#### 2. Hanging on Privileged Scopes Prompt

If you see this in debug output:
```
[DEBUG] Privileged scopes detected, asking for confirmation
```

But you're using `copilot_yolo`, this is a bug. YOLO mode should skip this prompt. Please report this issue.

**Workaround:** Use a GitHub token without admin scopes, or respond to the prompt with `y` or `n`.

#### 3. Docker Pull Hangs

If debug shows:
```
[DEBUG] Docker process started with PID: 12345
[DEBUG] Waiting for docker process to exit...
```

And then nothing else, Docker itself is hanging. Check:
- Is Docker running? `docker ps`
- Can you pull manually? `docker pull ghcr.io/gordonbeeming/copilot_here:latest`
- Network connectivity issues?

### Disable Debug Mode

**Bash/Zsh:**
```bash
unset COPILOT_HERE_DEBUG
```

**PowerShell:**
```powershell
Remove-Item Env:\COPILOT_HERE_DEBUG
```

## Getting Help

If you've enabled debug mode and still can't resolve the issue:

1. Copy the debug output
2. Open an issue at: https://github.com/GordonBeeming/copilot_here/issues
3. Include:
   - Your OS and shell (e.g., "macOS Sonoma, zsh")
   - The complete debug output
   - What you were trying to do
   - Expected vs actual behavior
