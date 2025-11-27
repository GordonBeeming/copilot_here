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
# Usage: pwsh tests/integration/test_airlock.ps1

$ErrorActionPreference = "Continue"

# Test counters
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0
$script:SkipCount = 0

# Global variables for test containers
$script:ProjectName = "airlock-test-$PID"
$script:ComposeFile = ""
$script:NetworkConfig = ""
$script:TestDir = ""

function Write-TestStart {
    param([string]$Message)
    Write-Host ""
    Write-Host "TEST: $Message" -ForegroundColor Blue
    $script:TestCount++
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "✓ PASS: $Message" -ForegroundColor Green
    $script:PassCount++
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "✗ FAIL: $Message" -ForegroundColor Red
    $script:FailCount++
}

function Write-TestSkip {
    param([string]$Message)
    Write-Host "⊘ SKIP: $Message" -ForegroundColor Yellow
    $script:SkipCount++
    $script:TestCount--  # Don't count skipped tests
}

function Write-TestSummary {
    Write-Host ""
    Write-Host "======================================"
    Write-Host "AIRLOCK TEST SUMMARY"
    Write-Host "======================================"
    Write-Host "Total Tests: $script:TestCount"
    Write-Host "Passed: $script:PassCount" -ForegroundColor Green
    if ($script:FailCount -gt 0) {
        Write-Host "Failed: $script:FailCount" -ForegroundColor Red
    } else {
        Write-Host "Failed: $script:FailCount"
    }
    if ($script:SkipCount -gt 0) {
        Write-Host "Skipped: $script:SkipCount" -ForegroundColor Yellow
    }
    Write-Host "======================================"
    
    return $script:FailCount -eq 0
}

function Test-Prerequisites {
    Write-Host "🔍 Checking prerequisites..."
    
    # Check Docker
    try {
        $null = docker --version 2>$null
    } catch {
        Write-Host "❌ Docker is not installed" -ForegroundColor Red
        exit 1
    }
    
    # Check Docker is running
    try {
        $null = docker info 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Docker not running" }
    } catch {
        Write-Host "❌ Docker is not running" -ForegroundColor Red
        exit 1
    }
    
    # Check docker compose
    try {
        $null = docker compose version 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Docker Compose not available" }
    } catch {
        Write-Host "❌ Docker Compose is not available" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ All prerequisites met" -ForegroundColor Green
}

function Initialize-TestEnvironment {
    Write-Host ""
    Write-Host "🔧 Setting up test environment..."
    
    # Create temp directory for test files
    $script:TestDir = Join-Path ([System.IO.Path]::GetTempPath()) "airlock-test-$PID"
    New-Item -ItemType Directory -Path $script:TestDir -Force | Out-Null
    
    # Create network config for testing
    $script:NetworkConfig = Join-Path $script:TestDir "network.json"
    $networkConfigContent = @'
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
'@
    $networkConfigContent | Set-Content -Path $script:NetworkConfig -Encoding UTF8
    
    # Convert Windows path to Unix path for Docker
    $networkConfigUnix = $script:NetworkConfig -replace '\\', '/' -replace '^([A-Za-z]):', '/$1'
    $networkConfigUnix = $networkConfigUnix.ToLower()
    
    # Create compose file for testing
    $script:ComposeFile = Join-Path $script:TestDir "docker-compose.yml"
    $composeContent = @"
networks:
  airlock:
    internal: true
  bridge:

volumes:
  proxy-ca:

services:
  proxy:
    image: ghcr.io/gordonbeeming/copilot_here:proxy
    container_name: "$script:ProjectName-proxy"
    networks:
      - airlock
      - bridge
    volumes:
      - ${networkConfigUnix}:/config/rules.json:ro
      - proxy-ca:/ca
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:58080/health"]
      interval: 2s
      timeout: 2s
      retries: 15
      start_period: 5s

  test-client:
    image: curlimages/curl:latest
    container_name: "$script:ProjectName-client"
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
"@
    $composeContent | Set-Content -Path $script:ComposeFile -Encoding UTF8
    
    Write-Host "   Project name: $script:ProjectName"
    Write-Host "   Compose file: $script:ComposeFile"
    Write-Host "   Network config: $script:NetworkConfig"
}

function Start-TestContainers {
    Write-Host ""
    Write-Host "🚀 Starting test containers..."
    
    # Pull images first (quiet)
    docker compose -f $script:ComposeFile -p $script:ProjectName pull --quiet 2>$null
    
    # Start containers
    $output = docker compose -f $script:ComposeFile -p $script:ProjectName up -d --wait 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to start containers" -ForegroundColor Red
        Write-Host $output
        return $false
    }
    
    Write-Host "✓ Containers started" -ForegroundColor Green
    
    # Wait a bit for everything to stabilize
    Start-Sleep -Seconds 2
    return $true
}

