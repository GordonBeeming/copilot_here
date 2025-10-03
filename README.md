# copilot-here: A Secure, Portable Copilot CLI Environment

Run the GitHub Copilot CLI from any directory on your machine, inside a sandboxed Docker container that automatically uses your existing `gh` authentication.

---

## 🚀 What is this?

This project solves a simple problem: you want to use the awesome [GitHub Copilot CLI](https://github.com/features/copilot/cli), but you also want a clean, portable, and secure environment for it.

The `copilot_here` shell function is a lightweight wrapper around a Docker container. When you run it in a terminal, it:
- **Enhances security** by isolating the tool in a container, granting it file system access **only** to the directory you're currently in. 🛡️
- **Keeps your machine clean** by avoiding a global Node.js installation.
- **Authenticates automatically** by using your host machine's existing `gh` CLI credentials.
- **Persists its configuration**, so it remembers which folders you've trusted across sessions.
- **Stays up-to-date** by automatically pulling the latest image version on every run.

## ✅ Prerequisites

Before you start, make sure you have the following installed and configured on your machine:
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine on Linux).
- The [GitHub CLI (`gh`)](https://cli.github.com/).
- You must be logged in to the GitHub CLI. You can check by running `gh auth status`.

## 🛠️ Setup Instructions

This is the quickest way to get up and running. You just need to add a single function to your shell's configuration file.

> ⚠️ **Important Security Note:** This function uses the `--allow-all-tools` flag, which allows Copilot to execute commands on your machine without asking for confirmation first. For example, if a prompt was misunderstood, it could potentially run a dangerous command like `rm`.
>
> **However, this is precisely why this containerized approach is so powerful!** Because Copilot is running inside a secure, isolated container, the "blast radius" of a bad command is limited. It can only affect files inside the container and the single project directory you've shared with it.
>
> While this doesn't eliminate all risk, it dramatically lowers it, making this a worthy option for those who value both convenience and a strong layer of security.

1.  **Add the function to your shell profile.**
    Open your shell's startup file (e.g., `~/.zshrc`, `~/.bashrc`, or `~/.config/fish/config.fish`) and add the following code:

    ```bash
    copilot_here() {
      # Define the image name for easy reference
      local image_name="ghcr.io/gordonbeeming/copilot_here:latest"

      # Pull the latest version of the image to stay up-to-date.
      # The output is suppressed for a cleaner experience.
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
        # No arguments provided, start interactive mode.
        docker run "${docker_args[@]}" copilot --allow-all-tools
      else
        # Arguments provided, run in non-interactive mode.
        docker run "${docker_args[@]}" copilot -p "$*" --allow-all-tools
      fi
    }
    ```

2.  **Reload your shell.**
    For the new function to be available, either restart your terminal or reload the configuration file (e.g., `source ~/.zshrc`).

## Usage

Once set up, using it is simple.

### 1. Interactive Mode
For a full, interactive chat session, run the command with no arguments:
```bash
copilot_here
````

### 2\. Non-Interactive Mode

Pass a prompt directly to get a response. Your entire query should be a single prompt string.

```bash
# Get a suggestion for a command
copilot_here "suggest a git command to view the last 5 commits"

# Explain a specific file
copilot_here "explain the code in ./my-script.js"

# Ask it to generate code
copilot_here "write a C# function that takes a string and returns it in reverse"
```

## 📜 License

This project is licensed under the MIT License.
