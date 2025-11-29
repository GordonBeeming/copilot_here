# Emoji Legend

This document provides a reference for the emojis used throughout the copilot_here CLI output.

## Configuration Sources

| Emoji | Meaning | Description |
|-------|---------|-------------|
| 🏭 | Application Default | Built-in default values shipped with the application |
| 🌍 | Global Config | User-wide configuration from `~/.config/copilot_here/` |
| 📍 | Local Config | Project-specific configuration from `.copilot_here/` |
| 🔧 | CLI Argument | Value provided via command-line argument |

## Priority Order

Configuration values are applied in the following order (later overrides earlier):

1. 🏭 Application Default
2. 🌍 Global Config
3. 📍 Local Config
4. 🔧 CLI Argument

## Other Emojis

| Emoji | Meaning | Description |
|-------|---------|-------------|
| 🖼️ | Image | Docker image configuration |
| 📁 | Mount | Volume mount configuration |
| ✅ | Success | Operation completed successfully |
| ❌ | Error | Operation failed |
| ⚠️ | Warning | Non-critical issue or important notice |
| 📋 | List | Displaying a list of items |
