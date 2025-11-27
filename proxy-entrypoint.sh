#!/bin/bash
set -e

echo "🔧 Initializing Secure Proxy..."

# Setup permissions
rm -rf /ca/*
chown -R proxy-user:proxy-user /logs /ca

echo "🚀 Starting Secure Proxy..."
# Run proxy as proxy-user (no iptables needed - network isolation handles security)
exec gosu proxy-user /app/secure-proxy
