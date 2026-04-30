# Package Manager Distribution Setup

This document describes the external setup steps required to enable automated package manager distribution.

## 1. NuGet (.NET Tool - Issue #50)

The CI workflow publishes the CLI as a .NET global tool to nuget.org.

### Setup steps

- [ ] Create/verify a nuget.org account at https://www.nuget.org/
- [ ] Generate an API key at https://www.nuget.org/account/apikeys
  - Scope: **Push new packages and package versions**
  - Glob pattern: `copilot_here`
- [ ] Add GitHub Actions secret on the `copilot_here` repo:
  - Go to **Settings > Secrets and variables > Actions > New repository secret**
  - Name: `NUGET_API_KEY`
  - Value: the API key from nuget.org

### Usage

```bash
dotnet tool install -g copilot_here
copilot_here --version
```

---

## 2. Homebrew Tap (Issue #51)

The CI workflow updates a Homebrew formula in a separate tap repository after each release.

### Setup steps

- [ ] Create a public repository: [`GordonBeeming/homebrew-tap`](https://github.com/GordonBeeming/homebrew-tap)
- [ ] Copy the formula template from `packaging/homebrew/Formula/copilot_here.rb` to the new repo at `Formula/copilot_here.rb`
- [ ] Generate an SSH deploy key pair:
  ```bash
  ssh-keygen -t ed25519 -C "homebrew-tap-deploy" -f homebrew_tap_key -N ""
  ```
- [ ] Add the **public** key (`homebrew_tap_key.pub`) as a deploy key on the `homebrew-tap` repo:
  - Go to `homebrew-tap` repo **Settings > Deploy keys > Add deploy key**
  - Title: `copilot_here CI`
  - Check **Allow write access**
- [ ] Add the **private** key (`homebrew_tap_key`) as a GitHub Actions secret on the `copilot_here` repo:
  - Name: `HOMEBREW_TAP_DEPLOY_KEY`
  - Value: the full contents of the private key file

### Usage

```bash
brew tap gordonbeeming/tap
# macOS
brew install --cask copilot-here
# Linux
brew install copilot_here
copilot_here --version
```

---

## 3. WinGet (Issue #52)

The CI workflow auto-submits manifest updates to the `microsoft/winget-pkgs` repository using `wingetcreate`.

### Setup steps

- [ ] Generate a Personal Access Token (classic) at https://github.com/settings/tokens
  - Scopes: `repo` **and** `workflow`
  - `repo` is needed to PATCH refs on the fork during the pre-submit fork sync. `workflow` is needed because upstream `microsoft/winget-pkgs` regularly pushes commits that touch workflow files, and the merge-upstream API refuses to fast-forward through them without it.
- [ ] Add GitHub Actions secret on the `copilot_here` repo:
  - Name: `WINGET_PAT`
  - Value: the PAT
- [ ] **First submission (manual):** The `wingetcreate new` command cannot run non-interactively, so the initial manifest must be submitted manually:
  1. Download `wingetcreate.exe` from https://github.com/microsoft/winget-create/releases
  2. Run `wingetcreate.exe new <installer-url-x64> <installer-url-arm64>` and follow the prompts
  3. This creates a PR to `microsoft/winget-pkgs` — initial review by Microsoft takes 1-3 days
  4. Once the first manifest is merged, the CI `wingetcreate update` step will handle all future versions automatically

> Local manifests under `packaging/winget/` are gitignored — `wingetcreate update` reads from `microsoft/winget-pkgs` upstream after the first submission, so tracking local copies just causes merge conflicts. Drop scratch yaml files there during a manual `wingetcreate new` run if you ever need to bootstrap a new package id.

### Usage

```powershell
winget install GordonBeeming.CopilotHere
copilot_here --version
```

---

## Summary of required GitHub secrets

| Secret Name | Purpose | Where to get it |
|---|---|---|
| `NUGET_API_KEY` | Push .NET tool to nuget.org | https://www.nuget.org/account/apikeys |
| `HOMEBREW_TAP_DEPLOY_KEY` | Update Homebrew formula | SSH deploy key (write access) on `homebrew-tap` repo |
| `WINGET_PAT` | Submit WinGet manifest PRs | https://github.com/settings/tokens (scopes: `repo` + `workflow`) |
