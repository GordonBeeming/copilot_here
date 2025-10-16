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

Choose your platform and preferred mode. You can install both modes side-by-side with different command names (e.g., `copilot_here` for safe mode and `copilot_yolo` for auto-approve mode).

### Understanding the Two Modes

**Safe Mode (Recommended)** - Always asks for confirmation before executing commands. Use this for general development work where you want control over what gets executed.

**YOLO Mode (Auto-Approve)** - Automatically approves all tool usage without confirmation. Convenient for trusted workflows but use with caution as it can execute commands without prompting.

> ⚠️ **Security Note:** Both modes check for proper GitHub token scopes and warn about overly privileged tokens. The YOLO mode adds the `--allow-all-tools` flag which bypasses execution confirmation.

---

### Option 1: Safe Mode (Recommended)

### Option 1: Safe Mode (Recommended)

This mode asks for confirmation before executing any commands, giving you full control.

#### For Linux/macOS (Bash/Zsh)

1. **Add the function to your shell profile.**
   
   Open your shell's startup file (e.g., `~/.zshrc`, `~/.bashrc`, or `~/.config/fish/config.fish`) and add:

   <details>
   <summary>Click to expand bash/zsh code</summary>

   ```bash
   copilot_here() {
     # --- SECURITY CHECK ---
     # 1. Ensure the 'copilot' scope is present using a robust grep check.
     if ! gh auth status 2>/dev/null | grep "Token scopes:" | grep -q "'copilot'"; then
       echo "❌ Error: Your gh token is missing the required 'copilot' scope."
       echo "Please run 'gh auth refresh -h github.com -s copilot' to add it."
       return 1
     fi

     # 2. Warn if the token has highly privileged scopes.
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
     # --- END SECURITY CHECK ---

     # Define the image name for easy reference
     local image_name="ghcr.io/gordonbeeming/copilot_here:latest"

     # Pull the latest version of the image, showing a spinner for feedback.
     printf "Checking for the latest version of copilot_here... "
     
     # Run docker pull in the background and capture its process ID (PID)
     (docker pull "$image_name" > /dev/null 2>&1) &
     local pull_pid=$!
     local spin='|/-\'
     
     # While the pull process is running, display a spinner
     local i=0
     while ps -p $pull_pid > /dev/null; do
       i=$(( (i+1) % 4 ))
       # Print the spinner character, then move the cursor back
       printf "%s\b" "${spin:$i:1}"
       sleep 0.1
     done

     # Wait for the process to finish and get its exit code
     wait $pull_pid
     local pull_status=$?
     
     # Replace the spinner with a final status and add a newline
     if [ $pull_status -eq 0 ]; then
       echo "✅"
     else
       echo "❌"
       echo "Error: Failed to pull the Docker image. Please check your Docker setup and network."
       return 1
     fi

     # Define path for our persistent copilot config on the host machine.
     local copilot_config_path="$HOME/.config/copilot-cli-docker"
     mkdir -p "$copilot_config_path"

     # Use the 'gh' CLI itself to reliably get the current auth token.
     local token=$(gh auth token 2>/dev/null)
     if [ -z "$token" ]; then
       echo "⚠️  Could not retrieve token using 'gh auth token'. Please ensure you are logged in."
     fi

     # Base Docker command arguments
     local docker_args=(
       --rm -it
       -v "$(pwd)":/work
       -v "$copilot_config_path":/home/appuser/.copilot
       -e PUID=$(id -u)
       -e PGID=$(id -g)
       -e GITHUB_TOKEN="$token"
       "$image_name"
     )

     if [ $# -eq 0 ]; then
       # No arguments provided, start interactive mode with the banner.
       docker run "${docker_args[@]}" copilot --banner
     else
       # Arguments provided, run in non-interactive (but safe) mode.
       docker run "${docker_args[@]}" copilot -p "$*"
     fi
   }
   ```
   </details>

2. **Reload your shell** (e.g., `source ~/.zshrc`).

#### For Windows (PowerShell)

1. **Create the PowerShell function file.**
   
   Save the following as `copilot_here.ps1` in a location of your choice (e.g., `C:\Users\YourName\Documents\PowerShell\`):

   <details>
   <summary>Click to expand PowerShell code</summary>

   ```powershell
   function Copilot-Here {
       [CmdletBinding()]
       param (
           [Parameter(ValueFromRemainingArguments=$true)]
           [string[]]$Prompt
       )

       # --- SECURITY CHECK ---
       Write-Host "Verifying GitHub CLI authentication..."
       $authStatus = gh auth status 2>$null
       if (-not ($authStatus | Select-String -Quiet "'copilot'")) {
           Write-Host "❌ Error: Your gh token is missing the required 'copilot' scope." -ForegroundColor Red
           Write-Host "Please run 'gh auth refresh -h github.com -s copilot' to add it."
           return
       }

       $privilegedScopesPattern = "'(admin:|manage_|write:public_key|delete_repo|(write|delete)_packages)'"
       if ($authStatus | Select-String -Quiet $privilegedScopesPattern) {
           Write-Host "⚠️  Warning: Your GitHub token has highly privileged scopes." -ForegroundColor Yellow
           $confirmation = Read-Host "Are you sure you want to proceed with this token? [y/N]"
           if ($confirmation.ToLower() -ne 'y' -and $confirmation.ToLower() -ne 'yes') {
               Write-Host "Operation cancelled by user."
               return
           }
       }
       Write-Host "✅ Security checks passed."
       # --- END SECURITY CHECK ---

       $imageName = "ghcr.io/gordonbeeming/copilot_here:latest"

       Write-Host -NoNewline "Checking for the latest version of copilot_here... "
       $pullJob = Start-Job -ScriptBlock { param($img) docker pull $img } -ArgumentList $imageName
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
       } else {
           Write-Host "❌" -ForegroundColor Red
           Write-Host "Error: Failed to pull the Docker image." -ForegroundColor Red
           if (-not [string]::IsNullOrEmpty($pullOutput)) {
               Write-Host "Docker output:`n$pullOutput"
           }
           Remove-Job $pullJob
           return
       }
       Remove-Job $pullJob

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
       if ($Prompt.Length -eq 0) {
           $copilotCommand += "--banner"
       } else {
           $copilotCommand += "-p", ($Prompt -join ' ')
       }

       $finalDockerArgs = $dockerBaseArgs + $copilotCommand
       docker run $finalDockerArgs
   }

   Set-Alias -Name copilot_here -Value Copilot-Here
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

