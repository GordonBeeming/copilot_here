# copilot_here PowerShell functions
# Version: 2025.12.02
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { "$env:USERPROFILE\.local\bin\copilot_here.exe" }

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
    $downloadUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here-win-${arch}.zip"
    $tmpArchive = [System.IO.Path]::GetTempFileName() + ".zip"
    
    Write-Host "📦 Downloading from: $downloadUrl"
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
    Write-Host "✅ Installed to: $script:CopilotHereBin"
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

# Reset function - re-downloads binary
function Reset-CopilotHere {
    Write-Host "🔄 Resetting copilot_here..."
    
    # Remove existing binary
    if (Test-Path $script:CopilotHereBin) {
        Remove-Item -Path $script:CopilotHereBin -Force
        Write-Host "🗑️  Removed existing binary"
    }
    
    # Download fresh binary
    Write-Host "📥 Downloading fresh binary..."
    if (-not (Download-CopilotHereBinary)) {
        Write-Host "❌ Failed to download binary" -ForegroundColor Red
        return $false
    }
    
    Write-Host ""
    Write-Host "✅ Reset complete!"
    Write-Host ""
    Write-Host "⚠️  To complete the reset, please re-import the PowerShell script:" -ForegroundColor Yellow
    Write-Host "   iex (iwr -UseBasicParsing https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1).Content"
    Write-Host ""
    Write-Host "   Or restart your terminal."
    return $true
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    # Handle --reset before binary check
    if ($Arguments -and ($Arguments[0] -eq "--reset" -or $Arguments[0] -eq "-Reset")) {
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
    
    # Handle --reset before binary check
    if ($Arguments -and ($Arguments[0] -eq "--reset" -or $Arguments[0] -eq "-Reset")) {
        Reset-CopilotHere
        return
    }
    
    if (-not (Ensure-CopilotHereBinary)) { return }
    & $script:CopilotHereBin --yolo @Arguments
}

# Aliases for compatibility
Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
