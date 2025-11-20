#!/bin/bash
# Image configuration validation tests
# Tests default image setting, retrieval, and precedence rules

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

# Mock HOME to use test directory
export HOME="$TEST_DIR"

# Create project directory structure
mkdir -p "$TEST_DIR/project/.copilot_here"
mkdir -p "$TEST_DIR/.config/copilot_here"

cleanup() {
  rm -rf "$TEST_DIR"
}
trap cleanup EXIT

# Source the script
source "$SCRIPT_DIR/copilot_here.sh"

echo "======================================"
echo "Image Configuration Tests (Bash)"
echo "======================================"
echo "Script: $SCRIPT_DIR/copilot_here.sh"
echo ""

# Switch to project directory for local config tests
cd "$TEST_DIR/project"

# Test 1: Default is "latest" when no config exists
test_start "Default is 'latest' when no config exists"
DEFAULT_IMAGE=$(__copilot_get_default_image)

if [ "$DEFAULT_IMAGE" = "latest" ]; then
  test_pass "Default is latest"
else
  test_fail "Expected 'latest', got '$DEFAULT_IMAGE'"
fi

# Test 2: Save to local config
test_start "Save to local config"
__copilot_save_image_config "dotnet" "false"

if [ -f ".copilot_here/image.conf" ]; then
  CONTENT=$(cat ".copilot_here/image.conf")
  if [ "$CONTENT" = "dotnet" ]; then
    test_pass "Local config saved correctly"
  else
    test_fail "Local config content incorrect: $CONTENT"
  fi
else
  test_fail "Local config file not created"
fi

# Test 3: Get default reads from local config
test_start "Get default reads from local config"
DEFAULT_IMAGE=$(__copilot_get_default_image)

if [ "$DEFAULT_IMAGE" = "dotnet" ]; then
  test_pass "Read from local config correctly"
else
  test_fail "Expected 'dotnet', got '$DEFAULT_IMAGE'"
fi

# Test 4: Save to global config
test_start "Save to global config"
__copilot_save_image_config "playwright" "true"

if [ -f "$HOME/.config/copilot_here/image.conf" ]; then
  CONTENT=$(cat "$HOME/.config/copilot_here/image.conf")
  if [ "$CONTENT" = "playwright" ]; then
    test_pass "Global config saved correctly"
  else
    test_fail "Global config content incorrect: $CONTENT"
  fi
else
  test_fail "Global config file not created"
fi

# Test 5: Local config takes precedence over global
test_start "Local config takes precedence over global"
# Local is 'dotnet', Global is 'playwright'
DEFAULT_IMAGE=$(__copilot_get_default_image)

if [ "$DEFAULT_IMAGE" = "dotnet" ]; then
  test_pass "Local config precedence respected"
else
  test_fail "Expected 'dotnet' (local), got '$DEFAULT_IMAGE'"
fi

# Test 6: Fallback to global when local missing
test_start "Fallback to global when local missing"
rm ".copilot_here/image.conf"
DEFAULT_IMAGE=$(__copilot_get_default_image)

if [ "$DEFAULT_IMAGE" = "playwright" ]; then
  test_pass "Fallback to global working"
else
  test_fail "Expected 'playwright' (global), got '$DEFAULT_IMAGE'"
fi

# Test 7: Show image output format
test_start "Show image output format"
# Re-create local config
echo "dotnet" > ".copilot_here/image.conf"

OUTPUT=$(__copilot_show_default_image)

if echo "$OUTPUT" | grep -q "Image Configuration"; then
  test_pass "Header found"
else
  test_fail "Header 'Image Configuration' not found"
fi

if echo "$OUTPUT" | grep -q "Current effective default: dotnet"; then
  test_pass "Effective default shown"
else
  test_fail "Effective default not shown correctly"
fi

if echo "$OUTPUT" | grep -q "Local config.*dotnet"; then
  test_pass "Local config shown"
else
  test_fail "Local config not shown correctly"
fi

if echo "$OUTPUT" | grep -q "Global config.*playwright"; then
  test_pass "Global config shown"
else
  test_fail "Global config not shown correctly"
fi

if echo "$OUTPUT" | grep -q "Base default: latest"; then
  test_pass "Base default shown"
else
  test_fail "Base default not shown correctly"
fi

print_summary
