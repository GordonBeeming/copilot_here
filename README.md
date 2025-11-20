# copilot-here: A Secure, Portable Copilot CLI Environment

Run the GitHub Copilot CLI from any directory on your machine, inside a sandboxed Docker container that automatically uses your existing `gh` authentication.

[![Build and Publish Docker Images](https://github.com/GordonBeeming/copilot_here/actions/workflows/publish.yml/badge.svg?branch=main)](https://github.com/GordonBeeming/copilot_here/actions/workflows/publish.yml)

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
- You must be logged in to the GitHub CLI. You can check by running `gh auth status`. Your token **must** have the `copilot` and `read:packages` scopes. If it doesn't, run `gh auth refresh -h github.com -s copilot,read:packages` to add them.

## 🛠️ Setup Instructions

Choose your platform below. The scripts include both **Safe Mode** (asks for confirmation) and **YOLO Mode** (auto-approves) functions. You can use either or both depending on your needs.

### Execution Modes

**Safe Mode (`copilot_here`)** - Always asks for confirmation before executing commands. Recommended for general development work where you want control over what gets executed.

**YOLO Mode (`copilot_yolo`)** - Automatically approves all tool usage without confirmation. Convenient for trusted workflows but use with caution as it can execute commands without prompting.

### Image Variants

All images support both **AMD64** (x86_64) and **ARM64** (Apple Silicon, etc.) architectures.

All functions support switching between Docker image variants using flags:
- **No flag** - Base image (Node.js, Git, basic tools)
- **`-d` or `--dotnet`** - .NET image (includes .NET 8, 9 & 10 SDKs)
- **`-d8` or `--dotnet8`** - .NET 8 image (includes .NET 8 SDK)
- **`-d9` or `--dotnet9`** - .NET 9 image (includes .NET 9 SDK)
- **`-d10` or `--dotnet10`** - .NET 10 image (includes .NET 10 SDK)
- **`-dp` or `--dotnet-playwright`** - .NET + Playwright image (includes browser automation)

### Additional Options

- **`-h` or `--help`** - Show usage help and examples (Bash/Zsh) or `-h` / `-Help` (PowerShell)
- **`--no-cleanup`** - Skip cleanup of unused Docker images (Bash/Zsh) or `-NoCleanup` (PowerShell)
- **`--no-pull`** - Skip pulling the latest image (Bash/Zsh) or `-NoPull` (PowerShell)
- **`--mount <path>`** - Mount a directory as read-only (Bash/Zsh) or `-Mount <path>` (PowerShell)
- **`--mount-rw <path>`** - Mount a directory as read-write (Bash/Zsh) or `-MountRw <path>` (PowerShell)
- **`--save-mount <path>`** - Save a mount to local config (Bash/Zsh) or `-SaveMount <path>` (PowerShell)
- **`--save-mount-global <path>`** - Save a mount to global config (Bash/Zsh) or `-SaveMountGlobal <path>` (PowerShell)
- **`--remove-mount <path>`** - Remove a saved mount (Bash/Zsh) or `-RemoveMount <path>` (PowerShell)
- **`--list-mounts`** - List all configured mounts (Bash/Zsh) or `-ListMounts` (PowerShell)
- **`--update-scripts`** - Update scripts from GitHub repository (Bash/Zsh) or `-UpdateScripts` (PowerShell)

> **Note:** The script automatically checks for updates before running and prompts you if a new version is available.

> ⚠️ **Security Note:** Both modes check for proper GitHub token scopes and warn about overly privileged tokens.

### Directory Mounting

By default, `copilot_here` only mounts the current working directory. You can mount additional directories using flags or configuration files.

**CLI Flags:**
- `--mount ./path/to/dir` (Read-only)
- `--mount-rw ./path/to/dir` (Read-write)

**Configuration Files:**
- Global: `~/.config/copilot_here/mounts.conf`
- Local: `.copilot_here/mounts.conf`

**Format:** `path/to/dir:ro` or `path/to/dir:rw` (one per line)

**Management Commands:**
Use `--save-mount`, `--save-mount-global`, `--remove-mount`, and `--list-mounts` to manage persistent mounts.

### Image Management

You can configure the default image tag to use (e.g., `dotnet`, `dotnet-playwright`, or a specific SHA) so you don't have to pass flags every time.

**Management Commands:**
- `--show-image` - Show current default image configuration (Bash/Zsh) or `-ShowImage` (PowerShell)
- `--set-image <tag>` - Set default image in local config (Bash/Zsh) or `-SetImage <tag>` (PowerShell)
- `--set-image-global <tag>` - Set default image in global config (Bash/Zsh) or `-SetImageGlobal <tag>` (PowerShell)

**Configuration Files:**
- Global: `~/.config/copilot_here/image.conf`
- Local: `.copilot_here/image.conf`



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



**Manual Install (Alternative):**

If you prefer not to use the quick install method, you can manually copy the script file:

1. **Download the script:**
   ```bash
   curl -fsSL https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh -o ~/.copilot_here.sh
   ```

2. **Add to your shell profile** (`~/.zshrc` or `~/.bashrc`):
   ```bash
   source ~/.copilot_here.sh
   ```

3. **Reload your shell:**
   ```bash
   source ~/.zshrc  # or source ~/.bashrc
   ```

**Note:** If you want to disable the auto-update functionality, you can remove the `--update-scripts` and `--upgrade-scripts` case blocks from the downloaded script file.



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



**Manual Install (Alternative):**

If you prefer not to use the quick install method, you can manually copy the script file:

1. **Download the script:**
   ```powershell
   $scriptPath = "$env:USERPROFILE\Documents\PowerShell\copilot_here.ps1"
   Invoke-WebRequest -Uri "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1" -OutFile $scriptPath
   ```

2. **Add to your PowerShell profile:**
   ```powershell
   # Add this line to your PowerShell profile
   . $scriptPath
   ```
   
   To edit your profile, run:
   ```powershell
   notepad $PROFILE
   ```

3. **Reload your PowerShell profile:**
   ```powershell
   . $PROFILE
   ```

**Note:** If you want to disable the auto-update functionality, you can remove the `-UpdateScripts` and `-UpgradeScripts` parameter blocks from the downloaded script file.




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
- .NET 10.0 SDK
- ASP.NET Core runtimes
- All base image features

**Usage:**
```bash
# Update the image_name in your function to use the .NET variant
local image_name="ghcr.io/gordonbeeming/copilot_here:dotnet"
```

**Best for:** .NET development, building/testing .NET applications, ASP.NET Core projects

#### .NET 8 Image
**Tag:** `dotnet-8`

Extends the base image with .NET 8 SDK support.

**Includes:**
- .NET 8.0 SDK
- All base image features

**Best for:** .NET 8 specific development

#### .NET 9 Image
**Tag:** `dotnet-9`

Extends the base image with .NET 9 SDK support.

**Includes:**
- .NET 9.0 SDK
- All base image features

**Best for:** .NET 9 specific development

#### .NET 10 Image
**Tag:** `dotnet-10`

Extends the base image with .NET 10 SDK support.

**Includes:**
- .NET 10.0 SDK
- All base image features

**Best for:** .NET 10 specific development

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
