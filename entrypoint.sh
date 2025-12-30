#!/bin/bash
set -e

# Get the user and group IDs from environment variables, default to 1000 if not set.
USER_ID=${PUID:-1000}
GROUP_ID=${PGID:-1000}

# Check if the desired UID is already taken
if id -u $USER_ID >/dev/null 2>&1; then
    # UID is taken, find the next available UID
    echo "UID $USER_ID is already in use, finding next available UID..." >&2
    while id -u $USER_ID >/dev/null 2>&1; do
        USER_ID=$((USER_ID + 1))
    done
    echo "Using UID $USER_ID for appuser" >&2
fi

# Check if the desired GID is already taken
if getent group $GROUP_ID >/dev/null 2>&1; then
    # GID is taken, find the next available GID
    echo "GID $GROUP_ID is already in use, finding next available GID..." >&2
    while getent group $GROUP_ID >/dev/null 2>&1; do
        GROUP_ID=$((GROUP_ID + 1))
    done
    echo "Using GID $GROUP_ID for appuser_group" >&2
fi

# Create a group and user with the available IDs.
groupadd --gid $GROUP_ID appuser_group >/dev/null 2>&1 || true
useradd --uid $USER_ID --gid $GROUP_ID --shell /bin/bash --create-home appuser >/dev/null 2>&1 || true

# Verify the user was created successfully
if ! id appuser >/dev/null 2>&1; then
    echo "Warning: Failed to create appuser, running as root" >&2
    mkdir -p /home/appuser/.copilot
    exec "$@"
fi

# Set up directories with correct ownership
mkdir -p /home/appuser/.copilot
mkdir -p /home/appuser/.dotnet
mkdir -p /home/appuser/.nuget
mkdir -p /home/appuser/.local
mkdir -p /home/appuser/.cache
mkdir -p /home/appuser/.config
mkdir -p /home/appuser/.npm
chown -R $USER_ID:$GROUP_ID /home/appuser/.copilot
chown -R $USER_ID:$GROUP_ID /home/appuser/.dotnet
chown -R $USER_ID:$GROUP_ID /home/appuser/.nuget
chown -R $USER_ID:$GROUP_ID /home/appuser/.local
chown -R $USER_ID:$GROUP_ID /home/appuser/.cache
chown -R $USER_ID:$GROUP_ID /home/appuser/.config
chown -R $USER_ID:$GROUP_ID /home/appuser/.npm

# Switch to the new user and execute the command passed to the script.
exec gosu appuser "$@"
