#!/bin/bash
set -e

echo "🔧 Initializing Copilot Container (Airlock)..."

# Wait for CA certificate from proxy
echo "⏳ Waiting for proxy CA certificate..."
while [ ! -f "/ca/certs/ca.pem" ]; do
    sleep 0.5
done

# Trust the proxy CA certificate (running as root here)
echo "📜 Trusting Proxy CA Certificate..."
cp /ca/certs/ca.pem /usr/local/share/ca-certificates/secure-proxy-ca.crt
update-ca-certificates 2>/dev/null

# Also set NODE_EXTRA_CA_CERTS for Node.js apps
export NODE_EXTRA_CA_CERTS=/ca/certs/ca.pem

echo "✅ Container Ready."
echo ""

# Run the original entrypoint with the command
exec /usr/local/bin/entrypoint.sh "$@"
