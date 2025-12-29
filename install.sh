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
  
  # Remove any old copilot_here entries
  local temp_file
  temp_file=$(mktemp)
  grep -v "copilot_here.sh" "$profile_path" > "$temp_file" 2>/dev/null || true
  
  # Add the new reference if not present
  local new_entry="source \"$SCRIPT_PATH\""
  if ! grep -qF "$new_entry" "$temp_file" 2>/dev/null; then
    echo "" >> "$temp_file"
    echo "$new_entry" >> "$temp_file"
  fi
  
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

echo ""
echo "✅ Installation complete!"
echo ""
echo "Try running: copilot_here --help"
