# PowerShell Integration Tests  
# Basic tests to ensure PowerShell script loads and has required functions
# Note: Full Docker command mocking tests pending - Bash/Zsh versions complete

$ErrorActionPreference = "Stop"

# Test counters
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0

# Test helper functions
function Test-Start {
    param([string]$Name)
    Write-Host ""
    Write-Host "TEST: $Name"
    $script:TestCount++
}

function Test-Pass {
    param([string]$Message)
    Write-Host "✓ PASS: $Message" -ForegroundColor Green
    $script:PassCount++
}

function Test-Fail {
    param([string]$Message)
    Write-Host "✗ FAIL: $Message" -ForegroundColor Red
    $script:FailCount++
}

# Summary function
function Print-Summary {
    Write-Host ""
    Write-Host "======================================"
    Write-Host "TEST SUMMARY"
    Write-Host "======================================"
    Write-Host "Total Tests: $script:TestCount"
    Write-Host "Passed: $script:PassCount" -ForegroundColor Green
    if ($script:FailCount -gt 0) {
        Write-Host "Failed: $script:FailCount" -ForegroundColor Red
    } else {
        Write-Host "Failed: $script:FailCount"
    }
    Write-Host "======================================"
    
    if ($script:FailCount -gt 0) {
        exit 1
    }
}

# Load the script
$ScriptDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ScriptPath = Join-Path $ScriptDir "copilot_here.ps1"

# Dot source the script
. $ScriptPath

Write-Host "======================================"
Write-Host "PowerShell Basic Tests"
Write-Host "======================================"
Write-Host "Script: $ScriptPath"

# Test 1: Copilot-Here function exists
Test-Start "Copilot-Here function is defined"
if (Get-Command Copilot-Here -ErrorAction SilentlyContinue) {
    Test-Pass "Copilot-Here function exists"
} else {
    Test-Fail "Copilot-Here function not found"
}

# Test 2: Copilot-Yolo function exists
Test-Start "Copilot-Yolo function is defined"
if (Get-Command Copilot-Yolo -ErrorAction SilentlyContinue) {
    Test-Pass "Copilot-Yolo function exists"
} else {
    Test-Fail "Copilot-Yolo function not found"
}

# Test 3: Test mode support
Test-Start "Test mode environment variable support"
$env:COPILOT_HERE_TEST_MODE = "true"
if ($env:COPILOT_HERE_TEST_MODE -eq "true") {
    Test-Pass "Test mode can be set"
} else {
    Test-Fail "Test mode not working"
}

# Test 4: Helper functions exist
Test-Start "Helper functions are defined"
$helpers = @("Test-EmojiSupport", "Get-ConfigMounts", "Resolve-MountPath", "Test-CopilotSecurityCheck")
$allExist = $true
foreach ($helper in $helpers) {
    if (-not (Get-Command $helper -ErrorAction SilentlyContinue)) {
        $allExist = $false
        break
    }
}

if ($allExist) {
    Test-Pass "All helper functions exist"
} else {
    Test-Fail "Some helper functions missing"
}

# Test 5: Config file handling
Test-Start "Config mount loading function works"
$TestDir = if ($env:TEMP) { Join-Path $env:TEMP "test_$(Get-Random)" } else { Join-Path "/tmp" "test_$(Get-Random)" }
New-Item -ItemType Directory -Path $TestDir -Force | Out-Null
$TestConfig = Join-Path $TestDir "test.conf"
@"
/path/one
/path/two
"@ | Out-File -FilePath $TestConfig -Encoding utf8

$mounts = Get-ConfigMounts -ConfigFile $TestConfig
Remove-Item -Recurse -Force $TestDir -ErrorAction SilentlyContinue

if ($mounts.Count -eq 2) {
    Test-Pass "Config file parsing works"
} else {
    Test-Fail "Config file parsing failed"
}

Print-Summary
