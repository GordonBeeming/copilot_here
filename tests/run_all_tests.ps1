# Main test runner for all integration tests (PowerShell)
# Runs all PowerShell integration tests

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$testDir = Join-Path $scriptDir "integration"

$totalSuites = 0
$passedSuites = 0
$failedSuites = 0

Write-Host "======================================"
Write-Host "Running All Integration Tests (PowerShell)"
Write-Host "======================================"
Write-Host ""

# Function to run a test file
function Run-Test($testFile) {
    $testName = [System.IO.Path]::GetFileName($testFile)
    Write-Host "Running $testName..." -ForegroundColor Cyan
    
    $global:totalSuites++
    
    try {
        & $testFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ $testName passed" -ForegroundColor Green
            $global:passedSuites++
        } else {
            Write-Host "✗ $testName failed" -ForegroundColor Red
            $global:failedSuites++
        }
    } catch {
        Write-Host "✗ $testName failed with exception: $_" -ForegroundColor Red
        $global:failedSuites++
    }
    Write-Host ""
}

# List of PowerShell tests to run
$tests = @(
    "test_powershell.ps1",
    "test_image_config.ps1",
    "test_window_title.ps1"
)

foreach ($test in $tests) {
    $fullPath = Join-Path $testDir $test
    if (Test-Path $fullPath) {
        Run-Test $fullPath
    } else {
        Write-Host "⚠ Test file not found: $test" -ForegroundColor Yellow
    }
}

# Overall summary
Write-Host "======================================"
Write-Host "OVERALL TEST SUMMARY"
Write-Host "======================================"
Write-Host "Total Test Suites: $totalSuites"
if ($failedSuites -eq 0) {
    Write-Host "Passed: $passedSuites" -ForegroundColor Green
} else {
    Write-Host "Passed: $passedSuites"
    Write-Host "Failed: $failedSuites" -ForegroundColor Red
}
Write-Host "======================================"

if ($failedSuites -gt 0) {
    exit 1
}

Write-Host "All tests passed!" -ForegroundColor Green
