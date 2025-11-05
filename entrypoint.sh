#!/bin/bash
set -e

# Get the user and group IDs from environment variables, default to 1000 if not set.
USER_ID=${PUID:-1000}
GROUP_ID=${PGID:-1000}

# Create a group and user with the specified IDs.
groupadd --gid $GROUP_ID appuser_group >/dev/null 2>&1 || true
useradd --uid $USER_ID --gid $GROUP_ID --shell /bin/bash --create-home appuser >/dev/null 2>&1 || true

# Verify the user was created successfully
if ! id appuser >/dev/null 2>&1; then
    echo "Warning: Failed to create appuser, running as root" >&2
    mkdir -p /home/appuser/.copilot
    exec "$@"
fi

# Set up the .copilot directory with correct ownership
mkdir -p /home/appuser/.copilot
chown $USER_ID:$GROUP_ID /home/appuser
chown -R $USER_ID:$GROUP_ID /home/appuser/.copilot

# Switch to the new user and execute the command passed to the script.
exec gosu appuser "$@"