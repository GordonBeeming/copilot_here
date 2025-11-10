# Debug script to understand path resolution on Windows
$ErrorActionPreference = "Stop"

# Simulate what the test does
$TestDir = if ($env:TEMP) { 
    Join-Path $env:TEMP "copilot_test_debug" 
} else { 
    Join-Path "/tmp" "copilot_test_debug" 
}

Write-Host "TestDir: $TestDir"
Write-Host "TestDir type: $($TestDir.GetType().FullName)"

$TestAbsPath = Join-Path $TestDir "absolute\path"
Write-Host "TestAbsPath: $TestAbsPath"
Write-Host "TestAbsPath type: $($TestAbsPath.GetType().FullName)"

# Now trace through Resolve-MountPath logic
$Path = $TestAbsPath
Write-Host "`nStep 1: Input Path = $Path"

$resolvedPath = [System.Environment]::ExpandEnvironmentVariables($Path)
Write-Host "Step 2: After ExpandEnvironmentVariables = $resolvedPath"

if ($env:USERPROFILE) {
    $resolvedPath = $resolvedPath.Replace('~', $env:USERPROFILE)
    Write-Host "Step 3: After ~ replacement = $resolvedPath"
}

Write-Host "Step 4: IsPathRooted check = $([System.IO.Path]::IsPathRooted($resolvedPath))"

if (-not [System.IO.Path]::IsPathRooted($resolvedPath)) {
    Write-Host "Step 5: Path is relative, joining with current location"
    $currentLocation = (Get-Location).Path
    Write-Host "  Current location: $currentLocation"
    $resolvedPath = Join-Path $currentLocation $resolvedPath
    Write-Host "  After Join-Path: $resolvedPath"
} else {
    Write-Host "Step 5: Path is already rooted, skipping join"
}

Write-Host "`nFinal resolved path: $resolvedPath"
