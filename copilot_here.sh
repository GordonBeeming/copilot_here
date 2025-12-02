# copilot_here shell functions
# Version: 2025.12.02.2
# Repository: https://github.com/GordonBeeming/copilot_here

# Configuration
COPILOT_HERE_BIN="${COPILOT_HERE_BIN:-$HOME/.local/bin/copilot_here}"

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
  local download_url="https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here-${os}-${arch}.tar.gz"
  local tmp_archive
  tmp_archive="$(mktemp)"
  
  echo "📦 Downloading from: $download_url"
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
  echo "✅ Installed to: $COPILOT_HERE_BIN"
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

# Reset function - re-downloads shell script and binary
__copilot_reset() {
  echo "🔄 Resetting copilot_here..."
  
  # Remove existing binary
  if [ -f "$COPILOT_HERE_BIN" ]; then
    rm -f "$COPILOT_HERE_BIN"
    echo "🗑️  Removed existing binary"
  fi
  
  # Download fresh binary
  echo "📥 Downloading fresh binary..."
  if ! __copilot_download_binary; then
    echo "❌ Failed to download binary"
    return 1
  fi
  
  echo ""
  echo "✅ Reset complete!"
  echo ""
  echo "⚠️  To complete the reset, please re-source the shell script:"
  echo "   source <(curl -fsSL https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh)"
  echo ""
  echo "   Or restart your terminal."
  return 0
}

# Safe Mode: Asks for confirmation before executing
copilot_here() {
  # Handle --reset before binary check
  if [ "$1" = "--reset" ] || [ "$1" = "-Reset" ]; then
    __copilot_reset
    return $?
  fi
  
  __copilot_ensure_binary || return 1
  "$COPILOT_HERE_BIN" "$@"
}

# YOLO Mode: Auto-approves all tool usage
copilot_yolo() {
  # Handle --reset before binary check
  if [ "$1" = "--reset" ] || [ "$1" = "-Reset" ]; then
    __copilot_reset
    return $?
  fi
  
  __copilot_ensure_binary || return 1
  "$COPILOT_HERE_BIN" --yolo "$@"
}
