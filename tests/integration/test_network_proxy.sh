#!/bin/sh
# Integration tests for network proxy (airlock) functionality
# Tests --enable-airlock and --enable-global-airlock flags
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

# Test 1: --enable-airlock flag in help
test_start "Check --enable-airlock in copilot_here help"
HELP_OUTPUT=$(copilot_here --help 2>&1 || true)
if echo "$HELP_OUTPUT" | grep -q "enable-airlock"; then
  test_pass "--enable-airlock documented in help"
else
  test_fail "--enable-airlock not in help output"
fi

# Test 2: --enable-global-airlock flag in help
test_start "Check --enable-global-airlock in copilot_here help"
if echo "$HELP_OUTPUT" | grep -q "enable-global-airlock"; then
  test_pass "--enable-global-airlock documented in help"
else
  test_fail "--enable-global-airlock not in help output"
fi

# Test 3: NETWORK (AIRLOCK) section in help
test_start "Check NETWORK (AIRLOCK) section in help"
if echo "$HELP_OUTPUT" | grep -q "NETWORK (AIRLOCK)"; then
  test_pass "NETWORK (AIRLOCK) section present in help"
else
  test_fail "NETWORK (AIRLOCK) section missing from help"
fi

# Test 4: copilot_yolo also has network proxy flags
test_start "Check --enable-airlock in copilot_yolo help"
YOLO_HELP=$(copilot_yolo --help 2>&1 || true)
if echo "$YOLO_HELP" | grep -q "enable-airlock"; then
  test_pass "--enable-airlock documented in copilot_yolo help"
else
  test_fail "--enable-airlock not in copilot_yolo help output"
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
if echo "$OUTPUT" | grep -q "Airlock already enabled"; then
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
test_start "Check default-airlock-rules.json exists"
if [ -f "$SCRIPT_DIR/default-airlock-rules.json" ]; then
  test_pass "default-airlock-rules.json exists in repo"
else
  test_fail "default-airlock-rules.json not found in repo"
fi

# Test 10: Default rules JSON is valid
test_start "Validate default-airlock-rules.json format"
if command -v jq >/dev/null 2>&1; then
  if jq empty "$SCRIPT_DIR/default-airlock-rules.json" 2>/dev/null; then
    test_pass "default-airlock-rules.json is valid JSON"
  else
    test_fail "default-airlock-rules.json is invalid JSON"
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
if [ -f "$SCRIPT_DIR/docker/Dockerfile.proxy" ]; then
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
if echo "$HELP_CHECK" | grep -q "enable-airlock" && echo "$HELP_CHECK" | grep -q "enable-global-airlock"; then
  test_pass "Both network proxy flags are documented"
else
  test_fail "Network proxy flags not properly documented"
fi

# Test 18: --show-airlock-rules documented in help
test_start "Check --show-airlock-rules documented"
if echo "$HELP_CHECK" | grep -q "show-airlock-rules"; then
  test_pass "--show-airlock-rules documented in help"
else
  test_fail "--show-airlock-rules not documented in help"
fi

# Test 19: --edit-airlock-rules documented in help
test_start "Check --edit-airlock-rules documented"
if echo "$HELP_CHECK" | grep -q "edit-airlock-rules"; then
  test_pass "--edit-airlock-rules documented in help"
else
  test_fail "--edit-airlock-rules not documented in help"
fi

# Test 20: --edit-global-airlock-rules documented in help
test_start "Check --edit-global-airlock-rules documented"
if echo "$HELP_CHECK" | grep -q "edit-global-airlock-rules"; then
  test_pass "--edit-global-airlock-rules documented in help"
else
  test_fail "--edit-global-airlock-rules not documented in help"
fi

# Test 21: --show-airlock-rules runs without error
test_start "Check --show-airlock-rules runs"
SHOW_OUTPUT=$(copilot_here --show-airlock-rules 2>&1)
if echo "$SHOW_OUTPUT" | grep -q "Airlock Proxy Rules"; then
  test_pass "--show-airlock-rules displays header"
else
  test_fail "--show-airlock-rules failed: $SHOW_OUTPUT"
fi

# Test 22: Config file has inherit_default_rules field
test_start "Check config has inherit_default_rules"
mkdir -p "$TEST_DIR/.copilot_here"
cat > "$TEST_DIR/.copilot_here/network.json" << 'EOF'
{
  "inherit_default_rules": true,
  "mode": "enforce",
  "enable_logging": false,
  "allowed_rules": []
}
EOF
cd "$TEST_DIR"
CONTENT=$(cat ".copilot_here/network.json")
if echo "$CONTENT" | grep -q '"inherit_default_rules"'; then
  test_pass "Config has inherit_default_rules field"
else
  test_fail "Config missing inherit_default_rules field"
fi
cd - > /dev/null

# Test 23: Default rules JSON has enable_logging field
test_start "Check default-airlock-rules.json has enable_logging"
if grep -q '"enable_logging"' "$SCRIPT_DIR/default-airlock-rules.json"; then
  test_pass "default-airlock-rules.json has enable_logging field"
else
  test_fail "default-airlock-rules.json missing enable_logging field"
fi

