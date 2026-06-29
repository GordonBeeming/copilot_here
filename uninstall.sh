#!/bin/bash
# Bash/Zsh uninstall script for copilot_here.
# Self-contained: it removes the install by known paths and does NOT depend on the
# binary still working, so it cleans up even a half-broken install.
#
# Usage:
#   bash <(curl -fsSL https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/uninstall.sh)
#   bash <(curl -fsSL .../uninstall.sh) --purge   # also delete config dirs
#   bash <(curl -fsSL .../uninstall.sh) --yes      # skip the confirmation prompt
#
# Never touches ~/.claude (your real Claude Code config).

__copilot_uninstall_main() {
  local purge=0
  local assume_yes=0
  local arg
  for arg in "$@"; do
    case "$arg" in
      --purge) purge=1 ;;
      --yes|-y) assume_yes=1 ;;
    esac
  done

  local bin="${COPILOT_HERE_BIN:-$HOME/.local/bin/copilot_here}"
  local script="$HOME/.copilot_here.sh"
  local fish_conf="$HOME/.config/fish/conf.d/copilot_here.fish"
  local marker_start="# >>> copilot_here >>>"
  local marker_end="# <<< copilot_here <<<"

  echo "🧹 Uninstalling copilot_here"
  echo "   This removes the binary, shell script, and shell integration."
  if [ "$purge" -eq 1 ]; then
    echo "   --purge: also deleting ~/.config/copilot_here and ~/.config/copilot-cli-docker"
  fi
  echo "   Your Claude config (~/.claude) is left untouched."

  if [ "$assume_yes" -ne 1 ]; then
    printf "   Continue? [y/N]: "
    read -r response
    local lower
    lower=$(echo "$response" | tr '[:upper:]' '[:lower:]')
    if [ "$lower" != "y" ] && [ "$lower" != "yes" ]; then
      echo "❌ Uninstall cancelled."
      return 1
    fi
  fi

  # Stop any running containers so the binary isn't in use.
  local running
  running=$(docker ps --filter "name=copilot_here-" -q 2>/dev/null)
  if [ -n "$running" ]; then
    echo "🛑 Stopping copilot_here containers..."
    docker stop $running >/dev/null 2>&1
  fi

  # Strip the marker block (and any stray sourcing line) from each profile.
  __copilot_strip_profile() {
    local profile="$1"
    [ -f "$profile" ] || return 0
    grep -qF "$marker_start" "$profile" 2>/dev/null || grep -qF "copilot_here.sh" "$profile" 2>/dev/null || return 0

    # Only strip a fenced block when both markers are present. With a start marker
    # but no matching end marker, treating everything after the start as in-block
    # would discard the user's config below it; instead drop only the marker and
    # stray sourcing lines.
    local has_end=0
    grep -qF "$marker_end" "$profile" 2>/dev/null && has_end=1

    local temp
    temp=$(mktemp)
    awk -v start="$marker_start" -v end="$marker_end" -v strip="$has_end" '
      BEGIN { in_block=0 }
      $0 ~ start { if (strip == "1") in_block=1; next }
      $0 ~ end { in_block=0; next }
      !in_block && $0 !~ /copilot_here\.sh/ { print }
    ' "$profile" > "$temp"

    # Preserve symlinks (e.g. GNU Stow): write through the resolved target.
    if [ -L "$profile" ]; then
      local real
      real="$(readlink -f "$profile")"
      mv "$temp" "$real"
    else
      cat "$temp" > "$profile" && rm -f "$temp"
    fi
    echo "✓ Cleaned shell integration from $(basename "$profile")"
  }

  __copilot_strip_profile "$HOME/.bashrc"
  __copilot_strip_profile "$HOME/.bash_profile"
  __copilot_strip_profile "$HOME/.profile"
  __copilot_strip_profile "$HOME/.zshrc"
  __copilot_strip_profile "$HOME/.zprofile"

  __copilot_rm() {
    [ -e "$1" ] || return 0
    rm -f "$1" && echo "✓ Removed $2"
  }

  __copilot_rm "$fish_conf" "fish wrapper"
  __copilot_rm "$script" "shell script (~/.copilot_here.sh)"
  __copilot_rm "$bin" "binary ($bin)"

  if [ "$purge" -eq 1 ]; then
    # Best-effort: remove copilot_here containers and pulled images so --purge
    # matches the CLI's --purge. Skipped silently when docker isn't available.
    if command -v docker >/dev/null 2>&1; then
      local containers
      containers=$(docker ps -aq --filter "name=copilot_here-" 2>/dev/null)
      if [ -n "$containers" ]; then
        docker rm -f $containers >/dev/null 2>&1 && echo "✓ Removed copilot_here containers"
      fi
      local images
      images=$(docker images --filter=reference='ghcr.io/gordonbeeming/copilot_here*' -q 2>/dev/null | sort -u)
      if [ -n "$images" ]; then
        docker rmi -f $images >/dev/null 2>&1 && echo "✓ Removed copilot_here images"
      fi
    fi
    if [ -d "$HOME/.config/copilot_here" ]; then
      rm -rf "$HOME/.config/copilot_here" && echo "✓ Removed config (~/.config/copilot_here)"
    fi
    if [ -d "$HOME/.config/copilot-cli-docker" ]; then
      rm -rf "$HOME/.config/copilot-cli-docker" && echo "✓ Removed config (~/.config/copilot-cli-docker)"
    fi
  else
    echo "• Kept config dirs (re-run with --purge to remove them)"
  fi

  echo ""
  echo "✅ copilot_here uninstalled."
  echo "   Restart your shell or open a new terminal to clear the loaded function."
  echo "   Installed via Homebrew / dotnet tool? Remove it with that package manager."
  return 0
}

__copilot_uninstall_main "$@"
__copilot_uninstall_status=$?
# Work whether the script is executed (bash <(...)) or sourced (source <(...)).
return $__copilot_uninstall_status 2>/dev/null || exit $__copilot_uninstall_status
