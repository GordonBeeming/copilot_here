# PowerShell Install Script for copilot_here
# This script downloads copilot_here.ps1 and configures the PowerShell profile

# Set console output encoding to UTF-8 for Unicode character support
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Download the main script
$scriptPath = "$env:USERPROFILE\.copilot_here.ps1"
Write-Host "📥 Downloading copilot_here.ps1..." -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.ps1" -OutFile $scriptPath
Write-Host "✅ Downloaded to: $scriptPath" -ForegroundColor Green

# Create profile if it doesn't exist
if (-not (Test-Path $PROFILE)) {
    Write-Host "📝 Creating PowerShell profile..." -ForegroundColor Cyan
    New-Item -ItemType File -Path $PROFILE -Force | Out-Null
}

# Remove any old copilot_here entries and add the new one
Write-Host "🔧 Updating PowerShell profile..." -ForegroundColor Cyan
$profileContent = Get-Content $PROFILE -Raw -ErrorAction SilentlyContinue
if ([string]::IsNullOrEmpty($profileContent)) {
    $profileContent = ""
}

# Remove all existing copilot_here.ps1 references
$profileContent = $profileContent -replace '(?m)^.*copilot_here\.ps1.*$\r?\n?', ''
$profileContent = $profileContent.TrimEnd()

# Add the new reference
$newEntry = ". `"$scriptPath`""
if (-not $profileContent.Contains($newEntry)) {
    $profileContent = $profileContent + "`n`n$newEntry"
}

Set-Content -Path $PROFILE -Value $profileContent.TrimStart()
Write-Host "✅ Profile updated: $PROFILE" -ForegroundColor Green

# Reload the profile
Write-Host "🔄 Reloading PowerShell profile..." -ForegroundColor Cyan
. $PROFILE

Write-Host ""
Write-Host "✅ Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Try running: copilot_here --help" -ForegroundColor Yellow
