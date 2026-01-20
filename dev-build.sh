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

# Detect OS and architecture for native binary
detect_rid() {
  local os=""
  local arch=""
  
  case "$(uname -s)" in
    Linux*)  os="linux" ;;
    Darwin*) os="osx" ;;
    MINGW*|MSYS*|CYGWIN*) os="win" ;;
    *)       echo "Unknown OS"; return 1 ;;
  esac
  
  case "$(uname -m)" in
    x86_64)  arch="x64" ;;
    aarch64|arm64) arch="arm64" ;;
    *)       arch="x64" ;;
  esac
  
  echo "${os}-${arch}"
}

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

# If --include-all, discover all variants from both folders
if $INCLUDE_ALL; then
  for variant_file in "${SCRIPT_DIR}/docker/variants/Dockerfile."*; do
    if [[ -f "$variant_file" ]]; then
      variant_name=$(basename "$variant_file" | sed 's/Dockerfile\.//')
      INCLUDE_VARIANTS+=("$variant_name")
    fi
  done
  for variant_file in "${SCRIPT_DIR}/docker/compound-variants/Dockerfile."*; do
    if [[ -f "$variant_file" ]]; then
      variant_name=$(basename "$variant_file" | sed 's/Dockerfile\.//')
      INCLUDE_VARIANTS+=("$variant_name")
    fi
  done
fi

# Define compound variants that depend on other variants (not base)
# These will be built after all regular variants
is_compound_variant() {
  case "$1" in
    dotnet-playwright|dotnet-rust) return 0 ;;
    *) return 1 ;;
  esac
}

# Separate variants into regular and compound
declare -a REGULAR_VARIANTS=()
declare -a COMPOUND_VARIANTS=()
for variant in "${INCLUDE_VARIANTS[@]}"; do
  if is_compound_variant "$variant"; then
    COMPOUND_VARIANTS+=("$variant")
  else
    REGULAR_VARIANTS+=("$variant")
  fi
done

echo "========================================"
echo "   Building Docker Images Locally"
echo "========================================"
echo ""
echo "Registry: $REGISTRY"
echo ""

# Build native binary
echo "🔧 Building native CLI binary..."
RID=$(detect_rid)
PUBLISH_DIR="${SCRIPT_DIR}/publish/${RID}"
BIN_DIR="$HOME/.local/bin"

# Check for running copilot_here containers
RUNNING_CONTAINERS=$(docker ps --filter "name=copilot_here-" -q)
if [ -n "$RUNNING_CONTAINERS" ]; then
  echo "⚠️  copilot_here is currently running in Docker"
  printf "   Stop running containers to continue? [y/N]: "
  read -r response
  if [ "$response" = "y" ] || [ "$response" = "Y" ] || [ "$response" = "yes" ] || [ "$response" = "YES" ]; then
    echo "🛑 Stopping copilot_here containers..."
    docker stop $RUNNING_CONTAINERS 2>/dev/null
    echo "   ✓ Stopped"
  else
    echo "❌ Cannot build while containers are running (binary is in use)"
    exit 1
  fi
  echo ""
fi

dotnet publish "${SCRIPT_DIR}/app/CopilotHere.csproj" \
  -c Release \
  -r "$RID" \
  -o "$PUBLISH_DIR" \
  --nologo \
  -v q

# Copy to local bin
mkdir -p "$BIN_DIR"
if [[ "$RID" == win-* ]]; then
  cp "${PUBLISH_DIR}/copilot_here.exe" "$BIN_DIR/"
  echo "   ✓ Copied to ${BIN_DIR}/copilot_here.exe"
else
  cp "${PUBLISH_DIR}/copilot_here" "$BIN_DIR/"
  chmod +x "$BIN_DIR/copilot_here"
  echo "   ✓ Copied to ${BIN_DIR}/copilot_here"
fi
echo ""

# Copy airlock config files to ~/.config/copilot_here
echo "📋 Copying config files to ~/.config/copilot_here..."
CONFIG_DIR="$HOME/.config/copilot_here"
mkdir -p "$CONFIG_DIR"

if [ -f "${SCRIPT_DIR}/default-airlock-rules.json" ]; then
  cp "${SCRIPT_DIR}/default-airlock-rules.json" "$CONFIG_DIR/"
  echo "   ✓ Copied default-airlock-rules.json"
fi
echo ""

# Copy and source shell script
echo "📋 Updating shell functions..."
SHELL_SCRIPT_PATH="$HOME/.copilot_here.sh"
cp "${SCRIPT_DIR}/copilot_here.sh" "$SHELL_SCRIPT_PATH"
echo "   ✓ Copied copilot_here.sh to $SHELL_SCRIPT_PATH"

# Source the updated script
source "$SHELL_SCRIPT_PATH"
echo "   ✓ Sourced updated shell functions"
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
echo "🔧 Building base image (GitHub Copilot)..."
docker build $NO_CACHE \
  -t "${REGISTRY}:latest" \
  -t "${REGISTRY}:copilot-latest" \
  -f "${SCRIPT_DIR}/docker/tools/github-copilot/Dockerfile" \
  "${SCRIPT_DIR}"
echo "   ✓ Tagged as ${REGISTRY}:latest"
echo "   ✓ Tagged as ${REGISTRY}:copilot-latest"
echo ""

# Build variant images (only if explicitly requested)
build_variant() {
  local variant_name="$1"
  local build_args="$2"
  
  # Check variants folder first, then compound-variants
  local variant_file="${SCRIPT_DIR}/docker/variants/Dockerfile.${variant_name}"
  if [[ ! -f "$variant_file" ]]; then
    variant_file="${SCRIPT_DIR}/docker/compound-variants/Dockerfile.${variant_name}"
  fi
  
  if [[ -f "$variant_file" ]]; then
    echo "🔧 Building variant: $variant_name (GitHub Copilot)..."
    docker build $NO_CACHE \
      $build_args \
      -t "${REGISTRY}:${variant_name}" \
      -t "${REGISTRY}:copilot-${variant_name}" \
      -f "$variant_file" \
      "${SCRIPT_DIR}"
    echo "   ✓ Tagged as ${REGISTRY}:${variant_name}"
    echo "   ✓ Tagged as ${REGISTRY}:copilot-${variant_name}"
    echo ""
  else
    echo "⚠️  Variant not found: $variant_name"
    echo ""
  fi
}

# Build regular variants first (depend on base)
if [[ ${#REGULAR_VARIANTS[@]} -gt 0 ]]; then
  for variant_name in "${REGULAR_VARIANTS[@]}"; do
    build_variant "$variant_name" "--build-arg BASE_IMAGE_TAG=copilot-latest"
  done
fi

# Build compound variants (depend on other variants like dotnet)
if [[ ${#COMPOUND_VARIANTS[@]} -gt 0 ]]; then
  for variant_name in "${COMPOUND_VARIANTS[@]}"; do
    build_variant "$variant_name" "--build-arg DOTNET_IMAGE_TAG=copilot-dotnet"
  done
fi

if [[ ${#INCLUDE_VARIANTS[@]} -eq 0 ]]; then
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
echo "  ./tests/integration/test_airlock.sh --use-local"
echo ""
echo "💡 To use locally built images (skip pulling from registry):"
echo "   copilot_here --no-pull"
echo "   copilot_yolo --no-pull"
