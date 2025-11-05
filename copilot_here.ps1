# copilot_here PowerShell functions
# Version: 2025-11-05.1
# Repository: https://github.com/GordonBeeming/copilot_here

# Helper function to detect emoji support (PowerShell typically supports it)
function Test-EmojiSupport {
    return $true  # PowerShell 5+ typically supports UTF-8/emojis
}

# Helper function to load mounts from config file
function Get-ConfigMounts {
    param([string]$ConfigFile)
    
    $mounts = @()
    if (Test-Path $ConfigFile) {
        Get-Content $ConfigFile | ForEach-Object {
            $line = $_.Trim()
            # Skip empty lines and comments
            if ($line -and -not $line.StartsWith('#')) {
                $mounts += $line
            }
        }
    }
    return $mounts
}

# Helper function to resolve and validate mount path
function Resolve-MountPath {
    param([string]$Path)
    
    # Expand environment variables and user home
    $resolvedPath = [System.Environment]::ExpandEnvironmentVariables($Path)
    $resolvedPath = $resolvedPath.Replace('~', $env:USERPROFILE)
    
    # Convert to absolute path if relative
    if (-not [System.IO.Path]::IsPathRooted($resolvedPath)) {
        $resolvedPath = Join-Path (Get-Location) $resolvedPath
    }
    
    # Normalize path separators for Docker (use forward slashes)
    $resolvedPath = $resolvedPath.Replace('\', '/')
    
    # Warn if path doesn't exist
    if (-not (Test-Path $resolvedPath.Replace('/', '\'))) {
        Write-Host "⚠️  Warning: Path does not exist: $resolvedPath" -ForegroundColor Yellow
    }
    
    # Security warning for sensitive paths - require confirmation
    $sensitivePatterns = @('^C:/$', '^C:/Windows', '^C:/Program Files', '/.ssh', '/AppData/Roaming')
    foreach ($pattern in $sensitivePatterns) {
        if ($resolvedPath -match $pattern) {
            Write-Host "⚠️  Warning: Mounting sensitive system path: $resolvedPath" -ForegroundColor Yellow
            $confirmation = Read-Host "Are you sure you want to mount this sensitive path? [y/N]"
            if ($confirmation.ToLower() -ne 'y' -and $confirmation.ToLower() -ne 'yes') {
                Write-Host "Operation cancelled by user." -ForegroundColor Yellow
                return $null
            }
            break
        }
    }
    
    return $resolvedPath
}

# Helper function to display mounts list
function Show-ConfiguredMounts {
    $globalConfig = "$env:USERPROFILE/.config/copilot_here/mounts.conf".Replace('\', '/')
    $localConfig = ".copilot_here/mounts.conf"
    
    $globalMounts = Get-ConfigMounts $globalConfig.Replace('/', '\')
    $localMounts = Get-ConfigMounts $localConfig
    
    if ($globalMounts.Count -eq 0 -and $localMounts.Count -eq 0) {
        Write-Host "📂 No saved mounts configured"
        Write-Host ""
        Write-Host "Add mounts with:"
        Write-Host "  Copilot-Here -SaveMount <path>         # Save to local config"
        Write-Host "  Copilot-Here -SaveMountGlobal <path>  # Save to global config"
        return
    }
    
    Write-Host "📂 Saved mounts:"
    
    $supportsEmoji = Test-EmojiSupport
    
    foreach ($mount in $globalMounts) {
        if ($supportsEmoji) {
            Write-Host "  🌍 $mount"
        } else {
            Write-Host "  G: $mount"
        }
    }
    
    foreach ($mount in $localMounts) {
        if ($supportsEmoji) {
            Write-Host "  📍 $mount"
        } else {
            Write-Host "  L: $mount"
        }
    }
    
    Write-Host ""
    Write-Host "Config files:"
    Write-Host "  Global: $globalConfig"
    Write-Host "  Local:  $localConfig"
}

# Helper function to save mount to config
function Save-MountToConfig {
    param(
        [string]$Path,
        [bool]$IsGlobal
    )
    
    $normalizedPath = $Path
    
    # Normalize path to absolute or ~/... format for global mounts
    if ($IsGlobal) {
        # Expand environment variables and tilde
        $expandedPath = [System.Environment]::ExpandEnvironmentVariables($Path)
        $expandedPath = $expandedPath.Replace('~', $env:USERPROFILE)
        
        # Convert to absolute if relative
        if (-not [System.IO.Path]::IsPathRooted($expandedPath)) {
            $expandedPath = Join-Path (Get-Location) $expandedPath
            $expandedPath = [System.IO.Path]::GetFullPath($expandedPath)
        }
        
        # If in user profile, convert to tilde format
        if ($expandedPath.StartsWith($env:USERPROFILE)) {
            $normalizedPath = "~" + $expandedPath.Substring($env:USERPROFILE.Length)
        } else {
            $normalizedPath = $expandedPath
        }
        
        # Normalize to forward slashes for consistency
        $normalizedPath = $normalizedPath.Replace('\', '/')
        
        $configFile = "$env:USERPROFILE/.config/copilot_here/mounts.conf".Replace('\', '/')
        $configDir = Split-Path $configFile.Replace('/', '\')
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }
    } else {
        # For local mounts, keep path as-is (relative is OK for project-specific)
        $configFile = ".copilot_here/mounts.conf"
        if (-not (Test-Path ".copilot_here")) {
            New-Item -ItemType Directory -Path ".copilot_here" -Force | Out-Null
        }
    }
    
    $configFilePath = $configFile.Replace('/', '\')
    
    # Check if already exists
    if ((Test-Path $configFilePath) -and (Select-String -Path $configFilePath -Pattern "^$([regex]::Escape($normalizedPath))$" -Quiet)) {
        Write-Host "⚠️  Mount already exists in config: $normalizedPath" -ForegroundColor Yellow
        return
    }
    
    Add-Content -Path $configFilePath -Value $normalizedPath
    
    if ($IsGlobal) {
        Write-Host "✅ Saved to global config: $normalizedPath" -ForegroundColor Green
        if ($normalizedPath -ne $Path) {
            Write-Host "   (normalized from: $Path)" -ForegroundColor Gray
        }
    } else {
        Write-Host "✅ Saved to local config: $normalizedPath" -ForegroundColor Green
    }
    Write-Host "   Config file: $configFile"
}

# Helper function to remove mount from config
function Remove-MountFromConfig {
    param([string]$Path)
    
    $globalConfig = "$env:USERPROFILE/.config/copilot_here/mounts.conf".Replace('/', '\')
    $localConfig = ".copilot_here/mounts.conf"
    $removed = $false
    
    # Try to remove from global config
    if ((Test-Path $globalConfig) -and (Select-String -Path $globalConfig -Pattern "^$([regex]::Escape($Path))$" -Quiet)) {
        (Get-Content $globalConfig) | Where-Object { $_ -ne $Path } | Set-Content $globalConfig
        Write-Host "✅ Removed from global config: $Path" -ForegroundColor Green
        $removed = $true
    }
    
    # Try to remove from local config
    if ((Test-Path $localConfig) -and (Select-String -Path $localConfig -Pattern "^$([regex]::Escape($Path))$" -Quiet)) {
        (Get-Content $localConfig) | Where-Object { $_ -ne $Path } | Set-Content $localConfig
        Write-Host "✅ Removed from local config: $Path" -ForegroundColor Green
        $removed = $true
    }
    
    if (-not $removed) {
        Write-Host "⚠️  Mount not found in any config: $Path" -ForegroundColor Yellow
    }
}

# Helper function for security checks (shared by all variants)
function Test-CopilotSecurityCheck {
    Write-Host "Verifying GitHub CLI authentication..."
    $authStatus = gh auth status 2>$null
    if (-not ($authStatus | Select-String -Quiet "'copilot'")) {
        Write-Host "❌ Error: Your gh token is missing the required 'copilot' scope." -ForegroundColor Red
        Write-Host "Please run 'gh auth refresh -h github.com -s copilot' to add it."
        return $false
    }

    $privilegedScopesPattern = "'(admin:|manage_|write:public_key|delete_repo|(write|delete)_packages)'"
    if ($authStatus | Select-String -Quiet $privilegedScopesPattern) {
        Write-Host "⚠️  Warning: Your GitHub token has highly privileged scopes." -ForegroundColor Yellow
        $confirmation = Read-Host "Are you sure you want to proceed with this token? [y/N]"
        if ($confirmation.ToLower() -ne 'y' -and $confirmation.ToLower() -ne 'yes') {
            Write-Host "Operation cancelled by user."
            return $false
        }
    }
    Write-Host "✅ Security checks passed."
    return $true
}

# Helper function to cleanup unused copilot_here images
function Remove-UnusedCopilotImages {
    param([string]$KeepImage)
    
    Write-Host "🧹 Cleaning up old copilot_here images (older than 7 days)..."
    
    # Get cutoff timestamp (7 days ago)
    $cutoffDate = (Get-Date).AddDays(-7)
    
    # Get all copilot_here images with the project label, excluding <none> tags
    $allImages = docker images --filter "label=project=copilot_here" --format "{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" 2>$null
    if (-not $allImages) {
        Write-Host "  ✓ No images to clean up"
        return
    }
    
    $imagesToProcess = $allImages | Where-Object { $_ -notmatch ':<none>' }
    if (-not $imagesToProcess) {
        Write-Host "  ✓ No images to clean up"
        return
    }
    
    $count = 0
    foreach ($imageInfo in $imagesToProcess) {
        $parts = $imageInfo -split '\|'
        $image = $parts[0]
        $createdAt = $parts[1]
        
        if ($image -ne $KeepImage) {
            # Parse creation date (format: "2025-01-28 12:34:56 +0000 UTC")
            try {
                $imageDate = [DateTime]::Parse($createdAt.Substring(0, 19))
                
                if ($imageDate -lt $cutoffDate) {
                    Write-Host "  🗑️  Removing old image: $image (created: $createdAt)"
                    $result = docker rmi $image 2>$null
                    if ($LASTEXITCODE -eq 0) {
                        $count++
                    } else {
                        Write-Host "  ⚠️  Failed to remove: $image (may be in use)"
                    }
                }
            } catch {
                # Skip if date parsing fails
            }
        }
    }
    
    if ($count -eq 0) {
        Write-Host "  ✓ No old images to clean up"
    } else {
        Write-Host "  ✓ Cleaned up $count old image(s)"
    }
}

# Helper function to pull image with spinner (shared by all variants)
function Get-CopilotImage {
    param([string]$ImageName)
    
    Write-Host -NoNewline "📥 Pulling latest image: ${ImageName}... "
    $pullJob = Start-Job -ScriptBlock { param($img) docker pull $img } -ArgumentList $ImageName
    $spinner = '|', '/', '-', '\'
    $i = 0
    while ($pullJob.State -eq 'Running') {
        Write-Host -NoNewline "$($spinner[$i])`b"
        $i = ($i + 1) % 4
        Start-Sleep -Milliseconds 100
    }

    Wait-Job $pullJob | Out-Null
    $pullOutput = Receive-Job $pullJob
    
    if ($pullJob.State -eq 'Completed') {
        Write-Host "✅"
        Remove-Job $pullJob
        return $true
    } else {
        Write-Host "❌" -ForegroundColor Red
        Write-Host "Error: Failed to pull the Docker image." -ForegroundColor Red
        if (-not [string]::IsNullOrEmpty($pullOutput)) {
            Write-Host "Docker output:`n$pullOutput"
        }
        Remove-Job $pullJob
        return $false
    }
}

# Core function to run copilot (shared by all variants)
function Invoke-CopilotRun {
    param(
        [string]$ImageTag,
        [bool]$AllowAllTools,
        [bool]$SkipCleanup,
        [bool]$SkipPull,
        [string[]]$MountsRO,
        [string[]]$MountsRW,
        [string[]]$Arguments
    )
    
    if (-not (Test-CopilotSecurityCheck)) { return }
    
    $imageName = "ghcr.io/gordonbeeming/copilot_here:$ImageTag"
    
    Write-Host "🚀 Using image: ${imageName}"
    
    # Pull latest image unless skipped
    if (-not $SkipPull) {
        if (-not (Get-CopilotImage -ImageName $imageName)) { return }
    } else {
        Write-Host "⏭️  Skipping image pull"
    }
    
    # Cleanup old images unless skipped
    if (-not $SkipCleanup) {
        Remove-UnusedCopilotImages -KeepImage $imageName
    } else {
        Write-Host "⏭️  Skipping image cleanup"
    }

    $copilotConfigPath = Join-Path $env:USERPROFILE ".config\copilot-cli-docker"
    if (-not (Test-Path $copilotConfigPath)) {
        New-Item -Path $copilotConfigPath -ItemType Directory -Force | Out-Null
    }

    $token = gh auth token 2>$null
    if ([string]::IsNullOrEmpty($token)) {
        Write-Host "⚠️  Could not retrieve token using 'gh auth token'." -ForegroundColor Yellow
    }

    $currentDir = (Get-Location).Path.Replace('\', '/')
    $dockerBaseArgs = @(
        "--rm", "-it",
        "-v", "$($currentDir):$($currentDir)",
        "-w", $currentDir,
        "-v", "$($copilotConfigPath.Replace('\', '/')):/home/appuser/.copilot",
        "-e", "GITHUB_TOKEN=$token"
    )

    # Load mounts from config files
    $globalConfig = "$env:USERPROFILE/.config/copilot_here/mounts.conf".Replace('\', '/')
    $localConfig = ".copilot_here/mounts.conf"
    
    $configMounts = @()
    $configMounts += Get-ConfigMounts $globalConfig.Replace('/', '\')
    $configMounts += Get-ConfigMounts $localConfig
    
    # Track all mounted paths for display and --add-dir
    $allMountPaths = @()
    $mountDisplay = @()
    $seenPaths = @{}
    
    # Add current working directory to display
    $mountDisplay += "📁 $currentDir"
    
    # Process config mounts
    foreach ($mount in $configMounts) {
        $mountPath = $mount
        $mountMode = "ro"
        
        if ($mount.Contains(':')) {
            $parts = $mount -split ':'
            $mountPath = $parts[0]
            $mountMode = $parts[1]
        }
        
        $resolvedPath = Resolve-MountPath $mountPath
        if ($null -eq $resolvedPath) {
            continue  # Skip this mount if user cancelled
        }
        
        # Skip if already seen (dedup)
        if ($seenPaths.ContainsKey($resolvedPath)) {
            continue
        }
        $seenPaths[$resolvedPath] = $mountMode
        
        $dockerBaseArgs += "-v", "$($resolvedPath):$($resolvedPath):$mountMode"
        $allMountPaths += $resolvedPath
        
        # Determine source for display
        $sourceIcon = if (Test-EmojiSupport) {
            if ((Test-Path $globalConfig.Replace('/', '\')) -and (Select-String -Path $globalConfig.Replace('/', '\') -Pattern ([regex]::Escape($mount)) -Quiet)) { "🌍" } else { "📍" }
        } else {
            if ((Test-Path $globalConfig.Replace('/', '\')) -and (Select-String -Path $globalConfig.Replace('/', '\') -Pattern ([regex]::Escape($mount)) -Quiet)) { "G:" } else { "L:" }
        }
        
        $mountDisplay += "   $sourceIcon $resolvedPath ($mountMode)"
    }
    
    # Process CLI read-only mounts
    foreach ($mountPath in $MountsRO) {
        $resolvedPath = Resolve-MountPath $mountPath
        if ($null -eq $resolvedPath) {
            continue  # Skip this mount if user cancelled
        }
        
        # Skip if already seen
        if ($seenPaths.ContainsKey($resolvedPath)) {
            continue
        }
        $seenPaths[$resolvedPath] = "ro"
        
        $dockerBaseArgs += "-v", "$($resolvedPath):$($resolvedPath):ro"
        $allMountPaths += $resolvedPath
        
        $icon = if (Test-EmojiSupport) { "🔧" } else { "CLI:" }
        $mountDisplay += "   $icon $resolvedPath (ro)"
    }
    
    # Process CLI read-write mounts
    foreach ($mountPath in $MountsRW) {
        $resolvedPath = Resolve-MountPath $mountPath
        if ($null -eq $resolvedPath) {
            continue  # Skip this mount if user cancelled
        }
        
        # Update to rw if already mounted as ro
        if ($seenPaths.ContainsKey($resolvedPath)) {
            $seenPaths[$resolvedPath] = "rw"
            # Find and update the docker arg
            for ($i = 0; $i -lt $dockerBaseArgs.Count; $i++) {
                if ($dockerBaseArgs[$i] -eq "-v" -and $dockerBaseArgs[$i+1] -like "$resolvedPath*:ro") {
                    $dockerBaseArgs[$i+1] = "$($resolvedPath):$($resolvedPath):rw"
                    break
                }
            }
        } else {
            $seenPaths[$resolvedPath] = "rw"
            $dockerBaseArgs += "-v", "$($resolvedPath):$($resolvedPath):rw"
            $allMountPaths += $resolvedPath
        }
        
        $icon = if (Test-EmojiSupport) { "🔧" } else { "CLI:" }
        $mountDisplay += "   $icon $resolvedPath (rw)"
    }
    
    # Display mounts if there are extras
    if ($allMountPaths.Count -gt 0) {
        Write-Host "📂 Mounts:"
        foreach ($display in $mountDisplay) {
            Write-Host $display
        }
    }
    
    $dockerBaseArgs += $imageName

    $copilotCommand = @("copilot")
    
    # Add --allow-all-tools and --allow-all-paths if in YOLO mode
    if ($AllowAllTools) {
        $copilotCommand += "--allow-all-tools", "--allow-all-paths"
        # In YOLO mode, also add current dir and mounts to avoid any prompts
        $copilotCommand += "--add-dir", $currentDir
        foreach ($mountPath in $allMountPaths) {
            $copilotCommand += "--add-dir", $mountPath
        }
    }
    # In Safe Mode, don't auto-add directories - let Copilot CLI ask for permission
    
    # If no arguments provided, start interactive mode with banner
    if ($Arguments.Length -eq 0) {
        $copilotCommand += "--banner"
    } else {
        # Pass all arguments directly to copilot for maximum flexibility
        $copilotCommand += $Arguments
    }

    $finalDockerArgs = $dockerBaseArgs + $copilotCommand
    docker run $finalDockerArgs
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param (
        [switch]$h,
        [switch]$Help,
        [switch]$d,
        [switch]$Dotnet,
        [switch]$dp,
        [switch]$DotnetPlaywright,
        [string[]]$Mount,
        [string[]]$MountRW,
        [switch]$ListMounts,
        [string]$SaveMount,
        [string]$SaveMountGlobal,
        [string]$RemoveMount,
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$UpdateScripts,
        [switch]$UpgradeScripts,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )

    # Handle mount management commands
    if ($ListMounts) {
        Show-ConfiguredMounts
        return
    }
    
    if ($SaveMount) {
        Save-MountToConfig -Path $SaveMount -IsGlobal $false
        return
    }
    
    if ($SaveMountGlobal) {
        Save-MountToConfig -Path $SaveMountGlobal -IsGlobal $true
        return
    }
    
    if ($RemoveMount) {
        Remove-MountFromConfig -Path $RemoveMount
        return
    }

    if ($UpdateScripts -or $UpgradeScripts) {
        Write-Host "📦 Updating copilot_here scripts from GitHub..."
        
        # Get current version
        $currentVersion = ""
        $standalonePath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
        if (Test-Path $standalonePath) {
            $currentVersion = (Get-Content $standalonePath -TotalCount 2)[1] -replace '# Version: ', ''
        } elseif (Get-Command Copilot-Here -ErrorAction SilentlyContinue) {
            $currentVersion = (Get-Command Copilot-Here).ScriptBlock.ToString() -match '# Version: (.+)' | Out-Null; $matches[1]
        }
        
        # Check for standalone file
        if (Test-Path $standalonePath) {
            # Check if it's a symlink (junction/symbolic link)
            $targetFile = $standalonePath
            $item = Get-Item $standalonePath -Force
            if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
                $targetFile = $item.Target
                Write-Host "🔗 Symlink detected, updating target: $targetFile"
            }
            
            # Download to temp first to check version
            $tempScript = Join-Path $env:TEMP "copilot_here_update.ps1"
            try {
                Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $tempScript
            } catch {
                Write-Host "❌ Failed to download: $_" -ForegroundColor Red
                return
            }
            
            $newVersion = (Get-Content $tempScript -TotalCount 2)[1] -replace '# Version: ', ''
            
            if ($currentVersion -and $newVersion) {
                Write-Host "📌 Version: $currentVersion → $newVersion"
            }
            
            # Update the actual file (following symlinks)
            Move-Item $tempScript $targetFile -Force
            Write-Host "✅ Scripts updated successfully!"
            Write-Host "🔄 Reloading..."
            . $standalonePath
            Write-Host "✨ Update complete! You're now on version $newVersion"
            return
        }
            Write-Host "❌ Failed to download: $_" -ForegroundColor Red
            return
        }
        
        $newVersion = (Get-Content $tempScript -TotalCount 2)[1] -replace '# Version: ', ''
        
        if ($currentVersion -and $newVersion) {
            Write-Host "📌 Version: $currentVersion → $newVersion"
        }
        
        # Backup
        $backupPath = "$PROFILE.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item $PROFILE $backupPath
        Write-Host "✅ Created backup: $backupPath"
        
        # Replace
        $profileContent = Get-Content $PROFILE -Raw
        if ($profileContent -match '# copilot_here PowerShell functions') {
            $newProfile = $profileContent -replace '(?s)# copilot_here PowerShell functions.*?Set-Alias -Name copilot_yolo -Value Copilot-Yolo', (Get-Content $tempScript -Raw)
            Set-Content $PROFILE $newProfile
            Write-Host "✅ Scripts updated!"
        } else {
            Add-Content $PROFILE "`n$(Get-Content $tempScript -Raw)"
            Write-Host "✅ Scripts added!"
        }
        
        Remove-Item $tempScript
        Write-Host "🔄 Reloading..."
        . $PROFILE
        Write-Host "✨ Update complete! You're now on version $newVersion"
        return
    }

    if ($h -or $Help) {
        Write-Host @"
copilot_here - GitHub Copilot CLI in a secure Docker container (Safe Mode)

USAGE:
  Copilot-Here [OPTIONS] [COPILOT_ARGS]
  Copilot-Here [MOUNT_MANAGEMENT]

OPTIONS:
  -d, -Dotnet              Use .NET image variant
  -dp, -DotnetPlaywright   Use .NET + Playwright image variant
  -Mount <path>            Mount additional directory (read-only)
  -MountRW <path>          Mount additional directory (read-write)
  -NoCleanup               Skip cleanup of unused Docker images
  -NoPull                  Skip pulling the latest image
  -UpdateScripts           Update scripts from GitHub repository
  -UpgradeScripts          Alias for -UpdateScripts
  -h, -Help                Show this help message

MOUNT MANAGEMENT:
  -ListMounts              Show all configured mounts
  -SaveMount <path>        Save mount to local config (.copilot_here/mounts.conf)
  -SaveMountGlobal <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  -RemoveMount <path>      Remove mount from configs

MOUNT CONFIG:
  Mounts can be configured in three ways (priority: CLI > Local > Global):
 1. Global: ~/.config/copilot_here/mounts.conf
 2. Local:  .copilot_here/mounts.conf
 3. CLI:    -Mount and -MountRW parameters
  
  Config file format (one path per line):
 ~/investigations:ro
 ~/notes:rw
 /data/research

COPILOT_ARGS:
  All standard GitHub Copilot CLI arguments are supported:
 -p, --prompt <text>     Execute a prompt directly
 --model <model>         Set AI model (claude-sonnet-4.5, gpt-5, etc.)
 --continue              Resume most recent session
 --resume [sessionId]    Resume from a previous session
 --log-level <level>     Set log level (none, error, warning, info, debug)
 --add-dir <directory>   Add directory to allowed list
 --allow-tool <tools>    Allow specific tools
 --deny-tool <tools>     Deny specific tools
 ... and more (run "copilot -h" for full list)

EXAMPLES:
  # Interactive mode
  Copilot-Here
  
  # Mount additional directories
  Copilot-Here -Mount ../investigations -p "analyze these files"
  Copilot-Here -MountRW ~/notes -Mount /data/research
  
  # Save mounts for reuse
  Copilot-Here -SaveMount ~/investigations
  Copilot-Here -SaveMountGlobal ~/common-data
  Copilot-Here -ListMounts
  
  # Ask a question
  Copilot-Here -p "how do I list files in PowerShell?"
  
  # Use specific AI model
  Copilot-Here --model gpt-5 -p "explain this code"
  
  # Resume previous session
  Copilot-Here --continue
  
  # Use .NET image
  Copilot-Here -d -p "build this .NET project"
  
  # Fast mode (skip cleanup and pull)
  Copilot-Here -NoCleanup -NoPull -p "quick question"

MODES:
  Copilot-Here  - Safe mode (asks for confirmation before executing)
  Copilot-Yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-05.1
REPOSITORY: https://github.com/GordonBeeming/copilot_here
"@
        return
    }

    $imageTag = "latest"
    if ($d -or $Dotnet) {
        $imageTag = "dotnet"
    } elseif ($dp -or $DotnetPlaywright) {
        $imageTag = "dotnet-playwright"
    }
    
    # Initialize mount arrays if not provided
    if (-not $Mount) { $Mount = @() }
    if (-not $MountRW) { $MountRW = @() }
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $false -SkipCleanup $NoCleanup -SkipPull $NoPull -MountsRO $Mount -MountsRW $MountRW -Arguments $Prompt
}

# YOLO Mode: Auto-approves all tool usage
function Copilot-Yolo {
    [CmdletBinding()]
    param (
        [switch]$h,
        [switch]$Help,
        [switch]$d,
        [switch]$Dotnet,
        [switch]$dp,
        [switch]$DotnetPlaywright,
        [string[]]$Mount,
        [string[]]$MountRW,
        [switch]$ListMounts,
        [string]$SaveMount,
        [string]$SaveMountGlobal,
        [string]$RemoveMount,
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$UpdateScripts,
        [switch]$UpgradeScripts,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )

    # Handle mount management commands
    if ($ListMounts) {
        Show-ConfiguredMounts
        return
    }
    
    if ($SaveMount) {
        Save-MountToConfig -Path $SaveMount -IsGlobal $false
        return
    }
    
    if ($SaveMountGlobal) {
        Save-MountToConfig -Path $SaveMountGlobal -IsGlobal $true
        return
    }
    
    if ($RemoveMount) {
        Remove-MountFromConfig -Path $RemoveMount
        return
    }

    if ($UpdateScripts -or $UpgradeScripts) {
        Write-Host "📦 Updating copilot_here scripts from GitHub..."
        
        # Get current version
        $currentVersion = ""
        $standalonePath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
        if (Test-Path $standalonePath) {
            $currentVersion = (Get-Content $standalonePath -TotalCount 2)[1] -replace '# Version: ', ''
        } elseif (Get-Command Copilot-Here -ErrorAction SilentlyContinue) {
            $currentVersion = (Get-Command Copilot-Here).ScriptBlock.ToString() -match '# Version: (.+)' | Out-Null; $matches[1]
        }
        
        # Check for standalone file
        if (Test-Path $standalonePath) {
            # Check if it's a symlink (junction/symbolic link)
            $targetFile = $standalonePath
            $item = Get-Item $standalonePath -Force
            if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
                $targetFile = $item.Target
                Write-Host "🔗 Symlink detected, updating target: $targetFile"
            }
            
            # Download to temp first to check version
            $tempScript = Join-Path $env:TEMP "copilot_here_update.ps1"
            try {
                Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $tempScript
            } catch {
                Write-Host "❌ Failed to download: $_" -ForegroundColor Red
                return
            }
            
            $newVersion = (Get-Content $tempScript -TotalCount 2)[1] -replace '# Version: ', ''
            
            if ($currentVersion -and $newVersion) {
                Write-Host "📌 Version: $currentVersion → $newVersion"
            }
            
            # Update the actual file (following symlinks)
            Move-Item $tempScript $targetFile -Force
            Write-Host "✅ Scripts updated successfully!"
            Write-Host "🔄 Reloading..."
            . $standalonePath
            Write-Host "✨ Update complete! You're now on version $newVersion"
            return
        }
            Write-Host "❌ Failed to download: $_" -ForegroundColor Red
            return
        }
        
        $newVersion = (Get-Content $tempScript -TotalCount 2)[1] -replace '# Version: ', ''
        
        if ($currentVersion -and $newVersion) {
            Write-Host "📌 Version: $currentVersion → $newVersion"
        }
        
        # Backup
        $backupPath = "$PROFILE.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item $PROFILE $backupPath
        Write-Host "✅ Created backup: $backupPath"
        
        # Replace
        $profileContent = Get-Content $PROFILE -Raw
        if ($profileContent -match '# copilot_here PowerShell functions') {
            $newProfile = $profileContent -replace '(?s)# copilot_here PowerShell functions.*?Set-Alias -Name copilot_yolo -Value Copilot-Yolo', (Get-Content $tempScript -Raw)
            Set-Content $PROFILE $newProfile
            Write-Host "✅ Scripts updated!"
        } else {
            Add-Content $PROFILE "`n$(Get-Content $tempScript -Raw)"
            Write-Host "✅ Scripts added!"
        }
        
        Remove-Item $tempScript
        Write-Host "🔄 Reloading..."
        . $PROFILE
        Write-Host "✨ Update complete! You're now on version $newVersion"
        return
    }

    if ($h -or $Help) {
        Write-Host @"
copilot_yolo - GitHub Copilot CLI in a secure Docker container (YOLO Mode)

USAGE:
  Copilot-Yolo [OPTIONS] [COPILOT_ARGS]
  Copilot-Yolo [MOUNT_MANAGEMENT]

OPTIONS:
  -d, -Dotnet              Use .NET image variant
  -dp, -DotnetPlaywright   Use .NET + Playwright image variant
  -Mount <path>            Mount additional directory (read-only)
  -MountRW <path>          Mount additional directory (read-write)
  -NoCleanup               Skip cleanup of unused Docker images
  -NoPull                  Skip pulling the latest image
  -UpdateScripts           Update scripts from GitHub repository
  -UpgradeScripts          Alias for -UpdateScripts
  -h, -Help                Show this help message

MOUNT MANAGEMENT:
  -ListMounts              Show all configured mounts
  -SaveMount <path>        Save mount to local config (.copilot_here/mounts.conf)
  -SaveMountGlobal <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  -RemoveMount <path>      Remove mount from configs

COPILOT_ARGS:
  All standard GitHub Copilot CLI arguments are supported:
 -p, --prompt <text>     Execute a prompt directly
 --model <model>         Set AI model (claude-sonnet-4.5, gpt-5, etc.)
 --continue              Resume most recent session
 --resume [sessionId]    Resume from a previous session
 --log-level <level>     Set log level (none, error, warning, info, debug)
 --add-dir <directory>   Add directory to allowed list
 --allow-tool <tools>    Allow specific tools
 --deny-tool <tools>     Deny specific tools
 ... and more (run "copilot -h" for full list)

EXAMPLES:
  # Interactive mode (auto-approves all)
  Copilot-Yolo
  
  # Execute without confirmation
  Copilot-Yolo -p "run the tests and fix failures"
  
  # Mount additional directories
  Copilot-Yolo -Mount ../data -p "analyze all data"
  
  # Use specific model
  Copilot-Yolo --model gpt-5 -p "optimize this code"
  
  # Resume session
  Copilot-Yolo --continue
  
  # Use .NET + Playwright image
  Copilot-Yolo -dp -p "write playwright tests"
  
  # Fast mode (skip cleanup)
  Copilot-Yolo -NoCleanup -p "generate README"

WARNING:
  YOLO mode automatically approves ALL tool usage without confirmation AND
  disables file path verification (--allow-all-tools + --allow-all-paths).
  Use with caution and only in trusted environments.

MODES:
  Copilot-Here  - Safe mode (asks for confirmation before executing)
  Copilot-Yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-05.1
REPOSITORY: https://github.com/GordonBeeming/copilot_here
"@
        return
    }

    $imageTag = "latest"
    if ($d -or $Dotnet) {
        $imageTag = "dotnet"
    } elseif ($dp -or $DotnetPlaywright) {
        $imageTag = "dotnet-playwright"
    }
    
    # Initialize mount arrays if not provided
    if (-not $Mount) { $Mount = @() }
    if (-not $MountRW) { $MountRW = @() }
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $true -SkipCleanup $NoCleanup -SkipPull $NoPull -MountsRO $Mount -MountsRW $MountRW -Arguments $Prompt
}

Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
notepad $PROFILE
. C:\Users\YourName\Documents\PowerShell\copilot_here.ps1
. $PROFILE
