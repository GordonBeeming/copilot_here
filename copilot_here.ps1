# copilot_here PowerShell functions
# Version: 2025.12.29.3
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
$script:CopilotHereHome = if ($PSVersionTable.PSVersion.Major -ge 6) {
    # PowerShell Core 6+
    if ($IsWindows) {
        if ($env:USERPROFILE) { $env:USERPROFILE } else { [Environment]::GetFolderPath('UserProfile') }
    } else {
        if ($env:HOME) { $env:HOME } else { [Environment]::GetFolderPath('UserProfile') }
    }
} else {
    # Windows PowerShell 5.1 (always Windows)
    if ($env:USERPROFILE) { $env:USERPROFILE } else { [Environment]::GetFolderPath('UserProfile') }
}

$script:CopilotHereScriptPath = Join-Path $script:CopilotHereHome ".copilot_here.ps1"

$script:DefaultCopilotHereBinDir = Join-Path (Join-Path $script:CopilotHereHome ".local") "bin"
$script:DefaultCopilotHereBinName = if ($PSVersionTable.PSVersion.Major -ge 6 -and -not $IsWindows) { "copilot_here" } else { "copilot_here.exe" }
$script:DefaultCopilotHereBin = Join-Path $script:DefaultCopilotHereBinDir $script:DefaultCopilotHereBinName

