# Set non-interactive frontend to avoid prompts during package installation.
ENV DEBIAN_FRONTEND=noninteractive

# Base utilities every image needs for the entrypoint script and testing.
# Python tooling (pip + venv + pipx) ships here so every image can install pip-distributed
# CLIs; Debian 12 enforces PEP 668, so `pipx install <tool>` is the supported path.
# nano and vim are both here so an agent CLI's editor shell-out always finds something:
# nano for people who just want to type, vim for anyone whose fingers expect `vi`.
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
  vim \
  wget \
  xdg-utils \
  zsh \
  && rm -rf /var/lib/apt/lists/*

# Agent CLIs that shell out to an editor read $COPILOT_EDITOR, then $VISUAL, then $EDITOR, and
# fall back to a hardcoded `vi` when all three are empty. These two keep that fallback off the
# table so editing a prompt or a commit message always opens something. nano is the default
# because it prints how to quit on screen; vim is installed above for anyone who prefers it.
# A run-time `-e EDITOR=...` still beats an image ENV, so SANDBOX_FLAGS overrides this per session.
ENV EDITOR=nano
ENV VISUAL=nano

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
