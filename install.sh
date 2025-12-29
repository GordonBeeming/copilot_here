#!/bin/bash
# Bash/Zsh Install Script for copilot_here
# Downloads the script and runs the update function

# Download the main script
SCRIPT_PATH="$HOME/.copilot_here.sh"
echo "📥 Downloading copilot_here.sh..."
if ! curl -fsSL "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.sh" -o "$SCRIPT_PATH"; then
  echo "❌ Failed to download copilot_here.sh" >&2
  return 1
fi
echo "✅ Downloaded to: $SCRIPT_PATH"

# Source the script to load functions
echo "🔄 Loading copilot_here functions..."
# shellcheck disable=SC1090
if ! source "$SCRIPT_PATH"; then
  echo "❌ Failed to load copilot_here functions" >&2
  return 1
fi

# Run update to set up everything (binary, profiles, etc.)
echo ""
echo "📦 Running update..."
copilot_here --update

# Run install-shells to set up shell integration
echo ""
echo "🔧 Setting up shell integration..."
copilot_here --install-shells

echo ""
echo "✅ Installation complete!"
if [ -n "$COPILOT_HERE_VERSION" ]; then
  echo "   Loaded version: $COPILOT_HERE_VERSION"
fi
echo ""
echo "⚠️  Please restart your shell or run:"
CURRENT_SHELL=$(basename "$SHELL")
case "$CURRENT_SHELL" in
  zsh)
    echo "   source ~/.zshrc"
    ;;
  bash)
    echo "   source ~/.bashrc"
    ;;
  *)
    echo "   source ~/.${CURRENT_SHELL}rc"
    ;;
esac
echo ""
echo "Then try: copilot_here --help"

