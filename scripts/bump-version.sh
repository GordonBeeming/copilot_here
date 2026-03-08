#!/usr/bin/env bash
# bump-version.sh — Update VERSION file and stamp all files.
# Usage: ./scripts/bump-version.sh <version>
# Example: ./scripts/bump-version.sh 2026.03.08

set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Usage: $0 <version>" >&2
  echo "Example: $0 2026.03.08" >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Bumping version to: $1"
"$SCRIPT_DIR/stamp-version.sh" "$1"