function Stop-TestContainers {
    Write-Host ""
    Write-Host "🧹 Cleaning up test containers..."
    
    if ($script:ComposeFile -and (Test-Path $script:ComposeFile)) {
        docker compose -f $script:ComposeFile -p $script:ProjectName down --volumes --remove-orphans 2>$null
    }
    
    if ($script:TestDir -and (Test-Path $script:TestDir)) {
        Remove-Item -Path $script:TestDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    
    Write-Host "✓ Cleanup complete"
}

function Invoke-InClient {
    param([string[]]$Command)
    $result = docker compose -f $script:ComposeFile -p $script:ProjectName exec -T test-client @Command 2>&1
    return $result
}

# ============================================================================
# TEST CASES
# ============================================================================

function Test-ProxyHealth {
    Write-TestStart "Proxy health check endpoint responds"
    
    $result = docker compose -f $script:ComposeFile -p $script:ProjectName exec -T proxy curl -sf http://localhost:58080/health 2>&1
    
    if ($result -eq "OK") {
        Write-TestPass "Proxy health endpoint returns OK"
    } else {
        Write-TestFail "Proxy health endpoint failed: $result"
    }
}

function Test-ProxyLogsRunning {
    Write-TestStart "Proxy shows running in logs"
    
    $logs = docker compose -f $script:ComposeFile -p $script:ProjectName logs proxy 2>&1
    
    if ($logs -match "Secure Proxy listening") {
        Write-TestPass "Proxy logs show server running"
    } else {
        Write-TestFail "Proxy logs don't show running: $logs"
    }
}

function Test-AllowedHostHttp {
    Write-TestStart "HTTP request to allowed host succeeds"
    
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "http://httpbin.org/get")
    
    if ($LASTEXITCODE -eq 0 -and $result -match '"url"') {
        Write-TestPass "HTTP request to httpbin.org/get succeeded"
    } else {
        Write-TestFail "HTTP request failed (exit $LASTEXITCODE): $result"
    }
}

function Test-AllowedHostHttps {
    Write-TestStart "HTTPS request to allowed host succeeds (with CA)"
    
    # First check if CA cert exists
    $caExists = Invoke-InClient @("ls", "/ca/certs/ca.pem")
    
    if ($caExists -notmatch "ca.pem") {
        Write-TestFail "CA certificate not found in /ca/"
        return
    }
    
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "--cacert", "/ca/certs/ca.pem", "https://httpbin.org/get")
    
    if ($result -match '"url"') {
        Write-TestPass "HTTPS request to httpbin.org/get succeeded with CA"
    } else {
        Write-TestFail "HTTPS request failed: $result"
    }
}

function Test-AllowedPathSucceeds {
    Write-TestStart "Request to allowed path succeeds"
    
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "--cacert", "/ca/certs/ca.pem", "https://httpbin.org/status/200")
    
    if ($LASTEXITCODE -eq 0) {
        Write-TestPass "Request to /status/200 succeeded"
    } else {
        Write-TestFail "Request to allowed path failed: $result"
    }
}

function Test-BlockedHost {
    Write-TestStart "Request to non-allowed host is blocked"
    
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "--cacert", "/ca/certs/ca.pem", "https://example.com/")
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-TestPass "Request to example.com was blocked (exit code: $exitCode)"
    } else {
        Write-TestFail "Request to blocked host should have failed but succeeded"
    }
}

function Test-BlockedPath {
    Write-TestStart "Request to non-allowed path is blocked"
    
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "--cacert", "/ca/certs/ca.pem", "https://httpbin.org/post")
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-TestPass "Request to /post was blocked (not in allowed_paths)"
    } else {
        Write-TestFail "Request to blocked path should have failed: $result"
    }
}

function Test-HttpBlockedWithoutAllowInsecure {
    Write-TestStart "HTTP request blocked when allow_insecure is false"
    
    # api.github.com has allow_insecure: false (or unset), so HTTP should be blocked
    $result = Invoke-InClient @("curl", "-sf", "--max-time", "10", "http://api.github.com/")
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-TestPass "HTTP request to api.github.com blocked (allow_insecure not set)"
    } else {
        Write-TestFail "HTTP request should be blocked when allow_insecure is false: $result"
    }
}

function Test-NoDirectInternet {
    Write-TestStart "Client cannot reach internet directly (bypassing proxy)"
    
    # Try to reach a host directly without proxy
    $result = docker compose -f $script:ComposeFile -p $script:ProjectName exec -T `
        -e HTTP_PROXY= -e HTTPS_PROXY= -e http_proxy= -e https_proxy= `
        test-client curl -sf --max-time 5 "http://httpbin.org/get" 2>&1
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -ne 0) {
        Write-TestPass "Direct internet access blocked (airlock working)"
    } else {
        Write-TestFail "Direct internet access should be blocked but succeeded"
    }
}

function Test-CaCertificateExists {
    Write-TestStart "CA certificate is generated and shared"
    
    $result = Invoke-InClient @("cat", "/ca/certs/ca.pem")
    
    if ($result -match "BEGIN CERTIFICATE") {
        Write-TestPass "CA certificate exists and contains valid PEM data"
    } else {
        Write-TestFail "CA certificate not found or invalid: $result"
    }
}

# ============================================================================
# MAIN
# ============================================================================

function Main {
    Write-Host "========================================"
    Write-Host "    AIRLOCK INTEGRATION TESTS"
    Write-Host "========================================"
    Write-Host ""
    
    # Check prerequisites first
    Test-Prerequisites
    
    try {
        # Setup and start
        Initialize-TestEnvironment
        if (-not (Start-TestContainers)) {
            Write-Host "Failed to start containers, aborting tests" -ForegroundColor Red
            exit 1
        }
        
        # Run tests
        Write-Host ""
        Write-Host "========================================"
        Write-Host "    RUNNING TESTS"
        Write-Host "========================================"
        
        Test-ProxyHealth
        Test-ProxyLogsRunning
        Test-CaCertificateExists
        Test-AllowedHostHttp
        Test-AllowedHostHttps
        Test-AllowedPathSucceeds
        Test-BlockedHost
        Test-BlockedPath
        Test-HttpBlockedWithoutAllowInsecure
        Test-NoDirectInternet
        
        # Print summary and capture result
        $success = Write-TestSummary
        
        if ($success) {
            exit 0
        } else {
            exit 1
        }
    }
    finally {
        # Always cleanup
        Stop-TestContainers
    }
}

# Run if executed directly
Main
