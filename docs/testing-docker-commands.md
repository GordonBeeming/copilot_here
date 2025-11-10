# Docker Command Testing Strategy

This document outlines different approaches to testing Docker command generation without actually running Docker containers.

## Approach 1: Using `--dry-run` Flag ⭐ Recommended

The scripts support a `--dry-run` flag that shows the Docker command without executing it.

### Example Test Pattern

```bash
#!/bin/bash
# Test that correct Docker command is generated

OUTPUT=$(copilot_here --dry-run --no-pull test 2>&1)

# Test for expected flags
if echo "$OUTPUT" | grep -q "\-\-rm"; then
  echo "✓ --rm flag present"
fi

if echo "$OUTPUT" | grep -q "\-v.*:/work"; then
  echo "✓ Working directory mounted"
fi

if echo "$OUTPUT" | grep -q "ghcr.io/.*/copilot_here"; then
  echo "✓ Correct image name"
fi
```

### Limitation
Currently requires valid GitHub auth even for dry-run. Consider adding a `--skip-auth-check` flag for testing.

## Approach 2: Mock Docker Function

Override the `docker` command with a function that logs arguments instead of executing.

### Bash Example

```bash
#!/bin/bash

# Create log file
DOCKER_LOG=$(mktemp)

# Mock docker function
docker() {
  echo "docker $*" >> "$DOCKER_LOG"
  # Return success without doing anything
  return 0
}
export -f docker

# Source script and run
source copilot_here.sh
copilot_here test 2>/dev/null

# Verify logged commands
if grep -q "docker run.*--rm" "$DOCKER_LOG"; then
  echo "✓ Docker called with --rm"
fi

if grep -q "\-v.*:/work" "$DOCKER_LOG"; then
  echo "✓ Working directory mounted"
fi
```

### PowerShell Example

```powershell
# Mock docker function
function docker {
    param([Parameter(ValueFromRemainingArguments)]$Args)
    $script:DockerCalls += ,"$Args"
}

$script:DockerCalls = @()

# Source and run
. copilot_here.ps1
Copilot-Here test

# Verify
if ($script:DockerCalls -match "--rm") {
    Write-Host "✓ Docker called with --rm"
}
```

### Limitation
Mocking may not catch all behaviors; requires auth check bypass.

## Approach 3: Extract Command Building Logic

Create testable functions that build Docker commands separately from execution.

### Proposed Refactoring

```bash
# Function that builds docker command (testable)
__copilot_build_docker_command() {
  local image="$1"
  local args="${@:2}"
  
  echo "docker run --rm -it \
    -v $(pwd):/work \
    -e USER_ID=$(id -u) \
    -e GROUP_ID=$(id -g) \
    $image $args"
}

# Function that executes (calls the builder)
copilot_here() {
  # ... auth checks, setup ...
  
  local cmd=$(__copilot_build_docker_command "$IMAGE" "$@")
  
  if [ "$DRY_RUN" = true ]; then
    echo "$cmd"
    return 0
  fi
  
  eval "$cmd"
}
```

### Testing

```bash
# Test the command builder directly
test_docker_command_basic() {
  local cmd=$(__copilot_build_docker_command "ghcr.io/user/copilot_here:latest" "test")
  
  [[ "$cmd" == *"--rm"* ]] || fail "Missing --rm"
  [[ "$cmd" == *"-it"* ]] || fail "Missing -it"
  [[ "$cmd" == *"-v $(pwd):/work"* ]] || fail "Missing volume mount"
}
```

### Benefit
- Fast (no Docker execution)
- No mocking needed
- Tests pure command construction logic
- Easy to verify all edge cases

## Approach 4: Snapshot Testing

Save expected Docker commands and compare against actual output.

### Example

```bash
# snapshots/basic_command.txt
docker run --rm -it \
  -v /home/user/project:/work \
  -e USER_ID=1000 \
  -e GROUP_ID=1000 \
  ghcr.io/user/copilot_here:latest \
  test

# test
EXPECTED=$(cat snapshots/basic_command.txt)
ACTUAL=$(copilot_here --dry-run test 2>&1 | grep "^docker run")

if diff <(echo "$EXPECTED") <(echo "$ACTUAL"); then
  echo "✓ Command matches snapshot"
else
  echo "✗ Command differs from snapshot"
  diff <(echo "$EXPECTED") <(echo "$ACTUAL")
fi
```

### Limitation
Brittle - breaks when paths or UIDs change.

## Recommended Approach for This Project

**Use Approach 3** (Extract Command Building Logic) because:

1. ✅ **Fast** - No Docker, no auth, pure function testing
2. ✅ **Reliable** - Tests exact command construction
3. ✅ **Maintainable** - Easy to add test cases
4. ✅ **No Dependencies** - No mocking frameworks needed

### Implementation Steps

1. Refactor scripts to separate command building from execution
2. Create test helper functions that call the command builders
3. Write tests for each scenario:
   - Basic run
   - Image variants (dotnet, playwright)
   - Additional mounts (--mount, --mount-rw)
   - Environment variables
   - User/group mapping

### Quick Win

For now, you can add a `--skip-auth` or `--test-mode` flag that:
- Skips GitHub auth validation
- Makes `--dry-run` actually show the command
- Allows the mock approach to work

```bash
copilot_here() {
  # ... existing code ...
  
  # Add test mode flag
  local SKIP_AUTH=false
  if [[ "$1" == "--test-mode" ]]; then
    SKIP_AUTH=true
    shift
  fi
  
  # Skip auth check in test mode
  if [ "$SKIP_AUTH" = false ]; then
    __copilot_check_auth || return 1
  fi
  
  # ... rest of function ...
}
```

Then tests can use:
```bash
copilot_here --test-mode --dry-run test
```

## Current Status

The integration tests focus on:
- ✅ Function existence and loading
- ✅ Config file parsing
- ✅ Path resolution
- ⏳ Docker command generation (needs auth bypass)

Next steps:
- [ ] Add `--test-mode` or `--skip-auth` flag
- [ ] Enable dry-run without auth requirements  
- [ ] Create Docker command validation tests
- [ ] Consider refactoring for better testability
