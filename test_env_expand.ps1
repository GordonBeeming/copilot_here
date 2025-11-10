# Test if ExpandEnvironmentVariables corrupts paths
$ErrorActionPreference = "Stop"

# Test various path formats
$paths = @(
    "C:\Users\runneradmin\AppData\Local\Temp\test\absolute\path",
    "%TEMP%\test\absolute\path",
    "C:\Users\%USERNAME%\test"
)

foreach ($path in $paths) {
    Write-Host "Original: $path"
    $expanded = [System.Environment]::ExpandEnvironmentVariables($path)
    Write-Host "Expanded: $expanded"
    Write-Host "IsRooted: $([System.IO.Path]::IsPathRooted($expanded))"
    Write-Host ""
}
