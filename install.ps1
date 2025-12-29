# PowerShell Install Script for copilot_here
# This script downloads copilot_here.ps1 and configures the PowerShell profile

# Set console output encoding to UTF-8 for Unicode character support
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Download the main script
$scriptPath = "$env:USERPROFILE\.copilot_here.ps1"
Write-Host "📥 Downloading copilot_here.ps1..." -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.ps1" -OutFile $scriptPath
Write-Host "✅ Downloaded to: $scriptPath" -ForegroundColor Green

# Function to update a profile file
function Update-ProfileFile {
    param([string]$ProfilePath)
    
    if (-not $ProfilePath) { return }
    
    # Create profile directory if it doesn't exist
    $profileDir = Split-Path $ProfilePath
    if (-not (Test-Path $profileDir)) {
        New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
    }
    
    # Create profile if it doesn't exist
    if (-not (Test-Path $ProfilePath)) {
        New-Item -ItemType File -Path $ProfilePath -Force | Out-Null
    }
    
    # Remove any old copilot_here entries and add the new one
    $profileContent = Get-Content $ProfilePath -Raw -ErrorAction SilentlyContinue
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
    
    Set-Content -Path $ProfilePath -Value $profileContent.TrimStart()
    Write-Host "   ✓ $ProfilePath" -ForegroundColor Gray
}

# Update all PowerShell profiles
Write-Host "🔧 Updating PowerShell profiles..." -ForegroundColor Cyan

# PowerShell Core (pwsh)
$pwshProfile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
Update-ProfileFile -ProfilePath $pwshProfile

# Windows PowerShell (powershell.exe)
$winPsProfile = "$env:USERPROFILE\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
Update-ProfileFile -ProfilePath $winPsProfile

Write-Host "✅ Profile(s) updated" -ForegroundColor Green

# Reload the new script directly (not just the profile)
Write-Host "🔄 Reloading copilot_here functions..." -ForegroundColor Cyan
. $scriptPath

Write-Host ""
Write-Host "✅ Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Try running: copilot_here --help" -ForegroundColor Yellow
