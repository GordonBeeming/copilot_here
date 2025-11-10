# Test Join-Path behavior with absolute paths on Linux (simulating Windows paths)
$ErrorActionPreference = "Stop"

# Simulate Windows-style absolute paths
$path1 = "C:\Users\RUNNER"
$path2 = "C:\Users\runneradmin1\AppData\Local\Temp\test\absolute\path"

Write-Host "Path1: $path1"
Write-Host "Path2: $path2"
Write-Host "IsPathRooted(path2): $([System.IO.Path]::IsPathRooted($path2))"

$joined = Join-Path $path1 $path2
Write-Host "Join-Path result: $joined"

# Test with Linux-style paths too
$path3 = "/home/user1"
$path4 = "/tmp/test/absolute/path"

Write-Host "`nPath3: $path3"
Write-Host "Path4: $path4"
Write-Host "IsPathRooted(path4): $([System.IO.Path]::IsPathRooted($path4))"

$joined2 = Join-Path $path3 $path4
Write-Host "Join-Path result: $joined2"
