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
brew install copilot_here
copilot_here --version
```

---

## 3. WinGet (Issue #52)

The CI workflow auto-submits manifest updates to the `microsoft/winget-pkgs` repository using `wingetcreate`.

### Setup steps

- [ ] Generate a Personal Access Token (classic) at https://github.com/settings/tokens
  - Scope: `public_repo` (needs to submit PRs to `microsoft/winget-pkgs`)
- [ ] Add GitHub Actions secret on the `copilot_here` repo:
  - Name: `WINGET_PAT`
  - Value: the PAT
- [ ] **First submission note:** After code changes are merged and a release is created, the CI will auto-submit the first PR to `microsoft/winget-pkgs`. This initial PR requires manual review/approval by Microsoft maintainers (typically takes 1-3 days). Subsequent version updates are auto-approved.

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
| `WINGET_PAT` | Submit WinGet manifest PRs | https://github.com/settings/tokens (scope: `public_repo`) |
