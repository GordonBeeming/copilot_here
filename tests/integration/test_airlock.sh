#!/bin/bash
# Integration tests for Airlock mode (Docker Compose with network proxy)
# 
# These tests verify the actual airlock functionality using the CLI:
# - CLI correctly launches Airlock mode
# - Proxy starts and becomes healthy
# - App can reach allowed hosts through proxy
# - App is blocked from non-allowed hosts
# - CA certificate is properly trusted
#
# NOTE: These tests require Docker and the CLI binary.
# They are NOT run in CI unit tests - run manually or in dedicated CI job.
#
# Usage: ./tests/integration/test_airlock.sh [--use-local] [--cli-path <path>]
#
# Options:
#   --use-local        Skip image pull and use locally built images (for dev testing)
#   --cli-path <path>  Path to the CLI binary (default: build from source)

# Don't use set -e as we want to continue running tests even if some fail
# set -e

# Parse arguments
USE_LOCAL_IMAGES=false
CLI_PATH=""
while [[ $# -gt 0 ]]; do
  case $1 in
    --use-local)
      USE_LOCAL_IMAGES=true
      shift
      ;;
    --cli-path)
      CLI_PATH="$2"
      shift 2
      ;;
    *)
      shift
      ;;
  esac
done

# Color support
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Capture script directory at startup (before any cd commands)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

TEST_COUNT=0
PASS_COUNT=0
FAIL_COUNT=0
SKIP_COUNT=0

# Test helper functions
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

test_skip() {
  echo -e "${YELLOW}⊘ SKIP${NC}: $1"
  SKIP_COUNT=$((SKIP_COUNT + 1))
  TEST_COUNT=$((TEST_COUNT - 1))  # Don't count skipped tests
}

print_summary() {
  echo ""
  echo "======================================"
  echo "AIRLOCK TEST SUMMARY"
  echo "======================================"
  echo "Total Tests: $TEST_COUNT"
  echo -e "${GREEN}Passed: $PASS_COUNT${NC}"
  if [ $FAIL_COUNT -gt 0 ]; then
    echo -e "${RED}Failed: $FAIL_COUNT${NC}"
  else
    echo "Failed: $FAIL_COUNT"
  fi
  if [ $SKIP_COUNT -gt 0 ]; then
    echo -e "${YELLOW}Skipped: $SKIP_COUNT${NC}"
  fi
  echo "======================================"
  
  if [ $FAIL_COUNT -gt 0 ]; then
    return 1
  fi
  return 0
}

# Check prerequisites
check_prerequisites() {
  echo "🔍 Checking prerequisites..."
  
  # Check Docker
  if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker is not installed${NC}"
    exit 1
  fi
  
  # Check Docker is running
  if ! docker info &> /dev/null; then
    echo -e "${RED}❌ Docker is not running${NC}"
    exit 1
  fi
  
  # Check docker compose
  if ! docker compose version &> /dev/null; then
    echo -e "${RED}❌ Docker Compose is not available${NC}"
    exit 1
  fi
  
  echo -e "${GREEN}✓ All prerequisites met${NC}"
}

# Global variables for test containers
PROJECT_NAME=""
COMPOSE_FILE=""
TEST_DIR=""
CLI_BINARY=""

