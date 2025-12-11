# copilot_here shell functions
# Version: 2025.12.10.1
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
COPILOT_HERE_BIN="${COPILOT_HERE_BIN:-$HOME/.local/bin/copilot_here}"
COPILOT_HERE_RELEASE_URL="https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"
COPILOT_HERE_VERSION="2025.12.10.1"

# Debug logging function
__copilot_debug() {
  if [ "$COPILOT_HERE_DEBUG" = "1" ] || [ "$COPILOT_HERE_DEBUG" = "true" ]; then
    echo "[DEBUG] $*" >&2
  fi
}

# Helper function to stop running containers with confirmation
__copilot_stop_containers() {
  local running_containers
  running_containers=$(docker ps --filter "name=copilot_here-" -q 2>/dev/null)
  
  if [ -n "$running_containers" ]; then
    echo "⚠️  copilot_here is currently running in Docker"
    printf "   Stop running containers to continue? [y/N]: "
    read -r response
    local lower_response
    lower_response=$(echo "$response" | tr '[:upper:]' '[:lower:]')
    if [ "$lower_response" = "y" ] || [ "$lower_response" = "yes" ]; then
      echo "🛑 Stopping copilot_here containers..."
      docker stop $running_containers 2>/dev/null
      echo "   ✓ Stopped"
      return 0
    else
      echo "❌ Cannot update while containers are running (binary is in use)"
      return 1
    fi
  fi
  return 0
}

# Helper function to download and install binary
__copilot_download_binary() {
  __copilot_debug "Downloading binary..."
  
  # Detect OS and architecture
  local os=""
  local arch=""
  
  case "$(uname -s)" in
    Linux*)  os="linux" ;;
    Darwin*) os="osx" ;;
    *)       echo "❌ Unsupported OS: $(uname -s)"; return 1 ;;
  esac
  
  case "$(uname -m)" in
    x86_64)  arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *)       echo "❌ Unsupported architecture: $(uname -m)"; return 1 ;;
  esac
  
  __copilot_debug "OS: $os, Arch: $arch"
  
  # Create bin directory
  local bin_dir
  bin_dir="$(dirname "$COPILOT_HERE_BIN")"
  mkdir -p "$bin_dir"
  
  # Download latest release archive
  local download_url="${COPILOT_HERE_RELEASE_URL}/copilot_here-${os}-${arch}.tar.gz"
  local tmp_archive
  tmp_archive="$(mktemp)"
  
  echo "📦 Downloading binary from: $download_url"
  if ! curl -fsSL "$download_url" -o "$tmp_archive"; then
    rm -f "$tmp_archive"
    echo "❌ Failed to download binary"
    return 1
  fi
  
  # Extract binary from archive
  if ! tar -xzf "$tmp_archive" -C "$bin_dir" copilot_here; then
    rm -f "$tmp_archive"
    echo "❌ Failed to extract binary"
    return 1
  fi
  
  rm -f "$tmp_archive"
  chmod +x "$COPILOT_HERE_BIN"
  echo "✅ Binary installed to: $COPILOT_HERE_BIN"
  return 0
}

# Helper function to ensure binary is installed
__copilot_ensure_binary() {
  __copilot_debug "Checking for binary at: $COPILOT_HERE_BIN"
  
  if [ ! -f "$COPILOT_HERE_BIN" ]; then
    echo "📥 copilot_here binary not found. Installing..."
    __copilot_download_binary
  else
    __copilot_debug "Binary found"
  fi
  
  return 0
}

# Update function - downloads fresh shell script and binary
__copilot_update() {
  echo "🔄 Updating copilot_here..."
  
  # Check and stop running containers
  if ! __copilot_stop_containers; then
    return 1
  fi
  
  # Download fresh binary
  echo ""
  echo "📥 Downloading latest binary..."
  if [ -f "$COPILOT_HERE_BIN" ]; then
    rm -f "$COPILOT_HERE_BIN"
  fi
  if ! __copilot_download_binary; then
    echo "❌ Failed to download binary"
    return 1
  fi
  
  # Download and source fresh shell script
  echo ""
  echo "📥 Downloading latest shell script..."
  local tmp_script
  tmp_script="$(mktemp)"
  if curl -fsSL "${COPILOT_HERE_RELEASE_URL}/copilot_here.sh" -o "$tmp_script" 2>/dev/null; then
    echo "✅ Update complete! Reloading shell functions..."
    source "$tmp_script"
    rm -f "$tmp_script"
  else
    rm -f "$tmp_script"
    echo ""
    echo "✅ Binary updated!"
    echo ""
    echo "⚠️  Could not auto-reload shell functions. Please re-source manually:"
    echo "   source <(curl -fsSL ${COPILOT_HERE_RELEASE_URL}/copilot_here.sh)"
    echo ""
    echo "   Or restart your terminal."
  fi
  return 0
}