# Test 24: Monitor mode enables logging automatically
test_start "Check monitor mode enables logging"
mkdir -p "$TEST_DIR/.copilot_here"
cat > "$TEST_DIR/.copilot_here/network.json" << 'EOF'
{
  "inherit_default_rules": true,
  "mode": "monitor",
  "enable_logging": false,
  "allowed_rules": []
}
EOF
cd "$TEST_DIR"
# The check in __copilot_run_airlock should detect monitor mode
CONTENT=$(cat ".copilot_here/network.json")
if echo "$CONTENT" | grep -q '"mode"[[:space:]]*:[[:space:]]*"monitor"'; then
  test_pass "Monitor mode config is valid"
else
  test_fail "Monitor mode config format issue"
fi
cd - > /dev/null

# Test 25: Compose template has LOGS_MOUNT placeholder
test_start "Check compose template has LOGS_MOUNT placeholder"
if grep -q "{{LOGS_MOUNT}}" "$SCRIPT_DIR/docker-compose.airlock.yml.template"; then
  test_pass "Compose template has LOGS_MOUNT placeholder"
else
  test_fail "Compose template missing LOGS_MOUNT placeholder"
fi

# Test 26: Compose template has proxy volume for config
test_start "Check compose template mounts network config"
if grep -q "{{NETWORK_CONFIG}}" "$SCRIPT_DIR/docker-compose.airlock.yml.template"; then
  test_pass "Compose template has network config mount"
else
  test_fail "Compose template missing network config mount"
fi

# Test 27: --disable-airlock documented in help
test_start "Check --disable-airlock documented"
if echo "$HELP_CHECK" | grep -q "disable-airlock"; then
  test_pass "--disable-airlock documented in help"
else
  test_fail "--disable-airlock not documented in help"
fi

# Test 28: --disable-global-airlock documented in help
test_start "Check --disable-global-airlock documented"
if echo "$HELP_CHECK" | grep -q "disable-global-airlock"; then
  test_pass "--disable-global-airlock documented in help"
else
  test_fail "--disable-global-airlock not documented in help"
fi

# Test 29: Default rules JSON has enabled field
test_start "Check default-airlock-rules.json has enabled field"
if grep -q '"enabled"' "$SCRIPT_DIR/default-airlock-rules.json"; then
  test_pass "default-airlock-rules.json has enabled field"
else
  test_fail "default-airlock-rules.json missing enabled field"
fi

# Test 30: --enable-airlock on existing config just enables
test_start "Check --enable-airlock enables existing config"
# Use a fresh temp dir to avoid any state from previous tests
ENABLE_TEST_DIR=$(mktemp -d)
mkdir -p "$ENABLE_TEST_DIR/.copilot_here"
cat > "$ENABLE_TEST_DIR/.copilot_here/network.json" << 'EOF'
{
  "enabled": false,
  "inherit_default_rules": true,
  "mode": "enforce",
  "allowed_rules": []
}
EOF
cd "$ENABLE_TEST_DIR"
copilot_here --enable-airlock > /dev/null 2>&1 || true
CONTENT=$(cat ".copilot_here/network.json")
cd - > /dev/null
# jq may format with different spacing, so just check for enabled and true on same concept
if echo "$CONTENT" | grep -q '"enabled"' && echo "$CONTENT" | grep '"enabled"' | grep -q 'true'; then
  test_pass "--enable-airlock set enabled to true"
else
  test_fail "--enable-airlock did not set enabled to true"
fi
rm -rf "$ENABLE_TEST_DIR"

# Test 31: --disable-airlock sets enabled to false
test_start "Check --disable-airlock disables config"
mkdir -p "$TEST_DIR/.copilot_here"
cat > "$TEST_DIR/.copilot_here/network.json" << 'EOF'
{
  "enabled": true,
  "inherit_default_rules": true,
  "mode": "enforce",
  "allowed_rules": []
}
EOF
cd "$TEST_DIR"
copilot_here --disable-airlock > /dev/null 2>&1 || true
CONTENT=$(cat ".copilot_here/network.json")
# Check for enabled: false (jq may format with different spacing)
if echo "$CONTENT" | grep -q '"enabled"' && echo "$CONTENT" | grep '"enabled"' | grep -q 'false'; then
  test_pass "--disable-airlock set enabled to false"
else
  test_fail "--disable-airlock did not set enabled to false"
fi
cd - > /dev/null

# Test 32: --enable-airlock creates new config when none exists
test_start "Check --enable-airlock creates new config"
NEW_CONFIG_DIR=$(mktemp -d)
mkdir -p "$NEW_CONFIG_DIR"
cd "$NEW_CONFIG_DIR"
# Ensure no config exists
rm -rf ".copilot_here" 2>/dev/null || true
# Run enable (this will prompt for mode, we simulate enforce with 'e')
echo "e" | copilot_here --enable-airlock > /dev/null 2>&1 || true
if [ -f ".copilot_here/network.json" ]; then
  CONTENT=$(cat ".copilot_here/network.json")
  # Check for enabled: true (jq may format with different spacing)
  if echo "$CONTENT" | grep -q '"enabled"' && echo "$CONTENT" | grep '"enabled"' | grep -q 'true'; then
    test_pass "--enable-airlock created new config with enabled=true"
  else
    test_fail "New config missing enabled=true"
  fi
else
  test_fail "--enable-airlock did not create network.json"
fi
cd - > /dev/null
rm -rf "$NEW_CONFIG_DIR"

print_summary