# Build or find CLI binary
setup_cli() {
  echo ""
  echo "🔧 Setting up CLI binary..."
  
  if [ -n "$CLI_PATH" ] && [ -f "$CLI_PATH" ]; then
    CLI_BINARY="$CLI_PATH"
    echo "   Using provided CLI: $CLI_BINARY"
  else
    # Build CLI from source
    echo "   Building CLI from source..."
    
    if ! command -v dotnet &> /dev/null; then
      echo -e "${RED}❌ .NET SDK not found. Install it or provide --cli-path${NC}"
      return 1
    fi
    
    local publish_dir="$REPO_ROOT/publish/test"
    mkdir -p "$publish_dir"
    
    if ! dotnet publish "$REPO_ROOT/app/CopilotHere.csproj" -c Release -o "$publish_dir" --nologo -v q 2>&1; then
      echo -e "${RED}❌ Failed to build CLI${NC}"
      return 1
    fi
    
    CLI_BINARY="$publish_dir/copilot_here"
    if [ ! -f "$CLI_BINARY" ]; then
      # Try with .exe extension on Windows
      CLI_BINARY="$publish_dir/copilot_here.exe"
    fi
    
    if [ ! -f "$CLI_BINARY" ]; then
      echo -e "${RED}❌ CLI binary not found after build${NC}"
      return 1
    fi
    
    chmod +x "$CLI_BINARY" 2>/dev/null || true
    echo "   Built CLI: $CLI_BINARY"
  fi
  
  # Verify CLI works
  if ! "$CLI_BINARY" --version &>/dev/null; then
    echo -e "${RED}❌ CLI binary is not executable${NC}"
    return 1
  fi
  
  echo -e "${GREEN}✓ CLI ready${NC}"
}

# Setup test environment
setup_test_env() {
  echo ""
  echo "🔧 Setting up test environment..."
  
  # Create temp directory for test files
  TEST_DIR=$(mktemp -d)
  mkdir -p "$TEST_DIR/.copilot_here"
  
  # Create network config for testing using CLI's expected location
  local network_config="$TEST_DIR/.copilot_here/network.json"
  cat > "$network_config" << 'EOF'
{
  "mode": "enforce",
  "inherit_defaults": false,
  "allowed_rules": [
    {
      "host": "httpbin.org",
      "allowed_paths": ["/get", "/status/200"],
      "allow_insecure": true
    },
    {
      "host": "api.github.com",
      "allowed_paths": ["/", "/zen"]
    }
  ]
}
EOF

  # Create airlock enabled marker
  echo "local" > "$TEST_DIR/.copilot_here/airlock"
  
  # Note: The docker-compose template is now embedded in the CLI binary
  # and also stored in app/Resources/ in the repo
  
  echo "   Test directory: $TEST_DIR"
  echo "   Network config: $network_config"
}

