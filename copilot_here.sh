# copilot_here shell functions
# Version: 2025-11-09
# Repository: https://github.com/GordonBeeming/copilot_here

# Test mode flag (set by tests to skip auth checks)
COPILOT_HERE_TEST_MODE="${COPILOT_HERE_TEST_MODE:-false}"

# Helper function to detect emoji support
__copilot_supports_emoji() {
  [[ "$LANG" == *"UTF-8"* ]] && [[ "$TERM" != "dumb" ]]
}

# Helper function to load mounts from config file
__copilot_load_mounts() {
  local config_file="$1"
  local var_name="$2"
  local actual_file="$config_file"
  
  # Follow symlink if config file is a symlink
  if [ -L "$config_file" ]; then
    actual_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
  fi
  
  if [ -f "$actual_file" ]; then
    while IFS= read -r line || [ -n "$line" ]; do
      # Skip empty lines, whitespace-only lines, and comments
      [[ -z "$line" || "$line" =~ ^[[:space:]]*$ || "$line" =~ ^[[:space:]]*# ]] && continue
      eval "${var_name}+=(\"\$line\")"
    done < "$actual_file"
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
    
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
      echo "🔗 Following symlink to: $config_file"
    fi
    
    /bin/mkdir -p "$(/usr/bin/dirname "$config_file")"
  else
    # For local mounts, keep path as-is (relative is OK for project-specific)
    normalized_path="$path"
    config_file=".copilot_here/mounts.conf"
    
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
      echo "🔗 Following symlink to: $config_file"
    fi
    
    /bin/mkdir -p "$(/usr/bin/dirname "$config_file")"
  fi
  
  # Check if already exists
  if [ -f "$config_file" ] && /usr/bin/grep -qF "$normalized_path" "$config_file"; then
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
  
  # Normalize the path similar to save logic
  local normalized_path
  if [[ "$path" == "~"* ]]; then
    normalized_path="$path"
  elif [[ "$path" == "/"* ]]; then
    if [[ "$path" == "$HOME"* ]]; then
      normalized_path="~${path#$HOME}"
    else
      normalized_path="$path"
    fi
  else
    local dir_part=$(dirname "$path" 2>/dev/null || echo ".")
    local base_part=$(basename "$path" 2>/dev/null || echo "$path")
    local abs_dir=$(cd "$dir_part" 2>/dev/null && pwd || echo "$PWD")
    normalized_path="$abs_dir/$base_part"
    
    if [[ "$normalized_path" == "$HOME"* ]]; then
      normalized_path="~${normalized_path#$HOME}"
    fi
  fi
  
  # Extract mount suffix if present (e.g., :rw, :ro)
  local mount_suffix=""
  if [[ "$normalized_path" == *:* ]]; then
    mount_suffix="${normalized_path##*:}"
    normalized_path="${normalized_path%:*}"
  fi
  
  # Check if global config is a symlink and follow it
  if [ -L "$global_config" ]; then
    global_config=$(readlink -f "$global_config" 2>/dev/null || readlink "$global_config")
  fi
  
  # Check if local config is a symlink and follow it
  if [ -L "$local_config" ]; then
    local_config=$(readlink -f "$local_config" 2>/dev/null || readlink "$local_config")
  fi
  
  # Try to remove from global config - match both with and without suffix
  if [ -f "$global_config" ]; then
    local temp_file="${global_config}.tmp"
    local found=false
    local matched_line=""
    
    # Ensure temp file is empty
    /usr/bin/true > "$temp_file"
    
    while IFS= read -r line || [ -n "$line" ]; do
      # Skip empty lines
      if [ -z "$line" ]; then
        continue
      fi
      
      local line_without_suffix="${line%:*}"
      
      # Match either exact path, path with any suffix, or normalized path
      if [[ "$line" == "$normalized_path" ]] || \
         [[ "$line_without_suffix" == "$normalized_path" ]] || \
         [[ "$line" == "$path" ]] || \
         [[ "$line_without_suffix" == "$path" ]]; then
        if [ "$found" = "false" ]; then
          found=true
          matched_line="$line"
        fi
      else
        echo "$line" >> "$temp_file"
      fi
    done < "$global_config"
    
    if [ "$found" = "true" ]; then
      /bin/mv "$temp_file" "$global_config"
      echo "✅ Removed from global config: $matched_line"
      removed=true
    else
      /bin/rm -f "$temp_file"
    fi
  fi
  
  # Try to remove from local config - match both with and without suffix
  if [ -f "$local_config" ]; then
    local temp_file="${local_config}.tmp"
    local found=false
    local matched_line=""
    
    # Ensure temp file is empty
    /usr/bin/true > "$temp_file"
    
    while IFS= read -r line || [ -n "$line" ]; do
      # Skip empty lines
      if [ -z "$line" ]; then
        continue
      fi
      
      local line_without_suffix="${line%:*}"
      
      # Match either exact path, path with any suffix, or normalized path
      if [[ "$line" == "$normalized_path" ]] || \
         [[ "$line_without_suffix" == "$normalized_path" ]] || \
         [[ "$line" == "$path" ]] || \
         [[ "$line_without_suffix" == "$path" ]]; then
        if [ "$found" = "false" ]; then
          found=true
          matched_line="$line"
        fi
      else
        echo "$line" >> "$temp_file"
      fi
    done < "$local_config"
    
    if [ "$found" = "true" ]; then
      /bin/mv "$temp_file" "$local_config"
      echo "✅ Removed from local config: $matched_line"
      removed=true
    else
      /bin/rm -f "$temp_file"
    fi
  fi
  
  if [ "$removed" = "false" ]; then
    echo "⚠️  Mount not found in any config: $path"
    return 1
  fi
}

# Helper function for security checks (shared by all variants)
__copilot_security_check() {
  # Skip in test mode
  if [ "$COPILOT_HERE_TEST_MODE" = "true" ]; then
    return 0
  fi
  
  if ! gh auth status 2>/dev/null | /usr/bin/grep "Token scopes:" | /usr/bin/grep -q "'copilot'"; then
    echo "❌ Error: Your gh token is missing the required 'copilot' scope."
    echo "Please run 'gh auth refresh -h github.com -s copilot' to add it."
    return 1
  fi

  if gh auth status 2>/dev/null | /usr/bin/grep "Token scopes:" | /usr/bin/grep -q -E "'(admin:|manage_|write:public_key|delete_repo|(write|delete)_packages)'"; then
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
  local all_images=$(docker images --filter "label=project=copilot_here" --format "{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" | /usr/bin/grep -v ":<none>" || true)
  
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
  
  # Determine container path for current directory - map to container home if needed
  local container_work_dir="$current_dir"
  if [[ "$current_dir" == "$HOME"* ]]; then
    # Path is under user's home directory - map to container's home
    local relative_path="${current_dir#$HOME}"
    container_work_dir="/home/appuser${relative_path}"
  fi
  
  local docker_args=(
    --rm -it
    -v "$current_dir:$container_work_dir"
    -w "$container_work_dir"
    -v "$copilot_config_path":/home/appuser/.copilot
    -e PUID=$(id -u)
    -e PGID=$(id -g)
    -e GITHUB_TOKEN="$token"
  )

  # Load mounts from config files
  local global_config="$HOME/.config/copilot_here/mounts.conf"
  local local_config=".copilot_here/mounts.conf"
  local global_mounts=()
  local local_mounts=()
  
  __copilot_load_mounts "$global_config" global_mounts
  __copilot_load_mounts "$local_config" local_mounts
  
  # Track all mounted paths for display and --add-dir
  local all_mount_paths=()
  local mount_display=()
  
  # Add current working directory to display
  mount_display+=("📁 $container_work_dir")
  
  # Process global config mounts
  # Initialize seen_paths with current directory to avoid duplicates
  local seen_paths=("$current_dir")
  for mount in "${global_mounts[@]}"; do
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
    
    # Determine container path - if under home directory, map to container home
    local container_path="$resolved_path"
    if [[ "$resolved_path" == "$HOME"* ]]; then
      # Path is under user's home directory - map to container's home
      local relative_path="${resolved_path#$HOME}"
      container_path="/home/appuser${relative_path}"
    fi
    
    docker_args+=(-v "$resolved_path:$container_path:$mount_mode")
    all_mount_paths+=("$container_path")
    
    # Global mount icon
    if __copilot_supports_emoji; then
      mount_display+=("   🌍 $container_path ($mount_mode)")
    else
      mount_display+=("   G: $container_path ($mount_mode)")
    fi
  done
  
  # Process local config mounts
  for mount in "${local_mounts[@]}"; do
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
    
    # Determine container path - if under home directory, map to container home
    local container_path="$resolved_path"
    if [[ "$resolved_path" == "$HOME"* ]]; then
      # Path is under user's home directory - map to container's home
      local relative_path="${resolved_path#$HOME}"
      container_path="/home/appuser${relative_path}"
    fi
    
    docker_args+=(-v "$resolved_path:$container_path:$mount_mode")
    all_mount_paths+=("$container_path")
    
    # Local mount icon
    if __copilot_supports_emoji; then
      mount_display+=("   📍 $container_path ($mount_mode)")
    else
      mount_display+=("   L: $container_path ($mount_mode)")
    fi
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
    
    # Determine container path - if under home directory, map to container home
    local container_path="$resolved_path"
    if [[ "$resolved_path" == "$HOME"* ]]; then
      local relative_path="${resolved_path#$HOME}"
      container_path="/home/appuser${relative_path}"
    fi
    
    docker_args+=(-v "$resolved_path:$container_path:ro")
    all_mount_paths+=("$container_path")
    
    if __copilot_supports_emoji; then
      mount_display+=("   🔧 $container_path (ro)")
    else
      mount_display+=("   CLI: $container_path (ro)")
    fi
  done
  
  # Process CLI read-write mounts
  eval "local mounts_rw_array=(\"\${${mounts_rw_name}[@]}\")"
  for mount_path in "${mounts_rw_array[@]}"; do
    local resolved_path=$(__copilot_resolve_mount_path "$mount_path")
    if [ $? -ne 0 ]; then
      continue  # Skip this mount if user cancelled
    fi
    
    # Determine container path - if under home directory, map to container home
    local container_path="$resolved_path"
    if [[ "$resolved_path" == "$HOME"* ]]; then
      local relative_path="${resolved_path#$HOME}"
      container_path="/home/appuser${relative_path}"
    fi
    
    # Skip if already seen (CLI overrides config)
    local override=false
    for seen_path in "${seen_paths[@]}"; do
      if [ "$seen_path" = "$resolved_path" ]; then
        # Replace read-only with read-write
        override=true
        # Update docker args to rw - rebuild array to avoid bash-specific array indexing
        local new_docker_args=()
        local skip_next=false
        local prev_arg=""
        for arg in "${docker_args[@]}"; do
          if [ "$skip_next" = "true" ]; then
            skip_next=false
            continue
          fi
          
          if [ "$prev_arg" = "-v" ] && [ "$arg" = "$resolved_path:$container_path:ro" ]; then
            # Replace this mount with rw version
            new_docker_args+=("$resolved_path:$container_path:rw")
          else
            new_docker_args+=("$arg")
          fi
          prev_arg="$arg"
        done
        docker_args=("${new_docker_args[@]}")
        break
      fi
    done
    
    if [ "$override" = "false" ]; then
      seen_paths+=("$resolved_path")
      docker_args+=(-v "$resolved_path:$container_path:rw")
      all_mount_paths+=("$container_path")
    fi
    
    if __copilot_supports_emoji; then
      mount_display+=("   🔧 $container_path (rw)")
    else
      mount_display+=("   CLI: $container_path (rw)")
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
    copilot_args+=("--add-dir" "$container_work_dir")
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
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 copilot_here --save-mount ~/notes:rw
 copilot_here --save-mount-global ~/data:rw

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

VERSION: 2025-11-09
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
          current_version=$(type copilot_here | /usr/bin/grep "# Version:" | head -1 | sed 's/.*# Version: //')
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
        if /usr/bin/grep -q "# copilot_here shell functions" "$config_file"; then
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
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 copilot_yolo --save-mount ~/notes:rw
 copilot_yolo --save-mount-global ~/data:rw

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

VERSION: 2025-11-09
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
          current_version=$(type copilot_here | /usr/bin/grep "# Version:" | head -1 | sed 's/.*# Version: //')
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
        if /usr/bin/grep -q "# copilot_here shell functions" "$config_file"; then
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
