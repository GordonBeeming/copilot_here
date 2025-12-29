#!/bin/bash
# Bash/Zsh Install Script for copilot_here
# This script downloads copilot_here.sh and configures shell profiles

# Note: Not using 'set -e' because this script is designed to be sourced,
# and we don't want profile errors to exit the user's shell

# Download the main script
SCRIPT_PATH="$HOME/.copilot_here.sh"
echo "📥 Downloading copilot_here.sh..."
if ! curl -fsSL "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.sh" -o "$SCRIPT_PATH"; then
  echo "❌ Failed to download copilot_here.sh" >&2
  return 1 2>/dev/null || exit 1
fi
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
  
  # Always clean up any rogue copilot_here entries outside markers
  local temp_file
  temp_file=$(mktemp)
  
  # Check if marker block exists
  if grep -qF "$marker_start" "$profile_path" 2>/dev/null; then
    # Markers exist - preserve only the marked block, remove everything else
    awk -v start="$marker_start" -v end="$marker_end" '
      BEGIN { in_block=0; outside="" }
      $0 ~ start { in_block=1; block=$0"\n"; next }
      in_block { block=block $0"\n"; if ($0 ~ end) { in_block=0 }; next }
      !in_block && $0 !~ /copilot_here\.sh/ { outside=outside $0"\n" }
      END { printf "%s%s", outside, block }
    ' "$profile_path" > "$temp_file"
    mv "$temp_file" "$profile_path"
    echo "   ✓ $profile_name (cleaned up rogue entries)"
    return
  fi
  
  # No markers - remove all copilot_here entries and add fresh block
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

# Don't reload profile files - they may have errors or complex logic
# Just source the copilot_here script directly for immediate availability
echo "🔄 Loading copilot_here functions..."
# shellcheck disable=SC1090
if ! source "$SCRIPT_PATH"; then
  echo "❌ Failed to load copilot_here functions" >&2
  return 1 2>/dev/null || exit 1
fi

echo ""
echo "✅ Installation complete!"
if [ -n "$COPILOT_HERE_VERSION" ]; then
  echo "   Loaded version: $COPILOT_HERE_VERSION"
fi
echo ""
echo "Try running: copilot_here --help"
