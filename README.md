# copilot-here: A Secure, Portable Copilot CLI Environment

Run the GitHub Copilot CLI from any directory on your machine, inside a sandboxed Docker container that automatically uses your existing `gh` authentication.

---

## 🚀 What is this?

This project solves a simple problem: you want to use the awesome [GitHub Copilot CLI](https://github.com/features/copilot/cli), but you also want a clean, portable, and secure environment for it.

The `copilot_here` shell function is a lightweight wrapper around a Docker container. When you run it in a terminal, it:
- **Enhances security** by isolating the tool in a container, granting it file system access **only** to the directory you're currently in. 🛡️
- **Keeps your machine clean** by avoiding a global Node.js installation.
- **Authenticates automatically** by using your host machine's existing `gh` CLI credentials.
- **Validates token permissions** by checking for required scopes and warning you about overly permissive tokens.
- **Persists its configuration**, so it remembers which folders you've trusted across sessions.
- **Stays up-to-date** by automatically pulling the latest image version on every run.

## ✅ Prerequisites

Before you start, make sure you have the following installed and configured on your machine:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine on Linux).
- The [GitHub CLI (`gh`)](https://cli.github.com/).
- You must be logged in to the GitHub CLI. You can check by running `gh auth status`. Your token **must** have the `copilot` scope. If it doesn't, run `gh auth refresh -h github.com -s copilot` to add it.

## 🛠️ Setup Instructions

This guide provides two options for your shell function. Option 1 is recommended as the safe default, while Option 2 offers convenience at the cost of security. You can add both to your profile with different names.

### Option 1: The Safe Version (Recommended)

This version will always ask for your confirmation before executing any commands (like `git commit` or `npm install`) when used in non-interactive mode.

1.  **Add the function to your shell profile.**
    Open your shell's startup file (e.g., `~/.zshrc`, `~/.bashrc`, or `~/.config/fish/config.fish`) and add the following code. This will be your default `copilot_here` command.

    ```bash
    copilot_here() {
      # --- SECURITY CHECK ---
      # 1. Ensure the 'copilot' scope is present using a robust grep check.
      if ! gh auth status 2>/dev/null | grep "Token scopes:" | grep -q "'copilot'"; then
        echo "❌ Error: Your gh token is missing the required 'copilot' scope."
        echo "Please run 'gh auth refresh -h github.com -s copilot' to add it."
        return 1
      fi

      # 2. Warn if the token has highly privileged scopes.
      if gh auth status 2>/dev/null | grep "Token scopes:" | grep -q -E "'admin:org'|'admin:enterprise'"; then
        echo "⚠️  Warning: Your GitHub token has highly privileged scopes (e.g., admin:org, admin:enterprise)."
        # Use a portable prompt that works in both bash and zsh
        printf "Are you sure you want to proceed with this token? [y/N]: "
        read confirmation
        # Use a portable method to convert to lowercase
        local lower_confirmation
        lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
        if [[ "$lower_confirmation" != "y" && "$lower_confirmation" != "yes" ]]; then
          echo "Operation cancelled by user."
          return 1
        fi
      fi
      # --- END SECURITY CHECK ---

      # Define the image name for easy reference
      local image_name="ghcr.io/gordonbeeming/copilot_here:latest"

      # Pull the latest version of the image to stay up-to-date.
      echo "Checking for the latest version of copilot_here..."
      docker pull "$image_name" > /dev/null 2>&1

      # Define path for our persistent copilot config on the host machine.
      local copilot_config_path="$HOME/.config/copilot-cli-docker"
      mkdir -p "$copilot_config_path"

      # Use the 'gh' CLI itself to reliably get the current auth token.
      local token=$(gh auth token 2>/dev/null)
      if [ -z "$token" ]; then
        echo "⚠️  Could not retrieve token using 'gh auth token'. Please ensure you are logged in."
      fi

      # Base Docker command arguments
      local docker_args=(
        --rm -it
        -v "$(pwd)":/work
        -v "$copilot_config_path":/home/appuser/.copilot
        -e PUID=$(id -u)
        -e PGID=$(id -g)
        -e GITHUB_TOKEN="$token"
        "$image_name"
      )

      if [ $# -eq 0 ]; then
        # No arguments provided, start interactive mode with the banner.
        docker run "${docker_args[@]}" copilot --banner
      else
        # Arguments provided, run in non-interactive (but safe) mode.
        docker run "${docker_args[@]}" copilot -p "$*"
      fi
    }
    ```

2.  **Reload your shell** (e.g., `source ~/.zshrc`).

### Option 2: The Auto-Approve Version

> ⚠️ **Important Security Note:** This version uses the `--allow-all-tools` flag, which allows Copilot to execute commands on your machine without asking for confirmation first. This is convenient but could lead to unintended consequences. This function includes the same token scope check for an added layer of safety.

1.  **Add the function to your shell profile.**
    You can add this function alongside the safe one, giving it a distinct name like `copilot_yolo`.

    ```bash
    copilot_yolo() {
      # --- SECURITY CHECK ---
      # 1. Ensure the 'copilot' scope is present using a robust grep check.
      if ! gh auth status 2>/dev/null | grep "Token scopes:" | grep -q "'copilot'"; then
        echo "❌ Error: Your gh token is missing the required 'copilot' scope."
        echo "Please run 'gh auth refresh -h github.com -s copilot' to add it."
        return 1
      fi

      # 2. Warn if the token has highly privileged scopes.
      if gh auth status 2>/dev/null | grep "Token scopes:" | grep -q -E "'admin:org'|'admin:enterprise'"; then
        echo "⚠️  Warning: Your GitHub token has highly privileged scopes (e.g., admin:org, admin:enterprise)."
        # Use a portable prompt that works in both bash and zsh
        printf "Are you sure you want to proceed with this token? [y/N]: "
        read confirmation
        # Use a portable method to convert to lowercase
        local lower_confirmation
        lower_confirmation=$(echo "$confirmation" | tr '[:upper:]' '[:lower:]')
        if [[ "$lower_confirmation" != "y" && "$lower_confirmation" != "yes" ]]; then
          echo "Operation cancelled by user."
          return 1
        fi
      fi
      # --- END SECURITY CHECK ---

      # Define the image name for easy reference
      local image_name="ghcr.io/gordonbeeming/copilot_here:latest"

      # Pull the latest version of the image to stay up-to-date.
      echo "Checking for the latest version of copilot_here..."
      docker pull "$image_name" > /dev/null 2>&1

      # Define path for our persistent copilot config on the host machine.
      local copilot_config_path="$HOME/.config/copilot-cli-docker"
      mkdir -p "$copilot_config_path"

      # Use the 'gh' CLI itself to reliably get the current auth token.
      local token=$(gh auth token 2>/dev/null)
      if [ -z "$token" ]; then
        echo "⚠️  Could not retrieve token using 'gh auth token'. Please ensure you are logged in."
      fi

      # Base Docker command arguments
      local docker_args=(
        --rm -it
        -v "$(pwd)":/work
        -v "$copilot_config_path":/home/appuser/.copilot
        -e PUID=$(id -u)
        -e PGID=$(id -g)
        -e GITHUB_TOKEN="$token"
        "$image_name"
      )

      if [ $# -eq 0 ]; then
        # No arguments provided, start interactive mode with banner and auto-approval.
        docker run "${docker_args[@]}" copilot --banner --allow-all-tools
      else
        # Arguments provided, run in non-interactive mode with auto-approval.
        docker run "${docker_args[@]}" copilot -p "$*" --allow-all-tools
      fi
    }
    ```

2.  **Reload your shell** (e.g., `source ~/.zshrc`).

## Usage

Once set up, using it is simple.

### 1. Interactive Mode
For a full, interactive chat session, run your default, safe command:
```bash
copilot_here
````

This will start with the welcome banner.

### 2\. Non-Interactive Mode

Pass a prompt directly to get a response. The safe version will ask for confirmation before running commands.

```bash
# Get a suggestion for a command (safe mode)
copilot_here "suggest a git command to view the last 5 commits"

# Explain a specific file (safe mode)
copilot_here "explain the code in ./my-script.js"

# Ask it to generate code with auto-approval (power user mode)
copilot_yolo "write a C# function that takes a string and returns it in reverse"
```

## 📜 License

This project is licensed under the MIT License.
