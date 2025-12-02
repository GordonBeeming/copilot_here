# Migration to Native Binary

This document tracks the migration from shell scripts (`.sh`/`.ps1`) to a .NET Native AOT binary for the `copilot_here` CLI tool.

## Migration Status

### Core Execution Features

| Feature | Status | Notes |
|---------|--------|-------|
| Docker container execution | ✅ Done | Full `docker run` with all args |
| Image selection (latest, dotnet, playwright, etc.) | ✅ Done | All variants supported |
| YOLO mode (`--allow-all-tools --allow-all-paths`) | ✅ Done | Passed via `--yolo` flag |
| Safe mode (default) | ✅ Done | Default behavior |
| Mount current directory | ✅ Done | |
| Container work directory mapping | ✅ Done | Maps ~/... to /home/appuser/... |
| GitHub token injection | ✅ Done | Via `gh auth token` |
| User/Group ID mapping (PUID/PGID) | ✅ Done | Uses `id -u` and `id -g` |
| Terminal title setting | ✅ Done | With emoji for mode indicator |
| Interactive mode with banner | ✅ Done | Auto-adds `--banner` when no args |

### Image Management

| Feature | Status | Notes |
|---------|--------|-------|
| `--dotnet` / `-d` | ✅ Done | |
| `--dotnet8` / `-d8` | ✅ Done | |
| `--dotnet9` / `-d9` | ✅ Done | |
| `--dotnet10` / `-d10` | ✅ Done | |
| `--playwright` / `-pw` | ✅ Done | |
| `--dotnet-playwright` / `-dp` | ✅ Done | |
| `--rust` / `-rs` | ✅ Done | |
| `--dotnet-rust` / `-dr` | ✅ Done | |
| `--list-images` | ✅ Done | Lists all available tags |
| `--show-image` | ✅ Done | Shows active/local/global config |
| `--set-image <tag>` | ✅ Done | Local config |
| `--set-image-global <tag>` | ✅ Done | Global config |
| `--clear-image` | ✅ Done | |
| `--clear-image-global` | ✅ Done | |
| Default image from config | ✅ Done | Priority: local > global > latest |
| Image cleanup (7+ days old) | ✅ Done | Skips currently used image |
| Image pull with spinner | ✅ Done | |

### Mount Management

| Feature | Status | Notes |
|---------|--------|-------|
| `--mount <path>` (read-only) | ✅ Done | |
| `--mount-rw <path>` (read-write) | ✅ Done | |
| `--list-mounts` | ✅ Done | Shows global/local mounts |
| `--save-mount <path>` | ✅ Done | Local config |
| `--save-mount-global <path>` | ✅ Done | Global config |
| `--remove-mount <path>` | ✅ Done | Removes from both configs |
| Load mounts from local config | ✅ Done | `.copilot_here/mounts.conf` |
| Load mounts from global config | ✅ Done | `~/.config/copilot_here/mounts.conf` |
| Symlink following for configs | ✅ Done | Uses `FileInfo.LinkTarget` |
| Path normalization (tilde, relative) | ✅ Done | Tilde expansion, relative paths |
| Sensitive path warnings | ✅ Done | `/`, `/etc`, `/root`, `~/.ssh` - prompts for confirmation |
| Mount priority (CLI > local > global) | ✅ Done | Runtime merge logic |
| Mount display with icons | ✅ Done | 📁, 🌍, 📍, 🔧 |

### Airlock (Network Proxy)

| Feature | Status | Notes |
|---------|--------|-------|
| `--enable-airlock` | ✅ Done | Local config |
| `--enable-global-airlock` | ✅ Done | Global config |
| `--disable-airlock` | ✅ Done | |
| `--disable-global-airlock` | ✅ Done | |
| `--show-airlock-rules` | ✅ Done | Shows enabled status and rules content |
| `--edit-airlock-rules` | ✅ Done | Opens in $EDITOR |
| `--edit-global-airlock-rules` | ✅ Done | Opens in $EDITOR |
| Docker Compose generation | ✅ Done | Generate from template |
| Proxy container management | ✅ Done | Start proxy, run app, cleanup |
| Network config placeholder replacement | ✅ Done | `{{GITHUB_OWNER}}`, `{{GITHUB_REPO}}` |
| Orphaned network cleanup | ✅ Done | Find and remove stale networks/containers |
| Logs directory setup | ✅ Done | Create `.copilot_here/logs` with gitignore |
| Monitor vs enforce mode | ✅ Done | Reads from config |
| Session ID generation | ✅ Done | SHA256 hash of PID+timestamp |
| Template download | ✅ Done | Download compose template if missing |

### Security

| Feature | Status | Notes |
|---------|--------|-------|
| Token scope validation | ✅ Done | Require `copilot`, `read:packages` |
| Privileged scope warning | ✅ Done | Warn+confirm on `admin:*`, `write:*`, `manage_*`, `delete_*` |
| Test mode bypass | ✅ Done | `COPILOT_HERE_TEST_MODE` env var |

### CLI Infrastructure

| Feature | Status | Notes |
|---------|--------|-------|
| `-h` / `--help` | ✅ Done | System.CommandLine auto-generated |
| `--help2` | ✅ Done | Shows native copilot --help |
| `--no-cleanup` | ✅ Done | |
| `--no-pull` / `--skip-pull` | ✅ Done | |
| `--update` / `-u` | ✅ Done | Checks GitHub releases for updates |
| Version check and update prompt | ✅ Done | Shows download instructions |
| Passthrough args to copilot | ✅ Done | `-p`, `--model`, `--continue`, `--resume`, `--` |
| Emoji support detection | ✅ Done | |

