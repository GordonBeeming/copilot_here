$ErrorActionPreference = "Stop"
$env:COPILOT_HERE_TEST_MODE = "true"

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Get-Item "$ScriptDir/../..").FullName

# Create isolated test directories
$TestDir = Join-Path ([System.IO.Path]::GetTempPath()) "copilot_test_title_$([System.Diagnostics.Process]::GetCurrentProcess().Id)"
$TestWorkDir = Join-Path $TestDir "work"
$TestHome = Join-Path $TestDir "home"
New-Item -ItemType Directory -Path $TestWorkDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $TestHome ".config/copilot_here") -Force | Out-Null

# Override USERPROFILE to use test directory (isolates from global config)
$env:USERPROFILE = $TestHome

# Cleanup function
$cleanupScript = {
    param($dir)
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
    }
}

try {
    # Mock gh
    function gh {
        param([Parameter(ValueFromRemainingArguments=$true)]$args)
        if ($args[0] -eq 'auth' -and $args[1] -eq 'status') {
            return "Token scopes: 'copilot', 'read:packages'"
        }
        if ($args[0] -eq 'auth' -and $args[1] -eq 'token') {
            return "mock_token"
        }
    }

    # Mock docker
    function docker {
        param([Parameter(ValueFromRemainingArguments=$true)]$args)
        Write-Host "MOCK: docker $args"
        
        # Check title
        $currentTitle = $Host.UI.RawUI.WindowTitle
        Write-Host "MOCK_TITLE_CHECK: $currentTitle"
    }

    # Source the script
    . "$RepoRoot/copilot_here.ps1"

    # Change to test work directory to avoid local .copilot_here/network.json
    Push-Location $TestWorkDir

    $currentDirName = Split-Path -Leaf (Get-Location).Path

    # Test 1: Standard Mode
    Write-Host "TEST: Standard Mode"
    # Capture output to check title
    try {
        $output = Copilot-Here -NoPull -NoCleanup -Prompt "test" *>&1 | Out-String
    } catch {
        Write-Host "Error: $_" -ForegroundColor Red
        Write-Host "ScriptStackTrace: $($_.ScriptStackTrace)" -ForegroundColor Red
        exit 1
    }

    if ($output -match "MOCK_TITLE_CHECK: .*🤖 $currentDirName") {
        Write-Host "✅ PASS: Standard mode title set correctly" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: Standard mode title not found" -ForegroundColor Red
        Write-Host "Output was:`n$output"
        exit 1
    }

    # Test 2: YOLO Mode
    Write-Host "TEST: YOLO Mode"
    $output = Copilot-Yolo -NoPull -NoCleanup -Prompt "test" *>&1 | Out-String

    if ($output -match "MOCK_TITLE_CHECK: .*🤖⚡️ $currentDirName") {
        Write-Host "✅ PASS: YOLO mode title set correctly" -ForegroundColor Green
    } else {
        Write-Host "❌ FAIL: YOLO mode title not found" -ForegroundColor Red
        Write-Host "Output was:`n$output"
        exit 1
    }

    Write-Host "All tests passed!" -ForegroundColor Green
    Pop-Location
    exit 0
} finally {
    # Cleanup
    Pop-Location -ErrorAction SilentlyContinue
    & $cleanupScript $TestDir
}
