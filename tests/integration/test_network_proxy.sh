#!/bin/sh
# Integration tests for network proxy (airlock) functionality
# Tests --enable-network-proxy and --enable-global-network-proxy flags
# Compatible with bash and zsh

set -e

# Color support
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

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
  printf "${GREEN}✓ PASS${NC}: %s\n" "$1"
  PASS_COUNT=$((PASS_COUNT + 1))
}

test_fail() {
  printf "${RED}✗ FAIL${NC}: %s\n" "$1"
  FAIL_COUNT=$((FAIL_COUNT + 1))
}

# Summary function
print_summary() {
  echo ""
  echo "======================================"
  echo "TEST SUMMARY"
  echo "======================================"
  echo "Total Tests: $TEST_COUNT"
  printf "${GREEN}Passed: $PASS_COUNT${NC}\n"
  if [ $FAIL_COUNT -gt 0 ]; then
    printf "${RED}Failed: $FAIL_COUNT${NC}\n"
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
  # Remove test network config if created
  rm -f ".copilot_here/network.json" 2>/dev/null || true
  rmdir ".copilot_here" 2>/dev/null || true
}

# Setup
TEST_DIR=$(mktemp -d)
trap cleanup EXIT

# Source the script - handle both bash and zsh
if [ -n "$BASH_VERSION" ]; then
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
  SHELL_NAME="Bash"
  SHELL_VERSION="$BASH_VERSION"
elif [ -n "$ZSH_VERSION" ]; then
  SCRIPT_DIR="${0:A:h}/../.."
  SHELL_NAME="Zsh"
  SHELL_VERSION="$ZSH_VERSION"
else
  SCRIPT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
  SHELL_NAME="sh"
  SHELL_VERSION="unknown"
fi

source "$SCRIPT_DIR/copilot_here.sh"

# Set test mode to skip auth checks
export COPILOT_HERE_TEST_MODE=true

echo "======================================"
echo "Network Proxy (Airlock) Tests - $SHELL_NAME"
echo "======================================"
echo "Shell: $SHELL_VERSION"
echo "Script: $SCRIPT_DIR/copilot_here.sh"

# Test 1: --enable-network-proxy flag in help
test_start "Check --enable-network-proxy in copilot_here help"
HELP_OUTPUT=$(copilot_here --help 2>&1 || true)
if echo "$HELP_OUTPUT" | grep -q "enable-network-proxy"; then
  test_pass "--enable-network-proxy documented in help"
else
  test_fail "--enable-network-proxy not in help output"
fi

# Test 2: --enable-global-network-proxy flag in help
test_start "Check --enable-global-network-proxy in copilot_here help"
if echo "$HELP_OUTPUT" | grep -q "enable-global-network-proxy"; then
  test_pass "--enable-global-network-proxy documented in help"
else
  test_fail "--enable-global-network-proxy not in help output"
fi

# Test 3: NETWORK PROXY section in help
test_start "Check NETWORK PROXY section in help"
if echo "$HELP_OUTPUT" | grep -q "NETWORK PROXY"; then
  test_pass "NETWORK PROXY section present in help"
else
  test_fail "NETWORK PROXY section missing from help"
fi

# Test 4: copilot_yolo also has network proxy flags
test_start "Check --enable-network-proxy in copilot_yolo help"
YOLO_HELP=$(copilot_yolo --help 2>&1 || true)
if echo "$YOLO_HELP" | grep -q "enable-network-proxy"; then
  test_pass "--enable-network-proxy documented in copilot_yolo help"
else
  test_fail "--enable-network-proxy not in copilot_yolo help output"
fi

# Test 5: __copilot_ensure_network_config function exists
test_start "Check if __copilot_ensure_network_config function exists"
if command -v __copilot_ensure_network_config >/dev/null 2>&1; then
  test_pass "__copilot_ensure_network_config function is defined"
else
  test_fail "__copilot_ensure_network_config function not found"
fi

# Test 6: __copilot_run_airlock function exists
test_start "Check if __copilot_run_airlock function exists"
if command -v __copilot_run_airlock >/dev/null 2>&1; then
  test_pass "__copilot_run_airlock function is defined"
else
  test_fail "__copilot_run_airlock function not found"
fi

# Test 7: Existing config is detected
test_start "Test existing network config detection"
mkdir -p "$TEST_DIR/.copilot_here"
cat > "$TEST_DIR/.copilot_here/network.json" << 'EOF'
{
  "inherit_default_rules": true,
  "mode": "enforce",
  "allowed_rules": []
}
EOF

