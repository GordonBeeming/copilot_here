# copilot_here shell functions
# Version: 2025-10-27.9
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
  
  # Add --allow-all-tools and --allow-all-paths if in YOLO mode
  if [ "$allow_all_tools" = "true" ]; then
    copilot_args+=("--allow-all-tools" "--allow-all-paths")
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
  --upgrade-scripts         Alias for --update-scripts
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
  copilot_yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-10-27.9
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
  --upgrade-scripts         Alias for --update-scripts
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
  YOLO mode automatically approves ALL tool usage without confirmation AND
  disables file path verification (--allow-all-tools + --allow-all-paths).
  Use with caution and only in trusted environments.

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-10-27.9
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
  
  __copilot_run "$image_tag" "true" "$skip_cleanup" "$skip_pull" "${args[@]}"
}
