#!/bin/bash
# GitHub info extraction tests
# Tests the __copilot_get_github_info function with various URL formats

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

# Setup - detect script directory (works in both bash and zsh)
if [ -n "$BASH_SOURCE" ]; then
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
else
  SCRIPT_DIR="$(cd "$(dirname "$0")/../.." && pwd)"
fi
TEST_DIR=$(mktemp -d)

cleanup() {
  rm -rf "$TEST_DIR"
}
trap cleanup EXIT

echo "======================================"
echo "GitHub Info Extraction Tests"
echo "======================================"
echo "Script: $SCRIPT_DIR/copilot_here.sh"
echo ""

# Source the script
source "$SCRIPT_DIR/copilot_here.sh"

# Helper to test URL parsing by creating a mock git repo
test_url_parsing() {
  local url="$1"
  local expected_owner="$2"
  local expected_repo="$3"
  local test_name="$4"
  
  # Create a temporary git repo with the specified remote
  local repo_dir="$TEST_DIR/test_repo_$$"
  mkdir -p "$repo_dir"
  cd "$repo_dir"
  git init -q
  git remote add origin "$url"
  
  # Call the production function
  local result=$(__copilot_get_github_info)
  
  # Parse result
  local actual_owner="${result%|*}"
  local actual_repo="${result#*|}"
  
  cd - > /dev/null
  rm -rf "$repo_dir"
  
  if [ "$actual_owner" = "$expected_owner" ] && [ "$actual_repo" = "$expected_repo" ]; then
    test_pass "$test_name"
    return 0
  else
    test_fail "$test_name (expected: $expected_owner|$expected_repo, got: $result)"
    return 1
  fi
}

# Test SSH URL format
test_start "Parse SSH URL (git@github.com:owner/repo.git)"
test_url_parsing "git@github.com:GordonBeeming/copilot_here.git" "GordonBeeming" "copilot_here" "SSH URL with .git"

test_start "Parse SSH URL without .git suffix"
test_url_parsing "git@github.com:GordonBeeming/copilot_here" "GordonBeeming" "copilot_here" "SSH URL without .git"

# Test HTTPS URL format
test_start "Parse HTTPS URL (https://github.com/owner/repo.git)"
test_url_parsing "https://github.com/GordonBeeming/copilot_here.git" "GordonBeeming" "copilot_here" "HTTPS URL with .git"

test_start "Parse HTTPS URL without .git suffix"
test_url_parsing "https://github.com/GordonBeeming/copilot_here" "GordonBeeming" "copilot_here" "HTTPS URL without .git"

# Test with different owner/repo names
test_start "Parse URL with hyphenated names"
test_url_parsing "git@github.com:my-org/my-awesome-repo.git" "my-org" "my-awesome-repo" "Hyphenated names"

test_start "Parse URL with underscores"
test_url_parsing "https://github.com/my_org/my_repo.git" "my_org" "my_repo" "Underscored names"

test_start "Parse URL with numbers"
test_url_parsing "git@github.com:user123/repo456.git" "user123" "repo456" "Names with numbers"

# Test placeholder replacement using production code
test_start "Placeholder replacement in network config"
config_file="$TEST_DIR/network.json"
cat > "$config_file" << 'EOF'
{
  "allowed_paths": ["/agents/{{GITHUB_OWNER}}/{{GITHUB_REPO}}"]
}
EOF

# Create a git repo context
repo_dir="$TEST_DIR/placeholder_test"
mkdir -p "$repo_dir"
cd "$repo_dir"
git init -q
git remote add origin "git@github.com:TestOwner/TestRepo.git"

# Call the production function to process the config
processed=$(__copilot_process_network_config "$config_file")

if [ -f "$processed" ]; then
  content=$(cat "$processed")
  rm -f "$processed"
  
  if echo "$content" | grep -q '"/agents/TestOwner/TestRepo"'; then
    test_pass "Placeholders replaced correctly"
  else
    test_fail "Placeholders not replaced (got: $content)"
  fi
else
  test_fail "Processed config file not created"
fi

cd - > /dev/null

# Test with no git repo (should return empty)
test_start "Handle non-git directory gracefully"
non_git_dir="$TEST_DIR/not_a_repo"
mkdir -p "$non_git_dir"
cd "$non_git_dir"

result=$(__copilot_get_github_info) || true
if [ -z "$result" ]; then
  test_pass "Returns empty for non-git directory"
else
  test_fail "Should return empty for non-git directory (got: $result)"
fi

cd - > /dev/null

# Test current repo (where we actually are)
test_start "Extract info from current repository"
cd "$SCRIPT_DIR"
result=$(__copilot_get_github_info)
if [ -n "$result" ]; then
  owner="${result%|*}"
  repo="${result#*|}"
  if [ -n "$owner" ] && [ -n "$repo" ]; then
    test_pass "Extracted owner=$owner, repo=$repo"
  else
    test_fail "Failed to parse result: $result"
  fi
else
  test_fail "No result from current repository"
fi

print_summary
