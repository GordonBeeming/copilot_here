# Set non-interactive frontend to avoid prompts during package installation.
ENV DEBIAN_FRONTEND=noninteractive

# Install git, curl, gpg, gosu, nano, xdg-utils, zsh, and related utilities for the entrypoint script and testing.
RUN apt-get update && apt-get install -y \
  apt-transport-https \
  curl \
  git \
  gosu \
  gpg \
  nano \
  software-properties-common \
  wget \
  xdg-utils \
  zsh \
  && rm -rf /var/lib/apt/lists/*
