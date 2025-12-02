# copilot_here PowerShell functions
# Version: 2025.12.02.6
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { "$env:USERPROFILE\.local\bin\copilot_here.exe" }
$script:CopilotHereReleaseUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"

# Helper function to download and install binary
function Download-CopilotHereBinary {
    # Detect architecture
    $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "arm64" } else { "x64" }
    
    # Create bin directory
    $binDir = Split-Path $script:CopilotHereBin
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }
    
    # Download latest release archive
    $downloadUrl = "$script:CopilotHereReleaseUrl/copilot_here-win-${arch}.zip"
    $tmpArchive = [System.IO.Path]::GetTempFileName() + ".zip"
    
    Write-Host "📦 Downloading binary from: $downloadUrl"
    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tmpArchive -UseBasicParsing
    } catch {
        Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
        Write-Host "❌ Failed to download binary: $_" -ForegroundColor Red
        return $false
    }
    
    # Extract binary from archive
    try {
        Expand-Archive -Path $tmpArchive -DestinationPath $binDir -Force
    } catch {
        Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
        Write-Host "❌ Failed to extract binary: $_" -ForegroundColor Red
        return $false
    }
    
    Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
    Write-Host "✅ Binary installed to: $script:CopilotHereBin"
    return $true
}

# Helper function to ensure binary is installed
function Ensure-CopilotHereBinary {
    if (-not (Test-Path $script:CopilotHereBin)) {
        Write-Host "📥 copilot_here binary not found. Installing..."
        return Download-CopilotHereBinary
    }
    
    return $true
}

# Update function - downloads fresh binary and script
function Update-CopilotHere {
    Write-Host "🔄 Updating copilot_here..."
    
    # Remove existing binary
    if (Test-Path $script:CopilotHereBin) {
        Remove-Item -Path $script:CopilotHereBin -Force
    }
    
    # Download fresh binary
    Write-Host ""
    Write-Host "📥 Downloading latest binary..."
    if (-not (Download-CopilotHereBinary)) {
        Write-Host "❌ Failed to download binary" -ForegroundColor Red
        return $false
    }
    
    # Download and execute fresh PowerShell script
    Write-Host ""
    Write-Host "📥 Downloading latest PowerShell script..."
    try {
        $scriptContent = (Invoke-WebRequest -Uri "$script:CopilotHereReleaseUrl/copilot_here.ps1" -UseBasicParsing).Content
        Write-Host "✅ Update complete! Reloading PowerShell functions..."
        Invoke-Expression $scriptContent
    } catch {
        Write-Host ""
        Write-Host "✅ Binary updated!"
        Write-Host ""
        Write-Host "⚠️  Could not auto-reload PowerShell functions. Please re-import manually:" -ForegroundColor Yellow
        Write-Host "   iex (iwr -UseBasicParsing $script:CopilotHereReleaseUrl/copilot_here.ps1).Content"
        Write-Host ""
        Write-Host "   Or restart your terminal."
    }
    return $true
}

# Reset function - same as update (kept for backwards compatibility)
function Reset-CopilotHere {
    Update-CopilotHere
}

# Check if argument is an update command
function Test-UpdateArg {
    param([string]$Arg)
    $updateArgs = @("--update", "-u", "--upgrade", "-Update", "-UpdateScripts", "-UpgradeScripts", "--update-scripts", "--upgrade-scripts")
    return $updateArgs -contains $Arg
}

# Check if argument is a reset command
function Test-ResetArg {
    param([string]$Arg)
    $resetArgs = @("--reset", "-Reset")
    return $resetArgs -contains $Arg
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    # Handle --update and variants before binary check
    if ($Arguments -and (Test-UpdateArg $Arguments[0])) {
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments -and (Test-ResetArg $Arguments[0])) {
        Reset-CopilotHere
        return
    }
    
    if (-not (Ensure-CopilotHereBinary)) { return }
    & $script:CopilotHereBin @Arguments
}

# YOLO Mode: Auto-approves all tool usage
function Copilot-Yolo {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    # Handle --update and variants before binary check
    if ($Arguments -and (Test-UpdateArg $Arguments[0])) {
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments -and (Test-ResetArg $Arguments[0])) {
        Reset-CopilotHere
        return
    }
    
    if (-not (Ensure-CopilotHereBinary)) { return }
    & $script:CopilotHereBin --yolo @Arguments
}

# Aliases for compatibility
Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
