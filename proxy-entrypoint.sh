#!/bin/sh
set -e

echo "🔧 Initializing Secure Proxy..."

# Setup permissions
rm -rf /ca/*
chown -R proxy-user:proxy-user /logs /ca

# Optional: brokered Docker socket bridge for --dind in airlock mode.
#
# When the host launches us with BROKER_BRIDGE_TARGET set (e.g.
# "host.docker.internal:54321"), we run a tiny socat in the background that
# listens on TCP 2375 inside this container and forwards to that target. The
# proxy container is dual-homed on both the airlock network AND the external
# network, so the workload container can reach us by Docker DNS (`proxy:2375`)
# and we can reach the host gateway. The host-side broker still enforces
# every Docker API rule — this is just a layer-4 hop, no inspection.
#
# Keeping this inside the proxy container (instead of a separate sidecar)
# matches Gordon's preference: airlock-only features should live in the
# airlock proxy image, not as extra services in the compose file.
if [ -n "$BROKER_BRIDGE_TARGET" ]; then
    echo "🔌 Starting Docker broker bridge: tcp/2375 -> $BROKER_BRIDGE_TARGET"
    socat -d TCP-LISTEN:2375,fork,reuseaddr "TCP:$BROKER_BRIDGE_TARGET" &
fi

echo "🚀 Starting Secure Proxy..."
# Run proxy as proxy-user (no iptables needed - network isolation handles security)
exec su-exec proxy-user /app/secure-proxy