### Self-Update

| Feature | Status | Notes |
|---------|--------|-------|
| Check for updates on GitHub | ✅ Done | Uses releases API |
| Download instructions | ✅ Done | Platform-specific curl/PowerShell commands |
| Version comparison | ✅ Done | Semver comparison |
| Runtime identifier detection | ✅ Done | Auto-detects OS and architecture |

### GitHub Integration

| Feature | Status | Notes |
|---------|--------|-------|
| Get owner/repo from git remote | ✅ Done | For placeholder replacement |
| Parse SSH and HTTPS remote URLs | ✅ Done | Handles all GitHub URL formats |

## Config File Locations

| Config | Path | Purpose |
|--------|------|---------|
| Local mounts | `.copilot_here/mounts.conf` | Project-specific mounts |
| Global mounts | `~/.config/copilot_here/mounts.conf` | User-wide mounts |
| Local image | `.copilot_here/image.conf` | Project-specific default image |
| Global image | `~/.config/copilot_here/image.conf` | User-wide default image |
| Local airlock | `.copilot_here/airlock.enabled` | Project-specific airlock flag |
| Global airlock | `~/.config/copilot_here/airlock.enabled` | User-wide airlock flag |
| Local network rules | `.copilot_here/network.json` | Project-specific airlock rules |
| Global network rules | `~/.config/copilot_here/network.json` | User-wide airlock rules |
| Default airlock rules | `~/.config/copilot_here/default-airlock-rules.json` | Base rules |
| Compose template | `~/.config/copilot_here/docker-compose.airlock.yml.template` | Docker compose template |
| Copilot config | `~/.config/copilot-cli-docker` | Copilot CLI persistence |

## Architecture Notes

### AOT Compatibility Requirements

1. **No reflection-based serialization** - Use source generators for JSON
2. **No dynamic code generation** - All types must be known at compile time
3. **Avoid `System.Text.Json` without source generators** - Use `[JsonSerializable]` attributes
4. **No `dynamic` keyword usage**
5. **Prefer struct over class for small data types** - Reduces heap allocations
6. **Use spans and stackalloc where possible** - Avoid allocations in hot paths

### Binary Size Optimization

Current project settings:
- `TrimMode=full` - Aggressive dead code elimination
- `InvariantGlobalization=true` - No ICU data
- `StackTraceSupport=false` - Smaller binaries
- `OptimizationPreference=Size` - Prefer size over speed

### Dependencies

- `System.CommandLine` (2.0.0) - AOT-compatible argument parsing

## Testing Requirements

All features must have corresponding tests:
- Unit tests for config parsing
- Unit tests for path resolution
- Integration tests for Docker command generation
- Integration tests for config file reading/writing

## Migration Priority

1. **High Priority** (Core functionality) - ✅ Complete
   - Security checks (token validation)
   - All image variants
   - Mount config loading
   - Help text

2. **Medium Priority** (User experience) - ✅ Complete
   - Terminal title
   - Progress spinners
   - Emoji detection
   - Update checking

3. **Low Priority** (Advanced features) - ✅ Complete
   - Airlock proxy mode (Docker Compose)
   - Self-update mechanism

## Distribution Strategy

### Binary Distribution

Native AOT binaries are built for multiple platforms:

| Platform | Runtime Identifier | Binary Name |
|----------|-------------------|-------------|
| Linux x64 | `linux-x64` | `copilot_here` |
| Linux ARM64 | `linux-arm64` | `copilot_here` |
| macOS x64 | `osx-x64` | `copilot_here` |
| macOS ARM64 | `osx-arm64` | `copilot_here` |
| Windows x64 | `win-x64` | `copilot_here.exe` |
| Windows ARM64 | `win-arm64` | `copilot_here.exe` |

### Installation Flow

1. **Initial Installation** (via shell script download):
   ```bash
   # Linux/macOS
   curl -fsSL https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.sh -o ~/.copilot_here.sh
   source ~/.copilot_here.sh
   
   # PowerShell
   irm https://raw.githubusercontent.com/GordonBeeming/copilot_here/main/copilot_here.ps1 | iex
   ```

2. **First Run** - Shell wrapper:
   - Checks for binary at `~/.local/bin/copilot_here[.exe]`
   - If missing, downloads from GitHub releases
   - Passes all arguments to binary
   - Adds `--yolo` flag when called as `copilot_yolo`

3. **Updates** - Via the binary itself:
   - `copilot_here --update` checks GitHub releases
   - Downloads new binary if available
   - Replaces existing binary

### Shell Wrapper Responsibilities

The thin shell wrappers (`copilot_here.sh` / `copilot_here.ps1`) handle:
- Binary location and download
- Mode detection (`copilot_here` vs `copilot_yolo`)
- Platform/architecture detection for correct binary
- First-time setup messaging

### GitHub Actions Workflow

The `publish.yml` workflow builds:
1. Docker images (existing functionality)
2. Native AOT binaries for all platforms (new)
   - Published as release artifacts
   - Tagged with commit SHA

### Version Management

- Binary version comes from `CopilotHere.csproj` `<Version>` property
- Shell wrapper version in header comment for backward compatibility
- Binary self-update checks GitHub releases API
