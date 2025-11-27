#!/bin/bash
# Integration tests for Window Title setting

set -e

# Setup test directory to isolate from production configs
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEST_DIR=$(mktemp -d)
TEST_WORK_DIR="$TEST_DIR/work"
TEST_HOME="$TEST_DIR/home"
mkdir -p "$TEST_WORK_DIR"
mkdir -p "$TEST_HOME/.config/copilot_here"

cleanup() {
  rm -rf "$TEST_DIR"
}
trap cleanup EXIT

# Override HOME to use test directory
export HOME="$TEST_HOME"

# Source the script to test
source "$SCRIPT_DIR/copilot_here.sh"

# Change to test work directory
cd "$TEST_WORK_DIR"

# Mock docker to avoid actual execution
docker() {
  echo "MOCK: docker $*"
}

# Mock gh to avoid auth checks
gh() {
  if [ "$1" == "auth" ] && [ "$2" == "status" ]; then
    echo "Token scopes: 'copilot', 'read:packages'"
  elif [ "$1" == "auth" ] && [ "$2" == "token" ]; then
    echo "mock_token"
  fi
}

# Mock tput/stty if needed (not used for title)

# Test 1: Standard Mode Title
echo "TEST: Standard Mode Title"
output=$(COPILOT_HERE_TEST_MODE=true copilot_here --no-pull --no-cleanup -p "test" 2>&1)

current_dir_name=$(basename "$PWD")
expected_title_seq="\033]0;🤖 ${current_dir_name}\007"

# We need to check if the output contains the escape sequence.
# Note: command substitution strips trailing newlines, but escape sequences should be preserved.
# However, printf output might be mixed with other output.
# We can use grep or just check string containment.

# Since printf interprets \033, the actual output contains the ESC character.
# We can't easily grep for the escape code in the variable unless we construct it carefully.

# Let's try to capture the specific printf call by mocking printf?
# But printf is a builtin.

# Alternative: Use script/screen to capture output? Too complex.
# Let's mock printf function.
printf_calls=""
printf() {
  local fmt="$1"
  shift
  # Store calls to a variable
  # We need to handle the case where printf is used for other things (like spinner)
  if [[ "$fmt" == *"\033]0;"* ]]; then
    # This is likely our title call
    # Format the string as it would be printed
    local msg
    # Simple formatting for %s
    if [[ "$fmt" == *"%s"* ]]; then
       msg="${fmt/\%s/$1}"
    else
       msg="$fmt"
    fi
    echo "MOCK_PRINTF_TITLE: $msg"
  else
    # Call original printf for other things (like spinner)
    builtin printf "$fmt" "$@"
  fi
}

# Run test again with mocked printf
output=$(COPILOT_HERE_TEST_MODE=true copilot_here --no-pull --no-cleanup -p "test" 2>&1)

if echo "$output" | grep -q "MOCK_PRINTF_TITLE: .*🤖 ${current_dir_name}"; then
  echo "✅ PASS: Standard mode title set correctly"
else
  echo "❌ FAIL: Standard mode title not found"
  echo "Output was:"
  echo "$output"
  exit 1
fi

# Test 2: YOLO Mode Title
echo "TEST: YOLO Mode Title"
output=$(COPILOT_HERE_TEST_MODE=true copilot_yolo --no-pull --no-cleanup -p "test" 2>&1)

if echo "$output" | grep -q "MOCK_PRINTF_TITLE: .*🤖⚡️.* ${current_dir_name}"; then
  echo "✅ PASS: YOLO mode title set correctly"
else
  echo "❌ FAIL: YOLO mode title not found"
  echo "Output was:"
  echo "$output"
  exit 1
fi

echo "All tests passed!"
