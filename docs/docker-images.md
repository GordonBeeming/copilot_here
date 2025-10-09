# Docker Image Variants

This repository now publishes three Docker image variants:

## Base Image: `latest`
**Tag:** `ghcr.io/gordonbeeming/copilot_here:latest`

The standard copilot_here image with:
- Node.js 20
- GitHub Copilot CLI
- Git, curl, gpg, gosu

**Usage:**
```bash
copilot_here() {
  local image_name="ghcr.io/gordonbeeming/copilot_here:latest"
  # ... rest of function
}
```

## Playwright Image: `playwright`
**Tag:** `ghcr.io/gordonbeeming/copilot_here:playwright`

Extends the base image with:
- Everything from the base image
- Playwright (latest version)
- Chromium browser with all dependencies
- System libraries for browser automation

**Use Case:** Web testing, browser automation, checking published web content

**Usage:**
```bash
copilot_playwright() {
  local image_name="ghcr.io/gordonbeeming/copilot_here:playwright"
  # ... rest of function
}
```

## .NET Image: `dotnet`
**Tag:** `ghcr.io/gordonbeeming/copilot_here:dotnet`

Extends the Playwright image with:
- Everything from the Playwright image
- .NET 8 SDK
- .NET 9 SDK

**Use Case:** .NET development, building and testing .NET applications with web testing capabilities

**Usage:**
```bash
copilot_dotnet() {
  local image_name="ghcr.io/gordonbeeming/copilot_here:dotnet"
  # ... rest of function
}
```

## Version Tags

Each image variant is also tagged with the commit SHA for reproducibility:
- `ghcr.io/gordonbeeming/copilot_here:sha-<commit>`
- `ghcr.io/gordonbeeming/copilot_here:playwright-sha-<commit>`
- `ghcr.io/gordonbeeming/copilot_here:dotnet-sha-<commit>`

## Build Dependency Chain

```
Base Image (Dockerfile)
    ↓
Playwright Image (Dockerfile.playwright) 
    ↓
.NET Image (Dockerfile.dotnet)
```

Each image in the chain uses the commit-specific tag from the same workflow run to ensure consistency.
