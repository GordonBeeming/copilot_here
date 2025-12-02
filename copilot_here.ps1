# copilot_here PowerShell functions
# Version: 2025-12-02
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { "$env:USERPROFILE\.local\bin\copilot-here.exe" }

# Helper function to ensure binary is installed
function Ensure-CopilotHereBinary {
    if (-not (Test-Path $script:CopilotHereBin)) {
        Write-Host "📥 copilot-here binary not found. Installing..."
        
        # Detect architecture
        $arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "arm64" } else { "x64" }
        
        # Create bin directory
        $binDir = Split-Path $script:CopilotHereBin
        if (-not (Test-Path $binDir)) {
            New-Item -ItemType Directory -Path $binDir -Force | Out-Null
        }
        
        # Download latest release archive
        $downloadUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot-here-win-${arch}.zip"
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
    }
    
    return $true
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
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
    
    if (-not (Ensure-CopilotHereBinary)) { return }
    & $script:CopilotHereBin --yolo @Arguments
}

# Aliases for compatibility
Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
