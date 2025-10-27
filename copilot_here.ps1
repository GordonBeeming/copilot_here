# copilot_here PowerShell functions
# Version: 2025-10-27.2
# Repository: https://github.com/GordonBeeming/copilot_here

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
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Prompt
    )

    if ($h -or $Help) {
        Write-Host @"
ilot_here - GitHub Copilot CLI in a secure Docker container (Safe Mode)

GE:
opilot_here [OPTIONS] [COPILOT_ARGS]

IONS:
d, -Dotnet              Use .NET image variant
dp, -DotnetPlaywright   Use .NET + Playwright image variant
NoCleanup               Skip cleanup of unused Docker images
NoPull                  Skip pulling the latest image
UpdateScripts           Update scripts from GitHub repository
h, -Help                Show this help message

ILOT_ARGS:
ll standard GitHub Copilot CLI arguments are supported:
 -p, --prompt <text>     Execute a prompt directly
 --model <model>         Set AI model (claude-sonnet-4.5, gpt-5, etc.)
 --continue              Resume most recent session
 --resume [sessionId]    Resume from a previous session
 --log-level <level>     Set log level (none, error, warning, info, debug)
 --add-dir <directory>   Add directory to allowed list
 --allow-tool <tools>    Allow specific tools
 --deny-tool <tools>     Deny specific tools
 ... and more (run "copilot -h" for full list)

MPLES:
 Interactive mode
opilot_here

 Ask a question
opilot_here -p "how do I list files in PowerShell?"

 Use specific AI model
opilot_here --model gpt-5 -p "explain this code"

 Resume previous session
opilot_here --continue

 Use .NET image
opilot_here -d -p "build this .NET project"

 Fast mode (skip cleanup and pull)
opilot_here -NoCleanup -NoPull -p "quick question"

ES:
opilot_here  - Safe mode (asks for confirmation before executing)
opilot_yolo  - YOLO mode (auto-approves all tool usage)

SION: 2025-10-27.2
OSITORY: https://github.com/GordonBeeming/copilot_here

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
ilot_yolo - GitHub Copilot CLI in a secure Docker container (YOLO Mode)

GE:
opilot_yolo [OPTIONS] [COPILOT_ARGS]

IONS:
d, -Dotnet              Use .NET image variant
dp, -DotnetPlaywright   Use .NET + Playwright image variant
NoCleanup               Skip cleanup of unused Docker images
NoPull                  Skip pulling the latest image
UpdateScripts           Update scripts from GitHub repository
h, -Help                Show this help message

ILOT_ARGS:
ll standard GitHub Copilot CLI arguments are supported:
 -p, --prompt <text>     Execute a prompt directly
 --model <model>         Set AI model (claude-sonnet-4.5, gpt-5, etc.)
 --continue              Resume most recent session
 --resume [sessionId]    Resume from a previous session
 --log-level <level>     Set log level (none, error, warning, info, debug)
 --add-dir <directory>   Add directory to allowed list
 --allow-tool <tools>    Allow specific tools
 --deny-tool <tools>     Deny specific tools
 ... and more (run "copilot -h" for full list)

MPLES:
 Interactive mode (auto-approves all)
opilot_yolo

 Execute without confirmation
opilot_yolo -p "run the tests and fix failures"

 Use specific model
opilot_yolo --model gpt-5 -p "optimize this code"

 Resume session
opilot_yolo --continue

 Use .NET + Playwright image
opilot_yolo -dp -p "write playwright tests"

 Fast mode (skip cleanup)
opilot_yolo -NoCleanup -p "generate README"

NING:
OLO mode automatically approves ALL tool usage without confirmation.
se with caution and only in trusted environments.

ES:
opilot_here  - Safe mode (asks for confirmation before executing)
opilot_yolo  - YOLO mode (auto-approves all tool usage)

SION: 2025-10-27.2
OSITORY: https://github.com/GordonBeeming/copilot_here

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
