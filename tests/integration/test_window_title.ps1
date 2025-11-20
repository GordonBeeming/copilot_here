$ErrorActionPreference = "Stop"
$env:COPILOT_HERE_TEST_MODE = "true"
$tmpDir = Join-Path (Get-Location).Path "tmp"
if (-not (Test-Path $tmpDir)) {
    New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
}
$env:USERPROFILE = Join-Path $tmpDir "copilot_here_test_home"
if (-not (Test-Path $env:USERPROFILE)) {
    New-Item -ItemType Directory -Path $env:USERPROFILE -Force | Out-Null
}

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
. ./copilot_here.ps1

$currentDirName = Split-Path -Leaf (Get-Location).Path

# Test 1: Standard Mode
Write-Host "TEST: Standard Mode"
# Capture output to check title
try {
    $output = Copilot-Here -NoPull -NoCleanup -p "test" *>&1 | Out-String
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
$output = Copilot-Yolo -NoPull -NoCleanup -p "test" *>&1 | Out-String

if ($output -match "MOCK_TITLE_CHECK: .*🤖⚡️ $currentDirName") {
    Write-Host "✅ PASS: YOLO mode title set correctly" -ForegroundColor Green
} else {
    Write-Host "❌ FAIL: YOLO mode title not found" -ForegroundColor Red
    Write-Host "Output was:`n$output"
    exit 1
}

Write-Host "All tests passed!" -ForegroundColor Green
exit 0
