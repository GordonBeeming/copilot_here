# copilot-here: A Secure, Portable Copilot CLI Environment

Run the GitHub Copilot CLI from any directory on your machine, inside a sandboxed Docker container that automatically uses your existing `gh` authentication.

---

## 🚀 What is this?

This project solves a simple problem: you want to use the awesome [GitHub Copilot CLI](https://github.com/features/copilot/cli), but you also want a clean, portable, and secure environment for it.

The `copilot_here` shell function is a lightweight wrapper around a Docker container. When you run it in a terminal, it:
- **Enhances security** by isolating the tool in a container, granting it file system access **only** to the directory you're currently in. 🛡️
- **Keeps your machine clean** by avoiding a global Node.js installation.
- **Authenticates automatically** by using your host machine's existing `gh` CLI credentials.
- **Validates token permissions** by checking for required scopes and warning you about overly permissive tokens.
- **Persists its configuration**, so it remembers which folders you've trusted across sessions.
- **Stays up-to-date** by automatically pulling the latest image version on every run.

## ✅ Prerequisites

Before you start, make sure you have the following installed and configured on your machine:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine on Linux).
- The [GitHub CLI (`gh`)](https://cli.github.com/).
- You must be logged in to the GitHub CLI. You can check by running `gh auth status`. Your token **must** have the `copilot` scope. If it doesn't, run `gh auth refresh -h github.com -s copilot` to add it.

## 🛠️ Setup Instructions

Choose your platform below. The scripts include both **Safe Mode** (asks for confirmation) and **YOLO Mode** (auto-approves) functions. You can use either or both depending on your needs.

### Execution Modes

**Safe Mode (`copilot_here`)** - Always asks for confirmation before executing commands. Recommended for general development work where you want control over what gets executed.

**YOLO Mode (`copilot_yolo`)** - Automatically approves all tool usage without confirmation. Convenient for trusted workflows but use with caution as it can execute commands without prompting.

### Image Variants

All functions support switching between Docker image variants using flags:
- **No flag** - Base image (Node.js, Git, basic tools)
- **`-d` or `--dotnet`** - .NET image (includes .NET 8 & 9 SDKs)
- **`-dp` or `--dotnet-playwright`** - .NET + Playwright image (includes browser automation)

### Additional Options

- **`-h` or `--help`** - Show usage help and examples (Bash/Zsh) or `-h` / `-Help` (PowerShell)
- **`--no-cleanup`** - Skip cleanup of unused Docker images (Bash/Zsh) or `-NoCleanup` (PowerShell)
- **`--no-pull`** - Skip pulling the latest image (Bash/Zsh) or `-NoPull` (PowerShell)
  --update-scripts          Update scripts from GitHub repository

> ⚠️ **Security Note:** Both modes check for proper GitHub token scopes and warn about overly privileged tokens.

---

### For Linux/macOS (Bash/Zsh)

**Quick Install (Recommended):**

Download and source the script in your shell profile:

```bash
# Download the script
curl -fsSL https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh -o ~/.copilot_here.sh

# Add to your shell profile (~/.zshrc or ~/.bashrc) - only if not already there
if ! grep -q "source ~/.copilot_here.sh" ~/.zshrc 2>/dev/null; then
  echo '' >> ~/.zshrc
  echo 'source ~/.copilot_here.sh' >> ~/.zshrc
fi

# Reload your shell
source ~/.zshrc  # or source ~/.bashrc
```

To update later, just run: `copilot_here --update-scripts`

---

**Manual Install (Alternative):**

Open your shell's startup file (e.g., `~/.zshrc`, `~/.bashrc`) and add:

   <details>
   <summary>Click to expand bash/zsh code</summary>

   ```bash
   # copilot_here shell functions
   # Version: 2025-10-27.5
   # Repository: https://github.com/GordonBeeming/copilot_here
   
   # Helper function for security checks (shared by all variants)
   __copilot_security_check() {
     if ! gh auth status 2>/dev/null | grep "Token scopes:" | grep -q "'copilot'"; then
       echo "❌ Error: Your gh token is missing the required 'copilot' scope."
       echo "Please run 'gh auth refresh -h github.com -s copilot' to add it."
       return 1
     fi

     if gh auth status 2>/dev/null | grep "Token scopes:" | grep -q -E "'(admin:|manage_|write:public_key|delete_repo|(write|delete)_packages)'"; then
       echo "⚠️  Warning: Your GitHub token has highly privileged scopes (e.g., admin:org, admin:enterprise)."
       printf "Are you sure you want to proceed with this token? [y/N]: "
       read confirmation
       local lower_confirmation
       lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
       if [[ "$lower_confirmation" != "y" && "$lower_confirmation" != "yes" ]]; then
         echo "Operation cancelled by user."
         return 1
       fi
     fi
     return 0
   }

   # Helper function to cleanup unused copilot_here images
   __copilot_cleanup_images() {
     local keep_image="$1"
     echo "🧹 Cleaning up unused copilot_here images..."
     
     # Get all copilot_here images with the project label
     local images_to_remove=$(docker images --filter "label=project=copilot_here" --format "{{.Repository}}:{{.Tag}}" | grep -v "^${keep_image}$" || true)
     
     if [ -z "$images_to_remove" ]; then
       echo "  ✓ No unused images to clean up"
       return 0
     fi
     
     local count=0
     while IFS= read -r image; do
       if [ -n "$image" ]; then
         echo "  🗑️  Removing: $image"
         docker rmi "$image" > /dev/null 2>&1 && ((count++)) || echo "  ⚠️  Failed to remove: $image"
       fi
     done <<< "$images_to_remove"
     
     echo "  ✓ Cleaned up $count image(s)"
   }

   # Helper function to pull image with spinner (shared by all variants)
   __copilot_pull_image() {
     local image_name="$1"
     printf "📥 Pulling latest image: ${image_name}... "
     
     (docker pull "$image_name" > /dev/null 2>&1) &
     local pull_pid=$!
     local spin='|/-\'
     
     local i=0
     while ps -p $pull_pid > /dev/null; do
       i=$(( (i+1) % 4 ))
       printf "%s\b" "${spin:$i:1}"
       sleep 0.1
     done

     wait $pull_pid
     local pull_status=$?
     
     if [ $pull_status -eq 0 ]; then
       echo "✅"
       return 0
     else
       echo "❌"
       echo "Error: Failed to pull the Docker image. Please check your Docker setup and network."
       return 1
     fi
   }

   # Core function to run copilot (shared by all variants)
   __copilot_run() {
     local image_tag="$1"
     local allow_all_tools="$2"
     local skip_cleanup="$3"
     local skip_pull="$4"
     shift 4
     
     __copilot_security_check || return 1
     
     local image_name="ghcr.io/gordonbeeming/copilot_here:${image_tag}"
     
     echo "🚀 Using image: ${image_name}"
     
     # Pull latest image unless skipped
     if [ "$skip_pull" != "true" ]; then
       __copilot_pull_image "$image_name" || return 1
     else
       echo "⏭️  Skipping image pull"
     fi
     
     # Cleanup old images unless skipped
     if [ "$skip_cleanup" != "true" ]; then
       __copilot_cleanup_images "$image_name"
     else
       echo "⏭️  Skipping image cleanup"
     fi

     local copilot_config_path="$HOME/.config/copilot-cli-docker"
     mkdir -p "$copilot_config_path"

     local token=$(gh auth token 2>/dev/null)
     if [ -z "$token" ]; then
       echo "⚠️  Could not retrieve token using 'gh auth token'. Please ensure you are logged in."
     fi

     local docker_args=(
       --rm -it
       -v "$(pwd)":/work
       -v "$copilot_config_path":/home/appuser/.copilot
       -e PUID=$(id -u)
       -e PGID=$(id -g)
       -e GITHUB_TOKEN="$token"
       "$image_name"
     )

     local copilot_args=("copilot")
     
     # Add --allow-all-tools if in YOLO mode
     if [ "$allow_all_tools" = "true" ]; then
       copilot_args+=("--allow-all-tools")
     fi
     
     # If no arguments provided, start interactive mode with banner
     if [ $# -eq 0 ]; then
       copilot_args+=("--banner")
     else
       # Pass all arguments directly to copilot for maximum flexibility
       copilot_args+=("$@")
     fi

     docker run "${docker_args[@]}" "${copilot_args[@]}"
   }

   # Safe Mode: Asks for confirmation before executing
   copilot_here() {
     local image_tag="latest"
     local skip_cleanup="false"
     local skip_pull="false"
     local args=()
     
     # Parse arguments for image variant and control flags
     while [[ $# -gt 0 ]]; do
       case "$1" in
        -h|--help)
          cat << 'EOF'
================================================================================
COPILOT_HERE WRAPPER - HELP
================================================================================
copilot_here - GitHub Copilot CLI in a secure Docker container (Safe Mode)

USAGE:
  copilot_here [OPTIONS] [COPILOT_ARGS]

OPTIONS:
  -d, --dotnet              Use .NET image variant
  -dp, --dotnet-playwright  Use .NET + Playwright image variant
  --no-cleanup              Skip cleanup of unused Docker images
  --no-pull                 Skip pulling the latest image
  --update-scripts          Update scripts from GitHub repository
  -h, --help                Show this help message

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
    ... and more (see GitHub Copilot CLI help below)

EXAMPLES:
  # Interactive mode
  copilot_here
  
  # Ask a question (short syntax)
  copilot_here -p "how do I list files in bash?"
  
  # Use specific AI model
  copilot_here --model gpt-5 -p "explain this code"
  
  # Resume previous session
  copilot_here --continue
  
  # Use .NET image with custom log level
  copilot_here -d --log-level debug -p "build this .NET project"
  
  # Fast mode (skip cleanup and pull)
  copilot_here --no-cleanup --no-pull -p "quick question"

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage)

VERSION: 2025-10-27.5
REPOSITORY: https://github.com/GordonBeeming/copilot_here

================================================================================
GITHUB COPILOT CLI - NATIVE HELP
================================================================================
EOF
          # Run copilot --help to show native help
          __copilot_run "$image_tag" "false" "true" "true" "--help"
          return 0
          ;;
         -d|--dotnet)
           image_tag="dotnet"
           shift
           ;;
         -dp|--dotnet-playwright)
           image_tag="dotnet-playwright"
           shift
           ;;
         --no-cleanup)
           skip_cleanup="true"
           shift
           ;;
         --no-pull)
           skip_pull="true"
           shift
           ;;
         --update-scripts)
           echo "📦 Updating copilot_here scripts from GitHub..."
           
           # Check if using standalone file installation
           if [ -f ~/.copilot_here.sh ]; then
             echo "✅ Detected standalone installation at ~/.copilot_here.sh"
             curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o ~/.copilot_here.sh
             echo "✅ Scripts updated successfully!"
             echo "🔄 Reload your shell to use the updated version"
             return 0
           fi
           
           # Inline installation - update shell config
           local config_file=""
           if [ -n "$ZSH_VERSION" ]; then
             config_file="${ZDOTDIR:-$HOME}/.zshrc"
           elif [ -n "$BASH_VERSION" ]; then
             config_file="$HOME/.bashrc"
           else
             echo "❌ Unsupported shell. Please update manually."
             return 1
           fi
           
           if [ ! -f "$config_file" ]; then
             echo "❌ Shell config not found: $config_file"
             return 1
           fi
           
           # Download latest
           local temp_script=$(mktemp)
           if ! curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o "$temp_script"; then
             echo "❌ Failed to download script"
             rm -f "$temp_script"
             return 1
           fi
           
           # Backup
           cp "$config_file" "${config_file}.backup.$(date +%Y%m%d_%H%M%S)"
           echo "✅ Created backup"
           
           # Replace script
           if grep -q "# copilot_here shell functions" "$config_file"; then
             awk '/# copilot_here shell functions/,/^}$/ {next} {print}' "$config_file" > "${config_file}.tmp"
             cat "$temp_script" >> "${config_file}.tmp"
             mv "${config_file}.tmp" "$config_file"
             echo "✅ Scripts updated!"
           else
             echo "" >> "$config_file"
             cat "$temp_script" >> "$config_file"
             echo "✅ Scripts added!"
           fi
           
           rm -f "$temp_script"
           echo "🔄 Reload: source $config_file"
           return 0
           ;;
         *)
           args+=("$1")
           shift
           ;;
       esac
     done
     
     __copilot_run "$image_tag" "false" "$skip_cleanup" "$skip_pull" "${args[@]}"
   }

   # YOLO Mode: Auto-approves all tool usage
   copilot_yolo() {
     local image_tag="latest"
     local skip_cleanup="false"
     local skip_pull="false"
     local args=()
     
     # Parse arguments for image variant and control flags
     while [[ $# -gt 0 ]]; do
       case "$1" in
        -h|--help)
          cat << 'EOF'
================================================================================
COPILOT_YOLO WRAPPER - HELP
================================================================================
copilot_yolo - GitHub Copilot CLI in a secure Docker container (YOLO Mode)

USAGE:
  copilot_yolo [OPTIONS] [COPILOT_ARGS]

OPTIONS:
  -d, --dotnet              Use .NET image variant
  -dp, --dotnet-playwright  Use .NET + Playwright image variant
  --no-cleanup              Skip cleanup of unused Docker images
  --no-pull                 Skip pulling the latest image
  --update-scripts          Update scripts from GitHub repository
  -h, --help                Show this help message

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
    ... and more (see GitHub Copilot CLI help below)

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
  copilot_yolo --no-cleanup -p "generate README"

WARNING:
  YOLO mode automatically approves ALL tool usage without confirmation.
  Use with caution and only in trusted environments.

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage)

VERSION: 2025-10-27.5
REPOSITORY: https://github.com/GordonBeeming/copilot_here

================================================================================
GITHUB COPILOT CLI - NATIVE HELP
================================================================================
EOF
          # Run copilot --help to show native help
          __copilot_run "$image_tag" "true" "true" "true" "--help"
          return 0
          ;;
         -d|--dotnet)
           image_tag="dotnet"
           shift
           ;;
         -dp|--dotnet-playwright)
           image_tag="dotnet-playwright"
           shift
           ;;
         --no-cleanup)
           skip_cleanup="true"
           shift
           ;;
         --no-pull)
           skip_pull="true"
           shift
           ;;
         --update-scripts)
           echo "📦 Updating copilot_here scripts from GitHub..."
           
           # Check if using standalone file installation
           if [ -f ~/.copilot_here.sh ]; then
             echo "✅ Detected standalone installation at ~/.copilot_here.sh"
             curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o ~/.copilot_here.sh
             echo "✅ Scripts updated successfully!"
             echo "🔄 Reload your shell to use the updated version"
             return 0
           fi
           
           # Inline installation - update shell config
           local config_file=""
           if [ -n "$ZSH_VERSION" ]; then
             config_file="${ZDOTDIR:-$HOME}/.zshrc"
           elif [ -n "$BASH_VERSION" ]; then
             config_file="$HOME/.bashrc"
           else
             echo "❌ Unsupported shell. Please update manually."
             return 1
           fi
           
           if [ ! -f "$config_file" ]; then
             echo "❌ Shell config not found: $config_file"
             return 1
           fi
           
           # Download latest
           local temp_script=$(mktemp)
           if ! curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o "$temp_script"; then
             echo "❌ Failed to download script"
             rm -f "$temp_script"
             return 1
           fi
           
           # Backup
           cp "$config_file" "${config_file}.backup.$(date +%Y%m%d_%H%M%S)"
           echo "✅ Created backup"
           
           # Replace script
           if grep -q "# copilot_here shell functions" "$config_file"; then
             awk '/# copilot_here shell functions/,/^}$/ {next} {print}' "$config_file" > "${config_file}.tmp"
             cat "$temp_script" >> "${config_file}.tmp"
             mv "${config_file}.tmp" "$config_file"
             echo "✅ Scripts updated!"
           else
             echo "" >> "$config_file"
             cat "$temp_script" >> "$config_file"
             echo "✅ Scripts added!"
           fi
           
           rm -f "$temp_script"
           echo "🔄 Reload: source $config_file"
           return 0
           ;;
         *)
           args+=("$1")
           shift
           ;;
       esac
     done
     
     __copilot_run "$image_tag" "true" "$skip_cleanup" "$skip_pull" "${args[@]}"
   }
   ```
   </details>

**Reload your shell** (e.g., `source ~/.zshrc`).

---

### For Windows (PowerShell)

**Quick Install (Recommended):**

Download and source the script in your PowerShell profile:

```powershell
# Download the script
$scriptPath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $scriptPath

# Add to your PowerShell profile - only if not already there
if (-not (Select-String -Path $PROFILE -Pattern "copilot_here.ps1" -Quiet -ErrorAction SilentlyContinue)) {
    Add-Content $PROFILE "`n. $scriptPath"
}

