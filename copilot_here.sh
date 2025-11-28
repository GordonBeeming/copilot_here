# copilot_here shell functions
# Version: 2025-11-28.7
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
  local ro_var_name="$2"
  local rw_var_name="$3"
  local actual_file="$config_file"
  
  # Follow symlink if config file is a symlink
  if [ -L "$config_file" ]; then
    actual_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
  fi
  
  if [ -f "$actual_file" ]; then
    while IFS= read -r line || [ -n "$line" ]; do
      # Skip empty lines, whitespace-only lines, and comments
      [[ -z "$line" || "$line" =~ ^[[:space:]]*$ || "$line" =~ ^[[:space:]]*# ]] && continue
      
      # Trim leading and trailing whitespace
      line="${line#"${line%%[![:space:]]*}"}"  # Trim leading
      line="${line%"${line##*[![:space:]]}"}"  # Trim trailing
      
      # Check for :rw suffix to determine read-write vs read-only
      if [[ "$line" == *":rw" ]]; then
        # Read-write mount - strip the :rw suffix
        local mount_path="${line%:rw}"
        eval "${rw_var_name}+=(\"\$mount_path\")"
      elif [[ "$line" == *":ro" ]]; then
        # Read-only mount - strip the :ro suffix
        local mount_path="${line%:ro}"
        eval "${ro_var_name}+=(\"\$mount_path\")"
      else
        # No suffix - default to read-only
        eval "${ro_var_name}+=(\"\$line\")"
      fi
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

# Helper function to load raw mounts from config (for display purposes)
__copilot_load_raw_mounts() {
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
      # Trim leading and trailing whitespace
      line="${line#"${line%%[![:space:]]*}"}"  # Trim leading
      line="${line%"${line##*[![:space:]]}"}"  # Trim trailing
      eval "${var_name}+=(\"\$line\")"
    done < "$actual_file"
  fi
}

# Helper function to display mounts list
__copilot_list_mounts() {
  local global_config="$HOME/.config/copilot_here/mounts.conf"
  local local_config=".copilot_here/mounts.conf"
  
  local global_mounts=()
  local local_mounts=()
  
  __copilot_load_raw_mounts "$global_config" global_mounts
  __copilot_load_raw_mounts "$local_config" local_mounts
  
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

# Helper function to save default image to config
__copilot_save_image_config() {
  local image_tag="$1"
  local is_global="$2"
  local config_file
  
  if [ "$is_global" = "true" ]; then
    config_file="$HOME/.config/copilot_here/image.conf"
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
    fi
    /bin/mkdir -p "$(/usr/bin/dirname "$config_file")"
    echo "✅ Saved default image to global config: $image_tag"
  else
    config_file=".copilot_here/image.conf"
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
    fi
    /bin/mkdir -p "$(/usr/bin/dirname "$config_file")"
    echo "✅ Saved default image to local config: $image_tag"
  fi
  
  echo "$image_tag" > "$config_file"
  echo "   Config file: $config_file"
}

# Helper function to clear default image from config
__copilot_clear_image_config() {
  local is_global="$1"
  local config_file
  
  if [ "$is_global" = "true" ]; then
    config_file="$HOME/.config/copilot_here/image.conf"
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
    fi
    
    if [ -f "$config_file" ]; then
      rm "$config_file"
      echo "✅ Cleared default image from global config"
    else
      echo "⚠️  No global image config found to clear"
    fi
  else
    config_file=".copilot_here/image.conf"
    # Check if config file is a symlink and follow it
    if [ -L "$config_file" ]; then
      config_file=$(readlink -f "$config_file" 2>/dev/null || readlink "$config_file")
    fi
    
    if [ -f "$config_file" ]; then
      rm "$config_file"
      echo "✅ Cleared default image from local config"
    else
      echo "⚠️  No local image config found to clear"
    fi
  fi
}

# Helper function to get default image
__copilot_get_default_image() {
  local local_config=".copilot_here/image.conf"
  local global_config="$HOME/.config/copilot_here/image.conf"
  
  # Check local config first
  if [ -f "$local_config" ]; then
    local image=$(head -n 1 "$local_config" | tr -d '[:space:]')
    if [ -n "$image" ]; then
      echo "$image"
      return 0
    fi
  fi
  
  # Check global config
  if [ -f "$global_config" ]; then
    local image=$(head -n 1 "$global_config" | tr -d '[:space:]')
    if [ -n "$image" ]; then
      echo "$image"
      return 0
    fi
  fi
  
  # Default
  echo "latest"
}

# Helper function to list available images
__copilot_list_images() {
  echo "📦 Available Images:"
  echo "  • latest (Base image)"
  echo "  • dotnet (.NET 8, 9, 10 SDKs)"
  echo "  • dotnet-8 (.NET 8 SDK)"
  echo "  • dotnet-9 (.NET 9 SDK)"
  echo "  • dotnet-10 (.NET 10 SDK)"
  echo "  • playwright (Playwright)"
  echo "  • dotnet-playwright (.NET + Playwright)"
  echo "  • dotnet-sha-<sha> (Specific commit SHA)"
}

# Helper function to show default image
__copilot_show_default_image() {
  local local_config=".copilot_here/image.conf"
  local global_config="$HOME/.config/copilot_here/image.conf"
  local current_default=$(__copilot_get_default_image)
  
  echo "🖼️  Image Configuration:"
  echo "  Current effective default: $current_default"
  echo ""
  
  if [ -f "$local_config" ]; then
    local local_img=$(head -n 1 "$local_config" | tr -d '[:space:]')
    echo "  📍 Local config (.copilot_here/image.conf): $local_img"
  else
    echo "  📍 Local config: (not set)"
  fi
  
  if [ -f "$global_config" ]; then
    local global_img=$(head -n 1 "$global_config" | tr -d '[:space:]')
    echo "  🌍 Global config (~/.config/copilot_here/image.conf): $global_img"
  else
    echo "  🌍 Global config: (not set)"
  fi
  
  echo ""
  echo "  🔧 Base default: latest"
}

# Helper function for security checks (shared by all variants)
__copilot_security_check() {
  # Skip in test mode
  if [ "$COPILOT_HERE_TEST_MODE" = "true" ]; then
    return 0
  fi
  
  if ! gh auth status 2>/dev/null | /usr/bin/grep "Token scopes:" | /usr/bin/grep -q "'copilot'" || \
     ! gh auth status 2>/dev/null | /usr/bin/grep "Token scopes:" | /usr/bin/grep -q "'read:packages'"; then
    echo "❌ Error: Your gh token is missing the required 'copilot' or 'read:packages' scope."
    echo "Please run 'gh auth refresh -h github.com -s copilot,read:packages' to add it."
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

# Helper function to get GitHub owner and repo from git remote
__copilot_get_github_info() {
  local remote_url=$(git remote get-url origin 2>/dev/null)
  
  if [ -z "$remote_url" ]; then
    echo ""
    return 1
  fi
  
  # Parse owner and repo from various URL formats:
  # git@github.com:owner/repo.git
  # https://github.com/owner/repo.git
  # https://github.com/owner/repo
  local owner=""
  local repo=""
  
  # Use sed for portable parsing (works in both bash and zsh)
  # Extract the part after github.com: or github.com/
  local path_part=$(echo "$remote_url" | sed -n 's/.*github\.com[:/]\([^/]*\/[^/]*\).*/\1/p')
  
  if [ -n "$path_part" ]; then
    # Split by / and remove .git suffix
    owner=$(echo "$path_part" | cut -d'/' -f1)
    repo=$(echo "$path_part" | cut -d'/' -f2 | sed 's/\.git$//')
  fi
  
  if [ -n "$owner" ] && [ -n "$repo" ]; then
    echo "${owner}|${repo}"
    return 0
  fi
  
  echo ""
  return 1
}

# Helper function to process network config with placeholders
__copilot_process_network_config() {
  local config_file="$1"
  
  # Create temp file in a location Docker can access (user's config dir)
  # Fall back to system temp if config dir creation fails
  local temp_dir="$HOME/.config/copilot_here/tmp"
  if ! mkdir -p "$temp_dir" 2>/dev/null; then
    temp_dir="${TMPDIR:-/tmp}"
  fi
  local temp_file="${temp_dir}/network-$(date +%s%N).json"
  
  # Get GitHub owner and repo
  local github_info=$(__copilot_get_github_info)
  local github_owner=""
  local github_repo=""
  
  if [ -n "$github_info" ]; then
    github_owner="${github_info%|*}"
    github_repo="${github_info#*|}"
  fi
  
  # Read and process the config file, replacing placeholders
  if [ -f "$config_file" ]; then
    local content=$(cat "$config_file")
    
    # Replace placeholders
    content="${content//\{\{GITHUB_OWNER\}\}/$github_owner}"
    content="${content//\{\{GITHUB_REPO\}\}/$github_repo}"
    
    echo "$content" > "$temp_file"
    echo "$temp_file"
  else
    rm -f "$temp_file"
    echo ""
  fi
}

# Helper function to cleanup unused copilot_here images
__copilot_cleanup_images() {
  local keep_image="$1"
  echo "🧹 Cleaning up old copilot_here images (older than 7 days)..."
  
  # Get cutoff timestamp (7 days ago)
  local cutoff_date=$(date -d '7 days ago' +%s 2>/dev/null || date -v-7d +%s 2>/dev/null)
  
  # Resolve the ID of the image we want to keep to ensure we don't delete it
  local keep_image_id=$(docker inspect --format="{{.Id}}" "$keep_image" 2>/dev/null || echo "")
  
  # Get all copilot_here images by repository name with full IDs
  local all_images=$(docker images --no-trunc "ghcr.io/gordonbeeming/copilot_here" --format "{{.ID}}|{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" || true)
  
  if [ -z "$all_images" ]; then
    echo "  ✓ No images to clean up"
    return 0
  fi
  
  local count=0
  while IFS='|' read -r cleanup_image_id cleanup_image_name cleanup_created_at; do
    # Check if this is the image we want to keep (by ID)
    if [ -n "$cleanup_image_id" ] && [ "$cleanup_image_id" != "$keep_image_id" ]; then
      # Parse creation date (format: "2025-01-28 12:34:56 +0000 UTC")
      local image_date=$(date -d "$cleanup_created_at" +%s 2>/dev/null || date -j -f "%Y-%m-%d %H:%M:%S %z %Z" "$cleanup_created_at" +%s 2>/dev/null)
      
      if [ -n "$image_date" ] && [ "$image_date" -lt "$cutoff_date" ]; then
        echo "  🗑️  Removing old image: $cleanup_image_name (ID: ${cleanup_image_id:7:12}...) (created: ${cleanup_created_at})"
        if docker rmi -f "$cleanup_image_id" > /dev/null 2>&1; then
          ((count++))
        else
          echo "  ⚠️  Failed to remove: $cleanup_image_name (may be in use by running container)"
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

# Helper function to cleanup orphaned copilot_here networks
__copilot_cleanup_orphaned_networks() {
  # Find proxy containers that are orphaned (their app container is not running)
  # Pattern: projectname-sessionid-proxy paired with projectname-sessionid-app (or app-run-xxx)
  # Only remove proxy containers where NO app container with matching prefix is running
  
  # Get list of currently RUNNING containers (not all, just running)
  local running_containers=$(docker ps --format "{{.Names}}" 2>/dev/null || true)
  
  # Get all containers (including stopped) to find proxy containers
  local all_containers=$(docker ps -a --format "{{.Names}}" 2>/dev/null || true)
  
  if [ -n "$all_containers" ]; then
    while IFS= read -r container_name; do
      [ -z "$container_name" ] && continue
      
      # Only process containers ending with -proxy
      case "$container_name" in
        *-proxy) ;;
        *) continue ;;
      esac
      
      # Get the project prefix (everything before -proxy)
      local project_prefix="${container_name%-proxy}"
      
      # Check if any app container with this prefix is running
      # docker compose run creates containers like: projectname-app-run-xyz
      # docker compose up creates containers like: projectname-app-1 or projectname-app
      local app_running=""
      if [ -n "$running_containers" ]; then
        app_running=$(echo "$running_containers" | grep "^${project_prefix}-app" || true)
      fi
      
      if [ -z "$app_running" ]; then
        # No app container running with this prefix - proxy is orphaned
        if docker rm -f "$container_name" 2>/dev/null; then
          echo "  🗑️  Removed orphaned proxy container: $container_name"
        fi
      fi
      # If app is running, don't touch this proxy
    done <<< "$all_containers"
  fi
  
  # Find orphaned networks (not attached to any containers)
  # Networks are named like: projectname-sessionid_airlock and projectname-sessionid_bridge
  local all_networks=$(docker network ls --format "{{.Name}}" 2>/dev/null || true)
  
  if [ -z "$all_networks" ]; then
    return 0
  fi
  
  local count=0
  while IFS= read -r network_name; do
    [ -z "$network_name" ] && continue
    
    # Skip non-copilot networks (those not ending in _airlock or _bridge)
    case "$network_name" in
      *_airlock|*_bridge) ;;
      *) continue ;;
    esac
    
    # Check if network has any attached containers
    local containers=$(docker network inspect "$network_name" --format '{{len .Containers}}' 2>/dev/null || echo "0")
    
    if [ "$containers" = "0" ]; then
      if docker network rm "$network_name" 2>/dev/null; then
        echo "  🗑️  Removed orphaned network: $network_name"
        count=$((count + 1))
      fi
    fi
  done <<< "$all_networks"
  
  if [ "$count" -gt 0 ]; then
    echo "  ✓ Cleaned up $count orphaned network(s)"
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

  # Load mounts from config files (raw for the old processing loop)
  local global_config="$HOME/.config/copilot_here/mounts.conf"
  local local_config=".copilot_here/mounts.conf"
  local global_mounts=()
  local local_mounts=()
  
  __copilot_load_raw_mounts "$global_config" global_mounts
  __copilot_load_raw_mounts "$local_config" local_mounts
  
  # Track all mounted paths for display and --add-dir
  local all_mount_paths=()
  local mount_display=()
  
  # Arrays to collect all resolved mounts (for passing to airlock)
  local all_resolved_mounts_ro=()
  local all_resolved_mounts_rw=()
  
  # Add current working directory to display
  mount_display+=("📁 $container_work_dir")
  
  # Priority order: CLI > LOCAL > GLOBAL
  # Process in priority order so higher priority mounts are added first
  # Lower priority mounts will be skipped if path already seen
  
  # Initialize seen_paths with current directory to avoid duplicates
  local seen_paths=("$current_dir")
  
  # Process CLI read-only mounts (highest priority)
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
    all_resolved_mounts_ro+=("$resolved_path:$container_path")
    
    if __copilot_supports_emoji; then
      mount_display+=("   🔧 $container_path (ro)")
    else
      mount_display+=("   CLI: $container_path (ro)")
    fi
  done
  
  # Process CLI read-write mounts (highest priority, rw overrides ro)
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
    
    # Check if already seen (rw overrides ro from CLI)
    local override=false
    for seen_path in "${seen_paths[@]}"; do
      if [ "$seen_path" = "$resolved_path" ]; then
        override=true
        # Update docker args to rw - rebuild array to avoid bash-specific array indexing
        local new_docker_args=()
        local prev_arg=""
        for arg in "${docker_args[@]}"; do
          if [ "$prev_arg" = "-v" ] && [ "$arg" = "$resolved_path:$container_path:ro" ]; then
            new_docker_args+=("$resolved_path:$container_path:rw")
          else
            new_docker_args+=("$arg")
          fi
          prev_arg="$arg"
        done
        docker_args=("${new_docker_args[@]}")
        
        # Update resolved mounts arrays - move from ro to rw
        local new_resolved_ro=()
        for m in "${all_resolved_mounts_ro[@]}"; do
          if [ "$m" != "$resolved_path:$container_path" ]; then
            new_resolved_ro+=("$m")
          fi
        done
        all_resolved_mounts_ro=("${new_resolved_ro[@]}")
        all_resolved_mounts_rw+=("$resolved_path:$container_path")
        
        break
      fi
    done
    
    if [ "$override" = "false" ]; then
      seen_paths+=("$resolved_path")
      docker_args+=(-v "$resolved_path:$container_path:rw")
      all_mount_paths+=("$container_path")
      all_resolved_mounts_rw+=("$resolved_path:$container_path")
      
      if __copilot_supports_emoji; then
        mount_display+=("   🔧 $container_path (rw)")
      else
        mount_display+=("   CLI: $container_path (rw)")
      fi
    fi
  done
  
  # Process local config mounts (second priority)
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
    
    # Skip if already seen (CLI takes priority)
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
    
    docker_args+=(-v "$resolved_path:$container_path:$mount_mode")
    all_mount_paths+=("$container_path")
    
    # Store resolved mount for airlock (host path:container path format)
    if [ "$mount_mode" = "rw" ]; then
      all_resolved_mounts_rw+=("$resolved_path:$container_path")
    else
      all_resolved_mounts_ro+=("$resolved_path:$container_path")
    fi
    
    # Local mount icon
    if __copilot_supports_emoji; then
      mount_display+=("   📍 $container_path ($mount_mode)")
    else
      mount_display+=("   L: $container_path ($mount_mode)")
    fi
  done
  
  # Process global config mounts (lowest priority)
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
    
    # Skip if already seen (CLI and local take priority)
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
    
    docker_args+=(-v "$resolved_path:$container_path:$mount_mode")
    all_mount_paths+=("$container_path")
    
    # Store resolved mount for airlock (host path:container path format)
    if [ "$mount_mode" = "rw" ]; then
      all_resolved_mounts_rw+=("$resolved_path:$container_path")
    else
      all_resolved_mounts_ro+=("$resolved_path:$container_path")
    fi
    
    # Global mount icon
    if __copilot_supports_emoji; then
      mount_display+=("   🌍 $container_path ($mount_mode)")
    else
      mount_display+=("   G: $container_path ($mount_mode)")
    fi
  done
  
  # Display mounts if there are extras
  if [ ${#all_mount_paths[@]} -gt 0 ]; then
    echo "📂 Mounts:"
    for display in "${mount_display[@]}"; do
      echo "$display"
    done
  fi
  
  # Display Airlock status
  local current_dir=$(pwd)
  local local_network_config="${current_dir}/.copilot_here/network.json"
  local global_network_config="$HOME/.config/copilot_here/network.json"
  local airlock_enabled="false"
  local airlock_source=""
  local airlock_mode=""
  
  # Check local config first (takes precedence)
  if [ -f "$local_network_config" ]; then
    local enabled_val=$(grep -o '"enabled"[[:space:]]*:[[:space:]]*[a-z]*' "$local_network_config" 2>/dev/null | grep -o 'true\|false' | head -1)
    if [ "$enabled_val" = "true" ]; then
      airlock_enabled="true"
      airlock_source="local (.copilot_here/network.json)"
      airlock_mode=$(grep -o '"mode"[[:space:]]*:[[:space:]]*"[^"]*"' "$local_network_config" 2>/dev/null | sed 's/.*"\([^"]*\)"$/\1/' | head -1)
    fi
  fi
  
  # Check global config if local not enabled
  if [ "$airlock_enabled" = "false" ] && [ -f "$global_network_config" ]; then
    local enabled_val=$(grep -o '"enabled"[[:space:]]*:[[:space:]]*[a-z]*' "$global_network_config" 2>/dev/null | grep -o 'true\|false' | head -1)
    if [ "$enabled_val" = "true" ]; then
      airlock_enabled="true"
      airlock_source="global (~/.config/copilot_here/network.json)"
      airlock_mode=$(grep -o '"mode"[[:space:]]*:[[:space:]]*"[^"]*"' "$global_network_config" 2>/dev/null | sed 's/.*"\([^"]*\)"$/\1/' | head -1)
    fi
  fi
  
  if [ "$airlock_enabled" = "true" ]; then
    local mode_display=""
    if [ "$airlock_mode" = "monitor" ]; then
      mode_display=" (monitor mode)"
    elif [ "$airlock_mode" = "enforce" ]; then
      mode_display=" (enforce mode)"
    fi
    echo "🛡️  Airlock: enabled${mode_display} - ${airlock_source}"
    
    # Determine which config file to use
    local network_config_file="$local_network_config"
    if [ "$airlock_source" = "global (~/.config/copilot_here/network.json)" ]; then
      network_config_file="$global_network_config"
    fi
    
    # Call airlock mode instead of normal docker run
    # Pass resolved mount arrays (all config + CLI mounts already processed)
    __copilot_run_airlock "$image_tag" "$allow_all_tools" "$skip_cleanup" "$skip_pull" \
      "$network_config_file" "all_resolved_mounts_ro" "all_resolved_mounts_rw" "$@"
    return $?
  else
    if [ -f "$local_network_config" ] || [ -f "$global_network_config" ]; then
      echo "🔓 Airlock: disabled"
    fi
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

  # Wrap in subshell to safely use trap for title reset
  (
    # Set terminal title
    local title_emoji="🤖"
    if [ "$allow_all_tools" = "true" ]; then
      title_emoji="🤖⚡️"
    fi
    
    local current_dir_name=$(basename "$current_dir")
    local title="${title_emoji} ${current_dir_name}"
    
    printf "\033]0;%s\007" "$title"
    trap 'printf "\033]0;\007"' EXIT
    
    docker run "${docker_args[@]}" "${copilot_args[@]}"
  )
}

# Helper function to create or load network proxy config
__copilot_ensure_network_config() {
  local is_global="$1"
  local config_file
  local config_dir
  
  if [ "$is_global" = "true" ]; then
    config_dir="$HOME/.config/copilot_here"
    config_file="$config_dir/network.json"
  else
    config_dir=".copilot_here"
    config_file="$config_dir/network.json"
  fi
  
  # Check if config already exists
  if [ -f "$config_file" ]; then
    # Config exists - just enable it
    if command -v jq >/dev/null 2>&1; then
      local current_enabled=$(jq -r 'if has("enabled") then .enabled else true end' "$config_file")
      if [ "$current_enabled" = "true" ]; then
        echo "✅ Airlock already enabled: $config_file"
      else
        # Update enabled to true
        local temp_file=$(mktemp)
        jq '.enabled = true' "$config_file" > "$temp_file" && mv "$temp_file" "$config_file"
        echo "✅ Airlock enabled: $config_file"
      fi
    else
      # No jq - check with grep
      if grep -q '"enabled"[[:space:]]*:[[:space:]]*false' "$config_file"; then
        # Use sed to update enabled to true (cross-platform)
        if [[ "$OSTYPE" == "darwin"* ]]; then
          sed -i '' 's/"enabled"[[:space:]]*:[[:space:]]*false/"enabled": true/' "$config_file"
        else
          sed -i 's/"enabled"[[:space:]]*:[[:space:]]*false/"enabled": true/' "$config_file"
        fi
        echo "✅ Airlock enabled: $config_file"
      else
        echo "✅ Airlock already enabled: $config_file"
      fi
    fi
    return 0
  fi
  
  # Config doesn't exist - ask user about mode and create it
  echo "📝 Creating Airlock configuration..."
  echo ""
  echo "   The Airlock proxy can run in two modes:"
  echo "   • [e]nforce - Block requests not in the allowlist (recommended for security)"
  echo "   • [m]onitor - Log all requests but allow everything (useful for testing)"
  echo ""
  printf "   Select mode (default: enforce): "
  read mode_choice
  
  local mode="enforce"
  local enable_logging="false"
  local lower_choice=$(echo "$mode_choice" | tr '[:upper:]' '[:lower:]')
  if [ "$lower_choice" = "monitor" ] || [ "$lower_choice" = "m" ]; then
    mode="monitor"
    enable_logging="true"
  fi
  
  # Create config directory
  if ! mkdir -p "$config_dir" 2>/dev/null; then
    echo "❌ Error: Failed to create config directory: $config_dir"
    return 1
  fi
  
  # Load default rules if available
  local default_rules_file="$HOME/.config/copilot_here/default-airlock-rules.json"
  local allowed_rules
  
  if [ -f "$default_rules_file" ]; then
    # Extract just the array value after "allowed_rules":
    # This handles the JSON structure properly
    allowed_rules=$(sed -n '/"allowed_rules"/,/^}$/p' "$default_rules_file" | sed '1s/.*\[/[/' | sed '$d')
  fi
  
  # Fallback to inline defaults if not found or empty
  if [ -z "$allowed_rules" ] || [ "$allowed_rules" = "[" ]; then
    allowed_rules='[
    {
      "host": "api.github.com",
      "allowed_paths": ["/user", "/graphql"]
    },
    {
      "host": "api.individual.githubcopilot.com",
      "allowed_paths": ["/models", "/mcp/readonly", "/chat/completions"]
    }
  ]'
  fi
  
  # Write config file with enabled: true
  cat > "$config_file" << EOF
{
  "enabled": true,
  "inherit_default_rules": true,
  "mode": "$mode",
  "enable_logging": $enable_logging,
  "allowed_rules": $allowed_rules
}
EOF
  
  if [ $? -ne 0 ]; then
    echo "❌ Error: Failed to write config file: $config_file"
    return 1
  fi
  
  echo ""
  echo "✅ Created Airlock config: $config_file"
  echo "   Mode: $mode"
  echo "   inherit_default_rules: true"
  echo ""
  
  return 0
}

# Helper function to disable Airlock
__copilot_disable_airlock() {
  local is_global="$1"
  local config_file
  
  if [ "$is_global" = "true" ]; then
    config_file="$HOME/.config/copilot_here/network.json"
  else
    config_file=".copilot_here/network.json"
  fi
  
  if [ ! -f "$config_file" ]; then
    echo "ℹ️  No Airlock config found: $config_file"
    return 0
  fi
  
  if command -v jq >/dev/null 2>&1; then
    local current_enabled=$(jq -r 'if has("enabled") then .enabled else true end' "$config_file")
    if [ "$current_enabled" = "false" ]; then
      echo "ℹ️  Airlock already disabled: $config_file"
    else
      # Update enabled to false
      local temp_file=$(mktemp)
      jq '.enabled = false' "$config_file" > "$temp_file" && mv "$temp_file" "$config_file"
      echo "✅ Airlock disabled: $config_file"
    fi
  else
    # No jq - check with grep and use sed
    if grep -q '"enabled"[[:space:]]*:[[:space:]]*true' "$config_file"; then
      if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' 's/"enabled"[[:space:]]*:[[:space:]]*true/"enabled": false/' "$config_file"
      else
        sed -i 's/"enabled"[[:space:]]*:[[:space:]]*true/"enabled": false/' "$config_file"
      fi
      echo "✅ Airlock disabled: $config_file"
    elif grep -q '"enabled"' "$config_file"; then
      echo "ℹ️  Airlock already disabled: $config_file"
    else
      # No enabled field - add it as false at the beginning
      if [[ "$OSTYPE" == "darwin"* ]]; then
        sed -i '' 's/^{/{\
  "enabled": false,/' "$config_file"
      else
        sed -i 's/^{/{\n  "enabled": false,/' "$config_file"
      fi
      echo "✅ Airlock disabled: $config_file"
    fi
  fi
  
  return 0
}

# Helper function to show Airlock rules
__copilot_show_airlock_rules() {
  local local_config=".copilot_here/network.json"
  local global_config="$HOME/.config/copilot_here/network.json"
  local default_rules="$HOME/.config/copilot_here/default-airlock-rules.json"
  
  echo "📋 Airlock Proxy Rules"
  echo "======================"
  echo ""
  
  # Show default rules
  if [ -f "$default_rules" ]; then
    echo "📦 Default Rules:"
    echo "   $default_rules"
    cat "$default_rules" | sed 's/^/   /'
    echo ""
  else
    echo "📦 Default Rules: Not found"
    echo ""
  fi
  
  # Show global config
  if [ -f "$global_config" ]; then
    echo "🌐 Global Config:"
    echo "   $global_config"
    cat "$global_config" | sed 's/^/   /'
    echo ""
  else
    echo "🌐 Global Config: Not configured"
    echo ""
  fi
  
  # Show local config
  if [ -f "$local_config" ]; then
    echo "📁 Local Config:"
    echo "   $local_config"
    cat "$local_config" | sed 's/^/   /'
    echo ""
  else
    echo "📁 Local Config: Not configured"
    echo ""
  fi
  
  return 0
}

# Helper function to edit Airlock rules
__copilot_edit_airlock_rules() {
  local is_global="$1"
  local config_file
  local config_dir
  
  if [ "$is_global" = "true" ]; then
    config_dir="$HOME/.config/copilot_here"
    config_file="$config_dir/network.json"
  else
    config_dir=".copilot_here"
    config_file="$config_dir/network.json"
  fi
  
  # Create config if it doesn't exist
  if [ ! -f "$config_file" ]; then
    echo "📝 Config file doesn't exist. Creating it first..."
    __copilot_ensure_network_config "$is_global"
    if [ $? -ne 0 ]; then
      return 1
    fi
  fi
  
  # Determine editor
  local editor="${EDITOR:-${VISUAL:-vi}}"
  
  echo "📝 Opening $config_file with $editor..."
  "$editor" "$config_file"
  
  return $?
}

# Helper function to run with airlock (Docker Compose mode)
__copilot_run_airlock() {
  local image_tag="$1"
  local allow_all_tools="$2"
  local skip_cleanup="$3"
  local skip_pull="$4"
  local network_config_file="$5"
  local mounts_ro_name="$6"
  local mounts_rw_name="$7"
  shift 7
  
  # Remaining args are copilot arguments
  local copilot_args=("$@")
  
  if ! __copilot_security_check; then return 1; fi
  
  # Process network config file to replace placeholders
  local processed_config_file=$(__copilot_process_network_config "$network_config_file")
  if [ -z "$processed_config_file" ]; then
    echo "❌ Failed to process network config file"
    return 1
  fi
  
  # Skip actual container launch in test mode
  if [ "$COPILOT_HERE_TEST_MODE" = "true" ]; then
    echo "🧪 Test mode: skipping container launch"
    rm -f "$processed_config_file"
    return 0
  fi
  
  # Cleanup orphaned containers and networks from previous failed runs FIRST
  # This ensures we have available subnets before trying to create new networks
  __copilot_cleanup_orphaned_networks
  
  local app_image="ghcr.io/gordonbeeming/copilot_here:$image_tag"
  local proxy_image="ghcr.io/gordonbeeming/copilot_here:proxy"
  
  echo "🛡️  Starting in Airlock mode..."
  echo "   App image: $app_image"
  echo "   Proxy image: $proxy_image"
  echo "   Network config: $network_config_file"
  
  # Pull images unless skipped
  if [ "$skip_pull" != "true" ]; then
    echo "📥 Pulling images..."
    docker pull "$app_image" || { echo "❌ Failed to pull app image"; return 1; }
    # Try to pull proxy image, but don't fail if it exists locally (for local dev)
    if ! docker pull "$proxy_image" 2>/dev/null; then
      if docker image inspect "$proxy_image" >/dev/null 2>&1; then
        echo "   Using local proxy image (not available in registry)"
      else
        echo "❌ Failed to pull proxy image and no local image found"
        if [ -f "./dev-build.sh" ]; then
          echo "   Run ./dev-build.sh to build the proxy image locally"
        else
          echo "   The proxy image is not yet available in the registry"
        fi
        return 1
      fi
    fi
  fi
  
  # Get current directory info for project name (matches terminal title format)
  local current_dir=$(pwd)
  local current_dir_name=$(basename "$current_dir")
  local title_emoji="🤖"
  if [ "$allow_all_tools" = "true" ]; then
    title_emoji="🤖⚡️"
  fi
  
  # Generate unique session ID (include PID for uniqueness across concurrent sessions)
  # Use $$ (PID) which works in both bash and zsh, plus timestamp
  local session_id=$(echo "$$-$(date +%s%N 2>/dev/null || date +%s)" | sha256sum | head -c 8)
  
  # Project name matches the terminal title format: dirname + session
  # Docker Compose requires lowercase project names
  local project_name=$(echo "${current_dir_name}-${session_id}" | tr '[:upper:]' '[:lower:]')
  
  # Create temporary compose file
  local temp_compose=$(mktemp)
  local template_file="$HOME/.config/copilot_here/docker-compose.airlock.yml.template"
  
  # Download template if not exists
  if [ ! -f "$template_file" ]; then
    echo "📥 Downloading compose template..."
    curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -o "$template_file" || {
      echo "❌ Failed to download compose template"
      rm -f "$temp_compose"
      return 1
    }
  fi
  
  # Get token
  local token=$(gh auth token 2>/dev/null)
  if [ -z "$token" ]; then
    echo "⚠️  Could not retrieve token using 'gh auth token'." >&2
  fi
  
  # Prepare copilot config path
  local copilot_config="$HOME/.config/copilot-cli-docker"
  mkdir -p "$copilot_config"
  
  # Container work directory - use same path mapping as non-airlock mode
  local container_work_dir="$current_dir"
  if [[ "$current_dir" == "$HOME"* ]]; then
    # Path is under user's home directory - map to container's home
    local relative_path="${current_dir#$HOME}"
    container_work_dir="/home/appuser${relative_path}"
  fi
  
  # Check if logging is enabled in network config (also enabled automatically for monitor mode)
  local logs_mount=""
  local enable_logging=$(grep -o '"enable_logging"[[:space:]]*:[[:space:]]*true' "$network_config_file" 2>/dev/null)
  local is_monitor_mode=$(grep -o '"mode"[[:space:]]*:[[:space:]]*"monitor"' "$network_config_file" 2>/dev/null)
  if [ -n "$enable_logging" ] || [ -n "$is_monitor_mode" ]; then
    local logs_dir="${current_dir}/.copilot_here/logs"
    mkdir -p "$logs_dir"
    # Create gitignore to prevent committing logs
    if [ ! -f "${current_dir}/.copilot_here/logs/.gitignore" ]; then
      echo "# Ignore all log files - may contain sensitive information" > "${current_dir}/.copilot_here/logs/.gitignore"
      echo "*" >> "${current_dir}/.copilot_here/logs/.gitignore"
      echo "!.gitignore" >> "${current_dir}/.copilot_here/logs/.gitignore"
    fi
    logs_mount="      - ${logs_dir}:/logs"
  fi
  
  # Build extra mounts string (using actual newlines, not \n)
  local extra_mounts=""
  local newline=$'\n'
  
  # Add read-only mounts (format: host_path:container_path)
  eval "local _mounts_ro=(\"\${${mounts_ro_name}[@]}\")"
  for mount_spec in "${_mounts_ro[@]}"; do
    # Parse host_path:container_path format
    local host_path="${mount_spec%%:*}"
    local container_path="${mount_spec#*:}"
    [ -z "$host_path" ] && continue
    extra_mounts="${extra_mounts}      - ${host_path}:${container_path}:ro${newline}"
  done
  
  # Add read-write mounts (format: host_path:container_path)
  eval "local _mounts_rw=(\"\${${mounts_rw_name}[@]}\")"
  for mount_spec in "${_mounts_rw[@]}"; do
    # Parse host_path:container_path format
    local host_path="${mount_spec%%:*}"
    local container_path="${mount_spec#*:}"
    [ -z "$host_path" ] && continue
    extra_mounts="${extra_mounts}      - ${host_path}:${container_path}:rw${newline}"
  done
  
  # Remove trailing newline to prevent empty line after insertion
  if [ -n "$extra_mounts" ]; then
    extra_mounts="${extra_mounts%$newline}"
  fi
  
  # Build copilot command args
  local copilot_cmd="[\"copilot\""
  if [ "$allow_all_tools" = "true" ]; then
    copilot_cmd="${copilot_cmd}, \"--allow-all-tools\", \"--allow-all-paths\""
    copilot_cmd="${copilot_cmd}, \"--add-dir\", \"${container_work_dir}\""
  fi
  
  if [ ${#copilot_args[@]} -eq 0 ]; then
    copilot_cmd="${copilot_cmd}, \"--banner\""
  else
    for arg in "${copilot_args[@]}"; do
      # Escape quotes in argument
      arg=$(echo "$arg" | sed 's/"/\\"/g')
      copilot_cmd="${copilot_cmd}, \"${arg}\""
    done
  fi
  copilot_cmd="${copilot_cmd}]"
  
  # Generate compose file from template
  # Use sed for reliable substitution (works on macOS and Linux)
  # Write multiline content to temp files to avoid awk newline issues on macOS
  local temp_extra_mounts=$(mktemp)
  local temp_logs_mount=$(mktemp)
  if [ -n "$extra_mounts" ]; then
    printf '%s' "$extra_mounts" > "$temp_extra_mounts"
  fi
  if [ -n "$logs_mount" ]; then
    printf '%s' "$logs_mount" > "$temp_logs_mount"
  fi
  
  # Copy template and do simple substitutions with sed
  cp "$template_file" "$temp_compose"
  sed -i.bak "s|{{PROJECT_NAME}}|$project_name|g" "$temp_compose"
  sed -i.bak "s|{{APP_IMAGE}}|$app_image|g" "$temp_compose"
  sed -i.bak "s|{{PROXY_IMAGE}}|$proxy_image|g" "$temp_compose"
  sed -i.bak "s|{{WORK_DIR}}|$current_dir|g" "$temp_compose"
  sed -i.bak "s|{{CONTAINER_WORK_DIR}}|$container_work_dir|g" "$temp_compose"
  sed -i.bak "s|{{COPILOT_CONFIG}}|$copilot_config|g" "$temp_compose"
  sed -i.bak "s|{{NETWORK_CONFIG}}|$processed_config_file|g" "$temp_compose"
  sed -i.bak "s|{{PUID}}|$(id -u)|g" "$temp_compose"
  sed -i.bak "s|{{PGID}}|$(id -g)|g" "$temp_compose"
  sed -i.bak "s|{{COPILOT_ARGS}}|$copilot_cmd|g" "$temp_compose"
  
  # Handle multiline substitutions using temp files
  # This approach avoids passing newlines through awk -v which breaks on macOS
  # For LOGS_MOUNT - replace placeholder line with file contents or remove if empty
  if [ -s "$temp_logs_mount" ]; then
    # Read line by line and replace the placeholder line with file contents
    # cat outputs without final newline, so we need to add one
    # Only replace if line is NOT a comment (doesn't start with #)
    while IFS= read -r line || [ -n "$line" ]; do
      if echo "$line" | grep -q '^{{LOGS_MOUNT}}'; then
        cat "$temp_logs_mount"
        echo ""
      else
        printf '%s\n' "$line"
      fi
    done < "$temp_compose" > "${temp_compose}.tmp" && mv "${temp_compose}.tmp" "$temp_compose"
  else
    # Remove the placeholder line if empty (only non-comment lines)
    grep -v '^{{LOGS_MOUNT}}' "$temp_compose" > "${temp_compose}.tmp" && mv "${temp_compose}.tmp" "$temp_compose"
  fi
  
  # For EXTRA_MOUNTS - replace placeholder line with file contents or remove if empty
  if [ -s "$temp_extra_mounts" ]; then
    # Read line by line and replace the placeholder line with file contents
    # cat outputs without final newline, so we need to add one
    # Only replace if line is NOT a comment (doesn't start with #)
    while IFS= read -r line || [ -n "$line" ]; do
      if echo "$line" | grep -q '^{{EXTRA_MOUNTS}}'; then
        cat "$temp_extra_mounts"
        echo ""
      else
        printf '%s\n' "$line"
      fi
    done < "$temp_compose" > "${temp_compose}.tmp" && mv "${temp_compose}.tmp" "$temp_compose"
  else
    # Remove the placeholder line if empty (only non-comment lines)
    grep -v '^{{EXTRA_MOUNTS}}' "$temp_compose" > "${temp_compose}.tmp" && mv "${temp_compose}.tmp" "$temp_compose"
  fi
  
  # Cleanup temp files
  rm -f "$temp_extra_mounts" "$temp_logs_mount" "${temp_compose}.bak"
  
  # Set terminal title
  local title="${title_emoji} ${current_dir_name} 🛡️"
  printf "\033]0;%s\007" "$title"
  
  # Set trap with values baked in (local variables may not be accessible in trap function)
  trap "printf '\033]0;\007'; echo ''; echo '🧹 Cleaning up airlock...'; docker stop '${project_name}-proxy' 2>/dev/null; docker rm '${project_name}-proxy' 2>/dev/null; docker network rm '${project_name}_airlock' 2>/dev/null; docker network rm '${project_name}_bridge' 2>/dev/null; docker volume rm '${project_name}_proxy-ca' 2>/dev/null; rm -f '$temp_compose' '$processed_config_file'" EXIT INT TERM
  
  # Start proxy service in background, then run app interactively
  # We use 'up -d' for proxy, then 'run' for interactive app
  # COMPOSE_MENU=0 disables the interactive Docker Desktop menu bar
  echo ""
  
  # Start proxy first
  GITHUB_TOKEN="$token" COMPOSE_MENU=0 docker compose -f "$temp_compose" -p "$project_name" up -d proxy
  
  # Run app interactively (--rm removes it on exit)
  GITHUB_TOKEN="$token" COMPOSE_MENU=0 docker compose -f "$temp_compose" -p "$project_name" run -i --rm app
  
  # Cleanup is handled by trap
}

# Helper function to update scripts
__copilot_update_scripts() {
  echo "📦 Updating copilot_here scripts from GitHub..."
  
  # Get current version
  local current_version=""
  if [ -f ~/.copilot_here.sh ]; then
    current_version=$(sed -n '2s/# Version: //p' ~/.copilot_here.sh 2>/dev/null)
  elif type copilot_here >/dev/null 2>&1; then
    current_version=$(type copilot_here | /usr/bin/grep "# Version:" | head -1 | sed 's/.*# Version: //')
  fi
  
  # Ensure config directory exists
  /bin/mkdir -p "$HOME/.config/copilot_here"
  
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
      echo "❌ Failed to download script"
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
    
    # Download default airlock rules
    echo "📥 Updating default Airlock rules..."
    local airlock_rules_file="$HOME/.config/copilot_here/default-airlock-rules.json"
    if curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/default-airlock-rules.json" -o "$airlock_rules_file"; then
      echo "✅ Airlock rules updated: $airlock_rules_file"
    else
      echo "⚠️  Failed to download Airlock rules (non-fatal)"
    fi
    
    # Download docker-compose template
    echo "📥 Updating compose template..."
    local compose_template_file="$HOME/.config/copilot_here/docker-compose.airlock.yml.template"
    if curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -o "$compose_template_file"; then
      echo "✅ Compose template updated: $compose_template_file"
    else
      echo "⚠️  Failed to download compose template (non-fatal)"
    fi
    
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
  
  # Download default airlock rules
  echo "📥 Updating default Airlock rules..."
  local airlock_rules_file="$HOME/.config/copilot_here/default-airlock-rules.json"
  if curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/default-airlock-rules.json" -o "$airlock_rules_file"; then
    echo "✅ Airlock rules updated: $airlock_rules_file"
  else
    echo "⚠️  Failed to download Airlock rules (non-fatal)"
  fi
  
  # Download docker-compose template
  echo "📥 Updating compose template..."
  local compose_template_file="$HOME/.config/copilot_here/docker-compose.airlock.yml.template"
  if curl -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/docker-compose.airlock.yml.template" -o "$compose_template_file"; then
    echo "✅ Compose template updated: $compose_template_file"
  else
    echo "⚠️  Failed to download compose template (non-fatal)"
  fi
  
  echo "🔄 Reloading..."
  source "$config_file"
  echo "✨ Update complete! You're now on version $new_version"
  return 0
}

# Helper function to check for updates
__copilot_check_for_updates() {
  # Skip in test mode
  if [ "$COPILOT_HERE_TEST_MODE" = "true" ]; then
    return 0
  fi

  # Get current version
  local current_version=""
  if [ -f ~/.copilot_here.sh ]; then
    current_version=$(sed -n '2s/# Version: //p' ~/.copilot_here.sh 2>/dev/null)
  elif type copilot_here >/dev/null 2>&1; then
    current_version=$(type copilot_here | /usr/bin/grep "# Version:" | head -1 | sed 's/.*# Version: //')
  fi
  
  if [ -z "$current_version" ]; then
    return 0
  fi

  # Fetch remote version (with timeout)
  local remote_version=$(curl -m 2 -fsSL "https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh" 2>/dev/null | sed -n '2s/# Version: //p')

  if [ -z "$remote_version" ]; then
    return 0 # Failed to check or offline
  fi

  if [ "$current_version" != "$remote_version" ]; then
     # Check if remote is actually newer using sort -V
     local newest=$(printf "%s\n%s" "$current_version" "$remote_version" | sort -V | tail -n1)
     if [ "$newest" = "$remote_version" ]; then
        echo "📢 Update available: $current_version → $remote_version"
        printf "Would you like to update now? [y/N]: "
        read confirmation
        local lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
        if [[ "$lower_confirmation" == "y" || "$lower_confirmation" == "yes" ]]; then
           __copilot_update_scripts
           return 1 # Updated
        fi
     fi
  fi
  return 0
}

# Common help function for both copilot_here and copilot_yolo
__copilot_show_help() {
  local is_yolo="$1"
  local cmd_name="copilot_here"
  local mode_desc="Safe Mode"
  
  if [ "$is_yolo" = "true" ]; then
    cmd_name="copilot_yolo"
    mode_desc="YOLO Mode"
  fi
  
  cat << EOF
$cmd_name - GitHub Copilot CLI in a secure Docker container ($mode_desc)

USAGE:
  $cmd_name [OPTIONS] [COPILOT_ARGS]
  $cmd_name [MOUNT_MANAGEMENT]
  $cmd_name [IMAGE_MANAGEMENT]

OPTIONS:
  -d, --dotnet              Use .NET image variant (all versions)
  -d8, --dotnet8            Use .NET 8 image variant
  -d9, --dotnet9            Use .NET 9 image variant
  -d10, --dotnet10          Use .NET 10 image variant
  -pw, --playwright         Use Playwright image variant
  -dp, --dotnet-playwright  Use .NET + Playwright image variant
  --mount <path>            Mount additional directory (read-only)
  --mount-rw <path>         Mount additional directory (read-write)
  --no-cleanup              Skip cleanup of unused Docker images
  --no-pull, --skip-pull    Skip pulling the latest image
  --update-scripts          Update scripts from GitHub repository
  --upgrade-scripts         Alias for --update-scripts
  -h, --help                Show this help message
  --help2                   Show GitHub Copilot CLI native help

NETWORK (AIRLOCK):
  --enable-airlock              Enable Airlock with local rules (.copilot_here/network.json)
  --enable-global-airlock       Enable Airlock with global rules (~/.config/copilot_here/network.json)
  --disable-airlock             Disable Airlock for local config
  --disable-global-airlock      Disable Airlock for global config
  --show-airlock-rules          Show current Airlock proxy rules
  --edit-airlock-rules          Edit local Airlock rules in \$EDITOR
  --edit-global-airlock-rules   Edit global Airlock rules in \$EDITOR

MOUNT MANAGEMENT:
  --list-mounts             Show all configured mounts
  --save-mount <path>       Save mount to local config (.copilot_here/mounts.conf)
  --save-mount-global <path>  Save mount to global config (~/.config/copilot_here/mounts.conf)
  --remove-mount <path>     Remove mount from configs
  
  Note: Saved mounts are read-only by default. To save as read-write, add :rw suffix:
 $cmd_name --save-mount ~/notes:rw
 $cmd_name --save-mount-global ~/data:rw

IMAGE MANAGEMENT:
  --list-images     List all available Docker images
  --show-image      Show current default image configuration
  --set-image <tag> Set default image in local config
  --set-image-global <tag> Set default image in global config
  --clear-image     Clear default image from local config
  --clear-image-global Clear default image from global config

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
 ... and more (run $cmd_name --help2 for full copilot help)

EXAMPLES:
  # Interactive mode
  $cmd_name
  
  # Mount additional directories
  $cmd_name --mount ../investigations -p "analyze these files"
  $cmd_name --mount-rw ~/notes --mount /data/research
  
  # Save mounts for reuse
  $cmd_name --save-mount ~/investigations
  $cmd_name --save-mount-global ~/common-data
  $cmd_name --list-mounts
  
  # Set default image
  $cmd_name --set-image dotnet
  $cmd_name --set-image-global dotnet-sha-bf08e6c875a919cd3440e8b3ebefc5d460edd870
  
  # Ask a question (short syntax)
  $cmd_name -p "how do I list files in bash?"
  
  # Use specific AI model
  $cmd_name --model gpt-5 -p "explain this code"
  
  # Resume previous session
  $cmd_name --continue
  
  # Use .NET image with custom log level
  $cmd_name -d --log-level debug -p "build this .NET project"
  
  # Fast mode (skip cleanup and pull)
  $cmd_name --no-cleanup --no-pull -p "quick question"

MODES:
  copilot_here  - Safe mode (asks for confirmation before executing)
  copilot_yolo  - YOLO mode (auto-approves all tool usage + all paths)

VERSION: 2025-11-28.7
REPOSITORY: https://github.com/GordonBeeming/copilot_here
EOF
}

# Show native copilot help
__copilot_show_native_help() {
  local image_tag=$(__copilot_get_default_image)
  local empty_mounts_ro=()
  local empty_mounts_rw=()
  __copilot_run "$image_tag" "false" "true" "true" empty_mounts_ro empty_mounts_rw "--help"
}

# Common main function for both copilot_here and copilot_yolo
__copilot_main() {
  local is_yolo="$1"
  shift
  
  local image_tag=$(__copilot_get_default_image)
  local skip_cleanup="false"
  local skip_pull="false"
  local enable_network_proxy="false"
  local network_proxy_global="false"
  local args=()
  local mounts_ro=()
  local mounts_rw=()

  # Parse arguments
  while [[ $# -gt 0 ]]; do
    case "$1" in
     -h|--help)
       __copilot_show_help "$is_yolo"
       return 0
       ;;
     --help2)
       __copilot_show_native_help
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
       return $?
       ;;
     --save-mount-global)
       shift
       if [ -z "$1" ]; then
         echo "❌ Error: --save-mount-global requires a path argument"
         return 1
       fi
       __copilot_save_mount "$1" "true"
       return $?
       ;;
     --remove-mount)
       shift
       if [ -z "$1" ]; then
         echo "❌ Error: --remove-mount requires a path argument"
         return 1
       fi
       __copilot_remove_mount "$1"
       return $?
       ;;
     --list-images)
       __copilot_list_images
       return 0
       ;;
     --show-image)
       __copilot_show_default_image
       return 0
       ;;
     --set-image)
       shift
       if [ -z "$1" ]; then
         echo "❌ Error: --set-image requires a tag argument"
         return 1
       fi
       __copilot_save_image_config "$1" "false"
       return $?
       ;;
     --set-image-global)
       shift
       if [ -z "$1" ]; then
         echo "❌ Error: --set-image-global requires a tag argument"
         return 1
       fi
       __copilot_save_image_config "$1" "true"
       return $?
       ;;
     --clear-image)
       __copilot_clear_image_config "false"
       return $?
       ;;
     --clear-image-global)
       __copilot_clear_image_config "true"
       return $?
       ;;
     -d|--dotnet)
       image_tag="dotnet"
       shift
       ;;
     -d8|--dotnet8)
       image_tag="dotnet8"
       shift
       ;;
     -d9|--dotnet9)
       image_tag="dotnet9"
       shift
       ;;
     -d10|--dotnet10)
       image_tag="dotnet10"
       shift
       ;;
     -pw|--playwright)
       image_tag="playwright"
       shift
       ;;
     -dp|--dotnet-playwright)
       image_tag="dotnet-playwright"
       shift
       ;;
     --mount)
       shift
       if [ -n "$1" ]; then
         mounts_ro+=("$1")
       fi
       shift
       ;;
     --mount-rw)
       shift
       if [ -n "$1" ]; then
         mounts_rw+=("$1")
       fi
       shift
       ;;
     --no-cleanup)
       skip_cleanup="true"
       shift
       ;;
     --no-pull|--skip-pull)
       skip_pull="true"
       shift
       ;;
      --enable-airlock)
        if [ "$enable_network_proxy" = "true" ]; then
          echo "❌ Error: Cannot use both --enable-airlock and --enable-global-airlock"
          return 1
        fi
        enable_network_proxy="true"
        network_proxy_global="false"
        shift
        ;;
      --enable-global-airlock)
        if [ "$enable_network_proxy" = "true" ]; then
          echo "❌ Error: Cannot use both --enable-airlock and --enable-global-airlock"
          return 1
        fi
        enable_network_proxy="true"
        network_proxy_global="true"
        shift
        ;;
      --disable-airlock)
        __copilot_disable_airlock "false"
        return $?
        ;;
      --disable-global-airlock)
        __copilot_disable_airlock "true"
        return $?
        ;;
      --show-airlock-rules)
        __copilot_show_airlock_rules
        return $?
        ;;
      --edit-airlock-rules)
        __copilot_edit_airlock_rules "false"
        return $?
        ;;
      --edit-global-airlock-rules)
        __copilot_edit_airlock_rules "true"
        return $?
        ;;
      --update-scripts|--upgrade-scripts)
        __copilot_update_scripts
        return $?
        ;;
      *)
        args+=("$1")
        shift
        ;;
    esac
  done
  
  # Handle network proxy configuration
  if [ "$enable_network_proxy" = "true" ]; then
    __copilot_ensure_network_config "$network_proxy_global"
    return $?
  fi
  
  __copilot_check_for_updates || return 0
  
  __copilot_run "$image_tag" "$is_yolo" "$skip_cleanup" "$skip_pull" mounts_ro mounts_rw "${args[@]}"
}

# Safe Mode: Asks for confirmation before executing
copilot_here() {
  __copilot_main "false" "$@"
}

# YOLO Mode: Auto-approves all tool usage
copilot_yolo() {
  __copilot_main "true" "$@"
}
