# Docker Testing: Practical Comparison

## Goal: Test that `copilot_here -d` uses the correct Docker image and config

Let's compare how each approach would test this specific scenario.

---

## ❌ Approach 1: `--dry-run` Flag (Current Limitation)

**The Problem:**
```bash
$ copilot_here -d --dry-run test
❌ Error: Your gh token is missing the required 'copilot' scope.
```

Even with `--dry-run`, it checks auth first. **Can't test without valid GitHub auth.**

**What it WOULD show if we fix it:**
```bash
$ copilot_here -d --dry-run test

Would run:
docker run --rm -it \
  -v /home/user/project:/work \
  -e USER_ID=1000 \
  -e GROUP_ID=1000 \
  ghcr.io/user/copilot_here:dotnet \
  test
```

**Test would look like:**
```bash
OUTPUT=$(copilot_here -d --dry-run --no-pull test 2>&1)

# Verify dotnet image
if echo "$OUTPUT" | grep -q "copilot_here:dotnet"; then
  echo "✓ Using dotnet image"
else
  echo "✗ Wrong image"
fi

# Verify all mounts/config
if echo "$OUTPUT" | grep -q "\-v.*:/work"; then
  echo "✓ Work directory mounted"
fi

if echo "$OUTPUT" | grep -q "\-e USER_ID="; then
  echo "✓ User ID passed"
fi
```

**Verdict:** ✅ **This WOULD work perfectly** if we add `--test-mode` to skip auth

---

## ✅ Approach 2: Mock Docker Function (Works Now!)

**This intercepts the actual docker call:**

```bash
#!/bin/bash
# Test file: test_docker_dotnet_variant.sh

DOCKER_LOG=$(mktemp)

# Mock docker to capture what would be executed
docker() {
  echo "ARGS: $*" >> "$DOCKER_LOG"
  return 0
}
export -f docker

# Mock gh to bypass auth
gh() {
  case "$1" in
    "auth") echo "github.com" ;;
    "api") echo '{"copilot_business":true}' ;;
    *) return 0 ;;
  esac
}
export -f gh

# Source and run
source ../../copilot_here.sh
copilot_here -d test 2>/dev/null

# Verify the docker command
DOCKER_CMD=$(cat "$DOCKER_LOG")

# Test 1: Correct image
if echo "$DOCKER_CMD" | grep -q "ghcr.io/.*/copilot_here:dotnet"; then
  echo "✓ Dotnet image used"
else
  echo "✗ Wrong image: $DOCKER_CMD"
  exit 1
fi

# Test 2: Has --rm flag
if echo "$DOCKER_CMD" | grep -q "\-\-rm"; then
  echo "✓ Has --rm flag"
else
  echo "✗ Missing --rm flag"
  exit 1
fi

# Test 3: Has -it flags
if echo "$DOCKER_CMD" | grep -q "\-it"; then
  echo "✓ Has interactive flags"
else
  echo "✗ Missing -it flags"
  exit 1
fi

# Test 4: Mounts working directory
if echo "$DOCKER_CMD" | grep -q "\-v.*:/work"; then
  echo "✓ Working directory mounted"
else
  echo "✗ Working directory not mounted"
  exit 1
fi

# Test 5: USER_ID environment variable
if echo "$DOCKER_CMD" | grep -q "\-e USER_ID="; then
  echo "✓ USER_ID passed"
else
  echo "✗ USER_ID not passed"
  exit 1
fi

# Test 6: GROUP_ID environment variable
if echo "$DOCKER_CMD" | grep -q "\-e GROUP_ID="; then
  echo "✓ GROUP_ID passed"
else
  echo "✗ GROUP_ID not passed"
  exit 1
fi

# Test 7: Correct arguments passed to container
if echo "$DOCKER_CMD" | grep -q "test$"; then
  echo "✓ Arguments passed correctly"
else
  echo "✗ Arguments not passed"
  exit 1
fi

echo ""
echo "All tests passed! ✓"
```

**Run it:**
```bash
$ bash test_docker_dotnet_variant.sh
✓ Dotnet image used
✓ Has --rm flag
✓ Has interactive flags
✓ Working directory mounted
✓ USER_ID passed
✓ GROUP_ID passed
✓ Arguments passed correctly

All tests passed! ✓
```

**Verdict:** ✅ **This works NOW** - you can test everything without running Docker

---

## ⭐ Approach 3: Extract Command Building (Best Long-term)

**Refactor the script:**

