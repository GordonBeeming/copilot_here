# copilot_here PowerShell functions
# Version: 2025.12.29.19
# Repository: https://github.com/GordonBeeming/copilot_here

# Set console output encoding to UTF-8 for Unicode character support
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Configuration
$script:CopilotHereHome = if ($env:USERPROFILE) { 
    $env:USERPROFILE 
} elseif ($env:HOME) { 
    $env:HOME 
} else { 
    [Environment]::GetFolderPath('UserProfile') 
}

$script:CopilotHereScriptPath = Join-Path $script:CopilotHereHome ".copilot_here.ps1"

$script:DefaultCopilotHereBinDir = Join-Path (Join-Path $script:CopilotHereHome ".local") "bin"
# Detect OS: Windows has USERPROFILE, Linux/macOS have HOME but not USERPROFILE
$script:DefaultCopilotHereBinName = if ($env:USERPROFILE) { "copilot_here.exe" } else { "copilot_here" }
$script:DefaultCopilotHereBin = Join-Path $script:DefaultCopilotHereBinDir $script:DefaultCopilotHereBinName

$script:CopilotHereBin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { $script:DefaultCopilotHereBin }
$script:CopilotHereReleaseUrl = "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"
$script:CopilotHereVersion = "2025.12.29.19"

# Debug logging function
function Write-CopilotDebug {
    param([string]$Message)
    if ($env:COPILOT_HERE_DEBUG -eq "1" -or $env:COPILOT_HERE_DEBUG -eq "true") {
        Write-Host "[DEBUG] $Message" -ForegroundColor DarkGray
    }
}

# Helper function to stop running containers with confirmation
function Stop-CopilotContainers {
    $runningContainers = docker ps --filter "name=copilot_here-" -q 2>&1 | Out-Null; if ($LASTEXITCODE -eq 0) { docker ps --filter "name=copilot_here-" -q }
    
    if ($runningContainers) {
        Write-Host "[WARNING]  copilot_here is currently running in Docker" -ForegroundColor Yellow
        $response = Read-Host "   Stop running containers to continue? [y/N]"
        if ($response -match '^[yY]') {
            Write-Host "[STOP] Stopping copilot_here containers..."
            docker stop $runningContainers 2>&1 | Out-Null
            Write-Host "   [OK] Stopped"
            return $true
        } else {
            Write-Host "[ERROR] Cannot update while containers are running (binary is in use)" -ForegroundColor Red
            return $false
        }
    }
    return $true
}

# Helper function to download and install binary
function Download-CopilotHereBinary {
    # Detect architecture using environment variable (works in all PowerShell versions)
    $procArch = $env:PROCESSOR_ARCHITECTURE
    $arch = if ($procArch -eq "ARM64" -or $procArch -eq "ARM") { 
        "arm64" 
    } elseif ($procArch -eq "AMD64" -or $procArch -eq "x64") {
        "x64"
    } else { 
        "x64"  # Default fallback
    }
    
    # Create bin directory
    $binDir = Split-Path $script:CopilotHereBin
    if (-not (Test-Path $binDir)) {
        New-Item -ItemType Directory -Path $binDir -Force | Out-Null
    }
    
    # Detect OS: Windows has USERPROFILE, macOS has specific uname, Linux is the rest
    $os = if ($env:USERPROFILE) {
        "win"
    } else {
        # Try to detect macOS with uname command
        try {
            $unameOutput = & uname 2>&1
            if ($LASTEXITCODE -eq 0 -and $unameOutput -eq "Darwin") { "macos" } else { "linux" }
        } catch {
            "linux"
        }
    }
    $ext = if ($os -eq "win") { "zip" } else { "tar.gz" }

    # Download latest release archive
    $downloadUrl = "$script:CopilotHereReleaseUrl/copilot_here-${os}-${arch}.${ext}"
    $tmpBase = [System.IO.Path]::GetTempFileName()
    Remove-Item -Path $tmpBase -ErrorAction SilentlyContinue
    $tmpArchive = $tmpBase + ".${ext}"
    
    Write-Host "[DOWNLOAD] Downloading binary from: $downloadUrl"
    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tmpArchive -UseBasicParsing
    } catch {
        Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
        Write-Host "[ERROR] Failed to download binary: $_" -ForegroundColor Red
        return $false
    }
    
    # Extract binary from archive
    try {
        if ($env:USERPROFILE) {
            # Windows (PowerShell 5.1 or Core)
            Expand-Archive -Path $tmpArchive -DestinationPath $binDir -Force
        } else {
            # Linux/macOS
            & tar -xzf $tmpArchive -C $binDir copilot_here
            if ($LASTEXITCODE -ne 0) { throw "tar extraction failed" }
            & chmod +x $script:CopilotHereBin 2>&1 | Out-Null
        }
    } catch {
        Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
        Write-Host "[ERROR] Failed to extract binary: $_" -ForegroundColor Red
        return $false
    }

    Remove-Item -Path $tmpArchive -ErrorAction SilentlyContinue
    Write-Host "[OK] Binary installed to: $script:CopilotHereBin"
    return $true
}