$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { $script:DefaultCopilotHereBin }
$script:CopilotHereReleaseUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"
$script:CopilotHereVersion = "2025.12.29.3"

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
    $arch = if ($PSVersionTable.PSVersion.Major -ge 6) {
        # PowerShell Core 6+ has RuntimeInformation
        if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq "Arm64") { "arm64" } else { "x64" }
    } else {
        # Windows PowerShell 5.1 - check environment
        if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }
    }
    
    # Create bin directory
    $binDir = Split-Path $script:CopilotHereBin
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }
    
    $os = if ($PSVersionTable.PSVersion.Major -ge 6) {
        # PowerShell Core 6+
        if ($IsWindows) { "win" } elseif ($IsMacOS) { "macos" } else { "linux" }
    } else {
        # Windows PowerShell 5.1 (always Windows)
        "win"
    }
    $ext = if ($os -eq "win") { "zip" } else { "tar.gz" }

    # Download latest release archive
    $downloadUrl = "$script:CopilotHereReleaseUrl/copilot_here-${os}-${arch}.${ext}"
    $tmpBase = [System.IO.Path]::GetTempFileName()
    Remove-Item -Path $tmpBase -ErrorAction SilentlyContinue
    $tmpArchive = $tmpBase + ".${ext}"
    
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
        if ($PSVersionTable.PSVersion.Major -lt 6 -or $IsWindows) {
            # Windows PowerShell 5.1 or PowerShell Core on Windows
            Expand-Archive -Path $tmpArchive -DestinationPath $binDir -Force
        } else {
            # PowerShell Core on Linux/macOS
            & tar -xzf $tmpArchive -C $binDir copilot_here
            if ($LASTEXITCODE -ne 0) { throw "tar extraction failed" }
            & chmod +x $script:CopilotHereBin 2>$null
        }
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
    
    # Download and persist fresh PowerShell script
    Write-Host ""
    Write-Host "📥 Downloading latest PowerShell script..."
    try {
        $scriptContent = (Invoke-WebRequest -Uri "$script:CopilotHereReleaseUrl/copilot_here.ps1" -UseBasicParsing).Content
        try {
            Set-Content -Path $script:CopilotHereScriptPath -Value $scriptContent -Encoding UTF8 -Force
            Write-Host "✅ Update complete! Reloading PowerShell functions..."
            . $script:CopilotHereScriptPath
        } catch {
            Write-Host "✅ Update complete! Reloading PowerShell functions..."
            Invoke-Expression $scriptContent
            Write-Host ""
            Write-Host "⚠️  Could not write updated PowerShell script to: $script:CopilotHereScriptPath" -ForegroundColor Yellow
            Write-Host "   It may keep prompting to update until the file can be written." -ForegroundColor Yellow
        }
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
        if ($remoteScript -match '\$script:CopilotHereVersion\s*=\s*"(.+?)"') {
            $remoteVersion = $matches[1]
        }
        
        if (-not $remoteVersion) {
            return $false  # Couldn't parse version
        }
        
        if ($script:CopilotHereVersion -ne $remoteVersion) {
            # Compare versions - convert to comparable format
            $currentParts = $script:CopilotHereVersion.Split('.')
            $remoteParts = $remoteVersion.Split('.')
            
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
    $updateArgs = @("--update", "-u", "--upgrade", "--update-scripts", "--upgrade-scripts")
    return $updateArgs -contains $Arg
}

# Check if argument is a reset command
function Test-ResetArg {
    param([string]$Arg)
    $resetArgs = @("--reset")
    return $resetArgs -contains $Arg
}

# Safe Mode: Asks for confirmation before executing
function copilot_here {
    $Arguments = @($args)

    Write-CopilotDebug "=== copilot_here called with args: $Arguments"
    
    # Check if script file version differs from in-memory version
    $scriptPath = $script:CopilotHereScriptPath
    if (Test-Path $scriptPath) {
        try {
            $fileContent = Get-Content $scriptPath -Raw -ErrorAction SilentlyContinue
            if ($fileContent -match '\$script:CopilotHereVersion\s*=\s*"(.+?)"') {
                $fileVersion = $matches[1]
                if ($fileVersion -and $fileVersion -ne $script:CopilotHereVersion) {
                    $currentParts = $script:CopilotHereVersion.Split('.')
                    $fileParts = $fileVersion.Split('.')

                    $maxLen = [Math]::Max($currentParts.Length, $fileParts.Length)
                    while ($currentParts.Length -lt $maxLen) { $currentParts += "0" }
                    while ($fileParts.Length -lt $maxLen) { $fileParts += "0" }

                    $isNewer = $false
                    for ($i = 0; $i -lt $maxLen; $i++) {
                        $currentNum = [int]$currentParts[$i]
                        $fileNum = [int]$fileParts[$i]
                        if ($fileNum -gt $currentNum) {
                            $isNewer = $true
                            break
                        } elseif ($fileNum -lt $currentNum) {
                            break
                        }
                    }

                    if ($isNewer) {
                        Write-CopilotDebug "Newer on-disk script detected: in-memory=$script:CopilotHereVersion, file=$fileVersion"
                        Write-Host "🔄 Detected updated shell script (v$fileVersion), reloading..."
                        . $scriptPath
                        copilot_here @Arguments
                        return
                    }
                }
            }
        } catch {
            # Ignore errors reading file
        }
    }
    
    # Handle --update before binary check
    if ($Arguments | Where-Object { Test-UpdateArg $_ } | Select-Object -First 1) {
        Write-CopilotDebug "Update argument detected"
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments | Where-Object { Test-ResetArg $_ } | Select-Object -First 1) {
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
function copilot_yolo {
    $Arguments = @($args)

    Write-CopilotDebug "=== copilot_yolo called with args: $Arguments"
    
    # Check if script file version differs from in-memory version
    $scriptPath = $script:CopilotHereScriptPath
    if (Test-Path $scriptPath) {
        try {
            $fileContent = Get-Content $scriptPath -Raw -ErrorAction SilentlyContinue
            if ($fileContent -match '\$script:CopilotHereVersion\s*=\s*"(.+?)"') {
                $fileVersion = $matches[1]
                if ($fileVersion -and $fileVersion -ne $script:CopilotHereVersion) {
                    $currentParts = $script:CopilotHereVersion.Split('.')
                    $fileParts = $fileVersion.Split('.')

                    $maxLen = [Math]::Max($currentParts.Length, $fileParts.Length)
                    while ($currentParts.Length -lt $maxLen) { $currentParts += "0" }
                    while ($fileParts.Length -lt $maxLen) { $fileParts += "0" }

                    $isNewer = $false
                    for ($i = 0; $i -lt $maxLen; $i++) {
                        $currentNum = [int]$currentParts[$i]
                        $fileNum = [int]$fileParts[$i]
                        if ($fileNum -gt $currentNum) {
                            $isNewer = $true
                            break
                        } elseif ($fileNum -lt $currentNum) {
                            break
                        }
                    }

                    if ($isNewer) {
                        Write-CopilotDebug "Newer on-disk script detected: in-memory=$script:CopilotHereVersion, file=$fileVersion"
                        Write-Host "🔄 Detected updated shell script (v$fileVersion), reloading..."
                        . $scriptPath
                        copilot_yolo @Arguments
                        return
                    }
                }
            }
        } catch {
            # Ignore errors reading file
        }
    }
    
    # Handle --update before binary check
    if ($Arguments | Where-Object { Test-UpdateArg $_ } | Select-Object -First 1) {
        Write-CopilotDebug "Update argument detected"
        Update-CopilotHere
        return
    }
    
    # Handle --reset before binary check
    if ($Arguments | Where-Object { Test-ResetArg $_ } | Select-Object -First 1) {
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
