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
  python3 \
  python3-pip \
  python3-venv \
  software-properties-common \
  wget \
  xdg-utils \
  zsh \
  && rm -rf /var/lib/apt/lists/*

# pipx installs apps into the runtime user's ~/.local/bin, so that dir needs to be on PATH
# for pipx-installed CLIs (e.g. `pipx install apm`) to be reachable without a per-session
# `pipx ensurepath`. The entrypoint always runs as appuser with HOME=/home/appuser.
# Append (not prepend): the entrypoint runs as root and resolves tools like getent/groupadd/
# node via PATH before dropping to appuser, so a system binary must always win over anything
# in the user-writable (and possibly bind-mounted) ~/.local/bin — otherwise a planted binary
# there could execute as root. Appending keeps pipx apps reachable while system paths stay
# authoritative; pipx app names don't collide with system binaries.
# ENV covers the exec'd command and non-login shells; the profile.d drop-in re-adds it for
# login shells, which otherwise reset PATH via /etc/profile (Debian zsh sources it too).
ENV PATH="${PATH}:/home/appuser/.local/bin"
RUN echo 'case ":${PATH}:" in *:/home/appuser/.local/bin:*) ;; *) PATH="${PATH:+${PATH}:}/home/appuser/.local/bin" ;; esac' \
  > /etc/profile.d/copilot-local-bin.sh
