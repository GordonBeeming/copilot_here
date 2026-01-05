# copilot_here shell functions
# Version: 2026.01.05
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
COPILOT_HERE_BIN="${COPILOT_HERE_BIN:-$HOME/.local/bin/copilot_here}"
COPILOT_HERE_RELEASE_URL="https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"
COPILOT_HERE_VERSION="2026.01.05"

# Ensure user bin directory is on PATH (required for the native binary + shell integration checks)
if [ -d "$HOME/.local/bin" ]; then
  case ":$PATH:" in
    *":$HOME/.local/bin:"*) ;;
    *) export PATH="$HOME/.local/bin:$PATH" ;;
  esac
fi

# Debug logging function
__copilot_debug() {
  if [ "$COPILOT_HERE_DEBUG" = "1" ] || [ "$COPILOT_HERE_DEBUG" = "true" ]; then
    echo "[DEBUG] $*" >&2
  fi
}

# Returns 0 if version A is newer than version B
__copilot_version_is_newer() {
  awk -v a="$1" -v b="$2" '
    function pad(arr, n, i) { for (i = n + 1; i <= 4; i++) arr[i] = 0 }
    BEGIN {
      na = split(a, A, ".");
      nb = split(b, B, ".");
      pad(A, na);
      pad(B, nb);
      for (i = 1; i <= 4; i++) {
        if ((A[i] + 0) > (B[i] + 0)) exit 0;
        if ((A[i] + 0) < (B[i] + 0)) exit 1;
      }
      exit 1;
    }' >/dev/null 2>&1
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
  local script_path="$HOME/.copilot_here.sh"
  local tmp_script
  tmp_script="$(mktemp)"
  if curl -fsSL "${COPILOT_HERE_RELEASE_URL}/copilot_here.sh" -o "$tmp_script" 2>/dev/null; then
    if cat "$tmp_script" > "$script_path" 2>/dev/null; then
      rm -f "$tmp_script"
      
      # Update shell profiles with marker blocks
      echo ""
      echo "🔧 Updating shell profiles..."
      __copilot_update_profile "$HOME/.bashrc" "bash (.bashrc)"
      __copilot_update_profile "$HOME/.zshrc" "zsh (.zshrc)"
      echo "✅ Profiles updated"
      
      echo "✅ Update complete! Reloading shell functions..."
      # shellcheck disable=SC1090
      source "$script_path"
      echo ""
      echo "[VERSION] Script: $COPILOT_HERE_VERSION"
      if [ -x "$COPILOT_HERE_BIN" ]; then
        BIN_VERSION=$($COPILOT_HERE_BIN --version 2>/dev/null | head -n 1)
        if [ -n "$BIN_VERSION" ]; then
          echo "[VERSION] Binary: $BIN_VERSION"
        fi
      fi
      echo ""
    else
      rm -f "$tmp_script"
      echo "✅ Binary updated!"
      echo ""
      echo "⚠️  Could not write updated shell script to: $script_path"
      echo "   Please re-source manually:"
      echo "   source <(curl -fsSL ${COPILOT_HERE_RELEASE_URL}/copilot_here.sh)"
      echo ""
      echo "   Or restart your terminal."
    fi
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

# Helper function to update a profile file with marker blocks
__copilot_update_profile() {
  local profile_path="$1"
  local profile_name="$2"
  local script_path="$HOME/.copilot_here.sh"
  
  # Create profile if it doesn't exist
  if [ ! -f "$profile_path" ]; then
    touch "$profile_path"
  fi
  
  local marker_start="# >>> copilot_here >>>"
  local marker_end="# <<< copilot_here <<<"
  local temp_file
  temp_file=$(mktemp)
  
  # Check if marker block exists
  if grep -qF "$marker_start" "$profile_path" 2>/dev/null; then
    # Remove the entire marker block and rebuild fresh
    awk -v start="$marker_start" -v end="$marker_end" '
      BEGIN { in_block=0 }
      $0 ~ start { in_block=1; next }
      $0 ~ end { in_block=0; next }
      !in_block && $0 !~ /copilot_here\.sh/ { print }
    ' "$profile_path" > "$temp_file"
  else
    # No markers - remove rogue entries
    grep -v "copilot_here.sh" "$profile_path" > "$temp_file" 2>/dev/null || cat "$profile_path" > "$temp_file" 2>/dev/null || true
  fi
  
  # Add fresh marker block
  cat >> "$temp_file" << EOF

$marker_start
# Ensure user bin directory is on PATH
if [ -d "$HOME/.local/bin" ]; then
  case ":$PATH:" in
    *":$HOME/.local/bin:"*) ;;
    *) export PATH="$HOME/.local/bin:$PATH" ;;
  esac
fi
if [ -f "$script_path" ]; then
  source "$script_path"
fi
$marker_end
EOF
  
  mv "$temp_file" "$profile_path"
  echo "   ✓ $profile_name"
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
    --update|-u|--upgrade|--update-scripts|--upgrade-scripts)
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
    --reset)
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
  
  # Check if script file version differs from in-memory version
  local script_path="$HOME/.copilot_here.sh"
  if [ -f "$script_path" ]; then
    local file_version
    file_version=$(grep '^COPILOT_HERE_VERSION=' "$script_path" 2>/dev/null | sed -n 's/^COPILOT_HERE_VERSION="\(.*\)"$/\1/p')
    if [ -n "$file_version" ] && __copilot_version_is_newer "$file_version" "$COPILOT_HERE_VERSION"; then
      __copilot_debug "Newer on-disk script detected: in-memory=$COPILOT_HERE_VERSION, file=$file_version"
      echo "🔄 Detected updated shell script (v$file_version), reloading..."
      source "$script_path"
      copilot_here "$@"
      return $?
    fi
  fi
  
  # Handle --update and variants before binary check
  local arg
  for arg in "$@"; do
    if __copilot_is_update_arg "$arg"; then
      __copilot_debug "Update argument detected"
      __copilot_update
      return $?
    fi
  done
  
  # Handle --reset before binary check
  for arg in "$@"; do
    if __copilot_is_reset_arg "$arg"; then
      __copilot_debug "Reset argument detected"
      __copilot_reset
      return $?
    fi
  done
  
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
  
  # Check if script file version differs from in-memory version
  local script_path="$HOME/.copilot_here.sh"
  if [ -f "$script_path" ]; then
    local file_version
    file_version=$(grep '^COPILOT_HERE_VERSION=' "$script_path" 2>/dev/null | sed -n 's/^COPILOT_HERE_VERSION="\(.*\)"$/\1/p')
    if [ -n "$file_version" ] && __copilot_version_is_newer "$file_version" "$COPILOT_HERE_VERSION"; then
      __copilot_debug "Newer on-disk script detected: in-memory=$COPILOT_HERE_VERSION, file=$file_version"
      echo "🔄 Detected updated shell script (v$file_version), reloading..."
      source "$script_path"
      copilot_yolo "$@"
      return $?
    fi
  fi
  
  # Handle --update and variants before binary check
  local arg
  for arg in "$@"; do
    if __copilot_is_update_arg "$arg"; then
      __copilot_debug "Update argument detected"
      __copilot_update
      return $?
    fi
  done
  
  # Handle --reset before binary check
  for arg in "$@"; do
    if __copilot_is_reset_arg "$arg"; then
      __copilot_debug "Reset argument detected"
      __copilot_reset
      return $?
    fi
  done
  
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
