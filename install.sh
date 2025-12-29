#!/bin/bash
# Bash/Zsh Install Script for copilot_here
# This script downloads copilot_here.sh and configures shell profiles

set -e

# Download the main script
SCRIPT_PATH="$HOME/.copilot_here.sh"
echo "📥 Downloading copilot_here.sh..."
curl -fsSL "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.sh" -o "$SCRIPT_PATH"
echo "✅ Downloaded to: $SCRIPT_PATH"

# Function to update a profile file
update_profile() {
  local profile_path="$1"
  local profile_name="$2"
  
  # Create profile if it doesn't exist
  if [ ! -f "$profile_path" ]; then
    touch "$profile_path"
  fi
  
  local marker_start="# >>> copilot_here >>>"
  local marker_end="# <<< copilot_here <<<"
  
  # Check if marker block already exists
  if grep -qF "$marker_start" "$profile_path" 2>/dev/null; then
    echo "   ✓ $profile_name (already installed)"
    return
  fi
  
  # Remove any old copilot_here entries (without markers)
  local temp_file
  temp_file=$(mktemp)
  grep -v "copilot_here.sh" "$profile_path" > "$temp_file" 2>/dev/null || true
  
  # Add the marker block
  cat >> "$temp_file" << EOF

$marker_start
if [ -f "$SCRIPT_PATH" ]; then
  source "$SCRIPT_PATH"
fi
$marker_end
EOF
  
  mv "$temp_file" "$profile_path"
  echo "   ✓ $profile_name ($profile_path)"
}

# Update all relevant shell profiles
echo "🔧 Updating shell profiles..."

# Bash profiles
if [ -f "$HOME/.bashrc" ] || command -v bash >/dev/null 2>&1; then
  if [ -f "$HOME/.bashrc" ]; then
    update_profile "$HOME/.bashrc" "bash (.bashrc)"
  elif [ -f "$HOME/.bash_profile" ]; then
    update_profile "$HOME/.bash_profile" "bash (.bash_profile)"
  elif [ -f "$HOME/.profile" ]; then
    update_profile "$HOME/.profile" "bash (.profile)"
  else
    # Create .bashrc if bash exists but no profile found
    update_profile "$HOME/.bashrc" "bash (.bashrc)"
  fi
fi

# Zsh profiles
if [ -f "$HOME/.zshrc" ] || command -v zsh >/dev/null 2>&1; then
  if [ -f "$HOME/.zshrc" ]; then
    update_profile "$HOME/.zshrc" "zsh (.zshrc)"
  elif [ -f "$HOME/.zprofile" ]; then
    update_profile "$HOME/.zprofile" "zsh (.zprofile)"
  else
    # Create .zshrc if zsh exists but no profile found
    update_profile "$HOME/.zshrc" "zsh (.zshrc)"
  fi
fi

echo "✅ Profile(s) updated"

# Reload the current shell profile
echo "🔄 Reloading shell profile..."
CURRENT_SHELL=$(basename "$SHELL")
case "$CURRENT_SHELL" in
  zsh)
    if [ -f "$HOME/.zshrc" ]; then
      # shellcheck disable=SC1091
      source "$HOME/.zshrc"
    fi
    ;;
  bash)
    if [ -f "$HOME/.bashrc" ]; then
      # shellcheck disable=SC1090
      source "$HOME/.bashrc"
    elif [ -f "$HOME/.bash_profile" ]; then
      # shellcheck disable=SC1090
      source "$HOME/.bash_profile"
    fi
    ;;
  *)
    # shellcheck disable=SC1090
    source "$SCRIPT_PATH"
    ;;
esac

# Source the script directly to get the version
# shellcheck disable=SC1090
source "$SCRIPT_PATH"

echo ""
echo "✅ Installation complete!"
if [ -n "$COPILOT_HERE_VERSION" ]; then
  echo "   Loaded version: $COPILOT_HERE_VERSION"
fi
echo ""
echo "Try running: copilot_here --help"
