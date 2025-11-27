#!/bin/zsh
# Test Docker command generation using function mocking (Zsh version)
# This validates that correct Docker commands are generated without running Docker
#
# NOTE: This file is separate from test_docker_commands.sh because function
# mocking works differently between shells. Bash requires `export -f` to export
# functions to subprocesses, while zsh handles function visibility differently.

setopt errexit

# Color support
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

TEST_COUNT=0
PASS_COUNT=0
FAIL_COUNT=0

# Test helper functions
test_start() {
  echo ""
  echo "TEST: $1"
  TEST_COUNT=$((TEST_COUNT + 1))
}

test_pass() {
  echo -e "${GREEN}✓ PASS${NC}: $1"
  PASS_COUNT=$((PASS_COUNT + 1))
}

test_fail() {
  echo -e "${RED}✗ FAIL${NC}: $1"
  FAIL_COUNT=$((FAIL_COUNT + 1))
}

print_summary() {
  echo ""
  echo "======================================"
  echo "TEST SUMMARY"
  echo "======================================"
  echo "Total Tests: $TEST_COUNT"
  echo -e "${GREEN}Passed: $PASS_COUNT${NC}"
  if [ $FAIL_COUNT -gt 0 ]; then
    echo -e "${RED}Failed: $FAIL_COUNT${NC}"
  else
    echo "Failed: $FAIL_COUNT"
  fi
  echo "======================================"
  
  if [ $FAIL_COUNT -gt 0 ]; then
    exit 1
  fi
}

# Setup
SCRIPT_DIR="$(cd "$(dirname "$0:A")/../.." && pwd)"
TEST_DIR=$(mktemp -d)
DOCKER_LOG="$TEST_DIR/docker.log"

# Create test working directory and HOME to isolate from production configs
TEST_WORK_DIR="$TEST_DIR/work"
TEST_HOME="$TEST_DIR/home"
mkdir -p "$TEST_WORK_DIR"
mkdir -p "$TEST_HOME/.config/copilot_here"

cleanup() {
  rm -rf "$TEST_DIR"
}
trap cleanup EXIT

# Override HOME to use test directory (isolates from global config)
export HOME="$TEST_HOME"

# Enable test mode to skip auth checks
export COPILOT_HERE_TEST_MODE=true

# Mock docker to capture commands instead of executing
docker() {
  echo "$*" >> "$DOCKER_LOG"
  return 0
}

# Source the script
source "$SCRIPT_DIR/copilot_here.sh"

# Change to test work directory to avoid local .copilot_here/network.json
cd "$TEST_WORK_DIR"

echo "======================================"
echo "Docker Command Tests (Zsh)"
echo "======================================"
echo "Script: $SCRIPT_DIR/copilot_here.sh"
echo ""

# Test 1: Base image variant
test_start "Base image uses :latest tag"
rm -f "$DOCKER_LOG"
copilot_here --no-pull test 2>/dev/null || true

if [ -f "$DOCKER_LOG" ] && grep -q "copilot_here:latest" "$DOCKER_LOG"; then
  test_pass "Base image correct"
else
  test_fail "Base image incorrect"
fi

# Test 2: --dotnet flag uses dotnet image
test_start "Dotnet variant uses :dotnet tag"
rm -f "$DOCKER_LOG"
copilot_here --dotnet --no-pull test 2>/dev/null || true

if grep -q "copilot_here:dotnet" "$DOCKER_LOG"; then
  test_pass "Dotnet image correct"
else
  test_fail "Dotnet image incorrect"
fi

# Test 3: -d shorthand for dotnet
test_start "Short flag -d uses :dotnet tag"
rm -f "$DOCKER_LOG"
copilot_here -d --no-pull test 2>/dev/null || true

if grep -q "copilot_here:dotnet" "$DOCKER_LOG"; then
  test_pass "-d shorthand works"
else
  test_fail "-d shorthand failed"
fi

# Test 4: Playwright variant
test_start "Playwright variant uses :dotnet-playwright tag"
rm -f "$DOCKER_LOG"
copilot_here --dotnet-playwright --no-pull test 2>/dev/null || true

if grep -q "copilot_here:dotnet-playwright" "$DOCKER_LOG"; then
  test_pass "Playwright image correct"
else
  test_fail "Playwright image incorrect"
fi

# Test 5: -dp shorthand for playwright
test_start "Short flag -dp uses :dotnet-playwright tag"
rm -f "$DOCKER_LOG"
copilot_here -dp --no-pull test 2>/dev/null || true

if grep -q "copilot_here:dotnet-playwright" "$DOCKER_LOG"; then
  test_pass "-dp shorthand works"
else
  test_fail "-dp shorthand failed"
fi

# Test 6: --rm flag present
test_start "Docker run includes --rm flag"
LAST_CMD=$(tail -1 "$DOCKER_LOG" 2>/dev/null || echo "")
if echo "$LAST_CMD" | grep -q "\-\-rm"; then
  test_pass "--rm flag present"
else
  test_fail "--rm flag missing"
fi

# Test 7: Interactive flags
test_start "Docker run includes -it flags"
if echo "$LAST_CMD" | grep -q "\-it"; then
  test_pass "-it flags present"
else
  test_fail "-it flags missing"
fi

# Test 8: Working directory mount
test_start "Current directory mounted"
if echo "$LAST_CMD" | grep -q "\-v.*:"; then
  test_pass "Directory mounted"
else
  test_fail "Directory not mounted"
fi

# Test 9: Environment variables
test_start "Environment variables passed"
if echo "$LAST_CMD" | grep -q "\-e"; then
  test_pass "Environment variables present"
else
  test_fail "Environment variables not passed"
fi

# Test 10: Additional mount with --mount
test_start "Additional mount with --mount flag"
rm -f "$DOCKER_LOG"
copilot_here --mount "$TEST_DIR" --no-pull test 2>/dev/null || true
LAST_CMD=$(tail -1 "$DOCKER_LOG" 2>/dev/null || echo "")

if echo "$LAST_CMD" | grep -q "\-v $TEST_DIR:"; then
  test_pass "Additional mount present"
else
  test_fail "Additional mount missing"
fi

# Test 11: Read-write mount with --mount-rw
test_start "Read-write mount with --mount-rw flag"
rm -f "$DOCKER_LOG"
copilot_here --mount-rw "$TEST_DIR" --no-pull test 2>/dev/null || true
LAST_CMD=$(tail -1 "$DOCKER_LOG" 2>/dev/null || echo "")

if echo "$LAST_CMD" | grep -q "\-v $TEST_DIR:"; then
  test_pass "Read-write mount present"
else
  test_fail "Read-write mount missing"
fi

print_summary