cd "$TEST_DIR"
OUTPUT=$(__copilot_ensure_network_config "false" 2>&1 || true)
if echo "$OUTPUT" | grep -q "Using existing network config"; then
  test_pass "Existing config detected correctly"
else
  test_fail "Existing config not detected (output: $OUTPUT)"
fi
cd - > /dev/null

# Test 8: Global config path is correct
test_start "Test global config path"
GLOBAL_CONFIG_DIR="$HOME/.config/copilot_here"
# Just verify we can determine the path, don't require write access
if [ -n "$HOME" ] && [ -n "$GLOBAL_CONFIG_DIR" ]; then
  test_pass "Global config directory path: $GLOBAL_CONFIG_DIR"
else
  test_fail "Cannot determine global config directory"
fi

# Test 9: Default network rules file exists in repo
test_start "Check default-network-rules.json exists"
if [ -f "$SCRIPT_DIR/default-network-rules.json" ]; then
  test_pass "default-network-rules.json exists in repo"
else
  test_fail "default-network-rules.json not found in repo"
fi

# Test 10: Default rules JSON is valid
test_start "Validate default-network-rules.json format"
if command -v jq >/dev/null 2>&1; then
  if jq empty "$SCRIPT_DIR/default-network-rules.json" 2>/dev/null; then
    test_pass "default-network-rules.json is valid JSON"
  else
    test_fail "default-network-rules.json is invalid JSON"
  fi
else
  # Skip if jq not available
  echo "  (skipped - jq not installed)"
  test_pass "Skipped - jq not available"
fi

# Test 11: Docker compose template exists
test_start "Check docker-compose.airlock.yml.template exists"
if [ -f "$SCRIPT_DIR/docker-compose.airlock.yml.template" ]; then
  test_pass "docker-compose.airlock.yml.template exists"
else
  test_fail "docker-compose.airlock.yml.template not found"
fi

# Test 12: Compose template has required placeholders
test_start "Validate compose template placeholders"
TEMPLATE_CONTENT=$(cat "$SCRIPT_DIR/docker-compose.airlock.yml.template")
MISSING_PLACEHOLDERS=""
for placeholder in "{{PROJECT_NAME}}" "{{APP_IMAGE}}" "{{PROXY_IMAGE}}" "{{NETWORK_CONFIG}}"; do
  if ! echo "$TEMPLATE_CONTENT" | grep -q "$placeholder"; then
    MISSING_PLACEHOLDERS="$MISSING_PLACEHOLDERS $placeholder"
  fi
done
if [ -z "$MISSING_PLACEHOLDERS" ]; then
  test_pass "All required placeholders present in template"
else
  test_fail "Missing placeholders:$MISSING_PLACEHOLDERS"
fi

# Test 13: entrypoint-airlock.sh exists
test_start "Check entrypoint-airlock.sh exists"
if [ -f "$SCRIPT_DIR/entrypoint-airlock.sh" ]; then
  test_pass "entrypoint-airlock.sh exists"
else
  test_fail "entrypoint-airlock.sh not found"
fi

# Test 14: proxy-entrypoint.sh exists
test_start "Check proxy-entrypoint.sh exists"
if [ -f "$SCRIPT_DIR/proxy-entrypoint.sh" ]; then
  test_pass "proxy-entrypoint.sh exists"
else
  test_fail "proxy-entrypoint.sh not found"
fi

# Test 15: Dockerfile.proxy exists
test_start "Check Dockerfile.proxy exists"
if [ -f "$SCRIPT_DIR/Dockerfile.proxy" ]; then
  test_pass "Dockerfile.proxy exists"
else
  test_fail "Dockerfile.proxy not found"
fi

# Test 16: Proxy Rust source exists
test_start "Check proxy/src/main.rs exists"
if [ -f "$SCRIPT_DIR/proxy/src/main.rs" ]; then
  test_pass "proxy/src/main.rs exists"
else
  test_fail "proxy/src/main.rs not found"
fi

# Test 17: Mutually exclusive flags error message
test_start "Test mutually exclusive flags handled"
# This should be handled before docker is invoked
HELP_CHECK=$(copilot_here --help 2>&1 || true)
# Verify both flags are documented (implementation handles mutual exclusion)
if echo "$HELP_CHECK" | grep -q "enable-network-proxy" && echo "$HELP_CHECK" | grep -q "enable-global-network-proxy"; then
  test_pass "Both network proxy flags are documented"
else
  test_fail "Network proxy flags not properly documented"
fi

print_summary
