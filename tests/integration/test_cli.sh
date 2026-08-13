#!/bin/bash
# Cross-platform CLI integration tests
# 
# These tests verify the CLI binary works correctly across platforms.
# They DO NOT require Docker - they test CLI functionality only.
#
# Usage: ./tests/integration/test_cli.sh [--cli-path <path>]

set -e

# Resolved once at load time: tests change the working directory as they run, so
# deriving these from $BASH_SOURCE later (including in the EXIT trap) resolves
# against the wrong directory.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Parse arguments
CLI_PATH=""
while [[ $# -gt 0 ]]; do
  case $1 in
    --cli-path)
      CLI_PATH="$2"
      shift 2
      ;;
    *)
      shift
      ;;
  esac
done

# Color support (disable on Windows/non-tty)
if [[ -t 1 ]] && [[ "$(uname)" != MINGW* ]] && [[ "$(uname)" != CYGWIN* ]]; then
  RED='\033[0;31m'
  GREEN='\033[0;32m'
  YELLOW='\033[1;33m'
  BLUE='\033[0;34m'
  NC='\033[0m'
else
  RED=''
  GREEN=''
  YELLOW=''
  BLUE=''
  NC=''
fi

TEST_COUNT=0
PASS_COUNT=0
FAIL_COUNT=0

test_start() {
  echo ""
  echo -e "${BLUE}TEST:${NC} $1"
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
  echo "CLI INTEGRATION TEST SUMMARY"
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
    return 1
  fi
  return 0
}

# Build or find CLI binary
CLI_BINARY=""

setup_cli() {
  echo "🔧 Setting up CLI binary..."
  
  local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local repo_root="$(cd "$script_dir/../.." && pwd)"
  
  if [ -n "$CLI_PATH" ] && [ -f "$CLI_PATH" ]; then
    CLI_BINARY="$CLI_PATH"
    echo "   Using provided CLI: $CLI_BINARY"
  else
    # Build CLI from source
    echo "   Building CLI from source..."
    
    if ! command -v dotnet &> /dev/null; then
      echo "❌ .NET SDK not found. Install it or provide --cli-path"
      exit 1
    fi
    
    local publish_dir="$repo_root/publish/cli-test"
    mkdir -p "$publish_dir"
    
    if ! dotnet publish "$repo_root/app/CopilotHere.csproj" -c Release -o "$publish_dir" --nologo -v q 2>&1; then
      echo "❌ Failed to build CLI"
      exit 1
    fi
    
    # Find binary (different name on different platforms)
    if [ -f "$publish_dir/copilot_here" ]; then
      CLI_BINARY="$publish_dir/copilot_here"
    elif [ -f "$publish_dir/copilot_here.exe" ]; then
      CLI_BINARY="$publish_dir/copilot_here.exe"
    else
      echo "❌ CLI binary not found after build"
      exit 1
    fi
    
    chmod +x "$CLI_BINARY" 2>/dev/null || true
    echo "   Built CLI: $CLI_BINARY"
  fi
}

cleanup() {
  rm -rf "$REPO_ROOT/publish/cli-test" 2>/dev/null || true
  rm -rf "$TEST_DIR" 2>/dev/null || true
}

# Test directory for config tests
TEST_DIR=""

setup_test_dir() {
  TEST_DIR=$(mktemp -d)
  mkdir -p "$TEST_DIR/.copilot_here"
}

# ============================================================================
# TEST CASES
# ============================================================================

test_version() {
  test_start "CLI shows version"
  
  local result
  result=$("$CLI_BINARY" --version 2>&1) || true
  
  # Version format: YYYY.MM.DD or YYYY.MM.DD.sha (e.g., 2025.12.02 or 2025.12.02.abc123)
  if echo "$result" | grep -qE "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}"; then
    test_pass "Version: $(echo "$result" | head -1)"
  else
    test_fail "Did not show version: $result"
  fi
}

test_help() {
  test_start "CLI shows help"
  
  local result
  result=$("$CLI_BINARY" --help 2>&1) || true
  
  if echo "$result" | grep -q "GitHub Copilot CLI"; then
    test_pass "Help text includes description"
  else
    test_fail "Help missing description: $result"
  fi
}

test_help_shows_options() {
  test_start "CLI help shows main options"
  
  local result
  result=$("$CLI_BINARY" --help 2>&1) || true
  
  local all_found=true
  local missing=""
  
  for option in "--dotnet" "--playwright" "--mount" "--no-pull" "--yolo"; do
    if ! echo "$result" | grep -q -- "$option"; then
      all_found=false
      missing="$missing $option"
    fi
  done
  
  if [ "$all_found" = true ]; then
    test_pass "All main options present"
  else
    test_fail "Missing options:$missing"
  fi
}

test_help_shows_commands() {
  test_start "CLI help shows commands"
  
  local result
  result=$("$CLI_BINARY" --help 2>&1) || true
  
  local all_found=true
  local missing=""
  
  for cmd in "--list-mounts" "--list-images" "--show-image" "--enable-airlock"; do
    if ! echo "$result" | grep -q -- "$cmd"; then
      all_found=false
      missing="$missing $cmd"
    fi
  done
  
  if [ "$all_found" = true ]; then
    test_pass "All main commands present"
  else
    test_fail "Missing commands:$missing"
  fi
}

