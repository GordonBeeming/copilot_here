#!/bin/bash
# Mount configuration validation tests
# Tests edge cases, invalid paths, and mount handling

set -e

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
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEST_DIR=$(mktemp -d)

cleanup() {
  rm -rf "$TEST_DIR"
}
trap cleanup EXIT

# Source the script
source "$SCRIPT_DIR/copilot_here.sh"

echo "======================================"
echo "Mount Configuration Tests (Bash)"
echo "======================================"
echo "Script: $SCRIPT_DIR/copilot_here.sh"
echo ""

# Test 1: Load mounts from valid config file
test_start "Load mounts from valid config file"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts"
echo "/valid/path1" > "$TEST_CONFIG"
echo "/valid/path2" >> "$TEST_CONFIG"

mounts=()
__copilot_load_raw_mounts "$TEST_CONFIG" "mounts"

if [ ${#mounts[@]} -eq 2 ]; then
  test_pass "Loaded 2 mounts correctly"
else
  test_fail "Expected 2 mounts, got ${#mounts[@]}"
fi

# Test 2: Ignore comments in config
test_start "Comments are properly ignored"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts_comments"
echo "# This is a comment" > "$TEST_CONFIG"
echo "/path/one" >> "$TEST_CONFIG"
echo "  # Indented comment" >> "$TEST_CONFIG"
echo "/path/two" >> "$TEST_CONFIG"

mounts=()
__copilot_load_raw_mounts "$TEST_CONFIG" "mounts"

if [ ${#mounts[@]} -eq 2 ]; then
  test_pass "Comments ignored correctly"
else
  test_fail "Comment handling failed (got ${#mounts[@]} mounts)"
fi

# Test 3: Ignore empty lines
test_start "Empty lines are ignored"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts_empty"
echo "/path/one" > "$TEST_CONFIG"
echo "" >> "$TEST_CONFIG"
echo "   " >> "$TEST_CONFIG"
echo "/path/two" >> "$TEST_CONFIG"

mounts=()
__copilot_load_raw_mounts "$TEST_CONFIG" "mounts"

if [ ${#mounts[@]} -eq 2 ]; then
  test_pass "Empty lines ignored"
else
  test_fail "Empty line handling failed"
fi

# Test 4: Handle non-existent config file
test_start "Non-existent config file handled gracefully"
mounts=()
__copilot_load_raw_mounts "$TEST_DIR/nonexistent.txt" "mounts"

if [ ${#mounts[@]} -eq 0 ]; then
  test_pass "Non-existent file handled"
else
  test_fail "Should have 0 mounts for non-existent file"
fi

# Test 5: Tilde expansion in path resolution
test_start "Tilde expansion works"
TEST_TILDE_PATH="$TEST_DIR/tilde_test"
mkdir -p "$TEST_TILDE_PATH"
# Create a symlink in a location we control to test tilde expansion
HOME_TEST="$TEST_DIR/home_test"
mkdir -p "$HOME_TEST/Documents"
RESOLVED=$(HOME="$HOME_TEST" __copilot_resolve_mount_path "~/Documents")

if [[ "$RESOLVED" == "$HOME_TEST/Documents" ]]; then
  test_pass "Tilde expanded correctly"
else
  test_fail "Tilde expansion failed (got: $RESOLVED)"
fi

# Test 6: Absolute path unchanged
test_start "Absolute path remains unchanged"
TEST_ABS_PATH="$TEST_DIR/absolute/path"
mkdir -p "$TEST_ABS_PATH"
RESOLVED=$(__copilot_resolve_mount_path "$TEST_ABS_PATH")

if [[ "$RESOLVED" == "$TEST_ABS_PATH" ]]; then
  test_pass "Absolute path unchanged"
else
  test_fail "Absolute path modified"
fi

# Test 7: Relative path resolved to absolute
test_start "Relative path resolved to absolute"
cd "$TEST_DIR"
mkdir -p subdir
RESOLVED=$(__copilot_resolve_mount_path "subdir")

if [[ "$RESOLVED" == "$TEST_DIR/subdir" ]]; then
  test_pass "Relative path resolved"
else
  test_fail "Relative path resolution failed (got: $RESOLVED, expected: $TEST_DIR/subdir)"
fi

# Test 8: Path with spaces
test_start "Path with spaces handled correctly"
TEST_PATH="$TEST_DIR/path with spaces"
mkdir -p "$TEST_PATH"
RESOLVED=$(__copilot_resolve_mount_path "$TEST_PATH")

if [[ "$RESOLVED" == "$TEST_PATH" ]]; then
  test_pass "Path with spaces handled"
else
  test_fail "Path with spaces failed"
fi

# Test 9: Path starting with ./ resolved
test_start "Path starting with ./ resolved"
cd "$TEST_DIR"
mkdir -p ./relative
RESOLVED=$(__copilot_resolve_mount_path "./relative")

if [[ "$RESOLVED" == "$TEST_DIR/relative" ]]; then
  test_pass "./path resolved correctly"
else
  test_fail "./path resolution failed"
fi

# Test 10: Path with mount suffix preserved
test_start "Mount suffix (e.g., :ro) preserved"
# Note: This tests the path resolution, not the mount creation
RESOLVED=$(__copilot_resolve_mount_path "$TEST_DIR:ro")

# The function should handle this, check implementation
if [[ "$RESOLVED" == *"$TEST_DIR"* ]]; then
  test_pass "Path with suffix handled"
else
  test_fail "Path with suffix failed"
fi

# Test 11: Config with trailing whitespace
test_start "Trailing whitespace in config handled"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts_whitespace"
echo "/path/one   " > "$TEST_CONFIG"
echo "  /path/two" >> "$TEST_CONFIG"
echo "  /path/three  " >> "$TEST_CONFIG"

mounts=()
__copilot_load_raw_mounts "$TEST_CONFIG" "mounts"

if [ ${#mounts[@]} -eq 3 ]; then
  test_pass "Whitespace handled correctly"
else
  test_fail "Whitespace handling failed"
fi

# Test 12: Symlink config file followed
# Linux/macOS only - tests symbolic link following with readlink
test_start "Symlink config file is followed"
TEST_CONFIG="$TEST_DIR/.copilot_here_mounts_real"
TEST_LINK="$TEST_DIR/.copilot_here_mounts_link"
echo "/symlink/path" > "$TEST_CONFIG"
ln -s "$TEST_CONFIG" "$TEST_LINK"

mounts=()
__copilot_load_raw_mounts "$TEST_LINK" "mounts"

if [ ${#mounts[@]} -eq 1 ] && [[ "${mounts[0]}" == "/symlink/path" ]]; then
  test_pass "Symlink followed correctly"
else
  test_fail "Symlink following failed"
fi

# Test 13: Mount with :rw suffix is correctly loaded into rw array
test_start "Mount with :rw suffix is loaded into rw array (not ro)"
TEST_CONFIG_RW="$TEST_DIR/.copilot_here_mounts_rw_test"
echo "$TEST_DIR/rw_path:rw" > "$TEST_CONFIG_RW"
mkdir -p "$TEST_DIR/rw_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_RW" ro_mounts rw_mounts

if [ ${#rw_mounts[@]} -eq 1 ] && [ ${#ro_mounts[@]} -eq 0 ]; then
  test_pass "Mount with :rw suffix placed in rw array"
else
  test_fail "Mount with :rw suffix not correctly categorized (ro=${#ro_mounts[@]}, rw=${#rw_mounts[@]})"
fi

# Test 14: Mount with :rw suffix has suffix stripped
test_start "Mount with :rw suffix has suffix stripped"
TEST_CONFIG_RW_STRIP="$TEST_DIR/.copilot_here_mounts_rw_strip"
echo "$TEST_DIR/stripped_path:rw" > "$TEST_CONFIG_RW_STRIP"
mkdir -p "$TEST_DIR/stripped_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_RW_STRIP" ro_mounts rw_mounts

if [[ "${rw_mounts[0]}" == "$TEST_DIR/stripped_path" ]] && [[ "${rw_mounts[0]}" != *":rw"* ]]; then
  test_pass "Mount path has :rw suffix stripped"
else
  test_fail "Mount path still has :rw suffix (got: ${rw_mounts[0]})"
fi

# Test 15: Mount with :ro suffix is correctly loaded into ro array
test_start "Mount with :ro suffix is loaded into ro array"
TEST_CONFIG_RO="$TEST_DIR/.copilot_here_mounts_ro_test"
echo "$TEST_DIR/ro_path:ro" > "$TEST_CONFIG_RO"
mkdir -p "$TEST_DIR/ro_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_RO" ro_mounts rw_mounts

if [ ${#ro_mounts[@]} -eq 1 ] && [ ${#rw_mounts[@]} -eq 0 ]; then
  test_pass "Mount with :ro suffix placed in ro array"
else
  test_fail "Mount with :ro suffix not correctly categorized"
fi

# Test 16: Mount without suffix defaults to ro array
test_start "Mount without suffix defaults to ro array"
TEST_CONFIG_DEFAULT="$TEST_DIR/.copilot_here_mounts_default"
echo "$TEST_DIR/default_path" > "$TEST_CONFIG_DEFAULT"
mkdir -p "$TEST_DIR/default_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_DEFAULT" ro_mounts rw_mounts

if [ ${#ro_mounts[@]} -eq 1 ] && [ ${#rw_mounts[@]} -eq 0 ]; then
  test_pass "Mount without suffix placed in ro array (default)"
else
  test_fail "Mount without suffix not defaulting to ro"
fi

# Test 17: Mount with trailing whitespace and :rw suffix is handled
test_start "Mount with trailing whitespace and :rw suffix is handled"
TEST_CONFIG_WS="$TEST_DIR/.copilot_here_mounts_ws"
printf "%s:rw   \n" "$TEST_DIR/ws_path" > "$TEST_CONFIG_WS"
mkdir -p "$TEST_DIR/ws_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_WS" ro_mounts rw_mounts

if [ ${#rw_mounts[@]} -eq 1 ] && [[ "${rw_mounts[0]}" == "$TEST_DIR/ws_path" ]]; then
  test_pass "Mount with trailing whitespace handled correctly"
else
  test_fail "Mount with trailing whitespace not handled (ro=${#ro_mounts[@]}, rw=${#rw_mounts[@]}, path='${rw_mounts[0]}')"
fi

# Test 18: Mount with leading whitespace and :rw suffix is handled
test_start "Mount with leading whitespace and :rw suffix is handled"
TEST_CONFIG_LWS="$TEST_DIR/.copilot_here_mounts_lws"
printf "   %s:rw\n" "$TEST_DIR/lws_path" > "$TEST_CONFIG_LWS"
mkdir -p "$TEST_DIR/lws_path"

ro_mounts=()
rw_mounts=()
__copilot_load_mounts "$TEST_CONFIG_LWS" ro_mounts rw_mounts

if [ ${#rw_mounts[@]} -eq 1 ] && [[ "${rw_mounts[0]}" == "$TEST_DIR/lws_path" ]]; then
  test_pass "Mount with leading whitespace handled correctly"
else
  test_fail "Mount with leading whitespace not handled (ro=${#ro_mounts[@]}, rw=${#rw_mounts[@]}, path='${rw_mounts[0]}')"
fi

print_summary
