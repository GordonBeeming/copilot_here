# Set non-interactive frontend to avoid prompts during package installation.
ENV DEBIAN_FRONTEND=noninteractive

# Install git, curl, gpg, gosu, nano, xdg-utils, zsh, and related utilities for the entrypoint script and testing.
# Python tooling (pip + venv + pipx) ships here so every image can install pip-distributed
# CLIs; Debian 12 enforces PEP 668, so `pipx install <tool>` is the supported path.
RUN apt-get update && apt-get install -y \
  apt-transport-https \
  curl \
  git \
  gosu \
  gpg \
  nano \
  pipx \
  python3-pip \
  python3-venv \
  software-properties-common \
  wget \
  xdg-utils \
  zsh \
  && rm -rf /var/lib/apt/lists/*

# pipx installs apps into the runtime user's ~/.local/bin. The entrypoint always runs as
# appuser with HOME=/home/appuser, so put that dir on PATH up front and pipx-installed CLIs
# (e.g. `pipx install apm`) are reachable without a per-session `pipx ensurepath`.
# ENV covers the exec'd command and non-login shells; the profile.d drop-in re-adds it for
# login shells, which otherwise reset PATH via /etc/profile (Debian zsh sources it too).
ENV PATH="/home/appuser/.local/bin:${PATH}"
RUN echo 'case ":${PATH}:" in *:/home/appuser/.local/bin:*) ;; *) PATH="/home/appuser/.local/bin:${PATH}" ;; esac' \
  > /etc/profile.d/copilot-local-bin.sh
