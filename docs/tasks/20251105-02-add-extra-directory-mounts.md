# Task: Add Extra Directory Mount Support

**Date:** 2025-11-05  
**Type:** Feature Enhancement  
**Version:** 2025-11-05.1

## Problem/Objective

Users want to mount additional directories beyond just the current working directory. Use cases include:
- Mounting an investigations folder for saving analysis
- Accessing reference materials in other locations
- Working with data stored in specific folders
- Supporting both relative (`../bleh`) and absolute (`/bleh`) paths

Currently, copilot_here only mounts the current directory, limiting access to files elsewhere on the system.

## Solution Approach

Implement a flexible mounting system with three levels of configuration:
1. **Global config:** `~/.config/copilot_here/mounts.conf` - User-wide default mounts
2. **Local config:** `.copilot_here/mounts.conf` - Project-specific mounts
3. **CLI flags:** `--mount` and `--mount-rw` - Per-session mounts

Priority: CLI > Local > Global (most defined wins)

## Design Decisions

### 1. Flag Syntax
**Decision:** Repeatable flag approach
```bash
--mount <path>       # Read-only (default)
--mount-rw <path>    # Read-write
```

**Rationale:** Clean, intuitive, follows common CLI patterns like Docker's `-v`

### 2. Mount Location in Container
**Decision:** Same path as host
- `~/investigations` on host → `~/investigations` in container

**Rationale:** Consistency with decision to map current dir to actual path (not `/work`)

### 3. Read-Only vs Read-Write
**Decision:** Default to read-only, explicit flag for read-write
- `--mount` → read-only
- `--mount-rw` → read-write

**Rationale:** Security-first approach, require explicit permission for write access

### 4. Copilot Path Permissions
**Decision:** Auto-add with warning
- Automatically add mounted paths to Copilot's allowed paths
- Display what was added at startup

**Rationale:** Convenience while maintaining visibility of permissions

### 5. Security Warnings
**Decision:** Warn for sensitive paths
- Warn when mounting `/`, `/etc`, `~/.ssh`, `/root`
- Do not block, just warn

**Rationale:** Inform users of risks without preventing valid use cases

### 6. Path Persistence
**Decision:** Multi-level config system
- Global config: `~/.config/copilot_here/mounts.conf`
- Local config: `.copilot_here/mounts.conf`
- CLI flags for per-session mounts
- All three can be used together (merged with priority)

**Rationale:** Flexibility for both persistent preferences and ad-hoc needs

### 7. Config File Format
**Decision:** One path per line with optional `:ro` or `:rw` suffix
```
~/investigations:ro
~/notes:rw
/data/research
```

**Rationale:** Simple, readable, extensible with permissions

### 8. Mount Management Commands
**Decision:** Standalone commands (no copilot launch)
```bash
--save-mount <path>         # Save to local config (default)
--save-mount-global <path>  # Save to global config
--remove-mount <path>       # Remove from config
--list-mounts               # Show all configured mounts
```

**Rationale:** Convenience for managing persistent mounts

### 9. List Mounts Output Format
**Decision:** Emoji indicators with fallback
```
📂 Saved mounts:
  🌍 ~/investigations (ro)
  📍 ~/notes (rw)
```
- 🌍 = global, 📍 = local
- Fallback to `G:` and `L:` if terminal doesn't support emojis

**Rationale:** Visual clarity with accessibility fallback

### 10. Startup Mount Display
**Decision:** Table-style compact format
```
📂 Mounts:
   📁 /current/work/dir
   🌍 ~/investigations (ro)
   📍 ~/notes (rw)
   🔧 ~/data (ro)
```
- 🔧 = CLI flag

**Rationale:** Clear, scannable, shows source and permissions

### 11. Path Validation
**Decision:** Warning mode
- Warn if path doesn't exist but continue
- Let Docker create directories if needed

**Rationale:** Don't block valid use cases (future directories, etc.)

