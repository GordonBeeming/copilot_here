# Integration tests for PowerShell script
# Tests copilot_here.ps1 functions

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

# Setup
$TestDir = Join-Path $env:TEMP "copilot_test_$(Get-Random)"
New-Item -ItemType Directory -Path $TestDir -Force | Out-Null

# Cleanup function
function Cleanup {
    if (Test-Path $TestDir) {
        Remove-Item -Recurse -Force $TestDir
    }
}

# Load the script
$ScriptDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ScriptPath = Join-Path $ScriptDir "copilot_here.ps1"

# Dot source the script
. $ScriptPath

Write-Host "======================================"
Write-Host "PowerShell Integration Tests"
Write-Host "======================================"
Write-Host "PowerShell Version: $($PSVersionTable.PSVersion)"
Write-Host "Script: $ScriptPath"

# Test 1: Functions are defined
Test-Start "Check if Copilot-Here function exists"
if (Get-Command Copilot-Here -ErrorAction SilentlyContinue) {
    Test-Pass "Copilot-Here function is defined"
} else {
    Test-Fail "Copilot-Here function not found"
}

# Test 2: Copilot-Yolo function exists
Test-Start "Check if Copilot-Yolo function exists"
if (Get-Command Copilot-Yolo -ErrorAction SilentlyContinue) {
    Test-Pass "Copilot-Yolo function is defined"
} else {
    Test-Fail "Copilot-Yolo function not found"
}

# Test 3: Helper functions exist
Test-Start "Check if helper functions exist"
if (Get-Command Test-EmojiSupport -ErrorAction SilentlyContinue) {
    Test-Pass "Test-EmojiSupport helper function exists"
} else {
    Test-Fail "Test-EmojiSupport helper function not found"
}

if (Get-Command Get-ConfigMounts -ErrorAction SilentlyContinue) {
    Test-Pass "Get-ConfigMounts helper function exists"
} else {
    Test-Fail "Get-ConfigMounts helper function not found"
}

if (Get-Command Resolve-MountPath -ErrorAction SilentlyContinue) {
    Test-Pass "Resolve-MountPath helper function exists"
} else {
    Test-Fail "Resolve-MountPath helper function not found"
}

# Test 4: Help output works
Test-Start "Check help output for Copilot-Here"
$HelpOutput = Copilot-Here -Help 2>&1 | Out-String
if ($HelpOutput -match "(?i)usage:") {
    Test-Pass "Help output contains Usage section"
} else {
    Test-Fail "Help output missing Usage section"
}

# Test 5: Version in help
Test-Start "Check version is displayed in help"
if ($HelpOutput -match "VERSION:") {
    Test-Pass "Version information present"
} else {
    Test-Fail "Version information missing"
}

# Test 6: Config file parsing
Test-Start "Test config file mount loading"
$TestConfig = Join-Path $TestDir ".copilot_here_mounts"
@"
# Test comment
/test/path1

/test/path2
"@ | Out-File -FilePath $TestConfig -Encoding utf8

$Mounts = Get-ConfigMounts -ConfigFile $TestConfig

if ($Mounts.Count -eq 2) {
    Test-Pass "Config file loaded 2 mounts correctly"
} else {
    Test-Fail "Config file parsing failed (expected 2, got $($Mounts.Count))"
}

# Test 7: Comments and empty lines ignored
Test-Start "Verify comments and empty lines are ignored"
if ($Mounts[0] -eq "/test/path1" -and $Mounts[1] -eq "/test/path2") {
    Test-Pass "Comments and empty lines correctly ignored"
} else {
    Test-Fail "Comment/empty line handling failed"
}

# Test 8: Path resolution (tilde/home expansion)
Test-Start "Test home path expansion"
$Resolved = Resolve-MountPath -Path "~/test"
$Expected = Join-Path $env:USERPROFILE "test"
if ($Resolved -eq $Expected) {
    Test-Pass "Home path expansion works correctly"
} else {
    Test-Fail "Home path expansion failed (expected: $Expected, got: $Resolved)"
}

# Test 9: Absolute path unchanged
Test-Start "Test absolute path resolution"
$Resolved = Resolve-MountPath -Path "C:\absolute\path"
if ($Resolved -eq "C:\absolute\path") {
    Test-Pass "Absolute path unchanged"
} else {
    Test-Fail "Absolute path changed (got: $Resolved)"
}

# Test 10: Copilot-Yolo help
Test-Start "Check help output for Copilot-Yolo"
$YoloHelp = Copilot-Yolo -Help 2>&1 | Out-String
if ($YoloHelp -match "(?i)usage:") {
    Test-Pass "Copilot-Yolo help output works"
} else {
    Test-Fail "Copilot-Yolo help output missing"
}

# Test 11: ListMounts parameter exists
Test-Start "Check -ListMounts parameter"
if ($HelpOutput -match "ListMounts") {
    Test-Pass "-ListMounts parameter documented"
} else {
    Test-Fail "-ListMounts parameter not documented"
}

# Test 12: DryRun parameter exists
Test-Start "Check -DryRun parameter"
if ($HelpOutput -match "DryRun") {
    Test-Pass "-DryRun parameter documented"
} else {
    Test-Fail "-DryRun parameter not documented"
}

# Cleanup and summary
Cleanup
Print-Summary
