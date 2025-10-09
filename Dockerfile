# Use a slim Node.js base image, which gives us `npm`.
FROM node:20-slim

# Set non-interactive frontend to avoid prompts during package installation.
ENV DEBIAN_FRONTEND=noninteractive

# Install git, curl, gpg, and gosu for the entrypoint script.
RUN apt-get update && apt-get install -y \
  curl \
  gpg \
  git \
  gosu \
  && rm -rf /var/lib/apt/lists/*

# ARG for the Copilot CLI version - passed from build process
# This ensures cache invalidation when a new version is available
ARG COPILOT_VERSION=latest

# Install the standalone GitHub Copilot CLI via npm.
RUN npm install -g @github/copilot@${COPILOT_VERSION}

# Set the working directory for the container.
WORKDIR /work

# Copy the entrypoint script into the container and make it executable.
COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/entrypoint.sh

# The entrypoint script will handle user creation and command execution.
ENTRYPOINT [ "entrypoint.sh" ]

# The default command to run if none is provided.
CMD [ "copilot", "--banner" ]