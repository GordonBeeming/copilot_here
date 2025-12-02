# copilot_here shell functions
# Version: 2025.12.02.6
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
COPILOT_HERE_BIN="${COPILOT_HERE_BIN:-$HOME/.local/bin/copilot_here}"
COPILOT_HERE_RELEASE_URL="https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest"

# Helper function to download and install binary
__copilot_download_binary() {
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
  if [ ! -f "$COPILOT_HERE_BIN" ]; then
    echo "📥 copilot_here binary not found. Installing..."
    __copilot_download_binary
  fi
  
  return 0
}

# Update function - downloads fresh shell script and binary
__copilot_update() {
  echo "🔄 Updating copilot_here..."
  
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
  # Handle --update and variants before binary check
  if __copilot_is_update_arg "$1"; then
    __copilot_update
    return $?
  fi
  
  # Handle --reset before binary check
  if __copilot_is_reset_arg "$1"; then
    __copilot_reset
    return $?
  fi
  
  __copilot_ensure_binary || return 1
  "$COPILOT_HERE_BIN" "$@"
}

# YOLO Mode: Auto-approves all tool usage
copilot_yolo() {
  # Handle --update and variants before binary check
  if __copilot_is_update_arg "$1"; then
    __copilot_update
    return $?
  fi
  
  # Handle --reset before binary check
  if __copilot_is_reset_arg "$1"; then
    __copilot_reset
    return $?
  fi
  
  __copilot_ensure_binary || return 1
  "$COPILOT_HERE_BIN" --yolo "$@"
}