### 12. Config File Location
**Decision:** `~/.config/copilot_here/mounts.conf`
- Follows XDG Base Directory Specification
- Alongside existing copilot config at `~/.config/copilot-cli-docker/`

**Rationale:** Standard Linux/Unix conventions for configuration files

## Implementation Plan

### Phase 1: Core Mounting
- [ ] Add `--mount` and `--mount-rw` flag parsing
- [ ] Implement path resolution (relative, absolute, tilde expansion)
- [ ] Add Docker volume arguments for extra mounts
- [ ] Update both Bash/Zsh and PowerShell scripts

### Phase 2: Config System
- [ ] Create config file loader for global config
- [ ] Create config file loader for local config
- [ ] Implement priority merging (CLI > Local > Global)
- [ ] Handle `:ro` and `:rw` suffixes in config files

### Phase 3: Management Commands
- [ ] Add `--save-mount` command (default to local)
- [ ] Add `--save-mount-global` command
- [ ] Add `--remove-mount` command
- [ ] Add `--list-mounts` command with emoji detection

### Phase 4: Display & UX
- [ ] Add startup mount display with emojis/icons
- [ ] Add security warnings for sensitive paths
- [ ] Update `--help` with all new options and examples
- [ ] Add auto-add mounted paths to Copilot's `--add-dir`

### Phase 5: Documentation
- [ ] Update README with mount feature documentation
- [ ] Add examples for common use cases
- [ ] Document config file format and locations
- [ ] Update version to `2025-11-05.1`
- [ ] Sync standalone scripts from README

## Files to Modify

1. **README.md**
   - Update Bash/Zsh script section
   - Update PowerShell script section
   - Add mount feature documentation
   - Update help text examples
   - Update version references

2. **copilot_here.sh**
   - Regenerate from README after changes

3. **copilot_here.ps1**
   - Regenerate from README after changes

4. **New Files**
   - This task documentation

## Testing Checklist

- [ ] Test `--mount` with relative path (`../investigations`)
- [ ] Test `--mount` with absolute path (`/data/research`)
- [ ] Test `--mount` with tilde path (`~/notes`)
- [ ] Test `--mount-rw` for read-write access
- [ ] Test multiple mounts in one command
- [ ] Test global config loading
- [ ] Test local config loading
- [ ] Test CLI overriding configs
- [ ] Test `--save-mount` command
- [ ] Test `--save-mount-global` command
- [ ] Test `--remove-mount` command
- [ ] Test `--list-mounts` command
- [ ] Test emoji detection fallback
- [ ] Test security warnings for sensitive paths
- [ ] Test with .NET image variant
- [ ] Test with Playwright image variant
- [ ] Test path validation warnings
- [ ] Verify mounted paths appear in startup display
- [ ] Verify paths auto-added to Copilot's allowed dirs

## Examples

### Basic Usage
```bash
# Mount read-only directory
copilot_here --mount ../investigations

# Mount read-write directory
copilot_here --mount-rw ~/notes

# Multiple mounts
copilot_here --mount ../data --mount-rw ~/output

# With other options
copilot_here -d --mount ~/research -p "analyze this"
```

### Save for Reuse
```bash
# Save to local project config
copilot_here --save-mount ~/investigations

# Save to global config
copilot_here --save-mount-global ~/common-data

# List all saved mounts
copilot_here --list-mounts

# Remove a mount
copilot_here --remove-mount ~/investigations
```

### Config Files
**Global:** `~/.config/copilot_here/mounts.conf`
```
~/investigations:ro
~/common-notes:rw
```

**Local:** `.copilot_here/mounts.conf`
```
../shared-data:ro
./output:rw
```

## Follow-up Items

- Consider adding mount aliases (e.g., `inv` → `~/investigations`)
- Consider adding `--mount-all` to mount common directories
- Monitor user feedback on default read-only behavior

## Notes

- All mounts from configs are automatically loaded on every run
- CLI flags merge with (don't replace) config mounts
- Path must be accessible by Docker (not inside encrypted volumes, etc.)
- Windows paths will need special handling in PowerShell version
