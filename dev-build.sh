#!/bin/bash
# Build all Docker images locally and tag them as if from GitHub Container Registry
# This allows local testing before pushing to GHCR
#
# Usage: ./dev-build.sh [--no-cache] [--include-all] [--include-dotnet] [--include-<variant>...]

set -e

REGISTRY="ghcr.io/gordonbeeming/copilot_here"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
NO_CACHE=""
INCLUDE_ALL=false
declare -a INCLUDE_VARIANTS=()

# Parse arguments
while [[ $# -gt 0 ]]; do
  case "$1" in
    --no-cache)
      NO_CACHE="--no-cache"
      shift
      ;;
    --include-all)
      INCLUDE_ALL=true
      shift
      ;;
    --include-*)
      variant="${1#--include-}"
      INCLUDE_VARIANTS+=("$variant")
      shift
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: ./dev-build.sh [--no-cache] [--include-all] [--include-dotnet] [--include-<variant>...]"
      exit 1
      ;;
  esac
done

# If --include-all, discover all variants
if $INCLUDE_ALL; then
  for variant_file in "${SCRIPT_DIR}/docker/variants/Dockerfile."*; do
    if [[ -f "$variant_file" ]]; then
      variant_name=$(basename "$variant_file" | sed 's/Dockerfile\.//')
      INCLUDE_VARIANTS+=("$variant_name")
    fi
  done
fi

echo "========================================"
echo "   Building Docker Images Locally"
echo "========================================"
echo ""
echo "Registry: $REGISTRY"
echo ""

# First, copy scripts to simulate update (like dev-scripts.sh does)
echo "📋 Copying scripts to ~/.config/copilot_here..."
CONFIG_DIR="$HOME/.config/copilot_here"
mkdir -p "$CONFIG_DIR"

if [ -f "${SCRIPT_DIR}/copilot_here.sh" ]; then
  cp "${SCRIPT_DIR}/copilot_here.sh" "$HOME/.copilot_here.sh"
  echo "   ✓ Copied copilot_here.sh"
fi
if [ -f "${SCRIPT_DIR}/default-airlock-rules.json" ]; then
  cp "${SCRIPT_DIR}/default-airlock-rules.json" "$CONFIG_DIR/"
  echo "   ✓ Copied default-airlock-rules.json"
fi
if [ -f "${SCRIPT_DIR}/docker-compose.airlock.yml.template" ]; then
  cp "${SCRIPT_DIR}/docker-compose.airlock.yml.template" "$CONFIG_DIR/"
  echo "   ✓ Copied docker-compose.airlock.yml.template"
fi
echo ""

# Build proxy image
echo "🔧 Building proxy image..."
docker build $NO_CACHE \
  -t "${REGISTRY}:proxy" \
  -f "${SCRIPT_DIR}/docker/Dockerfile.proxy" \
  "${SCRIPT_DIR}"
echo "   ✓ Tagged as ${REGISTRY}:proxy"
echo ""

# Build base image
echo "🔧 Building base image..."
docker build $NO_CACHE \
  -t "${REGISTRY}:latest" \
  -f "${SCRIPT_DIR}/docker/Dockerfile.base" \
  "${SCRIPT_DIR}"
echo "   ✓ Tagged as ${REGISTRY}:latest"
echo ""

# Build variant images (only if explicitly requested)
if [[ ${#INCLUDE_VARIANTS[@]} -gt 0 ]]; then
  for variant_name in "${INCLUDE_VARIANTS[@]}"; do
    variant_file="${SCRIPT_DIR}/docker/variants/Dockerfile.${variant_name}"
    if [[ -f "$variant_file" ]]; then
      echo "🔧 Building variant: $variant_name..."
      # Determine build args based on variant type
      BUILD_ARGS="--build-arg BASE_IMAGE_TAG=latest"
      case "$variant_name" in
        dotnet-playwright)
          # Playwright builds on dotnet, not base
          BUILD_ARGS="--build-arg DOTNET_IMAGE_TAG=dotnet"
          ;;
      esac
      docker build $NO_CACHE \
        $BUILD_ARGS \
        -t "${REGISTRY}:${variant_name}" \
        -f "$variant_file" \
        "${SCRIPT_DIR}"
      echo "   ✓ Tagged as ${REGISTRY}:${variant_name}"
      echo ""
    else
      echo "⚠️  Variant not found: $variant_name (expected ${variant_file})"
      echo ""
    fi
  done
else
  echo "ℹ️  Skipping variants (use --include-<variant> to build, e.g., --include-dotnet)"
  echo ""
fi

echo "========================================"
echo "   Build Complete!"
echo "========================================"
echo ""
echo "Built images:"
docker images --format "  {{.Repository}}:{{.Tag}}\t{{.Size}}" | grep "$REGISTRY" | sort
echo ""
echo "Run integration tests with:"
echo "  ./tests/integration/test_airlock.sh"
echo ""
echo "💡 To load shell functions, run:"
echo "   source ~/.copilot_here.sh"
echo ""
echo "💡 To use locally built images (skip pulling from registry):"
echo "   copilot_here --skip-pull"
echo "   copilot_yolo --skip-pull"
