#!/usr/bin/env bash
# stamp-version.sh — Stamps a version string into all files that contain it.
# Usage: ./scripts/stamp-version.sh <version>
# Example: ./scripts/stamp-version.sh 2026.03.08

set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  echo "Example: $0 2026.03.08" >&2
  exit 1
fi

NEW_VERSION="$1"

# Validate version format: YYYY.MM.DD or YYYY.MM.DD.N
if ! echo "$NEW_VERSION" | grep -qE '^[0-9]{4}\.[0-9]{2}\.[0-9]{2}(\.[0-9]+)?$'; then
  echo "Error: Version must be in YYYY.MM.DD or YYYY.MM.DD.N format" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION_REGEX='[0-9]{4}\.[0-9]{2}\.[0-9]{2}(\.[0-9]+)?'

stamp_file() {
  local file="$1"
  local pattern="$2"
  local replacement="$3"

  if [ ! -f "$file" ]; then
    echo "  SKIP $file (not found)"
    return
  fi

  if sed --version >/dev/null 2>&1; then
    # GNU sed
    sed -i -E "s|${pattern}|${replacement}|g" "$file"
  else
    # BSD sed (macOS)
    sed -i '' -E "s|${pattern}|${replacement}|g" "$file"
  fi
  echo "  OK   $file"
}

echo "Stamping version: $NEW_VERSION"
echo ""

# VERSION file
echo "$NEW_VERSION" > "$REPO_ROOT/VERSION"
echo "  OK   VERSION"

# copilot_here.sh — line 2 (comment) and line 8 (variable)
stamp_file "$REPO_ROOT/copilot_here.sh" \
  "^(# Version: )${VERSION_REGEX}" \
  "\1${NEW_VERSION}"
stamp_file "$REPO_ROOT/copilot_here.sh" \
  "^(COPILOT_HERE_VERSION=\")${VERSION_REGEX}(\")" \
  "\1${NEW_VERSION}\3"

# copilot_here.ps1 — line 2 (comment) and line 26 (variable)
stamp_file "$REPO_ROOT/copilot_here.ps1" \
  "^(# Version: )${VERSION_REGEX}" \
  "\1${NEW_VERSION}"
stamp_file "$REPO_ROOT/copilot_here.ps1" \
  "^(\\\$script:CopilotHereVersion = \")${VERSION_REGEX}(\")" \
  "\1${NEW_VERSION}\3"

# app/Infrastructure/BuildInfo.cs
stamp_file "$REPO_ROOT/app/Infrastructure/BuildInfo.cs" \
  "(BuildDate = \")${VERSION_REGEX}(\")" \
  "\1${NEW_VERSION}\3"

# packaging/winget/*.yaml — PackageVersion lines
for yaml in "$REPO_ROOT"/packaging/winget/*.yaml; do
  stamp_file "$yaml" \
    "^(PackageVersion: )${VERSION_REGEX}" \
    "\1${NEW_VERSION}"
done

echo ""
echo "Done."
