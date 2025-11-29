# Migration to Native Binary

This document tracks the migration from shell scripts (`.sh`/`.ps1`) to a .NET Native AOT binary for the `copilot_here` CLI tool.

## Migration Status

### Core Execution Features

| Feature | Status | Notes |
|---------|--------|-------|
| Docker container execution | ⬜ Partial | Basic `docker run` implemented |
| Image selection (latest, dotnet, playwright, etc.) | ⬜ Partial | Only `-d/--dotnet` implemented |
| YOLO mode (`--allow-all-tools --allow-all-paths`) | ⬜ TODO | |
| Safe mode (default) | ⬜ TODO | |
| Mount current directory | ✅ Done | |
| Container work directory mapping | ✅ Done | |
| GitHub token injection | ✅ Done | Via `gh auth token` |
| User/Group ID mapping (PUID/PGID) | ⬜ Partial | Hardcoded to 1000 |
| Terminal title setting | ⬜ TODO | |
| Interactive mode with banner | ⬜ TODO | |

### Image Management

| Feature | Status | Notes |
|---------|--------|-------|
| `--dotnet` / `-d` | ✅ Done | |
| `--dotnet8` / `-d8` | ⬜ TODO | |
| `--dotnet9` / `-d9` | ⬜ TODO | |
| `--dotnet10` / `-d10` | ⬜ TODO | |
| `--playwright` / `-pw` | ⬜ TODO | |
| `--dotnet-playwright` / `-dp` | ⬜ TODO | |
| `--list-images` | ⬜ TODO | |
| `--show-image` | ⬜ TODO | |
| `--set-image <tag>` | ⬜ TODO | Local config |
| `--set-image-global <tag>` | ⬜ TODO | Global config |
| `--clear-image` | ⬜ TODO | |
| `--clear-image-global` | ⬜ TODO | |
| Default image from config | ⬜ TODO | Priority: local > global > latest |
| Image cleanup (7+ days old) | ⬜ TODO | |
| Image pull with spinner | ⬜ TODO | |

### Mount Management

| Feature | Status | Notes |
|---------|--------|-------|
| `--mount <path>` (read-only) | ✅ Done | |
| `--mount-rw <path>` (read-write) | ✅ Done | |
| `--list-mounts` | ⬜ TODO | |
| `--save-mount <path>` | ⬜ TODO | Local config |
| `--save-mount-global <path>` | ⬜ TODO | Global config |
| `--remove-mount <path>` | ⬜ TODO | |
| Load mounts from local config | ⬜ TODO | `.copilot_here/mounts.conf` |
| Load mounts from global config | ⬜ TODO | `~/.config/copilot_here/mounts.conf` |
| Symlink following for configs | ⬜ TODO | |
| Path normalization (tilde, relative) | ⬜ Partial | Basic tilde expansion |
| Sensitive path warnings | ⬜ TODO | `/etc`, `~/.ssh`, etc. |
| Mount priority (CLI > local > global) | ⬜ TODO | |
| Mount display with icons | ⬜ TODO | 📁, 🌍, 📍, 🔧 |

### Airlock (Network Proxy)

| Feature | Status | Notes |
|---------|--------|-------|
| `--enable-airlock` | ⬜ TODO | Local config |
| `--enable-global-airlock` | ⬜ TODO | Global config |
| `--disable-airlock` | ⬜ TODO | |
| `--disable-global-airlock` | ⬜ TODO | |
| `--show-airlock-rules` | ⬜ TODO | |
| `--edit-airlock-rules` | ⬜ TODO | |
| `--edit-global-airlock-rules` | ⬜ TODO | |
| Docker Compose generation | ⬜ Partial | `AirlockComposer.cs` exists |
| Proxy container management | ⬜ TODO | |
| Network config placeholder replacement | ⬜ TODO | `{{GITHUB_OWNER}}`, `{{GITHUB_REPO}}` |
| Orphaned network cleanup | ⬜ TODO | |
| Logs directory setup | ⬜ TODO | |
| Monitor vs enforce mode | ⬜ TODO | |

### Security

| Feature | Status | Notes |
|---------|--------|-------|
| Token scope validation | ⬜ TODO | Require `copilot`, `read:packages` |
| Privileged scope warning | ⬜ TODO | Warn on `admin:*`, `write:*`, etc. |
| Test mode bypass | ⬜ TODO | `COPILOT_HERE_TEST_MODE` |

### CLI Infrastructure

| Feature | Status | Notes |
|---------|--------|-------|
| `-h` / `--help` | ⬜ TODO | |
| `--help2` | ⬜ TODO | Show native copilot help |
| `--no-cleanup` | ✅ Done | |
| `--no-pull` / `--skip-pull` | ✅ Done | |
| `--update-scripts` / `--upgrade-scripts` | ⬜ TODO | Self-update mechanism |
| Version check and update prompt | ⬜ TODO | |
| Passthrough args to copilot | ✅ Done | |
| Emoji support detection | ⬜ TODO | |

### Self-Update

| Feature | Status | Notes |
|---------|--------|-------|
| Check for updates on GitHub | ⬜ TODO | |
| Download and replace binary | ⬜ TODO | |
| Version comparison | ⬜ TODO | |
| Backup before update | ⬜ TODO | |

### GitHub Integration

| Feature | Status | Notes |
|---------|--------|-------|
| Get owner/repo from git remote | ⬜ TODO | For placeholder replacement |
| Parse SSH and HTTPS remote URLs | ⬜ TODO | |

## Config File Locations

| Config | Path | Purpose |
|--------|------|---------|
| Local mounts | `.copilot_here/mounts.conf` | Project-specific mounts |
| Global mounts | `~/.config/copilot_here/mounts.conf` | User-wide mounts |
| Local image | `.copilot_here/image.conf` | Project-specific default image |
| Global image | `~/.config/copilot_here/image.conf` | User-wide default image |
| Local network | `.copilot_here/network.json` | Project-specific airlock config |
| Global network | `~/.config/copilot_here/network.json` | User-wide airlock config |
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

1. **High Priority** (Core functionality)
   - Security checks (token validation)
   - All image variants
   - Mount config loading
   - Help text

2. **Medium Priority** (User experience)
   - Terminal title
   - Progress spinners
   - Emoji detection
   - Update checking

3. **Low Priority** (Advanced features)
   - Airlock proxy mode
   - Self-update mechanism
