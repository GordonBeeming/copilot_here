# copilot_here PowerShell functions
# Version: 2025-11-27
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
function Show-AvailableImages {
    Write-Host "📦 Available Images:"
    Write-Host "  • latest (Base image)"
    Write-Host "  • dotnet (.NET 8, 9, 10 SDKs)"
    Write-Host "  • dotnet-8 (.NET 8 SDK)"
    Write-Host "  • dotnet-9 (.NET 9 SDK)"
    Write-Host "  • dotnet-10 (.NET 10 SDK)"
    Write-Host "  • playwright (Playwright)"
    Write-Host "  • dotnet-playwright (.NET + Playwright)"
    Write-Host "  • dotnet-sha-<sha> (Specific commit SHA)"
}

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

# Helper function to create or load network proxy config
function Ensure-NetworkConfig {
    param(
        [bool]$IsGlobal
    )
    
    if ($IsGlobal) {
        $configDir = "$env:USERPROFILE\.config\copilot_here"
        $configFile = "$configDir\network.json"
    } else {
        $configDir = ".copilot_here"
        $configFile = "$configDir\network.json"
    }
    
    # Check if config already exists
    if (Test-Path $configFile) {
        Write-Host "✅ Using existing network config: $configFile"
        return $true
    }
    
    # Config doesn't exist - ask user about mode and create it
    Write-Host "📝 Creating network proxy configuration..."
    Write-Host ""
    Write-Host "   The network proxy can run in two modes:"
    Write-Host "   • [e]nforce - Block requests not in the allowlist (recommended for security)"
    Write-Host "   • [m]onitor - Log all requests but allow everything (useful for testing)"
    Write-Host ""
    $modeChoice = Read-Host "   Select mode (default: enforce)"
    
    $mode = "enforce"
    if ($modeChoice.ToLower() -eq "monitor" -or $modeChoice.ToLower() -eq "m") {
        $mode = "monitor"
    }
    
    # Create config directory
    if (-not (Test-Path $configDir)) {
        try {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        } catch {
            Write-Host "❌ Error: Failed to create config directory: $configDir" -ForegroundColor Red
            return $false
        }
    }
    
    # Load default rules if available
    $defaultRulesFile = "$env:USERPROFILE\.config\copilot_here\default-network-rules.json"
    $allowedRules = $null
    
    if (Test-Path $defaultRulesFile) {
        try {
            $defaultContent = Get-Content $defaultRulesFile -Raw | ConvertFrom-Json
            $allowedRules = $defaultContent.allowed_rules
        } catch {
            # Fallback to inline defaults
        }
    }
    
    # Fallback to inline defaults if not found
    if (-not $allowedRules) {
        $allowedRules = @(
            @{
                host = "api.github.com"
                allowed_paths = @("/user", "/graphql")
            },
            @{
                host = "api.individual.githubcopilot.com"
                allowed_paths = @("/models", "/mcp/readonly", "/chat/completions")
            }
        )
    }
    
    # Create config object
    $config = @{
        inherit_default_rules = $true
        mode = $mode
        allowed_rules = $allowedRules
    }
    
    # Write config file
    try {
        $config | ConvertTo-Json -Depth 10 | Set-Content $configFile -Encoding UTF8
    } catch {
        Write-Host "❌ Error: Failed to write config file: $configFile" -ForegroundColor Red
        return $false
    }
    
    Write-Host ""
    Write-Host "✅ Created network config: $configFile" -ForegroundColor Green
    Write-Host "   Mode: $mode"
    Write-Host "   inherit_default_rules: true"
    Write-Host ""
    
    return $true
}

# Helper function to show network rules
function Show-NetworkRules {
    $localConfig = ".copilot_here\network.json"
    $globalConfig = "$env:USERPROFILE\.config\copilot_here\network.json"
    $defaultRules = "$env:USERPROFILE\.config\copilot_here\default-network-rules.json"
    
    Write-Host "📋 Network Proxy Rules"
    Write-Host "======================"
    Write-Host ""
    
    # Show default rules
    if (Test-Path $defaultRules) {
        Write-Host "📦 Default Rules:"
        Write-Host "   $defaultRules"
        Get-Content $defaultRules | ForEach-Object { Write-Host "   $_" }
        Write-Host ""
    } else {
        Write-Host "📦 Default Rules: Not found"
        Write-Host ""
    }
    
    # Show global config
    if (Test-Path $globalConfig) {
        Write-Host "🌐 Global Config:"
        Write-Host "   $globalConfig"
        Get-Content $globalConfig | ForEach-Object { Write-Host "   $_" }
        Write-Host ""
    } else {
        Write-Host "🌐 Global Config: Not configured"
        Write-Host ""
    }
    
    # Show local config
    if (Test-Path $localConfig) {
        Write-Host "📁 Local Config:"
        Write-Host "   $localConfig"
        Get-Content $localConfig | ForEach-Object { Write-Host "   $_" }
        Write-Host ""
    } else {
        Write-Host "📁 Local Config: Not configured"
        Write-Host ""
    }
}

