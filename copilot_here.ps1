# copilot_here PowerShell functions
# Version: 2025.12.15
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { "$env:USERPROFILE\.local\bin\copilot_here.exe" }
$script:CopilotHereReleaseUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"
$script:CopilotHereVersion = "2025.12.15"

# Debug logging function
function Write-CopilotDebug {
    param([string]$Message)
    if ($env:COPILOT_HERE_DEBUG -eq "1" -or $env:COPILOT_HERE_DEBUG -eq "true") {
        Write-Host "[DEBUG] $Message" -ForegroundColor DarkGray
    }
}

# Helper function to stop running containers with confirmation
function Stop-CopilotContainers {
    $runningContainers = docker ps --filter "name=copilot_here-" -q 2>$null
    
    if ($runningContainers) {
        Write-Host "⚠️  copilot_here is currently running in Docker" -ForegroundColor Yellow
        $response = Read-Host "   Stop running containers to continue? [y/N]"
        if ($response -match '^[yY]') {
            Write-Host "🛑 Stopping copilot_here containers..."
            docker stop $runningContainers 2>$null | Out-Null
            Write-Host "   ✓ Stopped"
            return $true
        } else {
            Write-Host "❌ Cannot update while containers are running (binary is in use)" -ForegroundColor Red
            return $false
        }
    }
    return $true
}

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
    
    # Check and stop running containers
    if (-not (Stop-CopilotContainers)) {
        return $false
    }
    
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

# Check for updates (called at startup)
function Test-CopilotHereUpdates {
    try {
        # Fetch remote script with 2 second timeout
        $ProgressPreference = 'SilentlyContinue'
        $remoteScript = (Invoke-WebRequest -Uri "$script:CopilotHereReleaseUrl/copilot_here.ps1" -UseBasicParsing -TimeoutSec 2).Content
        
        # Extract version from remote script
        $remoteVersion = $null
        if ($remoteScript -match '\$script:CopilotHereVersion\s*=\s*"([^"]+)"') {
            $remoteVersion = $matches[1]
        }
        
        if (-not $remoteVersion) {
            return $false  # Couldn't parse version
        }
        
        if ($script:CopilotHereVersion -ne $remoteVersion) {
            # Compare versions - convert to comparable format
            $currentParts = $script:CopilotHereVersion -split '\.'
            $remoteParts = $remoteVersion -split '\.'
            
            # Pad arrays to same length
            $maxLen = [Math]::Max($currentParts.Length, $remoteParts.Length)
            while ($currentParts.Length -lt $maxLen) { $currentParts += "0" }
            while ($remoteParts.Length -lt $maxLen) { $remoteParts += "0" }
            
            # Compare each part
            $isNewer = $false
            for ($i = 0; $i -lt $maxLen; $i++) {
                $currentNum = [int]$currentParts[$i]
                $remoteNum = [int]$remoteParts[$i]
                if ($remoteNum -gt $currentNum) {
                    $isNewer = $true
                    break
                } elseif ($remoteNum -lt $currentNum) {
                    break
                }
            }
            
            if ($isNewer) {
                Write-Host "📢 Update available: $script:CopilotHereVersion → $remoteVersion"
                $confirmation = Read-Host "Would you like to update now? [y/N]"
                if ($confirmation -match '^[yY]') {
                    Update-CopilotHere
                    return $true  # Signal that update was performed
                }
            }
        }
    } catch {
        # Failed to check or offline - continue normally
    }
    return $false
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
    
    Write-CopilotDebug "=== Copilot-Here called with args: $Arguments"
    
    # Handle --update and variants before binary check
    if ($Arguments -and (Test-UpdateArg $Arguments[0])) {
        Write-CopilotDebug "Update argument detected"
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments -and (Test-ResetArg $Arguments[0])) {
        Write-CopilotDebug "Reset argument detected"
        Reset-CopilotHere
        return
    }
    
    # Check for updates at startup
    Write-CopilotDebug "Checking for updates..."
    if (Test-CopilotHereUpdates) { return }
    
    Write-CopilotDebug "Ensuring binary is installed..."
    if (-not (Ensure-CopilotHereBinary)) { return }
    
    Write-CopilotDebug "Executing binary: $script:CopilotHereBin $Arguments"
    & $script:CopilotHereBin @Arguments
    $exitCode = $LASTEXITCODE
    Write-CopilotDebug "Binary exited with code: $exitCode"
    $global:LASTEXITCODE = $exitCode
    return $exitCode
}

# YOLO Mode: Auto-approves all tool usage
function Copilot-Yolo {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    Write-CopilotDebug "=== Copilot-Yolo called with args: $Arguments"
    
    # Handle --update and variants before binary check
    if ($Arguments -and (Test-UpdateArg $Arguments[0])) {
        Write-CopilotDebug "Update argument detected"
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments -and (Test-ResetArg $Arguments[0])) {
        Write-CopilotDebug "Reset argument detected"
        Reset-CopilotHere
        return
    }
    
    # Check for updates at startup
    Write-CopilotDebug "Checking for updates..."
    if (Test-CopilotHereUpdates) { return }
    
    Write-CopilotDebug "Ensuring binary is installed..."
    if (-not (Ensure-CopilotHereBinary)) { return }
    
    Write-CopilotDebug "Executing binary in YOLO mode: $script:CopilotHereBin --yolo $Arguments"
    & $script:CopilotHereBin --yolo @Arguments
    $exitCode = $LASTEXITCODE
    Write-CopilotDebug "Binary exited with code: $exitCode"
    $global:LASTEXITCODE = $exitCode
    return $exitCode
}

# Aliases for compatibility
Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