# Helper function to ensure binary is installed
function Ensure-CopilotHereBinary {
    if (-not (Test-Path $script:CopilotHereBin)) {
        Write-Host "[INSTALL] copilot_here binary not found. Installing..."
        return Download-CopilotHereBinary
    }
    
    return $true
}

# Update function - downloads fresh binary and script
function Update-CopilotHere {
    Write-Host "[RELOAD] Updating copilot_here..."
    
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
    Write-Host "[INSTALL] Downloading latest binary..."
    if (-not (Download-CopilotHereBinary)) {
        Write-Host "[ERROR] Failed to download binary" -ForegroundColor Red
        return $false
    }
    
    # Download and persist fresh PowerShell script
    Write-Host ""
    Write-Host "[INSTALL] Downloading latest PowerShell script..."
    try {
        $scriptContent = (Invoke-WebRequest -Uri "$script:CopilotHereReleaseUrl/copilot_here.ps1" -UseBasicParsing).Content
        try {
            Set-Content -Path $script:CopilotHereScriptPath -Value $scriptContent -Encoding UTF8 -Force
            
            # Function to update a profile file
            function Update-SingleProfile {
                param([string]$ProfilePath)
                
                if (-not (Test-Path $ProfilePath)) { return $false }
                
                $profileContent = Get-Content $ProfilePath -Raw -ErrorAction SilentlyContinue
                if ([string]::IsNullOrEmpty($profileContent)) { return $false }
                
                # Remove all existing copilot_here.ps1 references
                $oldContent = $profileContent
                $profileContent = $profileContent -replace '(?m)^.*copilot_here\.ps1.*$\r?\n?', ''
                $profileContent = $profileContent.TrimEnd()
                
                # Add the new reference if not present
                $newEntry = ". `"$script:CopilotHereScriptPath`""
                $changed = $false
                if (-not $profileContent.Contains($newEntry)) {
                    $profileContent = $profileContent + "`n`n$newEntry"
                    $changed = $true
                } elseif ($oldContent -ne $profileContent) {
                    $changed = $true
                }
                
                if ($changed) {
                    Set-Content -Path $ProfilePath -Value $profileContent.TrimStart()
                }
                
                return $changed
            }
            
            # Clean up old profile entries in both PowerShell profiles
            Write-Host "[PROFILE] Cleaning up old entries in PowerShell profiles..."
            $pwshProfile = "$env:USERPROFILE\Documents\PowerShell\Microsoft.PowerShell_profile.ps1"
            $winPsProfile = "$env:USERPROFILE\Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1"
            
            $updated = $false
            if (Update-SingleProfile -ProfilePath $pwshProfile) {
                Write-Host "[OK] Updated PowerShell Core profile"
                $updated = $true
            }
            if (Update-SingleProfile -ProfilePath $winPsProfile) {
                Write-Host "[OK] Updated Windows PowerShell profile"
                $updated = $true
            }
            
            if (-not $updated) {
                Write-Host "[OK] Profiles already up to date"
            }
            
            Write-Host "[OK] Update complete! Reloading PowerShell functions..."
            . $script:CopilotHereScriptPath
        } catch {
            Write-Host "[OK] Update complete! Reloading PowerShell functions..."
            Invoke-Expression $scriptContent
            Write-Host ""
            Write-Host "[WARNING]  Could not write updated PowerShell script to: $script:CopilotHereScriptPath" -ForegroundColor Yellow
            Write-Host "   It may keep prompting to update until the file can be written." -ForegroundColor Yellow
        }
    } catch {
        Write-Host ""
        Write-Host "[OK] Binary updated!"
        Write-Host ""
        Write-Host "[WARNING]  Could not auto-reload PowerShell functions. Please re-import manually:" -ForegroundColor Yellow
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
                Write-Host "[UPDATE] Update available: $script:CopilotHereVersion -> $remoteVersion"
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
                        Write-Host "[RELOAD] Detected updated shell script (v$fileVersion), reloading..."
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
                        Write-Host "[RELOAD] Detected updated shell script (v$fileVersion), reloading..."
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
