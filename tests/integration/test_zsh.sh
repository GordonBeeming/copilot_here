#!/bin/zsh
# Integration tests for Zsh shell script
# Tests copilot_here.sh functions in zsh environment

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

# Summary function
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

# Cleanup function
cleanup() {
  rm -rf "$TEST_DIR"
}

# Setup
TEST_DIR=$(mktemp -d)
trap cleanup EXIT

# Source the script
SCRIPT_DIR="$(cd "$(dirname "$0:A")/../.." && pwd)"
source "$SCRIPT_DIR/copilot_here.sh"

echo "======================================"
echo "Zsh Integration Tests"
echo "======================================"
echo "Shell: Zsh $ZSH_VERSION"
echo "Script: $SCRIPT_DIR/copilot_here.sh"

# Test 1: Functions are defined
test_start "Check if copilot_here function exists"
if command -v copilot_here >/dev/null 2>&1; then
  test_pass "copilot_here function is defined"
else
  test_fail "copilot_here function not found"
fi

# Test 2: copilot_yolo function exists
test_start "Check if copilot_yolo function exists"
if command -v copilot_yolo >/dev/null 2>&1; then
  test_pass "copilot_yolo function is defined"
else
  test_fail "copilot_yolo function not found"
fi

# Test 3: Helper function exists
test_start "Check if helper functions exist"
if command -v __copilot_supports_emoji >/dev/null 2>&1; then
  test_pass "__copilot_supports_emoji helper function exists"
else
  test_fail "__copilot_supports_emoji helper function not found"
fi

# Test 4: Help output works
test_start "Check --help output for copilot_here"
HELP_OUTPUT=$(copilot_here --help 2>&1 || true)
if echo "$HELP_OUTPUT" | grep -qi "usage:"; then
  test_pass "Help output contains Usage section"
else
  test_fail "Help output missing Usage section"
fi

# Test 5: Version in help
test_start "Check version is displayed in help"
if echo "$HELP_OUTPUT" | grep -q "VERSION:"; then
  test_pass "Version information present"
else
  test_fail "Version information missing"
fi

# Test 6: Config file parsing (Zsh array handling)
test_start "Test config file mount loading in Zsh"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts"
echo "# Test comment" > "$TEST_CONFIG"
echo "/test/path1" >> "$TEST_CONFIG"
echo "" >> "$TEST_CONFIG"
echo "/test/path2" >> "$TEST_CONFIG"

# Zsh arrays
typeset -a mounts
__copilot_load_raw_mounts "$TEST_CONFIG" "mounts"

if [ ${#mounts[@]} -eq 2 ]; then
  test_pass "Config file loaded 2 mounts correctly in Zsh"
else
  test_fail "Config file parsing failed (expected 2, got ${#mounts[@]})"
fi

# Test 7: Empty lines and comments ignored
test_start "Verify comments and empty lines are ignored in Zsh"
if [[ "${mounts[1]}" == "/test/path1" ]] && [[ "${mounts[2]}" == "/test/path2" ]]; then
  test_pass "Comments and empty lines correctly ignored"
else
  test_fail "Comment/empty line handling failed (got: ${mounts[@]})"
fi

# Test 8: Path resolution (tilde expansion)
test_start "Test tilde path expansion in Zsh"
RESOLVED=$(__copilot_resolve_mount_path "~/test")
if [[ "$RESOLVED" == "$HOME/test" ]]; then
  test_pass "Tilde expansion works correctly"
else
  test_fail "Tilde expansion failed (got: $RESOLVED)"
fi

# Test 9: Absolute path unchanged
test_start "Test absolute path resolution in Zsh"
RESOLVED=$(__copilot_resolve_mount_path "/absolute/path")
if [[ "$RESOLVED" == "/absolute/path" ]]; then
  test_pass "Absolute path unchanged"
else
  test_fail "Absolute path changed (got: $RESOLVED)"
fi

# Test 10: copilot_yolo help
test_start "Check --help output for copilot_yolo"
YOLO_HELP=$(copilot_yolo --help 2>&1 || true)
if echo "$YOLO_HELP" | grep -qi "usage:"; then
  test_pass "copilot_yolo help output works"
else
  test_fail "copilot_yolo help output missing"
fi

print_summary
