# copilot_here PowerShell functions
# Version: 2025-10-27.2
# Repository: https://github.com/GordonBeeming/copilot_here
# DO NOT EDIT BELOW THIS LINE - Use 'Copilot-Here -UpdateScripts' to update
# ============================================================================

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
    
    Write-Host "🧹 Cleaning up unused copilot_here images..."
    
    # Get all copilot_here images with the project label
    $allImages = docker images --filter "label=project=copilot_here" --format "{{.Repository}}:{{.Tag}}" 2>$null
    if (-not $allImages) {
        Write-Host "  ✓ No unused images to clean up"
        return
    }
    
    $imagesToRemove = $allImages | Where-Object { $_ -ne $KeepImage }
    if (-not $imagesToRemove) {
        Write-Host "  ✓ No unused images to clean up"
        return
    }
    
    $count = 0
    foreach ($image in $imagesToRemove) {
        Write-Host "  🗑️  Removing: $image"
        $result = docker rmi $image 2>$null
        if ($LASTEXITCODE -eq 0) {
            $count++
        } else {
            Write-Host "  ⚠️  Failed to remove: $image"
        }
    }
    
    Write-Host "  ✓ Cleaned up $count image(s)"
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

    $dockerBaseArgs = @(
        "--rm", "-it",
        "-v", "$((Get-Location).Path):/work",
        "-v", "$($copilotConfigPath):/home/appuser/.copilot",
        "-e", "GITHUB_TOKEN=$token",
        $imageName
    )

    $copilotCommand = @("copilot")
    
    # Add --allow-all-tools if in YOLO mode
    if ($AllowAllTools) {
        $copilotCommand += "--allow-all-tools"
    }
    
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
        [switch]$NoCleanup,
        [switch]$NoPull,
        [switch]$UpdateScripts,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )

    if ($UpdateScripts) {
        Write-Host "📦 Updating copilot_here scripts from GitHub..."
        
        # Download latest script from GitHub
        $tempScript = Join-Path $env:TEMP "copilot_here_update.ps1"
        try {
            Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $tempScript -ErrorAction Stop
        } catch {
            Write-Host "❌ Failed to download script from GitHub: $_" -ForegroundColor Red
            return
        }
        
        # Find the script file that's currently loaded
        $scriptPath = $null
        if (Test-Path "$PSScriptRoot\copilot_here.ps1") {
            $scriptPath = "$PSScriptRoot\copilot_here.ps1"
        } else {
            # Try to find it in common locations
            $possiblePaths = @(
                "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1",
                "$env:USERPROFILE\Documents\WindowsPowerShell\copilot_here.ps1"
            )
            foreach ($path in $possiblePaths) {
                if (Test-Path $path) {
                    $scriptPath = $path
                    break
                }
            }
        }
        
        if (-not $scriptPath) {
            Write-Host "⚠️  Could not locate current script file." -ForegroundColor Yellow
            Write-Host "Please specify the path to copilot_here.ps1"
            $scriptPath = Read-Host "Enter path"
            if (-not (Test-Path $scriptPath)) {
                Write-Host "❌ Path not found: $scriptPath" -ForegroundColor Red
                Remove-Item $tempScript -Force
                return
            }
        }
        
        # Create backup
        $backupPath = "$scriptPath.backup.$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        Copy-Item $scriptPath $backupPath
        Write-Host "✅ Created backup: $backupPath"
        
        # Replace the file
        Copy-Item $tempScript $scriptPath -Force
        Remove-Item $tempScript -Force
        
        Write-Host "✅ Scripts updated successfully!"
        Write-Host ""
        Write-Host "🔄 Please reload the script:"
        Write-Host "   . $scriptPath"
        return
    }

    if ($h -or $Help) {
        Write-Host @"
copilot_here - GitHub Copilot CLI in a secure Docker container (Safe Mode)

USAGE:
  copilot_here [OPTIONS] [COPILOT_ARGS]

OPTIONS:
  -d, -Dotnet              Use .NET image variant
  -dp, -DotnetPlaywright   Use .NET + Playwright image variant
  -NoCleanup               Skip cleanup of unused Docker images
  -NoPull                  Skip pulling the latest image
  -UpdateScripts           Update scripts from GitHub repository
  -h, -Help                Show this help message

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
  copilot_here
  
  # Ask a question
  copilot_here -p "how do I list files in PowerShell?"
  
  # Use specific AI model
  copilot_here --model gpt-5 -p "explain this code"
  
  # Resume previous session
  copilot_here --continue
  
  # Use .NET image
  copilot_here -d -p "build this .NET project"
  
  # Fast mode (skip cleanup and pull)
  copilot_here -NoCleanup -NoPull -p "quick question"

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage)