# Reset function - same as update (kept for backwards compatibility)
__copilot_reset() {
  __copilot_update
}

# Check for updates (called at startup)
__copilot_check_for_updates() {
  __copilot_debug "Checking for updates..."
  
  # Fetch remote version with 2 second timeout
  local remote_version
  remote_version=$(curl -m 2 -fsSL "${COPILOT_HERE_RELEASE_URL}/copilot_here.sh" 2>/dev/null | sed -n 's/^COPILOT_HERE_VERSION="\(.*\)"$/\1/p')
  
  if [ -z "$remote_version" ]; then
    __copilot_debug "Could not fetch remote version (offline or timeout)"
    return 0  # Failed to check or offline - continue normally
  fi
  
  __copilot_debug "Local version: $COPILOT_HERE_VERSION, Remote version: $remote_version"
  
  if [ "$COPILOT_HERE_VERSION" != "$remote_version" ]; then
    # Check if remote is actually newer using sort -V
    local newest
    newest=$(printf "%s\n%s" "$COPILOT_HERE_VERSION" "$remote_version" | sort -V | tail -n1)
    if [ "$newest" = "$remote_version" ]; then
      echo "📢 Update available: $COPILOT_HERE_VERSION → $remote_version"
      printf "Would you like to update now? [y/N]: "
      read -r confirmation
      local lower_confirmation
      lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
      if [ "$lower_confirmation" = "y" ] || [ "$lower_confirmation" = "yes" ]; then
        __copilot_update
        return 1  # Signal that update was performed
      fi
    fi
  fi
  return 0
}

# Check if argument is an update command
__copilot_is_update_arg() {
  case "$1" in
    --update|-u|--upgrade|-Update|-UpdateScripts|-UpgradeScripts|--update-scripts|--upgrade-scripts)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

# Check if argument is a reset command
__copilot_is_reset_arg() {
  case "$1" in
    --reset|-Reset)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

# Safe Mode: Asks for confirmation before executing
copilot_here() {
  __copilot_debug "=== copilot_here called with args: $*"
  
  # Handle --update and variants before binary check
  if __copilot_is_update_arg "$1"; then
    __copilot_debug "Update argument detected"
    __copilot_update
    return $?
  fi
  
  # Handle --reset before binary check
  if __copilot_is_reset_arg "$1"; then
    __copilot_debug "Reset argument detected"
    __copilot_reset
    return $?
  fi
  
  # Check for updates at startup
  __copilot_debug "Checking for updates..."
  __copilot_check_for_updates || return 0
  
  __copilot_debug "Ensuring binary is installed..."
  __copilot_ensure_binary || return 1
  
  __copilot_debug "Executing binary: $COPILOT_HERE_BIN $*"
  "$COPILOT_HERE_BIN" "$@"
  local exit_code=$?
  __copilot_debug "Binary exited with code: $exit_code"
  return $exit_code
}

# YOLO Mode: Auto-approves all tool usage
copilot_yolo() {
  __copilot_debug "=== copilot_yolo called with args: $*"
  
  # Handle --update and variants before binary check
  if __copilot_is_update_arg "$1"; then
    __copilot_debug "Update argument detected"
    __copilot_update
    return $?
  fi
  
  # Handle --reset before binary check
  if __copilot_is_reset_arg "$1"; then
    __copilot_debug "Reset argument detected"
    __copilot_reset
    return $?
  fi
  
  # Check for updates at startup
  __copilot_debug "Checking for updates..."
  __copilot_check_for_updates || return 0
  
  __copilot_debug "Ensuring binary is installed..."
  __copilot_ensure_binary || return 1
  
  __copilot_debug "Executing binary in YOLO mode: $COPILOT_HERE_BIN --yolo $*"
  "$COPILOT_HERE_BIN" --yolo "$@"
  local exit_code=$?
  __copilot_debug "Binary exited with code: $exit_code"
  return $exit_code
}