test_list_images() {
  test_start "CLI lists available images"
  
  local result
  result=$("$CLI_BINARY" --list-images 2>&1) || true
  
  if echo "$result" | grep -q "latest" && echo "$result" | grep -q "dotnet"; then
    test_pass "Image list includes expected variants"
  else
    test_fail "Image list incomplete: $result"
  fi
}

test_show_image() {
  test_start "CLI shows current image config"
  
  local result
  result=$("$CLI_BINARY" --show-image 2>&1) || true
  
  if echo "$result" | grep -q "latest"; then
    test_pass "Shows image configuration"
  else
    test_fail "Did not show image config: $result"
  fi
}

test_list_mounts_empty() {
  test_start "CLI lists mounts (empty)"
  
  cd "$TEST_DIR"
  local result
  result=$("$CLI_BINARY" --list-mounts 2>&1) || true
  
  # Should run without error even with no mounts configured
  if [ $? -eq 0 ] || echo "$result" | grep -qi "mount"; then
    test_pass "List mounts works with empty config"
  else
    test_fail "List mounts failed: $result"
  fi
}

test_show_airlock_rules() {
  test_start "CLI shows airlock rules"
  
  cd "$TEST_DIR"
  local result
  result=$("$CLI_BINARY" --show-airlock-rules 2>&1) || true
  
  # Should run without error (may show no rules or default rules)
  if echo "$result" | grep -qi -E "(airlock|rules|no.*config)"; then
    test_pass "Shows airlock rules info"
  else
    test_fail "Did not show airlock info: $result"
  fi
}

test_dotnet_alias() {
  test_start "CLI accepts -d9 alias"
  
  local result
  result=$("$CLI_BINARY" -d9 --help 2>&1) || true
  
  # Should not show "unrecognized" error
  if ! echo "$result" | grep -qi "unrecognized"; then
    test_pass "-d9 alias accepted"
  else
    test_fail "-d9 not recognized: $result"
  fi
}

test_yolo_flag() {
  test_start "CLI accepts --yolo flag"
  
  local result
  result=$("$CLI_BINARY" --yolo --help 2>&1) || true
  
  # Should not show "unrecognized" error
  if ! echo "$result" | grep -qi "unrecognized"; then
    test_pass "--yolo flag accepted"
  else
    test_fail "--yolo not recognized: $result"
  fi
}

test_passthrough_help() {
  test_start "CLI passes --help2 through"
  
  local result
  result=$("$CLI_BINARY" --help2 2>&1) || true
  
  # --help2 should be recognized (passed to copilot)
  if ! echo "$result" | grep -qi "unrecognized.*help2"; then
    test_pass "--help2 recognized for passthrough"
  else
    test_fail "--help2 not handled: $result"
  fi
}

test_profile_block_keeps_path_variable() {
  test_start "Profile block references \$PATH instead of a snapshot of it"

  local fake_home profile result

  fake_home="$TEST_DIR/profile-home"
  mkdir -p "$fake_home/.local/bin"
  profile="$fake_home/.bashrc"
  printf 'export USER_CONFIG_KEPT=1\n' > "$profile"

  # Sentinel entries stand in for whatever PATH happens to hold at install time.
  # If any of them reach the profile, the block has baked in a snapshot and will
  # wipe the user's live PATH on every shell start.
  env HOME="$fake_home" PATH="/sentinel/alpha:/sentinel/beta:/usr/bin:/bin" \
    bash -c "source '$REPO_ROOT/copilot_here.sh' >/dev/null 2>&1; __copilot_update_profile \"\$HOME/.bashrc\" 'test profile'" >/dev/null 2>&1

  if grep -qF '/sentinel/alpha' "$profile" || grep -qF '/sentinel/beta' "$profile"; then
    test_fail "Profile block baked in the install-time PATH: $(grep -F '/sentinel/' "$profile" | head -n 1)"
    return
  fi

  if ! grep -qF ':$PATH:' "$profile" || ! grep -qF '$HOME/.local/bin' "$profile"; then
    test_fail "Profile block is missing the literal \$PATH / \$HOME references"
    return
  fi

  if ! grep -qF 'USER_CONFIG_KEPT' "$profile"; then
    test_fail "Profile update discarded the user's own config"
    return
  fi

  # Sourcing the block from an unrelated PATH must prepend, not replace.
  result=$(env -i HOME="$fake_home" PATH="/keep/me:/usr/bin:/bin" \
    bash -c 'source "$HOME/.bashrc" >/dev/null 2>&1; echo "$PATH"')

  if [[ "$result" == "$fake_home/.local/bin:/keep/me:/usr/bin:/bin" ]]; then
    test_pass "Block prepends ~/.local/bin and preserves the existing PATH"
  else
    test_fail "Unexpected PATH after sourcing profile: $result"
  fi
}

# ============================================================================
# MAIN
# ============================================================================

main() {
  echo "========================================"
  echo "    CLI INTEGRATION TESTS"
  echo "========================================"
  echo "    Platform: $(uname -s) $(uname -m)"
  echo "========================================"
  
  trap cleanup EXIT INT TERM
  
  setup_cli
  setup_test_dir
  
  # Run tests
  test_version
  test_help
  test_help_shows_options
  test_help_shows_commands
  test_list_images
  test_show_image
  test_list_mounts_empty
  test_show_airlock_rules
  test_dotnet_alias
  test_yolo_flag
  test_passthrough_help
  test_profile_block_keeps_path_variable

  print_summary
  exit $?
}

main "$@"