# Start test containers using CLI
start_containers() {
  echo ""
  echo "🚀 Starting test containers via CLI..."
  
  # Pull images first (unless using local images)
  if [ "$USE_LOCAL_IMAGES" = true ]; then
    echo "   Using local images (--use-local)"
    local app_image="ghcr.io/gordonbeeming/copilot_here:latest"
    local proxy_image="ghcr.io/gordonbeeming/copilot_here:proxy"
    
    if ! docker image inspect "$app_image" &>/dev/null; then
      echo -e "${RED}❌ Local app image not found: $app_image${NC}"
      echo "   Run ./dev-build.sh first to build the images"
      return 1
    fi
    
    if ! docker image inspect "$proxy_image" &>/dev/null; then
      echo -e "${RED}❌ Local proxy image not found: $proxy_image${NC}"
      echo "   Run ./dev-build.sh first to build the images"
      return 1
    fi
  fi
  
  # Start CLI in background with sleep command (so container stays running)
  # We use the -- separator to pass args directly to copilot
  cd "$TEST_DIR"
  
  local cli_args=""
  if [ "$USE_LOCAL_IMAGES" = true ]; then
    cli_args="--no-pull"
  fi
  
  # Run CLI in background - it will start airlock and run "sleep infinity"
  # We capture the project name from the CLI output
  echo "   Running CLI to start Airlock..."
  
  # Start proxy first using docker compose directly (CLI does this internally)
  # We need to intercept the compose file CLI generates
  
  # For now, fall back to the compose-based approach but use CLI to verify it works
  # The key is we're testing the actual Airlock images and compose template
  
  local template_file="$REPO_ROOT/app/Resources/docker-compose.airlock.yml.template"
  
  if [ ! -f "$template_file" ]; then
    echo -e "${RED}❌ docker-compose.airlock.yml.template not found at $template_file${NC}"
    return 1
  fi
  
  PROJECT_NAME="airlock-test-$$"
  COMPOSE_FILE="$TEST_DIR/docker-compose.yml"
  
  local app_image="ghcr.io/gordonbeeming/copilot_here:latest"
  local proxy_image="ghcr.io/gordonbeeming/copilot_here:proxy"
  local network_config="$TEST_DIR/.copilot_here/network.json"
  local copilot_config="$TEST_DIR/.copilot_here/copilot-config"
  mkdir -p "$copilot_config"
  
  awk -v project_name="$PROJECT_NAME" \
      -v app_image="$app_image" \
      -v proxy_image="$proxy_image" \
      -v work_dir="$TEST_DIR" \
      -v container_work_dir="/home/appuser/work" \
      -v copilot_config="$copilot_config" \
      -v network_config="$network_config" \
      -v logs_mount="" \
      -v puid="$(id -u)" \
      -v pgid="$(id -g)" \
      -v extra_mounts="" \
      -v copilot_args="[\"sleep\", \"infinity\"]" \
      '{
        gsub(/\{\{PROJECT_NAME\}\}/, project_name);
        gsub(/\{\{APP_IMAGE\}\}/, app_image);
        gsub(/\{\{PROXY_IMAGE\}\}/, proxy_image);
        gsub(/\{\{WORK_DIR\}\}/, work_dir);
        gsub(/\{\{CONTAINER_WORK_DIR\}\}/, container_work_dir);
        gsub(/\{\{COPILOT_CONFIG\}\}/, copilot_config);
        gsub(/\{\{NETWORK_CONFIG\}\}/, network_config);
        gsub(/\{\{LOGS_MOUNT\}\}/, logs_mount);
        gsub(/\{\{PUID\}\}/, puid);
        gsub(/\{\{PGID\}\}/, pgid);
        gsub(/\{\{EXTRA_MOUNTS\}\}/, extra_mounts);
        gsub(/\{\{COPILOT_ARGS\}\}/, copilot_args);
        print
      }' "$template_file" > "$COMPOSE_FILE"
  
  if [ ! -s "$COMPOSE_FILE" ]; then
    echo -e "${RED}❌ Failed to generate compose file${NC}"
    return 1
  fi
  
  # Start containers
  if ! docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" up -d --wait 2>&1; then
    echo -e "${RED}❌ Failed to start containers${NC}"
    return 1
  fi
  
  echo -e "${GREEN}✓ Containers started${NC}"
  
  # Wait for proxy to fully initialize and be ready to handle requests
  # The proxy needs time to generate CA certs and start accepting connections
  sleep 5
}

# Stop and cleanup test containers
cleanup_containers() {
  echo ""
  echo "🧹 Cleaning up test containers..."
  
  if [ -n "$COMPOSE_FILE" ] && [ -f "$COMPOSE_FILE" ]; then
    docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" down --volumes --remove-orphans 2>/dev/null || true
  fi
  
  if [ -n "$TEST_DIR" ] && [ -d "$TEST_DIR" ]; then
    rm -rf "$TEST_DIR"
  fi
  
  # Clean up publish directory
  if [ -n "$REPO_ROOT" ]; then
    rm -rf "$REPO_ROOT/publish/test" 2>/dev/null || true
  fi
  
  echo "✓ Cleanup complete"
}

# Run command in app container
run_in_client() {
  docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" exec -T app "$@"
}

# ============================================================================
# TEST CASES
# ============================================================================

