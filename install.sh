#!/bin/bash
# Bash/Zsh Install Script for copilot_here
# This script downloads copilot_here.sh and configures the shell profile

set -e

# Download the main script
SCRIPT_PATH="$HOME/.copilot_here.sh"
echo "📥 Downloading copilot_here.sh..."
curl -fsSL "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.sh" -o "$SCRIPT_PATH"
echo "✅ Downloaded to: $SCRIPT_PATH"

# Detect shell and profile
if [ -n "$ZSH_VERSION" ]; then
  SHELL_NAME="zsh"
  if [ -f "$HOME/.zshrc" ]; then
    PROFILE="$HOME/.zshrc"
  elif [ -f "$HOME/.zprofile" ]; then
    PROFILE="$HOME/.zprofile"
  else
    PROFILE="$HOME/.zshrc"
  fi
elif [ -n "$BASH_VERSION" ]; then
  SHELL_NAME="bash"
  if [ -f "$HOME/.bashrc" ]; then
    PROFILE="$HOME/.bashrc"
  elif [ -f "$HOME/.bash_profile" ]; then
    PROFILE="$HOME/.bash_profile"
  elif [ -f "$HOME/.profile" ]; then
    PROFILE="$HOME/.profile"
  else
    PROFILE="$HOME/.bashrc"
  fi
else
  SHELL_NAME="shell"
  PROFILE="$HOME/.profile"
fi

# Create profile if it doesn't exist
if [ ! -f "$PROFILE" ]; then
  echo "📝 Creating $SHELL_NAME profile..."
  touch "$PROFILE"
fi

# Remove any old copilot_here entries and add the new one
echo "🔧 Updating $SHELL_NAME profile..."
TEMP_FILE=$(mktemp)

# Remove all existing copilot_here.sh references
if [ -f "$PROFILE" ]; then
  grep -v "copilot_here.sh" "$PROFILE" > "$TEMP_FILE" || true
else
  touch "$TEMP_FILE"
fi

# Add the new reference if not present
NEW_ENTRY="source \"$SCRIPT_PATH\""
if ! grep -qF "$NEW_ENTRY" "$TEMP_FILE"; then
  echo "" >> "$TEMP_FILE"
  echo "$NEW_ENTRY" >> "$TEMP_FILE"
fi

mv "$TEMP_FILE" "$PROFILE"
echo "✅ Profile updated: $PROFILE"

# Reload the profile
echo "🔄 Reloading $SHELL_NAME profile..."
# shellcheck disable=SC1090
source "$PROFILE"

echo ""
echo "✅ Installation complete!"
echo ""
echo "Try running: copilot_here --help"
