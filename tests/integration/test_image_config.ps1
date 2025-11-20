# Integration tests for Image Configuration
# Tests default image setting, retrieval, and precedence rules

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
$TestDir = if ($env:TEMP) { 
    Join-Path $env:TEMP "copilot_test_image_$(Get-Random)" 
} else { 
    Join-Path "/tmp" "copilot_test_image_$(Get-Random)" 
}
New-Item -ItemType Directory -Path $TestDir -Force | Out-Null

# Create project directory structure
$ProjectDir = Join-Path $TestDir "project"
New-Item -ItemType Directory -Path $ProjectDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ProjectDir ".copilot_here") -Force | Out-Null

# Create global config directory
$GlobalConfigDir = Join-Path $TestDir ".config/copilot_here"
New-Item -ItemType Directory -Path $GlobalConfigDir -Force | Out-Null

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
Write-Host "Image Configuration Tests (PowerShell)"
Write-Host "======================================"
Write-Host "Script: $ScriptPath"

# Mock environment variables
$OldUserProfile = $env:USERPROFILE
$env:USERPROFILE = $TestDir

# Switch to project directory
Set-Location $ProjectDir

try {
    # Test 1: Default is "latest" when no config exists
    Test-Start "Default is 'latest' when no config exists"
    $DefaultImage = Get-DefaultImage
    
    if ($DefaultImage -eq "latest") {
        Test-Pass "Default is latest"
    } else {
        Test-Fail "Expected 'latest', got '$DefaultImage'"
    }
    
    # Test 2: Save to local config
    Test-Start "Save to local config"
    Save-ImageConfig -ImageTag "dotnet" -IsGlobal $false
    
    $LocalConfigFile = Join-Path ".copilot_here" "image.conf"
    if (Test-Path $LocalConfigFile) {
        $Content = Get-Content $LocalConfigFile -Raw
        if ($Content.Trim() -eq "dotnet") {
            Test-Pass "Local config saved correctly"
        } else {
            Test-Fail "Local config content incorrect: $Content"
        }
    } else {
        Test-Fail "Local config file not created"
    }
    
    # Test 3: Get default reads from local config
    Test-Start "Get default reads from local config"
    $DefaultImage = Get-DefaultImage
    
    if ($DefaultImage -eq "dotnet") {
        Test-Pass "Read from local config correctly"
    } else {
        Test-Fail "Expected 'dotnet', got '$DefaultImage'"
    }
    
    # Test 4: Save to global config
    Test-Start "Save to global config"
    Save-ImageConfig -ImageTag "playwright" -IsGlobal $true
    
    $GlobalConfigFile = Join-Path $GlobalConfigDir "image.conf"
    if (Test-Path $GlobalConfigFile) {
        $Content = Get-Content $GlobalConfigFile -Raw
        if ($Content.Trim() -eq "playwright") {
            Test-Pass "Global config saved correctly"
        } else {
            Test-Fail "Global config content incorrect: $Content"
        }
    } else {
        Test-Fail "Global config file not created"
    }
    
    # Test 5: Local config takes precedence over global
    Test-Start "Local config takes precedence over global"
    # Local is 'dotnet', Global is 'playwright'
    $DefaultImage = Get-DefaultImage
    
    if ($DefaultImage -eq "dotnet") {
        Test-Pass "Local config precedence respected"
    } else {
        Test-Fail "Expected 'dotnet' (local), got '$DefaultImage'"
    }
    
    # Test 6: Fallback to global when local missing
    Test-Start "Fallback to global when local missing"
    Remove-Item $LocalConfigFile -Force
    $DefaultImage = Get-DefaultImage
    
    if ($DefaultImage -eq "playwright") {
        Test-Pass "Fallback to global working"
    } else {
        Test-Fail "Expected 'playwright' (global), got '$DefaultImage'"
    }
    
    # Test 7: Show image output format
    Test-Start "Show image output format"
    # Re-create local config
    "dotnet" | Out-File -FilePath $LocalConfigFile -Encoding utf8
    
    # Capture output
    $Output = Show-ImageConfig *>&1 | Out-String
    
    if ($Output -match "Image Configuration") {
        Test-Pass "Header found"
    } else {
        Test-Fail "Header 'Image Configuration' not found"
    }
    
    if ($Output -match "Current effective default: dotnet") {
        Test-Pass "Effective default shown"
    } else {
        Test-Fail "Effective default not shown correctly"
    }
    
    if ($Output -match "Local config.*dotnet") {
        Test-Pass "Local config shown"
    } else {
        Test-Fail "Local config not shown correctly"
    }
    
    if ($Output -match "Global config.*playwright") {
        Test-Pass "Global config shown"
    } else {
        Test-Fail "Global config not shown correctly"
    }
    
    if ($Output -match "Base default: latest") {
        Test-Pass "Base default shown"
    } else {
        Test-Fail "Base default not shown correctly"
    }

} finally {
    # Restore environment
    $env:USERPROFILE = $OldUserProfile
    Set-Location $ScriptDir
    Cleanup
}

Print-Summary
exit 0
