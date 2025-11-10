#!/bin/bash
# Main test runner for all integration tests
# Runs tests for Bash, Zsh, and PowerShell (if available)

set -e

# Color support
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$SCRIPT_DIR/integration"

TOTAL_SUITES=0
PASSED_SUITES=0
FAILED_SUITES=0

echo "======================================"
echo "Running All Integration Tests"
echo "======================================"
echo ""

# Test Bash
echo -e "${BLUE}Running Bash tests...${NC}"
TOTAL_SUITES=$((TOTAL_SUITES + 1))
if bash "$TEST_DIR/test_bash.sh"; then
  echo -e "${GREEN}✓ Bash tests passed${NC}"
  PASSED_SUITES=$((PASSED_SUITES + 1))
else
  echo -e "${RED}✗ Bash tests failed${NC}"
  FAILED_SUITES=$((FAILED_SUITES + 1))
fi
echo ""

# Test Zsh (if available)
if command -v zsh >/dev/null 2>&1; then
  echo -e "${BLUE}Running Zsh tests...${NC}"
  TOTAL_SUITES=$((TOTAL_SUITES + 1))
  if zsh "$TEST_DIR/test_zsh.sh"; then
    echo -e "${GREEN}✓ Zsh tests passed${NC}"
    PASSED_SUITES=$((PASSED_SUITES + 1))
  else
    echo -e "${RED}✗ Zsh tests failed${NC}"
    FAILED_SUITES=$((FAILED_SUITES + 1))
  fi
  echo ""
else
  echo -e "${YELLOW}⚠ Zsh not available, skipping Zsh tests${NC}"
  echo ""
fi

# Test PowerShell (if available)
if command -v pwsh >/dev/null 2>&1; then
  echo -e "${BLUE}Running PowerShell tests...${NC}"
  TOTAL_SUITES=$((TOTAL_SUITES + 1))
  if pwsh -File "$TEST_DIR/test_powershell.ps1"; then
    echo -e "${GREEN}✓ PowerShell tests passed${NC}"
    PASSED_SUITES=$((PASSED_SUITES + 1))
  else
    echo -e "${RED}✗ PowerShell tests failed${NC}"
    FAILED_SUITES=$((FAILED_SUITES + 1))
  fi
  echo ""
else
  echo -e "${YELLOW}⚠ PowerShell not available, skipping PowerShell tests${NC}"
  echo ""
fi

# Test Docker commands (Bash only - uses mocking)
echo -e "${BLUE}Running Docker command tests...${NC}"
TOTAL_SUITES=$((TOTAL_SUITES + 1))
if bash "$TEST_DIR/test_docker_commands.sh"; then
  echo -e "${GREEN}✓ Docker command tests passed${NC}"
  PASSED_SUITES=$((PASSED_SUITES + 1))
else
  echo -e "${RED}✗ Docker command tests failed${NC}"
  FAILED_SUITES=$((FAILED_SUITES + 1))
fi
echo ""

# Overall summary
echo "======================================"
echo "OVERALL TEST SUMMARY"
echo "======================================"
echo "Total Test Suites: $TOTAL_SUITES"
echo -e "${GREEN}Passed: $PASSED_SUITES${NC}"
if [ $FAILED_SUITES -gt 0 ]; then
  echo -e "${RED}Failed: $FAILED_SUITES${NC}"
else
  echo "Failed: $FAILED_SUITES"
fi
echo "======================================"

if [ $FAILED_SUITES -gt 0 ]; then
  exit 1
fi

echo -e "${GREEN}All tests passed!${NC}"
