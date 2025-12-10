# Task: Add Dependency Checks for GitHub CLI and Docker

**Date:** 2025-12-10  
**Type:** Feature Enhancement

## Problem
The application would fail with unclear errors if:
1. GitHub CLI (`gh`) was not installed
2. GitHub CLI was not authenticated
3. Docker was not installed
4. Docker daemon was not running

This resulted in confusing error messages and poor user experience for first-time users.

## Solution
Created a comprehensive pre-flight dependency check system that validates all required dependencies before attempting to run the application.

### Changes Made

#### 1. New Infrastructure Component
- **File:** `app/Infrastructure/DependencyCheck.cs`
- Performs checks for:
  - GitHub CLI installation and version
  - GitHub CLI authentication status
  - Docker installation and version
  - Docker daemon running status
- Provides platform-specific installation instructions
- Shows authentication command with correct scopes
- Displays results in a nice formatted table with ✅/❌ indicators

#### 2. Updated RunCommand
- **File:** `app/Commands/Run/RunCommand.cs`
- Added dependency check before scope validation
- Checks run on every invocation to catch runtime issues
- Exits with error code 1 if any dependency is not satisfied

#### 3. Platform-Specific Help
The dependency checker provides tailored instructions for each platform:
- **Windows:** winget commands, Docker Desktop
- **macOS:** brew commands, Docker Desktop
- **Linux:** apt/package manager, systemctl commands

#### 4. Unit Tests
- **File:** `tests/CopilotHere.UnitTests/DependencyCheckTests.cs`
- Tests for all dependency check methods
- Validates result structure
- Tests display logic

### User Experience

**Silent Success:**
When all dependencies are satisfied, the check runs silently (no output):
```bash
$ copilot_here
🚀 Using image: ghcr.io/gordonbeeming/copilot_here:latest
...
```

**Visible Failures:**
Only when dependencies are missing, users see the detailed check with **all** dependencies (both passed and failed):

```
📋 Dependency Check:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ GitHub CLI (gh) (2.40.1)
❌ Docker
   Docker not found
   💡 Install: https://docs.docker.com/desktop/install/mac-install/
❌ Docker Daemon
   Docker daemon not running
   💡 Start Docker Desktop application
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

This way users can see what's working and what needs attention.

**Debug Mode:**
With `COPILOT_HERE_DEBUG=1`, always shows dependency status even when all are satisfied:

```
📋 Dependency Check:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ GitHub CLI (gh) (2.40.1)
✅ Docker (24.0.7)
✅ Docker Daemon (Running)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
```

### Authentication Guidance

When GitHub CLI is not authenticated, users see:

```
❌ GitHub CLI (gh) (2.40.1)
   Not authenticated
   💡 Authenticate: gh auth login -h github.com -s copilot,read:packages
```

This provides the exact command with the correct scopes needed for the application.

## Testing

- [x] Build compiles successfully
- [x] All existing tests pass (238 tests)
- [x] New dependency check tests added
- [x] Verified error messages are clear and actionable
- [x] Platform-specific help messages validated

## Benefits

1. **Silent Success:** No noise when everything is working correctly
2. **Better First-Run Experience:** New users get clear guidance only when there's a problem
3. **Faster Debugging:** Issues are caught early with actionable error messages
4. **Platform Awareness:** Installation instructions match user's operating system
5. **Proactive Validation:** Catches issues before they cause cryptic failures
6. **Correct Scope Guidance:** Shows exact `gh auth login` command with required scopes
7. **Debug Visibility:** Debug mode shows full status for troubleshooting

## Future Enhancements

Potential improvements for later:
- Cache dependency check results for a short duration to avoid repeated checks
- Add version requirements (minimum Docker/gh versions)
- Check for Docker Compose availability
- Validate network connectivity to GitHub
