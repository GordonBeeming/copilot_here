# Use a slim Node.js base image, which gives us `npm`.
FROM node:20-slim

# Set non-interactive frontend to avoid prompts during package installation.
ENV DEBIAN_FRONTEND=noninteractive

# Install git, curl, gpg, gosu, zsh, and PowerShell for the entrypoint script and testing.
RUN apt-get update && apt-get install -y \
  curl \
  gpg \
  git \
  gosu \
  zsh \
  wget \
  apt-transport-https \
  software-properties-common \
  && rm -rf /var/lib/apt/lists/*

# Install PowerShell - architecture-specific approach
RUN ARCH=$(dpkg --print-architecture) \
  && if [ "$ARCH" = "amd64" ]; then \
    # AMD64: Use Microsoft's APT repository
    wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y powershell \
    && rm -rf /var/lib/apt/lists/*; \
  elif [ "$ARCH" = "arm64" ]; then \
    # ARM64: Download and install from GitHub releases
    wget -q https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/powershell-7.4.6-linux-arm64.tar.gz -O /tmp/powershell.tar.gz \
    && mkdir -p /opt/microsoft/powershell/7 \
    && tar zxf /tmp/powershell.tar.gz -C /opt/microsoft/powershell/7 \
    && chmod +x /opt/microsoft/powershell/7/pwsh \
    && ln -s /opt/microsoft/powershell/7/pwsh /usr/bin/pwsh \
    && rm /tmp/powershell.tar.gz; \
  fi

# ARG for the Copilot CLI version - passed from build process
# This ensures cache invalidation when a new version is available
ARG COPILOT_VERSION=latest

# Install the standalone GitHub Copilot CLI via npm.
RUN npm install -g @github/copilot@${COPILOT_VERSION}

# Set the working directory for the container.
WORKDIR /work

# Copy the entrypoint script into the container and make it executable.
COPY docker/shared/entrypoint.sh /usr/local/bin/
COPY docker/shared/entrypoint-airlock.sh /usr/local/bin/
COPY docker/session-info.sh /usr/local/bin/session-info
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/entrypoint-airlock.sh /usr/local/bin/session-info

# The entrypoint script will handle user creation and command execution.
ENTRYPOINT [ "entrypoint.sh" ]

# The default command to run if none is provided.
CMD [ "copilot", "--banner" ]