# Reload your profile
. $PROFILE
```

To update later, just run: `Copilot-Here -UpdateScripts`

---

**Manual Install (Alternative):**

1. Save the following as `copilot_here.ps1` in a location of your choice (e.g., `C:\Users\YourName\Documents\PowerShell\`):

   <details>
   <summary>Click to expand PowerShell code</summary>

   ```powershell
   # copilot_here PowerShell functions
   # Version: 2025-10-27.5
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
           [switch]$UpdateScripts,
           [Parameter(ValueFromRemainingArguments=$true)]
           [string[]]$Prompt
       )

       if ($UpdateScripts) {
           Write-Host "📦 Updating copilot_here scripts from GitHub..."
           
           # Check for standalone file
           $standalonePath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
           if (Test-Path $standalonePath) {
               Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $standalonePath
               Write-Host "✅ Scripts updated successfully!"
               Write-Host "🔄 Reload: . $standalonePath"
               return
           }
           
           # Inline installation - update profile
           if (-not (Test-Path $PROFILE)) {
               Write-Host "❌ PowerShell profile not found: $PROFILE"
               return
           }
           
           # Download
           $tempScript = Join-Path $env:TEMP "copilot_here_update.ps1"
           try {
               Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $tempScript
           } catch {
               Write-Host "❌ Failed to download: $_" -ForegroundColor Red
               return
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
           Write-Host "🔄 Reload: . `$PROFILE"
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

VERSION: 2025-10-27.5
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
           [switch]$UpdateScripts,
           [Parameter(ValueFromRemainingArguments=$true)]
           [string[]]$Prompt
       )

       if ($UpdateScripts) {
           Write-Host "📦 Updating copilot_here scripts from GitHub..."
           
           # Check for standalone file
           $standalonePath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
           if (Test-Path $standalonePath) {
               Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $standalonePath
               Write-Host "✅ Scripts updated successfully!"
               Write-Host "🔄 Reload: . $standalonePath"
               return
           }
           
           # Inline installation - update profile
           if (-not (Test-Path $PROFILE)) {
               Write-Host "❌ PowerShell profile not found: $PROFILE"
               return
           }
           
           # Download
           $tempScript = Join-Path $env:TEMP "copilot_here_update.ps1"
           try {
               Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $tempScript
           } catch {
               Write-Host "❌ Failed to download: $_" -ForegroundColor Red
               return
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
           Write-Host "🔄 Reload: . `$PROFILE"
           return
       }

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

VERSION: 2025-10-27.5
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
