#!/bin/bash
# dev-scripts.sh - Copy copilot_here scripts to local config for testing
# Usage: source ./dev-scripts.sh  OR  . ./dev-scripts.sh
#
# This script must be sourced (not executed) to reload functions in current shell
# For full local dev including Docker builds, use ./dev-build.sh instead

# Check if being sourced or executed
if [ "${BASH_SOURCE[0]}" = "${0}" ]; then
    echo "⚠️  This script should be sourced, not executed!"
    echo ""
    echo "Run one of these instead:"
    echo "  source ./devtest.sh"
    echo "  . ./devtest.sh"
    echo ""
    echo "Or use this one-liner:"
    echo "  ./devtest.sh && source ~/.copilot_here.sh"
    exit 1
fi

# Don't use 'set -e' when sourcing - it will crash the shell on any error!

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
    return 1
fi

# Backup current version if it exists
TARGET_FILE="$HOME/.copilot_here.sh"
if [ -f "$TARGET_FILE" ]; then
    BACKUP_FILE="${TARGET_FILE}.backup.$(date +%Y%m%d_%H%M%S)"
    if ! cp "$TARGET_FILE" "$BACKUP_FILE"; then
        echo "❌ Error: Failed to backup existing script"
        return 1
    fi
    echo "💾 Backed up existing script to:"
    echo "   $BACKUP_FILE"
    echo ""
fi

# Copy new version
if ! cp copilot_here.sh "$TARGET_FILE"; then
    echo "❌ Error: Failed to copy script to $TARGET_FILE"
    return 1
fi
echo "✅ Copied copilot_here.sh to:"
echo "   $TARGET_FILE"
echo ""

# Copy network rules file (simulates what --update-scripts does)
CONFIG_DIR="$HOME/.config/copilot_here"
/bin/mkdir -p "$CONFIG_DIR"
if [ -f "default-airlock-rules.json" ]; then
    if cp default-airlock-rules.json "$CONFIG_DIR/default-airlock-rules.json"; then
        echo "✅ Copied default-airlock-rules.json to:"
        echo "   $CONFIG_DIR/default-airlock-rules.json"
        echo ""
    else
        echo "⚠️  Failed to copy default-airlock-rules.json"
    fi
fi

# Copy docker-compose template (simulates what --update-scripts does)
if [ -f "docker-compose.airlock.yml.template" ]; then
    if cp docker-compose.airlock.yml.template "$CONFIG_DIR/docker-compose.airlock.yml.template"; then
        echo "✅ Copied docker-compose.airlock.yml.template to:"
        echo "   $CONFIG_DIR/docker-compose.airlock.yml.template"
        echo ""
    else
        echo "⚠️  Failed to copy docker-compose.airlock.yml.template"
    fi
fi

# Check if already sourced in config
if grep -q "source.*\.copilot_here\.sh" "$CONFIG_FILE" 2>/dev/null; then
    echo "✓ Already configured in $CONFIG_FILE"
else
    echo "⚠️  Not yet configured in $CONFIG_FILE"
    echo "   Run this to add it:"
    echo "   echo 'source ~/.copilot_here.sh' >> $CONFIG_FILE"
fi
echo ""

# Source the new version into current shell
echo "🔄 Loading new version into current shell..."
if ! source "$TARGET_FILE"; then
    echo "❌ Error: Failed to source $TARGET_FILE"
    return 1
fi
echo "✅ Functions loaded successfully!"
echo ""

# Show version
VERSION=$(sed -n '2s/# Version: //p' "$TARGET_FILE" 2>/dev/null || echo "unknown")
echo "📌 Version: $VERSION"
echo ""

# Test that functions work
echo "🧪 Testing functions..."
if command -v copilot_here >/dev/null 2>&1; then
    echo "   ✓ copilot_here is available"
else
    echo "   ✗ copilot_here not found" >&2
fi
if command -v copilot_yolo >/dev/null 2>&1; then
    echo "   ✓ copilot_yolo is available"
else
    echo "   ✗ copilot_yolo not found" >&2
fi
echo ""

echo "✅ Dev test complete! Functions are now loaded in your current shell."
echo ""
echo "💡 Quick tests you can run now:"
echo "   copilot_here --help"
echo "   copilot_here --list-mounts"
echo "   copilot_yolo -d --help"
echo ""
