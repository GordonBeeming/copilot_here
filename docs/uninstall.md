# Uninstalling copilot_here

Most people want the one-liner in the [README](../README.md#-uninstalling). This page is the full reference: every file and directory the install creates, so you can remove things by hand or check that an automated uninstall got everything.

## The easy way

```bash
copilot_here --uninstall            # binary, scripts, shell integration
copilot_here --uninstall --purge    # the above + config dirs + pulled images
copilot_here --uninstall --yes      # skip the confirmation prompt
```

Or, when the binary is broken or already gone, the self-contained remote script:

```bash
# Linux/macOS
bash <(curl -fsSL https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/uninstall.sh)
```

```powershell
# Windows
& ([scriptblock]::Create((iwr -UseBasicParsing 'https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/uninstall.ps1').Content))
```

Both take `--purge` / `-Purge` and `--yes` / `-Yes`.

> **`~/.claude` is never removed.** That directory is your real Claude Code config and login, shared with Claude Code itself, and no uninstall mode touches it.

## What the install creates

### Linux/macOS

| What | Path |
|------|------|
| Native binary | `~/.local/bin/copilot_here` |
| Sourced shell script | `~/.copilot_here.sh` |
| Bash/Zsh integration | a marker block in `~/.bashrc`, `~/.bash_profile`, `~/.profile`, `~/.zshrc`, `~/.zprofile` (whichever exist) |
| Fish integration | `~/.config/fish/conf.d/copilot_here.fish` |
| Config (kept unless `--purge`) | `~/.config/copilot_here`, `~/.config/copilot-cli-docker` |
| Per-project config (kept unless `--purge`) | `.copilot_here/` inside each repo you ran it in |
| Docker images/containers (kept unless `--purge`) | containers named `copilot_here-*`, images under `ghcr.io/gordonbeeming/copilot_here` |

### Windows

| What | Path |
|------|------|
| Native binary | `%USERPROFILE%\.local\bin\copilot_here.exe` |
| Sourced script | `%USERPROFILE%\.copilot_here.ps1` |
| PowerShell integration | a marker block in `Documents\PowerShell\Microsoft.PowerShell_profile.ps1` and `Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1` |
| cmd wrappers | `%USERPROFILE%\.local\bin\copilot_here.cmd`, `copilot_yolo.cmd` |
| User PATH entry | `%USERPROFILE%\.local\bin` added to your user PATH |
| Config (kept unless `--purge`) | `%USERPROFILE%\.config\copilot_here`, `%USERPROFILE%\.config\copilot-cli-docker` |

## Manual removal checklist

If you'd rather do it yourself, or want to confirm nothing was left behind:

1. **Stop running containers** so the binary isn't in use:
   ```bash
   docker ps --filter "name=copilot_here-" -q | xargs -r docker stop
   ```
2. **Remove the shell integration block** from each profile listed above. It's fenced by these markers — delete everything from the start marker to the end marker, inclusive:
   ```bash
   # >>> copilot_here >>>
   ...
   # <<< copilot_here <<<
   ```
3. **Delete the script, binary, and wrappers:**
   ```bash
   rm -f ~/.copilot_here.sh ~/.local/bin/copilot_here
   rm -f ~/.config/fish/conf.d/copilot_here.fish
   ```
   On Windows, also remove `~/.copilot_here.ps1` and the two `.cmd` wrappers in `~/.local/bin`.
4. **(Optional) Remove config and images:**
   ```bash
   rm -rf ~/.config/copilot_here ~/.config/copilot-cli-docker
   docker images "ghcr.io/gordonbeeming/copilot_here*" -q | xargs -r docker rmi -f
   ```
5. **Restart your shell** (or open a new terminal). The `copilot_here` function stays loaded in your current session until then.

## Installed through a package manager?

The methods above are for the script/binary install. If you used a package manager, uninstall through it instead:

```bash
brew uninstall --cask copilot-here          # Homebrew (macOS)
brew uninstall copilot_here                 # Homebrew (Linux)
winget uninstall GordonBeeming.CopilotHere  # WinGet (Windows)
dotnet tool uninstall -g copilot_here       # .NET global tool
```
