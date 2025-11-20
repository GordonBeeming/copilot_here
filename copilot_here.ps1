# copilot_here PowerShell functions
# Version: 2025-11-20.14
# Repository: https://github.com/GordonBeeming/copilot_here

# Test mode flag (set by tests to skip auth checks)
if (-not $env:COPILOT_HERE_TEST_MODE) {
    $env:COPILOT_HERE_TEST_MODE = "false"
}

# Helper function to detect emoji support (PowerShell typically supports it)
function Test-EmojiSupport {
    return $true  # PowerShell 5+ typically supports UTF-8/emojis
}

# Helper function to load mounts from config file
function Get-ConfigMounts {
    param([string]$ConfigFile)
    
    $actualFile = $ConfigFile
    
    # Follow symlink if config file is a symlink
    if (Test-Path $ConfigFile) {
        $item = Get-Item $ConfigFile -Force
        if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
            $actualFile = $item.Target
            if ($actualFile -is [Array]) {
                $actualFile = $actualFile[0]
            }
        }
    }
    
    $mounts = @()
    if (Test-Path $actualFile) {
        Get-Content $actualFile | ForEach-Object {
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
    $resolvedPath = $resolvedPath.Trim()  # Remove any leading/trailing whitespace
    
    # Handle home directory expansion (both Windows and Linux)
    # Only replace ~ at the start of the path to avoid corrupting paths like RUNNER~1
    if ($resolvedPath.StartsWith('~')) {
        if ($env:USERPROFILE) {
            $resolvedPath = $env:USERPROFILE + $resolvedPath.Substring(1)
        } elseif ($env:HOME) {
            $resolvedPath = $env:HOME + $resolvedPath.Substring(1)
        }
    }
    
    # Convert to absolute path if relative
    # Check for both Unix-style (/) and Windows-style (C:\) absolute paths
    $isAbsolute = [System.IO.Path]::IsPathRooted($resolvedPath) -or ($resolvedPath -match '^[a-zA-Z]:\\')
    if (-not $isAbsolute) {
        # For relative paths, use GetFullPath with explicit base path to avoid process current directory issues
        $resolvedPath = [System.IO.Path]::GetFullPath($resolvedPath, (Get-Location).Path)
    }
    # For absolute paths, don't call GetFullPath as it can corrupt paths on Windows
    
    # Normalize path separators based on platform
    if ([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)) {
        # Windows: use backslashes
        $resolvedPath = $resolvedPath.Replace('/', '\')
        $sensitivePatterns = @('^C:\\$', '^C:\\Windows', '^C:\\Program Files', '\\\.ssh', '\\AppData\\Roaming')
    } else {
        # Linux/macOS: use forward slashes
        $resolvedPath = $resolvedPath.Replace('\', '/')
        $sensitivePatterns = @('^/$', '^/root', '^/etc', '/\.ssh')
    }
    
    # Warn if path doesn't exist
    if (-not (Test-Path $resolvedPath)) {
        Write-Host "⚠️  Warning: Path does not exist: $resolvedPath" -ForegroundColor Yellow
    }
    
    # Security warning for sensitive paths - require confirmation
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
        # Only replace ~ at the start of the path to avoid corrupting paths like RUNNER~1
        if ($expandedPath.StartsWith('~')) {
            $expandedPath = $env:USERPROFILE + $expandedPath.Substring(1)
        }
        
        # Convert to absolute if relative
        # Check for both Unix-style (/) and Windows-style (C:\) absolute paths
        $isAbsolute = [System.IO.Path]::IsPathRooted($expandedPath) -or ($expandedPath -match '^[a-zA-Z]:\\')
        if (-not $isAbsolute) {
            # For relative paths, use GetFullPath with explicit base path to avoid process current directory issues
            $expandedPath = [System.IO.Path]::GetFullPath($expandedPath, (Get-Location).Path)
        }
        # For absolute paths, don't call GetFullPath as it can corrupt paths on Windows
        
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
    
    # Normalize the path similar to save logic
    $normalizedPath = $Path
    $expandedPath = [System.Environment]::ExpandEnvironmentVariables($Path)
    # Only replace ~ at the start of the path to avoid corrupting paths like RUNNER~1
    if ($expandedPath.StartsWith('~')) {
        $expandedPath = $env:USERPROFILE + $expandedPath.Substring(1)
    }
    
    # Convert to absolute if relative
    # Check for both Unix-style (/) and Windows-style (C:\) absolute paths
    $isAbsolute = [System.IO.Path]::IsPathRooted($expandedPath) -or ($expandedPath -match '^[a-zA-Z]:\\')
    if (-not $isAbsolute) {
        # For relative paths, use GetFullPath with explicit base path to avoid process current directory issues
        $expandedPath = [System.IO.Path]::GetFullPath($expandedPath, (Get-Location).Path)
    }
    # For absolute paths, don't call GetFullPath as it can corrupt paths on Windows
    
    # If in user profile, convert to tilde format
    if ($expandedPath.StartsWith($env:USERPROFILE)) {
        $normalizedPath = "~" + $expandedPath.Substring($env:USERPROFILE.Length)
    } else {
        $normalizedPath = $expandedPath
    }
    
    # Normalize to forward slashes for consistency
    $normalizedPath = $normalizedPath.Replace('\', '/')
    
    # Extract mount suffix if present (e.g., :rw, :ro)
    $mountSuffix = ""
    if ($normalizedPath -match ':(.+)$') {
        $mountSuffix = $Matches[1]
        $normalizedPath = $normalizedPath -replace ':(.+)$', ''
    }
    
    # Check if global config is a symlink and follow it
    if (Test-Path $globalConfig) {
        $item = Get-Item $globalConfig -Force
        if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
            $target = $item.Target
            if ($target -is [Array]) {
                $target = $target[0]
            }
            $globalConfig = $target
        }
    }
    
    # Check if local config is a symlink and follow it
    if (Test-Path $localConfig) {
        $item = Get-Item $localConfig -Force
        if ($item.LinkType -eq "SymbolicLink" -or $item.LinkType -eq "Junction") {
            $target = $item.Target
            if ($target -is [Array]) {
                $target = $target[0]
            }
            $localConfig = $target
        }
    }
    
    # Try to remove from global config - match both with and without suffix
    if (Test-Path $globalConfig) {
        $lines = Get-Content $globalConfig
        $newLines = @()
        $found = $false
        $matchedLine = ""
        
        foreach ($line in $lines) {
            # Skip empty lines
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            
            $lineWithoutSuffix = $line -replace ':(.+)$', ''
            
            # Match either exact path, path with any suffix, or normalized path
            if (($line -eq $normalizedPath) -or 
                ($lineWithoutSuffix -eq $normalizedPath) -or 
                ($line -eq $Path) -or 
                ($lineWithoutSuffix -eq $Path)) {
                if (-not $found) {
                    $found = $true
                    $matchedLine = $line
                }
            } else {
                $newLines += $line
            }
        }
        
        if ($found) {
            $newLines | Set-Content $globalConfig
            Write-Host "✅ Removed from global config: $matchedLine" -ForegroundColor Green
            $removed = $true
        }
    }
    
    # Try to remove from local config - match both with and without suffix
    if (Test-Path $localConfig) {
        $lines = Get-Content $localConfig
        $newLines = @()
        $found = $false
        $matchedLine = ""
        
        foreach ($line in $lines) {
            # Skip empty lines
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            
            $lineWithoutSuffix = $line -replace ':(.+)$', ''
            
            # Match either exact path, path with any suffix, or normalized path
            if (($line -eq $normalizedPath) -or 
                ($lineWithoutSuffix -eq $normalizedPath) -or 
                ($line -eq $Path) -or 
                ($lineWithoutSuffix -eq $Path)) {
                if (-not $found) {
                    $found = $true
                    $matchedLine = $line
                }
            } else {
                $newLines += $line
            }
        }
        
        if ($found) {
            $newLines | Set-Content $localConfig
            Write-Host "✅ Removed from local config: $matchedLine" -ForegroundColor Green
            $removed = $true
        }
    }
    
    if (-not $removed) {
        Write-Host "⚠️  Mount not found in any config: $Path" -ForegroundColor Yellow
    }
}

# Helper function to save default image to config
function Save-ImageConfig {
    param(
        [string]$ImageTag,
        [bool]$IsGlobal
    )
    
    if ($IsGlobal) {
        $configFile = "$env:USERPROFILE/.config/copilot_here/image.conf".Replace('\', '/')
        $configDir = Split-Path $configFile.Replace('/', '\')
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }
        Write-Host "✅ Saved default image to global config: $ImageTag" -ForegroundColor Green
    } else {
        $configFile = ".copilot_here/image.conf"
        if (-not (Test-Path ".copilot_here")) {
            New-Item -ItemType Directory -Path ".copilot_here" -Force | Out-Null
        }
        Write-Host "✅ Saved default image to local config: $ImageTag" -ForegroundColor Green
    }
    
    $configFilePath = $configFile.Replace('/', '\')
    Set-Content -Path $configFilePath -Value $ImageTag
    Write-Host "   Config file: $configFile"
}

# Helper function to clear default image from config
function Clear-ImageConfig {
    param(
        [bool]$IsGlobal
    )
    
    if ($IsGlobal) {
        $configFile = "$env:USERPROFILE/.config/copilot_here/image.conf".Replace('\', '/')
        $configFilePath = $configFile.Replace('/', '\')
        
        if (Test-Path $configFilePath) {
            Remove-Item $configFilePath -Force
            Write-Host "✅ Cleared default image from global config" -ForegroundColor Green
        } else {
            Write-Host "⚠️  No global image config found to clear" -ForegroundColor Yellow
        }
    } else {
        $configFile = ".copilot_here/image.conf"
        $configFilePath = $configFile.Replace('/', '\')
        
        if (Test-Path $configFilePath) {
            Remove-Item $configFilePath -Force
            Write-Host "✅ Cleared default image from local config" -ForegroundColor Green
        } else {
            Write-Host "⚠️  No local image config found to clear" -ForegroundColor Yellow
        }
    }
}

# Helper function to get default image
function Get-DefaultImage {
    $localConfig = ".copilot_here/image.conf"
    $globalConfig = "$env:USERPROFILE/.config/copilot_here/image.conf".Replace('\', '/')
    
    if (Test-Path $localConfig) {
        $image = Get-Content $localConfig -TotalCount 1
        if (-not [string]::IsNullOrWhiteSpace($image)) {
            return $image.Trim()
        }
    }
    
    if (Test-Path $globalConfig) {
        $image = Get-Content $globalConfig -TotalCount 1
        if (-not [string]::IsNullOrWhiteSpace($image)) {
            return $image.Trim()
        }
    }
    
    return "latest"
}

# Helper function to show default image
function Show-ImageConfig {
    $localConfig = ".copilot_here/image.conf"
    $globalConfig = "$env:USERPROFILE/.config/copilot_here/image.conf".Replace('\', '/')
    $currentDefault = Get-DefaultImage
    
    Write-Host "🖼️  Image Configuration:"
    Write-Host "  Current effective default: $currentDefault"
    Write-Host ""
    
    $supportsEmoji = Test-EmojiSupport
    
    if (Test-Path $localConfig) {
        $localImg = (Get-Content $localConfig -TotalCount 1).Trim()
        if ($supportsEmoji) {
            Write-Host "  📍 Local config (.copilot_here/image.conf): $localImg"
        } else {
            Write-Host "  L: Local config (.copilot_here/image.conf): $localImg"
        }
    } else {
         if ($supportsEmoji) {
            Write-Host "  📍 Local config: (not set)"
        } else {
            Write-Host "  L: Local config: (not set)"
        }
    }
    
    if (Test-Path $globalConfig) {
        $globalImg = (Get-Content $globalConfig -TotalCount 1).Trim()
        if ($supportsEmoji) {
            Write-Host "  🌍 Global config (~/.config/copilot_here/image.conf): $globalImg"
        } else {
            Write-Host "  G: Global config (~/.config/copilot_here/image.conf): $globalImg"
        }
    } else {
         if ($supportsEmoji) {
            Write-Host "  🌍 Global config: (not set)"
        } else {
            Write-Host "  G: Global config: (not set)"
        }
    }
    
    Write-Host ""
    if ($supportsEmoji) {
        Write-Host "  🔧 Base default: latest"
    } else {
        Write-Host "  Base: latest"
    }
}

# Helper function for security checks (shared by all variants)
function Test-CopilotSecurityCheck {
    # Skip in test mode
    if ($env:COPILOT_HERE_TEST_MODE -eq "true") {
        return $true
    }
    
    Write-Host "Verifying GitHub CLI authentication..."
    $authStatus = gh auth status 2>$null
    if (-not ($authStatus | Select-String -Quiet "'copilot'") -or -not ($authStatus | Select-String -Quiet "'read:packages'")) {
        Write-Host "❌ Error: Your gh token is missing the required 'copilot' or 'read:packages' scope." -ForegroundColor Red
        Write-Host "Please run 'gh auth refresh -h github.com -s copilot,read:packages' to add it."
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
    
    # Resolve the ID of the image we want to keep to ensure we don't delete it
    $keepImageId = docker inspect --format="{{.Id}}" $KeepImage 2>$null
    
    # Get all copilot_here images by repository name with full IDs
    $allImages = docker images --no-trunc "ghcr.io/gordonbeeming/copilot_here" --format "{{.ID}}|{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" 2>$null
    if (-not $allImages) {
        Write-Host "  ✓ No images to clean up"
        return
    }
    
    $imagesToProcess = $allImages
    if (-not $imagesToProcess) {
        Write-Host "  ✓ No images to clean up"
        return
    }
    
    $count = 0
    foreach ($imageInfo in $imagesToProcess) {
        $parts = $imageInfo -split '\|'
        $imageId = $parts[0]
        $imageName = $parts[1]
        $createdAt = $parts[2]
        
        # Check if this is the image we want to keep (by ID)
        if ($imageId -ne $keepImageId) {
            # Parse creation date (format: "2025-01-28 12:34:56 +0000 UTC")
            try {
                $imageDate = [DateTime]::Parse($createdAt.Substring(0, 19))
                
                if ($imageDate -lt $cutoffDate) {
                    $shortId = $imageId.Substring(7, 12)
                    Write-Host "  🗑️  Removing old image: $imageName (ID: $shortId...) (created: $createdAt)"
                    $result = docker rmi -f $imageId 2>$null
                    if ($LASTEXITCODE -eq 0) {
                        $count++
                    } else {
                        Write-Host "  ⚠️  Failed to remove: $imageName (may be in use by running container)"
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
    
    # Determine container path for current directory - always map to container home
    # Windows paths (C:/, etc.) get mapped to /home/appuser/... for consistency
    $containerWorkDir = "/home/appuser/work"
    
    $dockerBaseArgs = @(
        "--rm", "-it",
        "-v", "$($currentDir):$($containerWorkDir)",
        "-w", $containerWorkDir,
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
    # Initialize seenPaths with current directory to avoid duplicates
    $seenPaths = @{$currentDir = "mounted"}
    
    # Add current working directory to display
    $mountDisplay += "📁 $containerWorkDir"
    
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
        $copilotCommand += "--add-dir", $containerWorkDir
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
    
    # Set terminal title
    $titleEmoji = "🤖"
    if ($AllowAllTools) {
        $titleEmoji = "🤖⚡️"
    }
    
    if (-not $currentDir) {
        $currentDir = (Get-Location).Path
        if ($currentDir) {
            $currentDir = $currentDir.Replace('\', '/')
        }
    }
    
    if ($currentDir) {
        $currentDirName = Split-Path -Leaf $currentDir
    } else {
        $currentDirName = "copilot_here"
    }
    $newTitle = "$titleEmoji $currentDirName"
    
    $originalTitle = $Host.UI.RawUI.WindowTitle
    $Host.UI.RawUI.WindowTitle = $newTitle
    
    try {
        docker run $finalDockerArgs
    } finally {
        $Host.UI.RawUI.WindowTitle = $originalTitle
    }
}

# Helper function to update scripts
function Update-CopilotScripts {
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
    
    # Update embedded version in profile
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
}

# Helper function to check for updates
function Test-CopilotUpdate {
    # Skip in test mode
    if ($env:COPILOT_HERE_TEST_MODE -eq "true") {
        return $false
    }

    # Get current version
    $currentVersion = ""
    $standalonePath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
    if (Test-Path $standalonePath) {
        $currentVersion = (Get-Content $standalonePath -TotalCount 2)[1] -replace '# Version: ', ''
    } elseif (Get-Command Copilot-Here -ErrorAction SilentlyContinue) {
        $currentVersion = (Get-Command Copilot-Here).ScriptBlock.ToString() -match '# Version: (.+)' | Out-Null; $matches[1]
    }
    
    if (-not $currentVersion) { return $false }

    # Fetch remote version (with timeout)
    try {
        $remoteContent = Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        $remoteVersion = ($remoteContent.Content -split "`n")[1] -replace '# Version: ', '' -replace "`r", ""
    } catch {
        return $false # Failed to check
    }

    if (-not $remoteVersion) { return $false }

    if ($currentVersion -ne $remoteVersion) {
        # Compare versions
        try {
            $v1Str = $currentVersion -replace '-', '.'
            $v2Str = $remoteVersion -replace '-', '.'
            $v1 = [System.Version]$v1Str
            $v2 = [System.Version]$v2Str
            if ($v2 -gt $v1) {
                Write-Host "📢 Update available: $currentVersion → $remoteVersion"
                $confirmation = Read-Host "Would you like to update now? [y/N]"
                if ($confirmation.ToLower() -eq 'y' -or $confirmation.ToLower() -eq 'yes') {
                    Update-CopilotScripts
                    return $true
                }
            }
        } catch {
            # Fallback string compare
            if ($remoteVersion -gt $currentVersion) {
                 Write-Host "📢 Update available: $currentVersion → $remoteVersion"
                $confirmation = Read-Host "Would you like to update now? [y/N]"
                if ($confirmation.ToLower() -eq 'y' -or $confirmation.ToLower() -eq 'yes') {
                    Update-CopilotScripts
                    return $true
                }
            }
        }
    }
    return $false
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param (
        [switch]$h,
        [switch]$Help,
        [switch]$d,
        [switch]$Dotnet,
        [switch]$d8,
        [switch]$Dotnet8,
        [switch]$d9,
        [switch]$Dotnet9,
        [switch]$d10,
        [switch]$Dotnet10,
        [switch]$dp,
        [switch]$DotnetPlaywright,
        [string[]]$Mount,
        [string[]]$MountRW,
        [switch]$ListMounts,
        [string]$SaveMount,
        [string]$SaveMountGlobal,
        [string]$RemoveMount,
        [switch]$ShowImage,
        [string]$SetImage,
        [string]$SetImageGlobal,
        [switch]$ClearImage,
        [switch]$ClearImageGlobal,
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

    if ($ShowImage) {
        Show-ImageConfig
        return
    }

    if ($SetImage) {
        Save-ImageConfig -ImageTag $SetImage -IsGlobal $false
        return
    }

    if ($SetImageGlobal) {
        Save-ImageConfig -ImageTag $SetImageGlobal -IsGlobal $true
        return
    }

    if ($ClearImage) {
        Clear-ImageConfig -IsGlobal $false
        return
    }

    if ($ClearImageGlobal) {
        Clear-ImageConfig -IsGlobal $true
        return
    }

    if ($UpdateScripts -or $UpgradeScripts) {
        Update-CopilotScripts
        return
    }

    if ($h -or $Help) {
        Write-Output @"
copilot_here - GitHub Copilot CLI in a secure Docker container (Safe Mode)

USAGE:
  Copilot-Here [OPTIONS] [COPILOT_ARGS]
  Copilot-Here [MOUNT_MANAGEMENT]
  Copilot-Here [IMAGE_MANAGEMENT]

OPTIONS:
  -d, -Dotnet              Use .NET image variant (all versions)
  -d8, -Dotnet8            Use .NET 8 image variant
  -d9, -Dotnet9            Use .NET 9 image variant
  -d10, -Dotnet10          Use .NET 10 image variant
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
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 Copilot-Here -SaveMount ~/notes:rw
 Copilot-Here -SaveMountGlobal ~/data:rw

IMAGE MANAGEMENT:
  -ShowImage               Show current default image configuration
  -SetImage <tag>   Set default image in local config
  -SetImageGlobal <tag> Set default image in global config
  -ClearImage              Clear default image from local config
  -ClearImageGlobal        Clear default image from global config

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
  
  # Set default image
  Copilot-Here -SetImage dotnet
  Copilot-Here -SetImageGlobal dotnet-sha-bf08e6c875a919cd3440e8b3ebefc5d460edd870

  # Ask a question
  Copilot-Here -p "how do I list files in PowerShell?"
  
  # Use specific AI model
  Copilot-Here --model gpt-5 -p "explain this code"
  
  # Resume previous session
  Copilot-Here --continue
  
  # Use .NET image
  Copilot-Here -d -p "build this .NET project"
  
  # Use .NET 9 image
  Copilot-Here -d9 -p "build this .NET 9 project"
  
  # Fast mode (skip cleanup and pull)
  Copilot-Here -NoCleanup -NoPull -p "quick question"

MODES:
  Copilot-Here  - Safe mode (asks for confirmation before executing)
  Copilot-Yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-20.14
REPOSITORY: https://github.com/GordonBeeming/copilot_here
"@
        return
    }

    $imageTag = "latest"
    if ($d -or $Dotnet) {
        $imageTag = "dotnet"
    } elseif ($d8 -or $Dotnet8) {
        $imageTag = "dotnet-8"
    } elseif ($d9 -or $Dotnet9) {
        $imageTag = "dotnet-9"
    } elseif ($d10 -or $Dotnet10) {
        $imageTag = "dotnet-10"
    } elseif ($dp -or $DotnetPlaywright) {
        $imageTag = "dotnet-playwright"
    }
    
    # Initialize mount arrays if not provided
    if (-not $Mount) { $Mount = @() }
    if (-not $MountRW) { $MountRW = @() }
    
    if (Test-CopilotUpdate) { return }
    
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
        [switch]$d8,
        [switch]$Dotnet8,
        [switch]$d9,
        [switch]$Dotnet9,
        [switch]$d10,
        [switch]$Dotnet10,
        [switch]$dp,
        [switch]$DotnetPlaywright,
        [string[]]$Mount,
        [string[]]$MountRW,
        [switch]$ListMounts,
        [string]$SaveMount,
        [string]$SaveMountGlobal,
        [string]$RemoveMount,
        [switch]$ShowImage,
        [string]$SetImage,
        [string]$SetImageGlobal,
        [switch]$ClearImage,
        [switch]$ClearImageGlobal,
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

    if ($ShowImage) {
        Show-ImageConfig
        return
    }

    if ($SetImage) {
        Save-ImageConfig -ImageTag $SetImage -IsGlobal $false
        return
    }

    if ($SetImageGlobal) {
        Save-ImageConfig -ImageTag $SetImageGlobal -IsGlobal $true
        return
    }

    if ($ClearImage) {
        Clear-ImageConfig -IsGlobal $false
        return
    }

    if ($ClearImageGlobal) {
        Clear-ImageConfig -IsGlobal $true
        return
    }

    if ($UpdateScripts -or $UpgradeScripts) {
        Update-CopilotScripts
        return
    }

    if ($h -or $Help) {
        Write-Output @"
copilot_yolo - GitHub Copilot CLI in a secure Docker container (YOLO Mode)

USAGE:
  Copilot-Yolo [OPTIONS] [COPILOT_ARGS]
  Copilot-Yolo [MOUNT_MANAGEMENT]
  Copilot-Yolo [IMAGE_MANAGEMENT]

OPTIONS:
  -d, -Dotnet              Use .NET image variant (all versions)
  -d8, -Dotnet8            Use .NET 8 image variant
  -d9, -Dotnet9            Use .NET 9 image variant
  -d10, -Dotnet10          Use .NET 10 image variant
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
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 Copilot-Yolo -SaveMount ~/notes:rw
 Copilot-Yolo -SaveMountGlobal ~/data:rw

IMAGE MANAGEMENT:
  -ShowImage               Show current default image configuration
  -SetImage <tag>   Set default image in local config
  -SetImageGlobal <tag> Set default image in global config
  -ClearImage              Clear default image from local config
  -ClearImageGlobal        Clear default image from global config

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
  
  # Set default image
  Copilot-Yolo -SetImage dotnet
  Copilot-Yolo -SetImageGlobal dotnet-sha-bf08e6c875a919cd3440e8b3ebefc5d460edd870

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
  
  # Use .NET 10 image
  Copilot-Yolo -d10 -p "explore .NET 10 features"
  
  # Fast mode (skip cleanup)
  Copilot-Yolo -NoCleanup -p "generate README"

WARNING:
  YOLO mode automatically approves ALL tool usage without confirmation AND
  disables file path verification (--allow-all-tools + --allow-all-paths).
  Use with caution and only in trusted environments.

MODES:
  Copilot-Here  - Safe mode (asks for confirmation before executing)
  Copilot-Yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-20.14
REPOSITORY: https://github.com/GordonBeeming/copilot_here
"@
        return
    }

    $imageTag = "latest"
    if ($d -or $Dotnet) {
        $imageTag = "dotnet"
    } elseif ($d8 -or $Dotnet8) {
        $imageTag = "dotnet-8"
    } elseif ($d9 -or $Dotnet9) {
        $imageTag = "dotnet-9"
    } elseif ($d10 -or $Dotnet10) {
        $imageTag = "dotnet-10"
    } elseif ($dp -or $DotnetPlaywright) {
        $imageTag = "dotnet-playwright"
    }
    
    # Initialize mount arrays if not provided
    if (-not $Mount) { $Mount = @() }
    if (-not $MountRW) { $MountRW = @() }
    
    if (Test-CopilotUpdate) { return }
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $true -SkipCleanup $NoCleanup -SkipPull $NoPull -MountsRO $Mount -MountsRW $MountRW -Arguments $Prompt
}

Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