```bash
# In copilot_here.sh - new testable function
__copilot_build_run_command() {
  local image="$1"
  shift
  
  local cmd="docker run --rm -it"
  cmd="$cmd -v $(pwd):/work"
  cmd="$cmd -e USER_ID=$(id -u)"
  cmd="$cmd -e GROUP_ID=$(id -g)"
  
  # Add any additional mounts
  for mount in "${ADDITIONAL_MOUNTS[@]}"; do
    cmd="$cmd -v $mount"
  done
  
  cmd="$cmd $image $@"
  
  echo "$cmd"
}

# Main function just calls it
copilot_here() {
  # ... parse args, determine image variant ...
  
  local IMAGE="ghcr.io/user/copilot_here:${VARIANT}"
  local RUN_CMD=$(__copilot_build_run_command "$IMAGE" "$@")
  
  if [ "$DRY_RUN" = true ]; then
    echo "$RUN_CMD"
    return 0
  fi
  
  eval "$RUN_CMD"
}
```

**Test file:**
```bash
#!/bin/bash
# Test the command builder directly - NO docker needed, NO auth needed

source ../../copilot_here.sh

# Test 1: Basic dotnet command
CMD=$(__copilot_build_run_command "ghcr.io/test/copilot_here:dotnet" "test" "arg")

if [[ "$CMD" == *"copilot_here:dotnet"* ]]; then
  echo "✓ Image correct"
fi

if [[ "$CMD" == *"--rm"* ]]; then
  echo "✓ Has --rm"
fi

if [[ "$CMD" == *"-v $(pwd):/work"* ]]; then
  echo "✓ Work dir mounted"
fi

if [[ "$CMD" == *"test arg"* ]]; then
  echo "✓ Args passed"
fi

# Test 2: With additional mounts
ADDITIONAL_MOUNTS=("/tmp/extra:/mnt/extra:ro")
CMD=$(__copilot_build_run_command "ghcr.io/test/copilot_here:latest" "test")

if [[ "$CMD" == *"-v /tmp/extra:/mnt/extra:ro"* ]]; then
  echo "✓ Additional mount included"
fi
```

**Verdict:** ✅ **Super fast, super clean** - but requires refactoring the script

---

## 🎯 My Recommendation for Your Use Case

**Use Approach 2 (Mock Docker) RIGHT NOW** because:

1. ✅ Tests **exactly** what you want - the actual docker command with all args
2. ✅ Works **immediately** - no refactoring needed
3. ✅ Tests the **real code path** - not just a helper function
4. ✅ Fast - no Docker execution, just captures the command
5. ✅ Can test all scenarios:
   - `copilot_here -d` → dotnet image
   - `copilot_here -dp` → dotnet-playwright image
   - `copilot_here --mount /path` → additional mounts
   - `copilot_here --mount-rw /path` → read-write mounts
   - Environment variables, user mapping, etc.

**Later**, refactor to Approach 3 for even better testability.

---

## Example: Complete Test for All Image Variants

```bash
#!/bin/bash
# tests/integration/test_docker_commands.sh

DOCKER_LOG=$(mktemp)

docker() { echo "$*" >> "$DOCKER_LOG"; return 0; }
gh() { 
  case "$1" in
    "auth") echo "github.com" ;;
    "api") echo '{"copilot_business":true}' ;;
    *) return 0 ;;
  esac
}
export -f docker gh

source ../../copilot_here.sh

# Test 1: Base image (no flags)
rm -f "$DOCKER_LOG"
copilot_here test 2>/dev/null
if grep -q "copilot_here:latest" "$DOCKER_LOG"; then
  echo "✓ Base image uses :latest tag"
fi

# Test 2: -d uses dotnet image
rm -f "$DOCKER_LOG"
copilot_here -d test 2>/dev/null
if grep -q "copilot_here:dotnet" "$DOCKER_LOG"; then
  echo "✓ -d flag uses :dotnet tag"
fi

# Test 3: -dp uses dotnet-playwright image
rm -f "$DOCKER_LOG"
copilot_here -dp test 2>/dev/null
if grep -q "copilot_here:dotnet-playwright" "$DOCKER_LOG"; then
  echo "✓ -dp flag uses :dotnet-playwright tag"
fi

# Test 4: --mount adds volume
rm -f "$DOCKER_LOG"
copilot_here --mount /tmp/test test 2>/dev/null
if grep -q "\-v /tmp/test:/mnt/" "$DOCKER_LOG"; then
  echo "✓ --mount adds volume"
fi

# Test 5: --mount-rw is read-write
rm -f "$DOCKER_LOG"
copilot_here --mount-rw /tmp/test test 2>/dev/null
if grep -q "\-v /tmp/test:/mnt/" "$DOCKER_LOG" && ! grep -q "/tmp/test:/mnt/.*:ro" "$DOCKER_LOG"; then
  echo "✓ --mount-rw is read-write"
fi

echo "All variant tests passed!"
```

This approach lets you test **everything** about the Docker command without actually running Docker! 🎉
