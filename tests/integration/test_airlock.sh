#!/bin/bash
# Integration tests for Airlock mode (Docker Compose with network proxy)
# 
# These tests verify the actual airlock functionality:
# - Proxy starts and becomes healthy
# - App can reach allowed hosts through proxy
# - App is blocked from non-allowed hosts
# - CA certificate is properly trusted
#
# NOTE: These tests require Docker and actually run containers.
# They are NOT run in CI unit tests - run manually or in dedicated CI job.
#
# Usage: ./tests/integration/test_airlock.sh [--use-local]
#
# Options:
#   --use-local    Skip image pull and use locally built images (for dev testing)

# Don't use set -e as we want to continue running tests even if some fail
# set -e

# Parse arguments
USE_LOCAL_IMAGES=false
for arg in "$@"; do
  case $arg in
    --use-local)
      USE_LOCAL_IMAGES=true
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
PROJECT_NAME="airlock-test-$$"
COMPOSE_FILE=""
NETWORK_CONFIG=""

# Setup test environment
setup_test_env() {
  echo ""
  echo "🔧 Setting up test environment..."
  
  # Create temp directory for test files
  TEST_DIR=$(mktemp -d)
  
  # Create network config for testing
  NETWORK_CONFIG="$TEST_DIR/network.json"
  cat > "$NETWORK_CONFIG" << 'EOF'
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
      "allowed_paths": ["/"]
    }
  ]
}
EOF

  # Find the template file - check repo first, then installed location
  local script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  local repo_root="$(cd "$script_dir/../.." && pwd)"
  local template_file=""
  
  if [ -f "$repo_root/docker-compose.airlock.yml.template" ]; then
    template_file="$repo_root/docker-compose.airlock.yml.template"
  elif [ -f "$HOME/.config/copilot_here/docker-compose.airlock.yml.template" ]; then
    template_file="$HOME/.config/copilot_here/docker-compose.airlock.yml.template"
  else
    echo -e "${RED}❌ docker-compose.airlock.yml.template not found${NC}"
    echo "   Checked: $repo_root/docker-compose.airlock.yml.template"
    echo "   Checked: $HOME/.config/copilot_here/docker-compose.airlock.yml.template"
    return 1
  fi
  
  echo "   Using template: $template_file"
  
  # Create compose file using the ACTUAL template (same logic as production code)
  COMPOSE_FILE="$TEST_DIR/docker-compose.yml"
  
  # Use awk to substitute placeholders - same method as copilot_here.sh
  local app_image="ghcr.io/gordonbeeming/copilot_here:latest"
  local proxy_image="ghcr.io/gordonbeeming/copilot_here:proxy"
  local work_dir="$TEST_DIR"
  local container_work_dir="/home/appuser/work"
  local copilot_config="$TEST_DIR/copilot-config"
  mkdir -p "$copilot_config"
  
  awk -v project_name="$PROJECT_NAME" \
      -v app_image="$app_image" \
      -v proxy_image="$proxy_image" \
      -v work_dir="$work_dir" \
      -v container_work_dir="$container_work_dir" \
      -v copilot_config="$copilot_config" \
      -v network_config="$NETWORK_CONFIG" \
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
  
  # Verify the compose file was created and is valid YAML
  if [ ! -s "$COMPOSE_FILE" ]; then
    echo -e "${RED}❌ Failed to generate compose file${NC}"
    return 1
  fi
  
  # Quick validation - check it has the expected structure
  if ! grep -q "services:" "$COMPOSE_FILE"; then
    echo -e "${RED}❌ Generated compose file is invalid (missing services:)${NC}"
    echo "Generated file contents:"
    cat "$COMPOSE_FILE"
    return 1
  fi

  echo "   Project name: $PROJECT_NAME"
  echo "   Compose file: $COMPOSE_FILE"
  echo "   Network config: $NETWORK_CONFIG"
}

# Start test containers
start_containers() {
  echo ""
  echo "🚀 Starting test containers..."
  
  # Pull images first (unless using local images)
  if [ "$USE_LOCAL_IMAGES" = true ]; then
    echo "   Using local images (--use-local)"
    # Verify local images exist
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
  else
    docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" pull --quiet 2>/dev/null || true
  fi
  
  # Start containers
  if ! docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" up -d --wait 2>&1; then
    echo -e "${RED}❌ Failed to start containers${NC}"
    return 1
  fi
  
  echo -e "${GREEN}✓ Containers started${NC}"
  
  # Wait a bit for everything to stabilize
  sleep 2
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
  local result exit_code max_retries=3 retry=0
  while [ $retry -lt $max_retries ]; do
    result=$(run_in_client curl -sf --max-time 15 "http://httpbin.org/get" 2>&1) || exit_code=$?
    exit_code=${exit_code:-0}
    
    if [ $exit_code -eq 0 ] && echo "$result" | grep -q '"url"'; then
      test_pass "HTTP request to httpbin.org/get succeeded"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 2
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
  
  local result max_retries=3 retry=0
  while [ $retry -lt $max_retries ]; do
    result=$(run_in_client curl -sf --max-time 15 --cacert /ca/certs/ca.pem "https://httpbin.org/get" 2>&1) || true
    
    if echo "$result" | grep -q '"url"'; then
      test_pass "HTTPS request to httpbin.org/get succeeded with CA"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 2
    fi
  done
  
  test_fail "HTTPS request failed after $max_retries attempts: $result"
}

test_allowed_path_succeeds() {
  test_start "Request to allowed path succeeds"
  
  local result exit_code max_retries=3 retry=0
  while [ $retry -lt $max_retries ]; do
    result=$(run_in_client curl -sf --max-time 15 --cacert /ca/certs/ca.pem "https://httpbin.org/status/200" 2>&1) || exit_code=$?
    exit_code=${exit_code:-0}
    
    if [ $exit_code -eq 0 ]; then
      test_pass "Request to /status/200 succeeded"
      return
    fi
    
    retry=$((retry + 1))
    if [ $retry -lt $max_retries ]; then
      echo "   Retry $retry/$max_retries after transient failure..."
      sleep 2
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
  
  # Setup and start
  setup_test_env
  if ! start_containers; then
    echo -e "${RED}Failed to start containers, aborting tests${NC}"
    exit 1
  fi
  
  # Run tests
  echo ""
  echo "========================================"
  echo "    RUNNING TESTS"
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