VERSION: 2025-10-27.2
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
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $false -SkipCleanup $NoCleanup -SkipPull $NoPull -Arguments $Prompt
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
        [switch]$NoCleanup,
        [switch]$NoPull,
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )

    if ($h -or $Help) {
        Write-Host @"
copilot_yolo - GitHub Copilot CLI in a secure Docker container (YOLO Mode)

USAGE:
  copilot_yolo [OPTIONS] [COPILOT_ARGS]

OPTIONS:
  -d, -Dotnet              Use .NET image variant
  -dp, -DotnetPlaywright   Use .NET + Playwright image variant
  -NoCleanup               Skip cleanup of unused Docker images
  -NoPull                  Skip pulling the latest image
  -h, -Help                Show this help message

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
  copilot_yolo
  
  # Execute without confirmation
  copilot_yolo -p "run the tests and fix failures"
  
  # Use specific model
  copilot_yolo --model gpt-5 -p "optimize this code"
  
  # Resume session
  copilot_yolo --continue
  
  # Use .NET + Playwright image
  copilot_yolo -dp -p "write playwright tests"
  
  # Fast mode (skip cleanup)
  copilot_yolo -NoCleanup -p "generate README"

WARNING:
  YOLO mode automatically approves ALL tool usage without confirmation.
  Use with caution and only in trusted environments.

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage)

VERSION: 2025-10-27.2
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
    
    Invoke-CopilotRun -ImageTag $imageTag -AllowAllTools $true -SkipCleanup $NoCleanup -SkipPull $NoPull -Arguments $Prompt
}

Set-Alias -Name copilot_here -Value Copilot-Here
Set-Alias -Name copilot_yolo -Value Copilot-Yolo

# ============================================================================
# END copilot_here PowerShell functions
# ============================================================================
```
</details>

2. **Add it to your PowerShell profile.**

Open your PowerShell profile for editing:
```powershell
notepad $PROFILE
```

Add this line (adjust the path to where you saved the file):
```powershell
. C:\Users\YourName\Documents\PowerShell\copilot_here.ps1
```

3. **Reload your PowerShell profile:**
```powershell
. $PROFILE
```

---

## Usage

Once set up, using it is simple on any platform.

### Interactive Mode

Start a full chat session with the welcome banner:

**Base image (default):**
```bash
# Linux/macOS
copilot_here

# Windows
copilot_here
```

**With .NET image:**
```bash
# Linux/macOS
copilot_here -d
copilot_here --dotnet

# Windows
copilot_here -d
copilot_here -Dotnet
```

**With .NET + Playwright image:**
```bash
# Linux/macOS
copilot_here -dp
copilot_here --dotnet-playwright

# Windows
copilot_here -dp
copilot_here -DotnetPlaywright
```

**Get help:**
```bash
# Linux/macOS
copilot_here --help
copilot_yolo --help

# Windows
copilot_here -Help
copilot_yolo -Help
```

### Non-Interactive Mode

Pass a prompt directly to get a quick response.

**Safe Mode** (asks for confirmation before executing):

```bash
# Linux/macOS - Base image
copilot_here "suggest a git command to view the last 5 commits"
copilot_here "explain the code in ./my-script.js"

