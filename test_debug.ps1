# Debug script to understand the path resolution issue
$ErrorActionPreference = "Stop"

Write-Host "=== Path Resolution Debug ==="

# Simulate the test scenario
$TestDir = "C:\Users\RUNNER~1\AppData\Local\Temp\copilot_test_556359480"
$TestAbsPath = Join-Path $TestDir "absolute\path"

Write-Host "TestDir: $TestDir"
Write-Host "TestAbsPath: $TestAbsPath"
Write-Host "Current Location: $((Get-Location).Path)"

# Test IsPathRooted
$isRooted = [System.IO.Path]::IsPathRooted($TestAbsPath)
Write-Host "IsPathRooted: $isRooted"

# Test regex
$matchesRegex = $TestAbsPath -match '^[a-zA-Z]:\\'
Write-Host "Matches regex ^[a-zA-Z]:\\: $matchesRegex"

# Test combined condition
$isAbsolute = [System.IO.Path]::IsPathRooted($TestAbsPath) -or ($TestAbsPath -match '^[a-zA-Z]:\\')
Write-Host "isAbsolute: $isAbsolute"

# Show what GetFullPath does
Write-Host "`nTesting GetFullPath with different approaches:"
try {
    $result1 = [System.IO.Path]::GetFullPath($TestAbsPath)
    Write-Host "GetFullPath(path): $result1"
} catch {
    Write-Host "GetFullPath(path) ERROR: $_"
}

try {
    $currentDir = (Get-Location).Path
    $result2 = [System.IO.Path]::GetFullPath($TestAbsPath, $currentDir)
    Write-Host "GetFullPath(path, currentDir): $result2"
    Write-Host "  currentDir was: $currentDir"
} catch {
    Write-Host "GetFullPath(path, currentDir) ERROR: $_"
}

# Test with environment variable expansion
$testWithEnv = [System.Environment]::ExpandEnvironmentVariables($TestAbsPath)
Write-Host "`nAfter ExpandEnvironmentVariables: $testWithEnv"
Write-Host "IsPathRooted after expansion: $([System.IO.Path]::IsPathRooted($testWithEnv))"
