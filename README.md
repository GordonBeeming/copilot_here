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
  --upgrade-scripts         Alias for --update-scripts

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
   # Version: 2025-11-05.1
   # Repository: https://github.com/GordonBeeming/copilot_here
   
   # Helper function to detect emoji support
   __copilot_supports_emoji() {
     [[ "$LANG" == *"UTF-8"* ]] && [[ "$TERM" != "dumb" ]]
   }
   
   # Helper function to load mounts from config file
   __copilot_load_mounts() {
     local config_file="$1"
     local var_name="$2"
     
     if [ -f "$config_file" ]; then
       while IFS= read -r line || [ -n "$line" ]; do
         # Skip empty lines and comments
         [[ -z "$line" || "$line" =~ ^[[:space:]]*# ]] && continue
         eval "${var_name}+=(\"\$line\")"
       done < "$config_file"
     fi
   }
   
   # Helper function to resolve and validate mount path
   __copilot_resolve_mount_path() {
     local path="$1"
     local resolved_path
     
     # Expand tilde
     if [[ "$path" == "~"* ]]; then
       # Remove leading ~ and prepend HOME (works in both bash and zsh)
       local path_without_tilde="${path#\~}"
       resolved_path="${HOME}${path_without_tilde}"
     # Handle relative paths
     elif [[ "$path" != "/"* ]]; then
       local dir_part=$(dirname "$path" 2>/dev/null || echo ".")
       local base_part=$(basename "$path" 2>/dev/null || echo "$path")
       local abs_dir=$(cd "$dir_part" 2>/dev/null && pwd || echo "$PWD")
       resolved_path="$abs_dir/$base_part"
     else
       resolved_path="$path"
     fi
     
     # Warn if path doesn't exist
     if [ ! -e "$resolved_path" ]; then
       echo "⚠️  Warning: Path does not exist: $resolved_path" >&2
     fi
     
     # Security warning for sensitive paths - require confirmation
     case "$resolved_path" in
       /|/etc|/etc/*|~/.ssh|~/.ssh/*|/root|/root/*)
         echo "⚠️  Warning: Mounting sensitive system path: $resolved_path" >&2
         printf "Are you sure you want to mount this sensitive path? [y/N]: " >&2
         read confirmation
         local lower_confirmation
         lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
         if [[ "$lower_confirmation" != "y" && "$lower_confirmation" != "yes" ]]; then
           echo "Operation cancelled by user." >&2
           return 1
         fi
         ;;
     esac
     
     echo "$resolved_path"
   }
   
   # Helper function to display mounts list
   __copilot_list_mounts() {
     local global_config="$HOME/.config/copilot_here/mounts.conf"
     local local_config=".copilot_here/mounts.conf"
     
     local global_mounts=()
     local local_mounts=()
     
     __copilot_load_mounts "$global_config" global_mounts
     __copilot_load_mounts "$local_config" local_mounts
     
     if [ ${#global_mounts[@]} -eq 0 ] && [ ${#local_mounts[@]} -eq 0 ]; then
       echo "📂 No saved mounts configured"
       echo ""
       echo "Add mounts with:"
       echo "  copilot_here --save-mount <path>         # Save to local config"
       echo "  copilot_here --save-mount-global <path>  # Save to global config"
       return 0
     fi
     
     echo "📂 Saved mounts:"
     
     if __copilot_supports_emoji; then
       for mount in "${global_mounts[@]}"; do
         echo "  🌍 $mount"
       done
       for mount in "${local_mounts[@]}"; do
         echo "  📍 $mount"
       done
     else
       for mount in "${global_mounts[@]}"; do
         echo "  G: $mount"
       done
       for mount in "${local_mounts[@]}"; do
         echo "  L: $mount"
       done
     fi
     
     echo ""
     echo "Config files:"
     echo "  Global: $global_config"
     echo "  Local:  $local_config"
   }
   
   # Helper function to save mount to config
   __copilot_save_mount() {
     local path="$1"
     local is_global="$2"
     local config_file
     local normalized_path
     
     # Normalize path to absolute or home-relative for global mounts
     if [ "$is_global" = "true" ]; then
       # For global mounts, convert to absolute or ~/... format
       if [[ "$path" == "~"* ]]; then
         # Keep tilde format for home directory
         normalized_path="$path"
       elif [[ "$path" == "/"* ]]; then
         # Already absolute - check if it's in home directory
         if [[ "$path" == "$HOME"* ]]; then
           # Convert to tilde format
           normalized_path="~${path#$HOME}"
         else
           normalized_path="$path"
         fi
       else
         # Relative path - convert to absolute
         local dir_part=$(dirname "$path" 2>/dev/null || echo ".")
         local base_part=$(basename "$path" 2>/dev/null || echo "$path")
         local abs_dir=$(cd "$dir_part" 2>/dev/null && pwd || echo "$PWD")
         normalized_path="$abs_dir/$base_part"
         
         # If it's in home directory, convert to tilde format
         if [[ "$normalized_path" == "$HOME"* ]]; then
           normalized_path="~${normalized_path#$HOME}"
         fi
       fi
       
       config_file="$HOME/.config/copilot_here/mounts.conf"
       /bin/mkdir -p "$HOME/.config/copilot_here"
     else
       # For local mounts, keep path as-is (relative is OK for project-specific)
       normalized_path="$path"
       config_file=".copilot_here/mounts.conf"
       /bin/mkdir -p ".copilot_here"
     fi
     
     # Check if already exists
     if [ -f "$config_file" ] && grep -qF "$normalized_path" "$config_file"; then
       echo "⚠️  Mount already exists in config: $normalized_path"
       return 1
     fi
     
     echo "$normalized_path" >> "$config_file"
     
     if [ "$is_global" = "true" ]; then
       echo "✅ Saved to global config: $normalized_path"
       if [ "$normalized_path" != "$path" ]; then
         echo "   (normalized from: $path)"
       fi
     else
       echo "✅ Saved to local config: $normalized_path"
     fi
     echo "   Config file: $config_file"
   }
   
   # Helper function to remove mount from config
   __copilot_remove_mount() {
     local path="$1"
     local global_config="$HOME/.config/copilot_here/mounts.conf"
     local local_config=".copilot_here/mounts.conf"
     local removed=false
     
     # Try to remove from global config
     if [ -f "$global_config" ] && grep -qF "$path" "$global_config"; then
       grep -vF "$path" "$global_config" > "$global_config.tmp" && mv "$global_config.tmp" "$global_config"
       echo "✅ Removed from global config: $path"
       removed=true
     fi
     
     # Try to remove from local config
     if [ -f "$local_config" ] && grep -qF "$path" "$local_config"; then
       grep -vF "$path" "$local_config" > "$local_config.tmp" && mv "$local_config.tmp" "$local_config"
       echo "✅ Removed from local config: $path"
       removed=true
     fi
     
     if [ "$removed" = "false" ]; then
       echo "⚠️  Mount not found in any config: $path"
       return 1
     fi
   }
   
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
     echo "🧹 Cleaning up old copilot_here images (older than 7 days)..."
     
     # Get cutoff timestamp (7 days ago)
     local cutoff_date=$(date -d '7 days ago' +%s 2>/dev/null || date -v-7d +%s 2>/dev/null)
     
     # Get all copilot_here images with the project label, excluding <none> tags
     local all_images=$(docker images --filter "label=project=copilot_here" --format "{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" | grep -v ":<none>" || true)
     
     if [ -z "$all_images" ]; then
       echo "  ✓ No images to clean up"
       return 0
     fi
     
     local count=0
     while IFS='|' read -r image created_at; do
       if [ -n "$image" ] && [ "$image" != "$keep_image" ]; then
         # Parse creation date (format: "2025-01-28 12:34:56 +0000 UTC")
         local image_date=$(date -d "$created_at" +%s 2>/dev/null || date -j -f "%Y-%m-%d %H:%M:%S %z %Z" "$created_at" +%s 2>/dev/null)
         
         if [ -n "$image_date" ] && [ "$image_date" -lt "$cutoff_date" ]; then
           echo "  🗑️  Removing old image: $image (created: ${created_at})"
           if docker rmi "$image" > /dev/null 2>&1; then
             ((count++))
           else
             echo "  ⚠️  Failed to remove: $image (may be in use)"
           fi
         fi
       fi
     done <<< "$all_images"
     
     if [ "$count" -eq 0 ]; then
       echo "  ✓ No old images to clean up"
     else
       echo "  ✓ Cleaned up $count old image(s)"
     fi
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
     local mounts_ro_name="$5"
     local mounts_rw_name="$6"
     shift 6
     
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
     /bin/mkdir -p "$copilot_config_path"

     local token=$(gh auth token 2>/dev/null)
     if [ -z "$token" ]; then
       echo "⚠️  Could not retrieve token using 'gh auth token'. Please ensure you are logged in."
     fi

     local current_dir="$(pwd)"
     local docker_args=(
       --rm -it
       -v "$current_dir:$current_dir"
       -w "$current_dir"
       -v "$copilot_config_path":/home/appuser/.copilot
       -e PUID=$(id -u)
       -e PGID=$(id -g)
       -e GITHUB_TOKEN="$token"
     )

     # Load mounts from config files
     local global_config="$HOME/.config/copilot_here/mounts.conf"
     local local_config=".copilot_here/mounts.conf"
     local config_mounts=()
     
     __copilot_load_mounts "$global_config" config_mounts
     __copilot_load_mounts "$local_config" config_mounts
     
     # Track all mounted paths for display and --add-dir
     local all_mount_paths=()
     local mount_display=()
     
     # Add current working directory to display
     mount_display+=("📁 $current_dir")
     
     # Process config mounts
     local seen_paths=()
     for mount in "${config_mounts[@]}"; do
       local mount_path="${mount%:*}"
       local mount_mode="${mount##*:}"
       
       # If no mode specified, default to ro
       if [ "$mount_path" = "$mount_mode" ]; then
         mount_mode="ro"
       fi
       
       local resolved_path=$(__copilot_resolve_mount_path "$mount_path")
       if [ $? -ne 0 ]; then
         continue  # Skip this mount if user cancelled
       fi
       
       # Skip if already seen (dedup)
       if [[ " ${seen_paths[@]} " =~ " ${resolved_path} " ]]; then
         continue
       fi
       seen_paths+=("$resolved_path")
       
       docker_args+=(-v "$resolved_path:$resolved_path:$mount_mode")
       all_mount_paths+=("$resolved_path")
       
       # Determine source for display
       local source_icon
       if __copilot_supports_emoji; then
         if grep -qF "$mount" "$global_config" 2>/dev/null; then
           source_icon="🌍"
         else
           source_icon="📍"
         fi
       else
         if grep -qF "$mount" "$global_config" 2>/dev/null; then
           source_icon="G:"
         else
           source_icon="L:"
         fi
       fi
       
       mount_display+=("   $source_icon $resolved_path ($mount_mode)")
     done
     
     # Process CLI read-only mounts
     eval "local mounts_ro_array=(\"\${${mounts_ro_name}[@]}\")"
     for mount_path in "${mounts_ro_array[@]}"; do
       local resolved_path=$(__copilot_resolve_mount_path "$mount_path")
       if [ $? -ne 0 ]; then
         continue  # Skip this mount if user cancelled
       fi
       
       # Skip if already seen
       if [[ " ${seen_paths[@]} " =~ " ${resolved_path} " ]]; then
         continue
       fi
       seen_paths+=("$resolved_path")
       
       docker_args+=(-v "$resolved_path:$resolved_path:ro")
       all_mount_paths+=("$resolved_path")
       
       if __copilot_supports_emoji; then
         mount_display+=("   🔧 $resolved_path (ro)")
       else
         mount_display+=("   CLI: $resolved_path (ro)")
       fi
     done
     
     # Process CLI read-write mounts
     eval "local mounts_rw_array=(\"\${${mounts_rw_name}[@]}\")"
     for mount_path in "${mounts_rw_array[@]}"; do
       local resolved_path=$(__copilot_resolve_mount_path "$mount_path")
       if [ $? -ne 0 ]; then
         continue  # Skip this mount if user cancelled
       fi
       
       # Skip if already seen (CLI overrides config)
       local override=false
       for i in "${!seen_paths[@]}"; do
         if [ "${seen_paths[$i]}" = "$resolved_path" ]; then
           # Replace read-only with read-write
           override=true
           # Update docker args to rw
           for j in "${!docker_args[@]}"; do
             if [[ "${docker_args[$j]}" == "-v" ]] && [[ "${docker_args[$((j+1))]}" == "$resolved_path:$resolved_path:ro" ]]; then
               docker_args[$((j+1))]="$resolved_path:$resolved_path:rw"
             fi
           done
           break
         fi
       done
       
       if [ "$override" = "false" ]; then
         seen_paths+=("$resolved_path")
         docker_args+=(-v "$resolved_path:$resolved_path:rw")
         all_mount_paths+=("$resolved_path")
       fi
       
       if __copilot_supports_emoji; then
         mount_display+=("   🔧 $resolved_path (rw)")
       else
         mount_display+=("   CLI: $resolved_path (rw)")
       fi
     done
     
     # Display mounts if there are extras
     if [ ${#all_mount_paths[@]} -gt 0 ]; then
       echo "📂 Mounts:"
       for display in "${mount_display[@]}"; do
         echo "$display"
       done
     fi
     
     docker_args+=("$image_name")

     local copilot_args=("copilot")
     
     # Add --allow-all-tools and --allow-all-paths if in YOLO mode
     if [ "$allow_all_tools" = "true" ]; then
       copilot_args+=("--allow-all-tools" "--allow-all-paths")
       # In YOLO mode, also add current dir and mounts to avoid any prompts
       copilot_args+=("--add-dir" "$current_dir")
       for mount_path in "${all_mount_paths[@]}"; do
         copilot_args+=("--add-dir" "$mount_path")
       done
     fi
     # In Safe Mode, don't auto-add directories - let Copilot CLI ask for permission
     
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
     local mounts_ro=()
     local mounts_rw=()
     
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
  copilot_here [MOUNT_MANAGEMENT]

OPTIONS:
  -d, --dotnet              Use .NET image variant
  -dp, --dotnet-playwright  Use .NET + Playwright image variant
  --mount <path>            Mount additional directory (read-only)
  --mount-rw <path>         Mount additional directory (read-write)
  --no-cleanup              Skip cleanup of unused Docker images
  --no-pull                 Skip pulling the latest image
  --update-scripts          Update scripts from GitHub repository
  --upgrade-scripts         Alias for --update-scripts
  -h, --help                Show this help message

MOUNT MANAGEMENT:
  --list-mounts             Show all configured mounts
  --save-mount <path>       Save mount to local config (.copilot_here/mounts.conf)
  --save-mount-global <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  --remove-mount <path>     Remove mount from configs

MOUNT CONFIG:
  Mounts can be configured in three ways (priority: CLI > Local > Global):
    1. Global: ~/.config/copilot_here/mounts.conf
    2. Local:  .copilot_here/mounts.conf
    3. CLI:    --mount and --mount-rw flags
  
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
    ... and more (see GitHub Copilot CLI help below)

EXAMPLES:
  # Interactive mode
  copilot_here
  
  # Mount additional directories
  copilot_here --mount ../investigations -p "analyze these files"
  copilot_here --mount-rw ~/notes --mount /data/research
  
  # Save mounts for reuse
  copilot_here --save-mount ~/investigations
  copilot_here --save-mount-global ~/common-data
  copilot_here --list-mounts
  
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
  copilot_yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-05.1
REPOSITORY: https://github.com/GordonBeeming/copilot_here

================================================================================
GITHUB COPILOT CLI - NATIVE HELP
================================================================================
EOF
          # Run copilot --help to show native help
          local empty_mounts_ro=()
          local empty_mounts_rw=()
          __copilot_run "$image_tag" "false" "true" "true" empty_mounts_ro empty_mounts_rw "--help"
          return 0
          ;;
        --list-mounts)
          __copilot_list_mounts
          return 0
          ;;
        --save-mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --save-mount requires a path argument"
            return 1
          fi
          __copilot_save_mount "$1" "false"
          return 0
          ;;
        --save-mount-global)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --save-mount-global requires a path argument"
            return 1
          fi
          __copilot_save_mount "$1" "true"
          return 0
          ;;
        --remove-mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --remove-mount requires a path argument"
            return 1
          fi
          __copilot_remove_mount "$1"
          return 0
          ;;
        --mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --mount requires a path argument"
            return 1
          fi
          mounts_ro+=("$1")
          shift
          ;;
        --mount-rw)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --mount-rw requires a path argument"
            return 1
          fi
          mounts_rw+=("$1")
          shift
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
         --update-scripts|--upgrade-scripts)
           echo "📦 Updating copilot_here scripts from GitHub..."
           
           # Get current version
           local current_version=""
           if [ -f ~/.copilot_here.sh ]; then
             current_version=$(sed -n '2s/# Version: //p' ~/.copilot_here.sh 2>/dev/null)
           elif type copilot_here >/dev/null 2>&1; then
             current_version=$(type copilot_here | grep "# Version:" | head -1 | sed 's/.*# Version: //')
           fi
           
           # Check if using standalone file installation
           if [ -f ~/.copilot_here.sh ]; then
             echo "✅ Detected standalone installation at ~/.copilot_here.sh"
             
             # Check if it's a symlink
             local target_file=~/.copilot_here.sh
             if [ -L ~/.copilot_here.sh ]; then
               target_file=$(readlink -f ~/.copilot_here.sh)
               echo "🔗 Symlink detected, updating target: $target_file"
             fi
             
             # Download to temp first to check version
             local temp_script=$(mktemp)
             if ! curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o "$temp_script"; then
                Failed to download script"echo "
               rm -f "$temp_script"
               return 1
             fi
             
             local new_version=$(sed -n '2s/# Version: //p' "$temp_script" 2>/dev/null)
             
             if [ -n "$current_version" ] && [ -n "$new_version" ]; then
               echo "📌 Version: $current_version → $new_version"
             fi
             
             # Update the actual file (following symlinks)
             mv "$temp_script" "$target_file"
             echo "✅ Scripts updated successfully!"
             echo "🔄 Reloading..."
             source ~/.copilot_here.sh
             echo "✨ Update complete! You're now on version $new_version"
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
           
           local new_version=$(sed -n '2s/# Version: //p' "$temp_script" 2>/dev/null)
           
           if [ -n "$current_version" ] && [ -n "$new_version" ]; then
             echo "📌 Version: $current_version → $new_version"
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
           echo "🔄 Reloading..."
           source "$config_file"
           echo "✨ Update complete! You're now on version $new_version"
           return 0
           ;;
         *)
           args+=("$1")
           shift
           ;;
       esac
     done
     
     __copilot_run "$image_tag" "false" "$skip_cleanup" "$skip_pull" mounts_ro mounts_rw "${args[@]}"
   }

   # YOLO Mode: Auto-approves all tool usage
   copilot_yolo() {
     local image_tag="latest"
     local skip_cleanup="false"
     local skip_pull="false"
     local args=()
     local mounts_ro=()
     local mounts_rw=()
     
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
  copilot_yolo [MOUNT_MANAGEMENT]

OPTIONS:
  -d, --dotnet              Use .NET image variant
  -dp, --dotnet-playwright  Use .NET + Playwright image variant
  --mount <path>            Mount additional directory (read-only)
  --mount-rw <path>         Mount additional directory (read-write)
  --no-cleanup              Skip cleanup of unused Docker images
  --no-pull                 Skip pulling the latest image
  --update-scripts          Update scripts from GitHub repository
  --upgrade-scripts         Alias for --update-scripts
  -h, --help                Show this help message

MOUNT MANAGEMENT:
  --list-mounts             Show all configured mounts
  --save-mount <path>       Save mount to local config (.copilot_here/mounts.conf)
  --save-mount-global <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  --remove-mount <path>     Remove mount from configs

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
  
  # Mount additional directories
  copilot_yolo --mount ../data -p "analyze all data"
  
  # Use specific model
  copilot_yolo --model gpt-5 -p "optimize this code"
  
  # Resume session
  copilot_yolo --continue
  
  # Use .NET + Playwright image
  copilot_yolo -dp -p "write playwright tests"
  
  # Fast mode (skip cleanup)
  copilot_yolo --no-cleanup -p "generate README"

WARNING:
  YOLO mode automatically approves ALL tool usage without confirmation AND
  disables file path verification (--allow-all-tools + --allow-all-paths).
  Use with caution and only in trusted environments.

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-05.1
REPOSITORY: https://github.com/GordonBeeming/copilot_here

================================================================================
GITHUB COPILOT CLI - NATIVE HELP
================================================================================
EOF
          # Run copilot --help to show native help
          local empty_mounts_ro=()
          local empty_mounts_rw=()
          __copilot_run "$image_tag" "true" "true" "true" empty_mounts_ro empty_mounts_rw "--help"
          return 0
          ;;
        --list-mounts)
          __copilot_list_mounts
          return 0
          ;;
        --save-mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --save-mount requires a path argument"
            return 1
          fi
          __copilot_save_mount "$1" "false"
          return 0
          ;;
        --save-mount-global)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --save-mount-global requires a path argument"
            return 1
          fi
          __copilot_save_mount "$1" "true"
          return 0
          ;;
        --remove-mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --remove-mount requires a path argument"
            return 1
          fi
          __copilot_remove_mount "$1"
          return 0
          ;;
        --mount)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --mount requires a path argument"
            return 1
          fi
          mounts_ro+=("$1")
          shift
          ;;
        --mount-rw)
          shift
          if [ -z "$1" ]; then
            echo "❌ Error: --mount-rw requires a path argument"
            return 1
          fi
          mounts_rw+=("$1")
          shift
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
         --update-scripts|--upgrade-scripts)
           echo "📦 Updating copilot_here scripts from GitHub..."
           
           # Get current version
           local current_version=""
           if [ -f ~/.copilot_here.sh ]; then
             current_version=$(sed -n '2s/# Version: //p' ~/.copilot_here.sh 2>/dev/null)
           elif type copilot_here >/dev/null 2>&1; then
             current_version=$(type copilot_here | grep "# Version:" | head -1 | sed 's/.*# Version: //')
           fi
           
           # Check if using standalone file installation
           if [ -f ~/.copilot_here.sh ]; then
             echo "✅ Detected standalone installation at ~/.copilot_here.sh"
             
             # Check if it's a symlink
             local target_file=~/.copilot_here.sh
             if [ -L ~/.copilot_here.sh ]; then
               target_file=$(readlink -f ~/.copilot_here.sh)
               echo "🔗 Symlink detected, updating target: $target_file"
             fi
             
             # Download to temp first to check version
             local temp_script=$(mktemp)
             if ! curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" -o "$temp_script"; then
                Failed to download script"echo "
               rm -f "$temp_script"
               return 1
             fi
             
             local new_version=$(sed -n '2s/# Version: //p' "$temp_script" 2>/dev/null)
             
             if [ -n "$current_version" ] && [ -n "$new_version" ]; then
               echo "📌 Version: $current_version → $new_version"
             fi
             
             # Update the actual file (following symlinks)
             mv "$temp_script" "$target_file"
             echo "✅ Scripts updated successfully!"
             echo "🔄 Reloading..."
             source ~/.copilot_here.sh
             echo "✨ Update complete! You're now on version $new_version"
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
           
           local new_version=$(sed -n '2s/# Version: //p' "$temp_script" 2>/dev/null)
           
           if [ -n "$current_version" ] && [ -n "$new_version" ]; then
             echo "📌 Version: $current_version → $new_version"
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
           echo "🔄 Reloading..."
           source "$config_file"
           echo "✨ Update complete! You're now on version $new_version"
           return 0
           ;;
         *)
           args+=("$1")
           shift
           ;;
       esac
     done
     
     __copilot_run "$image_tag" "true" "$skip_cleanup" "$skip_pull" mounts_ro mounts_rw "${args[@]}"
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