### Option 2: YOLO Mode (Auto-Approve)

This mode automatically approves all tool usage. Use with caution!

### Option 2: YOLO Mode (Auto-Approve)

This mode automatically approves all tool usage. Use with caution!

#### For Linux/macOS (Bash/Zsh)

1. **Add the function to your shell profile.**
   
   You can add this alongside the safe version with a different name like `copilot_yolo`:

   <details>
   <summary>Click to expand bash/zsh code</summary>

   ```bash
   copilot_yolo() {
     # --- SECURITY CHECK ---
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
     # --- END SECURITY CHECK ---

     local image_name="ghcr.io/gordonbeeming/copilot_here:latest"

     printf "Checking for the latest version of copilot_here... "
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
     else
       echo "❌"
       echo "Error: Failed to pull the Docker image. Please check your Docker setup and network."
       return 1
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

     if [ $# -eq 0 ]; then
       docker run "${docker_args[@]}" copilot --banner --allow-all-tools
     else
       docker run "${docker_args[@]}" copilot -p "$*" --allow-all-tools
     fi
   }
   ```
   </details>

2. **Reload your shell** (e.g., `source ~/.zshrc`).

#### For Windows (PowerShell)

1. **Create the PowerShell function file.**
   
   Save the following as `copilot_yolo.ps1` (or add to your existing file):

   <details>
   <summary>Click to expand PowerShell code</summary>

   ```powershell
   function Copilot-Yolo {
       [CmdletBinding()]
       param (
           [Parameter(ValueFromRemainingArguments=$true)]
           [string[]]$Prompt
       )

       # --- SECURITY CHECK ---
       Write-Host "Verifying GitHub CLI authentication..."
       $authStatus = gh auth status 2>$null
       if (-not ($authStatus | Select-String -Quiet "'copilot'")) {
           Write-Host "❌ Error: Your gh token is missing the required 'copilot' scope." -ForegroundColor Red
           Write-Host "Please run 'gh auth refresh -h github.com -s copilot' to add it."
           return
       }

       $privilegedScopesPattern = "'(admin:|manage_|write:public_key|delete_repo|(write|delete)_packages)'"
       if ($authStatus | Select-String -Quiet $privilegedScopesPattern) {
           Write-Host "⚠️  Warning: Your GitHub token has highly privileged scopes." -ForegroundColor Yellow
           $confirmation = Read-Host "Are you sure you want to proceed with this token? [y/N]"
           if ($confirmation.ToLower() -ne 'y' -and $confirmation.ToLower() -ne 'yes') {
               Write-Host "Operation cancelled by user."
               return
           }
       }
       Write-Host "✅ Security checks passed."
       # --- END SECURITY CHECK ---

       $imageName = "ghcr.io/gordonbeeming/copilot_here:latest"

       Write-Host -NoNewline "Checking for the latest version of copilot_here... "
       $pullJob = Start-Job -ScriptBlock { param($img) docker pull $img } -ArgumentList $imageName
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
       } else {
           Write-Host "❌" -ForegroundColor Red
           Write-Host "Error: Failed to pull the Docker image." -ForegroundColor Red
           if (-not [string]::IsNullOrEmpty($pullOutput)) {
               Write-Host "Docker output:`n$pullOutput"
           }
           Remove-Job $pullJob
           return
       }
       Remove-Job $pullJob

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
       if ($Prompt.Length -eq 0) {
           $copilotCommand += "--banner", "--allow-all-tools"
       } else {
           $copilotCommand += "-p", ($Prompt -join ' '), "--allow-all-tools"
       }

       $finalDockerArgs = $dockerBaseArgs + $copilotCommand
       docker run $finalDockerArgs
   }

   Set-Alias -Name copilot_yolo -Value Copilot-Yolo
   ```
   </details>

2. **Add it to your PowerShell profile** (same process as Option 1).

3. **Reload your PowerShell profile:**
   ```powershell
   . $PROFILE
   ```

## Usage

Once set up, using it is simple on any platform.

### Interactive Mode

Start a full chat session with the welcome banner:

**Linux/macOS:**
```bash
copilot_here
```

**Windows:**
```powershell
copilot_here
```

### Non-Interactive Mode

Pass a prompt directly to get a quick response.

**Safe Mode** (asks for confirmation before executing):

```bash
# Linux/macOS
copilot_here "suggest a git command to view the last 5 commits"
copilot_here "explain the code in ./my-script.js"

# Windows (same commands work!)
copilot_here "suggest a git command to view the last 5 commits"
copilot_here "explain the code in ./my-script.js"
```

**YOLO Mode** (auto-approves execution):

```bash
# Linux/macOS
copilot_yolo "write a C# function that takes a string and returns it in reverse"
copilot_yolo "run the tests and fix any failures"

# Windows (same commands work!)
copilot_yolo "write a C# function that takes a string and returns it in reverse"
copilot_yolo "run the tests and fix any failures"
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
