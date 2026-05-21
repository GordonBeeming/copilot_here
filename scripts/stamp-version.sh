#!/usr/bin/env bash
# stamp-version.sh — Stamps a version string into the shell scripts so users
# who download the released `copilot_here.sh` / `copilot_here.ps1` get a real
# version their auto-update check can compare against.
#
# Source files carry `0.0.0-dev` placeholders; CI calls this script with the
# version produced by the `compute-version` job before testing and packaging.
# The .NET binary's version is set via -p:CopilotHereVersion=... on the dotnet
# invocation — it doesn't need stamping into source.
#
# Usage: ./scripts/stamp-version.sh <version>
# Example: ./scripts/stamp-version.sh 2026.05.21.1

set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  echo "Example: $0 2026.05.21.1" >&2
  exit 1
fi

NEW_VERSION="$1"

if ! echo "$NEW_VERSION" | grep -qE '^[0-9]{4}\.[0-9]{2}\.[0-9]{2}(\.[0-9]+)?$'; then
  echo "Error: Version must be in YYYY.MM.DD or YYYY.MM.DD.N format" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Matches either the dev sentinel or any previously-stamped real version.
# Flat alternation (no nested groups) keeps BSD sed -E happy on macOS.
VERSION_REGEX='(0\.0\.0-dev|[0-9]{4}\.[0-9]{2}\.[0-9]{2}\.[0-9]+|[0-9]{4}\.[0-9]{2}\.[0-9]{2})'

stamp_file() {
  local file="$1"
  local pattern="$2"
  local replacement="$3"

  if [ ! -f "$file" ]; then
    echo "  SKIP $file (not found)"
    return
  fi

  # Use `~` as the sed delimiter: `|` is the ERE alternation operator and
  # `#` appears in the script comments we're matching, so both would terminate
  # the s/// expression mid-pattern. `~` shows up nowhere in our regexes.
  if sed --version >/dev/null 2>&1; then
    sed -i -E "s~${pattern}~${replacement}~g" "$file"
  else
    sed -i '' -E "s~${pattern}~${replacement}~g" "$file"
  fi
  echo "  OK   $file"
}

echo "Stamping version: $NEW_VERSION"
echo ""

stamp_file "$REPO_ROOT/copilot_here.sh" \
  "^(# Version: )${VERSION_REGEX}" \
  "\1${NEW_VERSION}"
stamp_file "$REPO_ROOT/copilot_here.sh" \
  "^(COPILOT_HERE_VERSION=\")${VERSION_REGEX}(\")" \
  "\1${NEW_VERSION}\3"

stamp_file "$REPO_ROOT/copilot_here.ps1" \
  "^(# Version: )${VERSION_REGEX}" \
  "\1${NEW_VERSION}"
stamp_file "$REPO_ROOT/copilot_here.ps1" \
  "^(\\\$script:CopilotHereVersion = \")${VERSION_REGEX}(\")" \
  "\1${NEW_VERSION}\3"

echo ""
echo "Done."