# Helper function to edit network rules
function Edit-NetworkRules {
    param(
        [bool]$IsGlobal
    )
    
    if ($IsGlobal) {
        $configDir = "$env:USERPROFILE\.config\copilot_here"
        $configFile = "$configDir\network.json"
    } else {
        $configDir = ".copilot_here"
        $configFile = "$configDir\network.json"
    }
    
    # Create config if it doesn't exist
    if (-not (Test-Path $configFile)) {
        Write-Host "📝 Config file doesn't exist. Creating it first..."
        $result = Ensure-NetworkConfig -IsGlobal $IsGlobal
        if (-not $result) {
            return
        }
    }
    
    # Determine editor
    $editor = $env:EDITOR
    if (-not $editor) { $editor = $env:VISUAL }
    if (-not $editor) { $editor = "notepad" }
    
    Write-Host "📝 Opening $configFile with $editor..."
    & $editor $configFile
}

# Helper function to run with airlock (Docker Compose mode)
function Invoke-CopilotAirlock {
    param(
        [string]$ImageTag,
        [bool]$AllowAllTools,
        [bool]$SkipCleanup,
        [bool]$SkipPull,
        [string]$NetworkConfigFile,
        [string[]]$MountsRO,
        [string[]]$MountsRW,
        [string[]]$Arguments
    )
    
    if (-not (Test-CopilotSecurityCheck)) { return }
    
    # Skip actual container launch in test mode
    if ($env:COPILOT_HERE_TEST_MODE -eq "true") {
        Write-Host "🧪 Test mode: skipping container launch"
        return
    }
    
    $appImage = "ghcr.io/gordonbeeming/copilot_here:$ImageTag"
    $proxyImage = "ghcr.io/gordonbeeming/copilot_here:proxy"
    
    Write-Host "🛡️  Starting in Airlock mode..."
    Write-Host "   App image: $appImage"
    Write-Host "   Proxy image: $proxyImage"
    Write-Host "   Network config: $NetworkConfigFile"
    
    # Pull images unless skipped
    if (-not $SkipPull) {
        Write-Host "📥 Pulling images..."
        docker pull $appImage
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to pull app image" -ForegroundColor Red
            return
        }
        docker pull $proxyImage
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to pull proxy image" -ForegroundColor Red
            return
        }
    }
    
    # Get current directory info for project name
    $currentDir = (Get-Location).Path
    $currentDirName = Split-Path -Leaf $currentDir
    $titleEmoji = "🤖"
    if ($AllowAllTools) {
        $titleEmoji = "🤖⚡️"
    }
    
    # Generate unique session ID
    $sessionId = [System.Guid]::NewGuid().ToString().Substring(0, 8)
    
    # Project name matches terminal title format
    $projectName = "$currentDirName-$sessionId"
    
    # Create temporary compose file
    $tempCompose = [System.IO.Path]::GetTempFileName() + ".yml"
    $configDir = "$env:USERPROFILE\.config\copilot_here"
    $templateFile = "$configDir\docker-compose.airlock.yml.template"
    
    # Download template if not exists
    if (-not (Test-Path $templateFile)) {
        Write-Host "📥 Downloading compose template..."
        try {
            Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -OutFile $templateFile
        } catch {
            Write-Host "❌ Failed to download compose template" -ForegroundColor Red
            Remove-Item $tempCompose -ErrorAction SilentlyContinue
            return
        }
    }
    
    # Get token
    $token = gh auth token 2>$null
    if ([string]::IsNullOrEmpty($token)) {
        Write-Host "⚠️  Could not retrieve token using 'gh auth token'." -ForegroundColor Yellow
    }
    
    # Prepare copilot config path
    $copilotConfig = "$env:USERPROFILE\.config\copilot-cli-docker"
    if (-not (Test-Path $copilotConfig)) {
        New-Item -ItemType Directory -Path $copilotConfig -Force | Out-Null
    }
    
    # Container work directory
    $containerWorkDir = "/home/appuser/work"
    
    # Build extra mounts string
    $extraMounts = ""
    foreach ($mountPath in $MountsRO) {
        $resolved = Resolve-MountPath $mountPath
        if ($resolved) {
            $extraMounts += "      - ${resolved}:${resolved}:ro`n"
        }
    }
    foreach ($mountPath in $MountsRW) {
        $resolved = Resolve-MountPath $mountPath
        if ($resolved) {
            $extraMounts += "      - ${resolved}:${resolved}:rw`n"
        }
    }
    
    # Build copilot command args as JSON array
    $copilotCmd = '["copilot"'
    if ($AllowAllTools) {
        $copilotCmd += ', "--allow-all-tools", "--allow-all-paths"'
        $copilotCmd += ", `"--add-dir`", `"$containerWorkDir`""
    }
    
    if ($Arguments.Count -eq 0) {
        $copilotCmd += ', "--banner"'
    } else {
        foreach ($arg in $Arguments) {
            $escapedArg = $arg -replace '"', '\"'
            $copilotCmd += ", `"$escapedArg`""
        }
    }
    $copilotCmd += ']'
    
    # Read template and substitute variables
    $template = Get-Content $templateFile -Raw
    $compose = $template `
        -replace '\{\{PROJECT_NAME\}\}', $projectName `
        -replace '\{\{APP_IMAGE\}\}', $appImage `
        -replace '\{\{PROXY_IMAGE\}\}', $proxyImage `
        -replace '\{\{WORK_DIR\}\}', ($currentDir -replace '\\', '/') `
        -replace '\{\{CONTAINER_WORK_DIR\}\}', $containerWorkDir `
        -replace '\{\{COPILOT_CONFIG\}\}', ($copilotConfig -replace '\\', '/') `
        -replace '\{\{NETWORK_CONFIG\}\}', ($NetworkConfigFile -replace '\\', '/') `
        -replace '\{\{PUID\}\}', [System.Environment]::GetEnvironmentVariable("PUID", "Process") ?? "1000" `
        -replace '\{\{PGID\}\}', [System.Environment]::GetEnvironmentVariable("PGID", "Process") ?? "1000" `
        -replace '\{\{GITHUB_TOKEN\}\}', $token `
        -replace '\{\{EXTRA_MOUNTS\}\}', $extraMounts `
        -replace '\{\{COPILOT_ARGS\}\}', $copilotCmd
    
    Set-Content $tempCompose $compose -Encoding UTF8
    
    # Set terminal title
    $title = "$titleEmoji $currentDirName [airlock]"
    $originalTitle = $Host.UI.RawUI.WindowTitle
    $Host.UI.RawUI.WindowTitle = $title
    
    try {
        Write-Host ""
        docker compose -f $tempCompose -p $projectName up --abort-on-container-exit --attach app
    } finally {
        # Cleanup
        $Host.UI.RawUI.WindowTitle = $originalTitle
        Write-Host ""
        Write-Host "🧹 Cleaning up airlock..."
        docker compose -f $tempCompose -p $projectName down --volumes 2>$null
        Remove-Item $tempCompose -ErrorAction SilentlyContinue
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
    
    # Ensure config directory exists
    $configDir = "$env:USERPROFILE\.config\copilot_here"
    if (-not (Test-Path $configDir)) {
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    }
    
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
        
        # Download default network rules
        Write-Host "📥 Updating default network rules..."
        $networkRulesFile = "$configDir\default-network-rules.json"
        try {
            Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/default-network-rules.json" -OutFile $networkRulesFile
            Write-Host "✅ Network rules updated: $networkRulesFile"
        } catch {
            Write-Host "⚠️  Failed to download network rules (non-fatal)" -ForegroundColor Yellow
        }
        
        # Download docker-compose template
        Write-Host "📥 Updating compose template..."
        $composeTemplateFile = "$configDir\docker-compose.airlock.yml.template"
        try {
            Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -OutFile $composeTemplateFile
            Write-Host "✅ Compose template updated: $composeTemplateFile"
        } catch {
            Write-Host "⚠️  Failed to download compose template (non-fatal)" -ForegroundColor Yellow
        }
        
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
    
    # Download default network rules
    Write-Host "📥 Updating default network rules..."
    $networkRulesFile = "$configDir\default-network-rules.json"
    try {
        Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/default-network-rules.json" -OutFile $networkRulesFile
        Write-Host "✅ Network rules updated: $networkRulesFile"
    } catch {
        Write-Host "⚠️  Failed to download network rules (non-fatal)" -ForegroundColor Yellow
    }
    
    # Download docker-compose template
    Write-Host "📥 Updating compose template..."
    $composeTemplateFile = "$configDir\docker-compose.airlock.yml.template"
    try {
        Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -OutFile $composeTemplateFile
        Write-Host "✅ Compose template updated: $composeTemplateFile"
    } catch {
        Write-Host "⚠️  Failed to download compose template (non-fatal)" -ForegroundColor Yellow
    }
    
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

# Common help function for both Copilot-Here and Copilot-Yolo
function Show-CopilotHelp {
    param (
        [bool]$IsYolo = $false
    )
    
    $cmdName = if ($IsYolo) { "Copilot-Yolo" } else { "Copilot-Here" }
    $modeDesc = if ($IsYolo) { "YOLO Mode" } else { "Safe Mode" }
    
    Write-Output @"
$cmdName - GitHub Copilot CLI in a secure Docker container ($modeDesc)

USAGE:
  $cmdName [OPTIONS] [COPILOT_ARGS]
  $cmdName [MOUNT_MANAGEMENT]
  $cmdName [IMAGE_MANAGEMENT]

OPTIONS:
  -d, -Dotnet              Use .NET image variant (all versions)
  -d8, -Dotnet8            Use .NET 8 image variant
  -d9, -Dotnet9            Use .NET 9 image variant
  -d10, -Dotnet10          Use .NET 10 image variant
  -pw, -Playwright         Use Playwright image variant
  -dp, -DotnetPlaywright   Use .NET + Playwright image variant
  -Mount <path>            Mount additional directory (read-only)
  -MountRW <path>          Mount additional directory (read-write)
  -NoCleanup               Skip cleanup of unused Docker images
  -NoPull                  Skip pulling the latest image
  -UpdateScripts           Update scripts from GitHub repository
  -UpgradeScripts          Alias for -UpdateScripts
  -h, -Help                Show this help message
  -Help2                   Show GitHub Copilot CLI native help

NETWORK PROXY (EXPERIMENTAL):
  -EnableNetworkProxy        Enable network proxy with local rules (.copilot_here/network.json)
  -EnableGlobalNetworkProxy  Enable network proxy with global rules (~/.config/copilot_here/network.json)
  -ShowNetworkRules          Show current network proxy rules
  -EditNetworkRules          Edit local network rules in `$env:EDITOR
  -EditGlobalNetworkRules    Edit global network rules in `$env:EDITOR

MOUNT MANAGEMENT:
  -ListMounts              Show all configured mounts
  -SaveMount <path>        Save mount to local config (.copilot_here/mounts.conf)
  -SaveMountGlobal <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  -RemoveMount <path>      Remove mount from configs
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 $cmdName -SaveMount ~/notes:rw
 $cmdName -SaveMountGlobal ~/data:rw

IMAGE MANAGEMENT:
  -ListImages              List all available Docker images
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
 ... and more (run $cmdName -Help2 for full copilot help)

EXAMPLES:
  # Interactive mode
  $cmdName
  
  # Mount additional directories
  $cmdName -Mount ../investigations -p "analyze these files"
  $cmdName -MountRW ~/notes -Mount /data/research
  
  # Save mounts for reuse
  $cmdName -SaveMount ~/investigations
  $cmdName -SaveMountGlobal ~/common-data
  $cmdName -ListMounts
  
  # Set default image
  $cmdName -SetImage dotnet
  $cmdName -SetImageGlobal dotnet-sha-bf08e6c875a919cd3440e8b3ebefc5d460edd870
  
  # Ask a question (short syntax)
  $cmdName -p "how do I list files in bash?"
  
  # Use specific AI model
  $cmdName --model gpt-5 -p "explain this code"
  
  # Resume previous session
  $cmdName --continue
  
  # Use .NET image with custom log level
  $cmdName -d --log-level debug -p "build this .NET project"
  
  # Fast mode (skip cleanup and pull)
  $cmdName -NoCleanup -NoPull -p "quick question"

MODES:
  Copilot-Here  - Safe mode (asks for confirmation before executing)
  Copilot-Yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-27
REPOSITORY: https://github.com/GordonBeeming/copilot_here
"@
}

# Show native copilot help
function Show-CopilotNativeHelp {
    $imageTag = Get-DefaultImage
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $false -SkipCleanup $true -SkipPull $true -Arguments "--help"
}

# Common internal function for both Copilot-Here and Copilot-Yolo
function Invoke-CopilotMain {
    param (
        [bool]$IsYolo,
        [switch]$h,
        [switch]$Help,
        [switch]$Help2,
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
        [switch]$ListImages,
        [switch]$ShowImage,
        [string]$SetImage,
        [string]$SetImageGlobal,
        [switch]$ClearImage,
        [switch]$ClearImageGlobal,
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$EnableNetworkProxy,
        [switch]$EnableGlobalNetworkProxy,
        [switch]$ShowNetworkRules,
        [switch]$EditNetworkRules,
        [switch]$EditGlobalNetworkRules,
        [switch]$UpdateScripts,
        [switch]$UpgradeScripts,
        [string[]]$Prompt
    )

    # Check for mutually exclusive network proxy flags
    if ($EnableNetworkProxy -and $EnableGlobalNetworkProxy) {
        Write-Host "❌ Error: Cannot use both -EnableNetworkProxy and -EnableGlobalNetworkProxy" -ForegroundColor Red
        return
    }

    # Handle network rules management commands
    if ($ShowNetworkRules) {
        Show-NetworkRules
        return
    }
    
    if ($EditNetworkRules) {
        Edit-NetworkRules -IsGlobal $false
        return
    }
    
    if ($EditGlobalNetworkRules) {
        Edit-NetworkRules -IsGlobal $true
        return
    }

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

    if ($ListImages) {
        Show-AvailableImages
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
        Show-CopilotHelp -IsYolo $IsYolo
        return
    }

    if ($Help2) {
        Show-CopilotNativeHelp
        return
    }

    # Determine image tag
    $imageTag = Get-DefaultImage
    
    if ($d -or $Dotnet) { $imageTag = "dotnet" }
    if ($d8 -or $Dotnet8) { $imageTag = "dotnet8" }
    if ($d9 -or $Dotnet9) { $imageTag = "dotnet9" }
    if ($d10 -or $Dotnet10) { $imageTag = "dotnet10" }
    if ($dp -or $DotnetPlaywright) { $imageTag = "dotnet-playwright" }

    if ($EnableNetworkProxy -or $EnableGlobalNetworkProxy) {
        $isGlobal = $EnableGlobalNetworkProxy
        Ensure-NetworkConfig -IsGlobal $isGlobal
        return
    }
    
    if (Test-CopilotUpdate) { return }
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $IsYolo -SkipCleanup $NoCleanup -SkipPull $NoPull -MountsRO $Mount -MountsRW $MountRW -Arguments $Prompt
}

# Safe Mode: Asks for confirmation before executing
function Copilot-Here {
    [CmdletBinding()]
    param (
        [switch]$h,
        [switch]$Help,
        [switch]$Help2,
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
        [switch]$ListImages,
        [switch]$ShowImage,
        [string]$SetImage,
        [string]$SetImageGlobal,
        [switch]$ClearImage,
        [switch]$ClearImageGlobal,
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$EnableNetworkProxy,
        [switch]$EnableGlobalNetworkProxy,
        [switch]$ShowNetworkRules,
        [switch]$EditNetworkRules,
        [switch]$EditGlobalNetworkRules,
        [switch]$UpdateScripts,
        [switch]$UpgradeScripts,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )
    
    Invoke-CopilotMain -IsYolo $false @PSBoundParameters
}

# YOLO Mode: Auto-approves all tool usage
function Copilot-Yolo {
    [CmdletBinding()]
    param (
        [switch]$h,
        [switch]$Help,
        [switch]$Help2,
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
        [switch]$ListImages,
        [switch]$ShowImage,
        [string]$SetImage,
        [string]$SetImageGlobal,
        [switch]$ClearImage,
        [switch]$ClearImageGlobal,
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$EnableNetworkProxy,
        [switch]$EnableGlobalNetworkProxy,
        [switch]$ShowNetworkRules,
        [switch]$EditNetworkRules,
        [switch]$EditGlobalNetworkRules,
        [switch]$UpdateScripts,
        [switch]$UpgradeScripts,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )
    
    Invoke-CopilotMain -IsYolo $true @PSBoundParameters
}

Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo
