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
    
    $markerStart = "# >>> copilot_here >>>"
    $markerEnd = "# <<< copilot_here <<<"
    
    # Load profile content
    $profileContent = Get-Content $ProfilePath -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($profileContent)) {
        $profileContent = ""
    }
    
    # Always clean up rogue copilot_here entries outside markers
    if ($profileContent.Contains($markerStart)) {
        # Markers exist - extract marked block and remove rogue entries
        $startIndex = $profileContent.IndexOf($markerStart)
        $endIndex = $profileContent.IndexOf($markerEnd, $startIndex)
        if ($endIndex -gt $startIndex) {
            $endIndex += $markerEnd.Length
            $markedBlock = $profileContent.Substring($startIndex, $endIndex - $startIndex)
            $beforeBlock = $profileContent.Substring(0, $startIndex)
            $afterBlock = if ($endIndex -lt $profileContent.Length) { $profileContent.Substring($endIndex) } else { "" }
            
            # Remove any rogue copilot_here entries from before/after
            $beforeBlock = $beforeBlock -replace '(?m)^.*copilot_here\.ps1.*$\r?\n?', ''
            $afterBlock = $afterBlock -replace '(?m)^.*copilot_here\.ps1.*$\r?\n?', ''
            
            # Reconstruct profile with only marked block
            $profileContent = ($beforeBlock.TrimEnd() + "`n`n" + $markedBlock + "`n" + $afterBlock.TrimStart()).Trim()
            Set-Content -Path $ProfilePath -Value $profileContent
            Write-Host "   ✓ $ProfilePath (cleaned up rogue entries)" -ForegroundColor Gray
        }
        return
    }
    
    # No markers - remove all copilot_here entries and add fresh block
    $profileContent = $profileContent -replace '(?m)^.*copilot_here\.ps1.*$\r?\n?', ''
    $profileContent = $profileContent.TrimEnd()
    
    # Add the marker block
    $block = @"

$markerStart
if (Test-Path "$scriptPath") {
    . "$scriptPath"
}
$markerEnd
"@
    
    $profileContent = $profileContent + $block
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

# Reload the new script directly
Write-Host "🔄 Reloading copilot_here functions..." -ForegroundColor Cyan

# Remove existing functions if they exist to ensure clean reload
$functionsToRemove = @(
    'Write-CopilotDebug',
    'Stop-CopilotContainers',
    'Download-CopilotHereBinary',
    'Ensure-CopilotHereBinary',
    'Update-CopilotHere',
    'Reset-CopilotHere',
    'Test-CopilotHereUpdates',
    'Test-UpdateArg',
    'Test-ResetArg',
    'copilot_here',
    'copilot_yolo'
)
foreach ($func in $functionsToRemove) {
    Remove-Item "Function:\$func" -ErrorAction SilentlyContinue
}

# Load script content and execute it directly in current scope
# This will define functions in the scope where this installer script runs
# Since installer is invoked with iex, that's the interactive session
Invoke-Expression (Get-Content $scriptPath -Raw)

Write-Host ""
Write-Host "✅ Installation complete!" -ForegroundColor Green
if (Get-Variable -Name CopilotHereVersion -Scope Script -ErrorAction SilentlyContinue) {
    Write-Host "   Loaded version: $script:CopilotHereVersion" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Try running: copilot_here --help" -ForegroundColor Yellow
