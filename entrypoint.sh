#!/bin/bash
set -e

# Get the user and group IDs from environment variables, default to 1000 if not set.
USER_ID=${PUID:-1000}
GROUP_ID=${PGID:-1000}

# Create a group and user with the specified IDs.
groupadd --gid $GROUP_ID appuser_group >/dev/null 2>&1 || true
useradd --uid $USER_ID --gid $GROUP_ID --shell /bin/bash --create-home appuser >/dev/null 2>&1 || true

# Set up the .copilot directory and ensure ownership of the entire home dir.
mkdir -p /home/appuser/.copilot
chown -R $USER_ID:$GROUP_ID /home/appuser

# Switch to the new user and execute the command passed to the script.
exec gosu appuser "$@"