# Linux/macOS - .NET image
copilot_here -d "build and test this .NET project"
copilot_here --dotnet "explain this C# code"

# Linux/macOS - .NET + Playwright image
copilot_here -dp "run playwright tests for this app"

# Linux/macOS - Skip cleanup and pull for faster startup
copilot_here --no-cleanup --no-pull "quick question about this code"

# Windows - Base image
copilot_here "suggest a git command to view the last 5 commits"

# Windows - .NET image
copilot_here -d "build and test this .NET project"
copilot_here -Dotnet "explain this C# code"

# Windows - .NET + Playwright image
copilot_here -dp "run playwright tests for this app"

# Windows - Skip cleanup and pull for faster startup
copilot_here -NoCleanup -NoPull "quick question about this code"
```

**YOLO Mode** (auto-approves execution):

```bash
# Linux/macOS - Base image
copilot_yolo "write a function that reverses a string"
copilot_yolo "run the tests and fix any failures"

# Linux/macOS - .NET image
copilot_yolo -d "create a new ASP.NET Core API project"
copilot_yolo --dotnet "add unit tests for this controller"

# Linux/macOS - .NET + Playwright image
copilot_yolo -dp "write playwright tests for the login page"

# Linux/macOS - Skip cleanup for faster execution
copilot_yolo --no-cleanup "generate a README for this project"

# Windows - Base image
copilot_yolo "write a function that reverses a string"

# Windows - .NET image
copilot_yolo -d "create a new ASP.NET Core API project"
copilot_yolo -Dotnet "add unit tests for this controller"

# Windows - .NET + Playwright image
copilot_yolo -dp "write playwright tests for the login page"

# Windows - Skip cleanup for faster execution
copilot_yolo -NoCleanup "generate a README for this project"
```


## 🐳 Docker Image Variants

This project provides multiple Docker image variants for different development scenarios. All images include the GitHub Copilot CLI and inherit the base security and authentication features.

### Available Images

#### Base Image
**Tag:** `latest`

The standard Copilot CLI environment with Node.js 20, Git, and essential tools. Use this for general-purpose development and scripting tasks.

```bash
# Already configured in the setup instructions above
copilot_here() {
  local image_name="ghcr.io/gordonbeeming/copilot_here:latest"
  # ... rest of function
}
```

#### .NET Image
**Tag:** `dotnet`

Extends the base image with .NET SDK support for building and testing .NET applications.

**Includes:**
- .NET 8.0 SDK
- .NET 9.0 SDK
- ASP.NET Core runtimes
- All base image features

**Usage:**
```bash
# Update the image_name in your function to use the .NET variant
local image_name="ghcr.io/gordonbeeming/copilot_here:dotnet"
```

**Best for:** .NET development, building/testing .NET applications, ASP.NET Core projects

#### .NET + Playwright Image
**Tag:** `dotnet-playwright`

Extends the .NET image with Playwright browser automation capabilities.

**Includes:**
- Everything from the .NET image
- Playwright 1.56.0
- Chromium browser with dependencies
- FFmpeg for video recording

**Usage:**
```bash
# Update the image_name in your function to use the .NET + Playwright variant
local image_name="ghcr.io/gordonbeeming/copilot_here:dotnet-playwright"
```

**Best for:** .NET web testing, browser automation, E2E testing with Playwright

**Note:** This image is approximately 500-600MB larger than the .NET image due to Chromium browser binaries.

### Choosing the Right Image

- Use **`latest`** for general development, scripting, and Node.js projects
- Use **`dotnet`** when working with .NET projects without browser testing needs
- Use **`dotnet-playwright`** when you need both .NET and browser automation capabilities

Future variants may include Python, Java, and other language-specific toolchains.

## 📚 Documentation

- [Docker Images Documentation](docs/docker-images.md) - Details about available image variants
- [Task Documentation](docs/tasks/) - Development task history and changes

## 📜 License

This project is licensed under the MIT License.
