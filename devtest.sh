#!/bin/bash
# devtest.sh - Quick script to test copilot_here changes locally
# Usage: ./devtest.sh

set -e

echo "🔧 Developer Test Script"
echo "════════════════════════════════════════════════════════════"
echo ""

# Detect the user's actual shell (not the script's shell)
USER_SHELL=$(basename "$SHELL")
if [ "$USER_SHELL" = "zsh" ]; then
    SHELL_TYPE="zsh"
    CONFIG_FILE="${ZDOTDIR:-$HOME}/.zshrc"
elif [ "$USER_SHELL" = "bash" ]; then
    SHELL_TYPE="bash"
    CONFIG_FILE="$HOME/.bashrc"
else
    echo "⚠️  Could not detect shell from \$SHELL ($SHELL)"
    echo "   Defaulting to bash"
    SHELL_TYPE="bash"
    CONFIG_FILE="$HOME/.bashrc"
fi

echo "📋 Detected shell: $SHELL_TYPE"
echo "📁 Config file: $CONFIG_FILE"
echo ""

# Check if script exists
if [ ! -f "copilot_here.sh" ]; then
    echo "❌ Error: copilot_here.sh not found in current directory"
    exit 1
fi

# Backup current version if it exists
TARGET_FILE="$HOME/.copilot_here.sh"
if [ -f "$TARGET_FILE" ]; then
    BACKUP_FILE="${TARGET_FILE}.backup.$(date +%Y%m%d_%H%M%S)"
    cp "$TARGET_FILE" "$BACKUP_FILE"
    echo "💾 Backed up existing script to:"
    echo "   $BACKUP_FILE"
    echo ""
fi

# Copy new version
cp copilot_here.sh "$TARGET_FILE"
echo "✅ Copied copilot_here.sh to:"
echo "   $TARGET_FILE"
echo ""

# Check if already sourced in config
if grep -q "source.*\.copilot_here\.sh" "$CONFIG_FILE" 2>/dev/null; then
    echo "✓ Already configured in $CONFIG_FILE"
else
    echo "⚠️  Not yet configured in $CONFIG_FILE"
    echo "   Run this to add it:"
    echo "   echo 'source ~/.copilot_here.sh' >> $CONFIG_FILE"
fi
echo ""

# Source the new version into the actual shell (bash or zsh)
echo "🔄 Loading new version into current $SHELL_TYPE shell..."
if [ "$SHELL_TYPE" = "zsh" ]; then
    zsh -c "source '$TARGET_FILE' && echo '✅ Functions loaded successfully in zsh!' && copilot_here --help | head -15"
else
    bash -c "source '$TARGET_FILE' && echo '✅ Functions loaded successfully in bash!' && copilot_here --help | head -15"
fi
echo ""

# Show version
VERSION=$(sed -n '2s/# Version: //p' "$TARGET_FILE" 2>/dev/null || echo "unknown")
echo "📌 Version: $VERSION"
echo ""

# Show available functions
echo "✨ Available functions:"
echo "   • copilot_here  - Safe mode (asks for confirmation)"
echo "   • copilot_yolo  - YOLO mode (auto-approves everything)"
echo ""

echo "✅ Dev test complete!"
echo ""
echo "💡 To use in your current terminal, run:"
echo "   source ~/.copilot_here.sh"
echo ""
echo "💡 Quick tests you can run (after sourcing):"
echo "   copilot_here --help"
echo "   copilot_here --list-mounts"
echo "   copilot_here --mount /tmp -p 'test'"
echo ""
echo "🔄 To reload in future $SHELL_TYPE terminals, run:"
echo "   source $CONFIG_FILE"
echo ""
