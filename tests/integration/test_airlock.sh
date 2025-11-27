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
# Usage: ./tests/integration/test_airlock.sh

# Don't use set -e as we want to continue running tests even if some fail
# set -e

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

  # Create compose file for testing
  COMPOSE_FILE="$TEST_DIR/docker-compose.yml"
  cat > "$COMPOSE_FILE" << EOF
networks:
  airlock:
    internal: true
  bridge:

volumes:
  proxy-ca:

services:
  proxy:
    image: ghcr.io/gordonbeeming/copilot_here:proxy
    container_name: "${PROJECT_NAME}-proxy"
    networks:
      - airlock
      - bridge
    volumes:
      - ${NETWORK_CONFIG}:/config/rules.json:ro
      - proxy-ca:/ca
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:58080/health"]
      interval: 2s
      timeout: 2s
      retries: 15
      start_period: 5s

  test-client:
    image: curlimages/curl:latest
    container_name: "${PROJECT_NAME}-client"
    networks:
      - airlock
    environment:
      - HTTP_PROXY=http://proxy:58080
      - HTTPS_PROXY=http://proxy:58080
      - http_proxy=http://proxy:58080
      - https_proxy=http://proxy:58080
    volumes:
      - proxy-ca:/ca:ro
    depends_on:
      proxy:
        condition: service_healthy
    entrypoint: ["sleep", "infinity"]
EOF

  echo "   Project name: $PROJECT_NAME"
  echo "   Compose file: $COMPOSE_FILE"
  echo "   Network config: $NETWORK_CONFIG"
}

# Start test containers
start_containers() {
  echo ""
  echo "🚀 Starting test containers..."
  
  # Pull images first
  docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" pull --quiet 2>/dev/null || true
  
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

# Run command in test client container
run_in_client() {
  docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" exec -T test-client "$@"
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
  local result exit_code
  result=$(run_in_client curl -sf --max-time 10 "http://httpbin.org/get" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -eq 0 ] && echo "$result" | grep -q '"url"'; then
    test_pass "HTTP request to httpbin.org/get succeeded"
  else
    test_fail "HTTP request failed (exit $exit_code): $result"
  fi
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
  
  local result
  result=$(run_in_client curl -sf --max-time 10 --cacert /ca/certs/ca.pem "https://httpbin.org/get" 2>&1) || true
  
  if echo "$result" | grep -q '"url"'; then
    test_pass "HTTPS request to httpbin.org/get succeeded with CA"
  else
    test_fail "HTTPS request failed: $result"
  fi
}

test_allowed_path_succeeds() {
  test_start "Request to allowed path succeeds"
  
  local result exit_code
  result=$(run_in_client curl -sf --max-time 10 --cacert /ca/certs/ca.pem "https://httpbin.org/status/200" 2>&1) || exit_code=$?
  exit_code=${exit_code:-0}
  
  if [ $exit_code -eq 0 ]; then
    test_pass "Request to /status/200 succeeded"
  else
    test_fail "Request to allowed path failed: $result"
  fi
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
    test-client curl -sf --max-time 5 "http://httpbin.org/get" 2>&1) || exit_code=$?
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