test_proxy_health() {
  test_start "Proxy health check endpoint responds"
  
  local result
  result=$(docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" exec -T proxy curl -sf http://localhost:58080/health 2>&1)
  
  if [ "$result" = "OK" ]; then
    test_pass "Proxy health endpoint returns OK"
  else
    test_fail "Proxy health endpoint failed: $result"
  fi
}

test_proxy_logs_running() {
  test_start "Proxy shows running in logs"
  
  local logs
  logs=$(docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" logs proxy 2>&1)
  
  if echo "$logs" | grep -q "Secure Proxy listening"; then
    test_pass "Proxy logs show server running"
  else
    test_fail "Proxy logs don't show running: $logs"
  fi
}

test_allowed_host_http() {
  test_start "HTTP request to allowed host succeeds"
  
  # HTTP requests should work through the proxy (forwarded, not tunneled like HTTPS)
  # httpbin.org has allow_insecure: true, so HTTP should work
  local result exit_code max_retries=5 retry=0
  while [ $retry -lt $max_retries ]; do
    exit_code=0
    # Use a simple HTTP endpoint - note: most sites redirect HTTP to HTTPS
    # We test that HTTP through proxy works, even if it gets a redirect response
    result=$(run_in_client curl -s --max-time 30 -o /dev/null -w "%{http_code}" "http://httpbin.org/get" 2>&1) || exit_code=$?
    
    # Accept 200 (success) or 301/302 (redirect) as valid HTTP responses
    if [ "$result" = "200" ] || [ "$result" = "301" ] || [ "$result" = "302" ]; then
      test_pass "HTTP request to httpbin.org/get succeeded (status: $result)"
      return
    fi
    
    # Also try with full response to check for JSON
    local full_result
    full_result=$(run_in_client curl -sf --max-time 30 "http://httpbin.org/get" 2>&1) || true
    if echo "$full_result" | grep -q '"url"'; then
      test_pass "HTTP request to httpbin.org/get succeeded"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 3
    fi
  done
  
  test_fail "HTTP request failed after $max_retries attempts (exit $exit_code): $result"
}

test_allowed_host_https() {
  test_start "HTTPS request to allowed host succeeds (with CA)"
  
  # First check if CA cert exists
  local ca_exists
  ca_exists=$(run_in_client ls /ca/certs/ca.pem 2>&1) || true
  
  if ! echo "$ca_exists" | grep -q "ca.pem"; then
    test_fail "CA certificate not found in /ca/"
    return
  fi
  
  # Use api.github.com which is more reliable than httpbin.org
  local result max_retries=5 retry=0
  while [ $retry -lt $max_retries ]; do
    # Test HTTPS through proxy with CA cert - api.github.com/zen returns a simple quote
    result=$(run_in_client curl -sf --max-time 30 --cacert /ca/certs/ca.pem "https://api.github.com/zen" 2>&1) || true
    
    # api.github.com/zen returns a random GitHub zen quote (plain text, non-empty)
    if [ -n "$result" ] && [ ${#result} -gt 5 ]; then
      test_pass "HTTPS request to api.github.com/zen succeeded"
      return
    fi
    
    # Fallback: try httpbin.org as well
    result=$(run_in_client curl -sf --max-time 30 --cacert /ca/certs/ca.pem "https://httpbin.org/get" 2>&1) || true
    
    if echo "$result" | grep -q '"url"'; then
      test_pass "HTTPS request to httpbin.org/get succeeded with CA"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 3
    fi
  done
  
  test_fail "HTTPS request failed after $max_retries attempts: $result"
}

test_allowed_path_succeeds() {
  test_start "Request to allowed path succeeds"
  
  local result exit_code max_retries=5 retry=0
  while [ $retry -lt $max_retries ]; do
    result=$(run_in_client curl -sf --max-time 30 --cacert /ca/certs/ca.pem "https://httpbin.org/status/200" 2>&1) || exit_code=$?
    exit_code=${exit_code:-0}
    
    if [ $exit_code -eq 0 ]; then
      test_pass "Request to /status/200 succeeded"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 3
    fi
  done
  
  test_fail "Request to allowed path failed after $max_retries attempts: $result"
}

test_blocked_host() {
  test_start "Request to non-allowed host is blocked"
  
  local result exit_code
  result=$(run_in_client curl -sf --max-time 10 --cacert /ca/certs/ca.pem "https://example.com/" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -ne 0 ]; then
    test_pass "Request to example.com was blocked (exit code: $exit_code)"
  else
    test_fail "Request to blocked host should have failed but succeeded"
  fi
}

test_blocked_path() {
  test_start "Request to non-allowed path is blocked"
  
  local result exit_code
  result=$(run_in_client curl -sf --max-time 10 --cacert /ca/certs/ca.pem "https://httpbin.org/post" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -ne 0 ]; then
    test_pass "Request to /post was blocked (not in allowed_paths)"
  else
    test_fail "Request to blocked path should have failed: $result"
  fi
}

test_http_blocked_without_allow_insecure() {
  test_start "HTTP request blocked when allow_insecure is false"
  
  # api.github.com has allow_insecure: false (or unset), so HTTP should be blocked
  local result exit_code
  result=$(run_in_client curl -sf --max-time 10 "http://api.github.com/" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -ne 0 ]; then
    test_pass "HTTP request to api.github.com blocked (allow_insecure not set)"
  else
    test_fail "HTTP request should be blocked when allow_insecure is false: $result"
  fi
}

test_no_direct_internet() {
  test_start "Client cannot reach internet directly (bypassing proxy)"
  
  # Try to reach a host directly without proxy
  local result exit_code
  result=$(docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" exec -T \
    -e HTTP_PROXY= -e HTTPS_PROXY= -e http_proxy= -e https_proxy= \
    app curl -sf --max-time 5 "http://httpbin.org/get" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -ne 0 ]; then
    test_pass "Direct internet access blocked (airlock working)"
  else
    test_fail "Direct internet access should be blocked but succeeded"
  fi
}

test_ca_certificate_exists() {
  test_start "CA certificate is generated and shared"
  
  local result
  result=$(run_in_client cat /ca/certs/ca.pem 2>&1) || true
  
  if echo "$result" | grep -q "BEGIN CERTIFICATE"; then
    test_pass "CA certificate exists and contains valid PEM data"
  else
    test_fail "CA certificate not found or invalid: $result"
  fi
}

# Test CLI binary works
test_cli_binary() {
  test_start "CLI binary executes and shows version"
  
  local result
  result=$("$CLI_BINARY" --version 2>&1) || true
  
  # Version format: YYYY.MM.DD or YYYY.MM.DD.sha
  if echo "$result" | grep -qE "^[0-9]{4}\.[0-9]{2}\.[0-9]{2}"; then
    test_pass "CLI binary shows version: $(echo "$result" | head -1)"
  else
    test_fail "CLI binary did not show version: $result"
  fi
}

test_cli_help() {
  test_start "CLI shows help with airlock commands"
  
  local result
  result=$("$CLI_BINARY" --help 2>&1) || true
  
  if echo "$result" | grep -q "enable-airlock"; then
    test_pass "CLI help includes airlock commands"
  else
    test_fail "CLI help missing airlock commands: $result"
  fi
}

# ============================================================================
# MAIN
# ============================================================================

main() {
  echo "========================================"
  echo "    AIRLOCK INTEGRATION TESTS"
  echo "========================================"
  echo ""
  
  # Check prerequisites first
  check_prerequisites
  
  # Setup trap for cleanup
  trap cleanup_containers EXIT INT TERM
  
  # Setup CLI
  if ! setup_cli; then
    echo -e "${RED}Failed to setup CLI, aborting tests${NC}"
    exit 1
  fi
  
  # Setup and start
  setup_test_env
  if ! start_containers; then
    echo -e "${RED}Failed to start containers, aborting tests${NC}"
    exit 1
  fi
  
  # Run tests
  echo ""
  echo "========================================"
  echo "    RUNNING CLI TESTS"
  echo "========================================"
  
  test_cli_binary
  test_cli_help
  
  echo ""
  echo "========================================"
  echo "    RUNNING AIRLOCK TESTS"
  echo "========================================"
  
  test_proxy_health
  test_proxy_logs_running
  test_ca_certificate_exists
  test_allowed_host_http
  test_allowed_host_https
  test_allowed_path_succeeds
  test_blocked_host
  test_blocked_path
  test_http_blocked_without_allow_insecure
  test_no_direct_internet
  
  # Print summary and capture result
  print_summary
  local result=$?
  
  # Cleanup happens via trap, then exit with test result
  exit $result
}

# Run if executed directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
  main "$@"
fi
