# Task: Local Docker Image Support in CLI Runtime

**Date:** 2026-04-29  
**Version:** 2026.04.29

## Objective

Support local Docker image references (for example `my-local-image:dev`) without forcing `ghcr.io/...` rewriting.

## Solution Approach

Follow existing image-resolution patterns, but broaden explicit-image detection and make Airlock reuse the same resolved image path used by standard runs.

## Changes Made

### Runtime image resolution

- [x] Updated `app/Infrastructure/ContainerRunner.cs`
  - Expanded `IsAbsoluteImageReference` to treat `/`, `:`, and `@` forms as explicit image references.
  - Added `ImageExists(...)` helper for local image presence checks.

- [x] Updated `app/Commands/Run/RunCommand.cs`
  - Airlock now receives the already-resolved `imageName` and `noPull` value, keeping resolution consistent with standard mode.

- [x] Updated `app/Infrastructure/AirlockRunner.cs`
  - `Run(...)` now accepts resolved `imageName` and `noPull`.
  - In `--no-pull` mode, Airlock validates both app/proxy images are present locally before starting.
  - `GenerateComposeFile(...)` now receives `imageTag` directly and no longer derives it via string splitting from full image name.

### Tests

- [x] Updated `tests/CopilotHere.UnitTests/ContainerRunnerImageTests.cs`
  - Added coverage for local tagged images without `/` and digest-style refs.
  - Added `GetImageName` assertion for `my-local-image:dev`.

- [x] Updated `tests/CopilotHere.UnitTests/GitHubCopilotToolTests.cs`
  - Added local tagged image passthrough assertion.

- [x] Updated `tests/CopilotHere.UnitTests/EchoToolTests.cs`
  - Added local tagged image passthrough assertion.

- [x] Updated call sites for `GenerateComposeFile(...)` signature changes:
  - `tests/CopilotHere.UnitTests/AirlockComposeDindTests.cs`
  - `tests/CopilotHere.IntegrationTests/AirlockSmokeTests.cs`

### Version updates

- [x] Bumped version to `2026.04.29` across stamped files via `scripts/bump-version.sh`.

## Testing Performed

- [x] `docker run --rm -v "$PWD":/work -w /work mcr.microsoft.com/dotnet/sdk:10.0 dotnet build`
- [x] `docker run --rm -v "$PWD":/work -w /work mcr.microsoft.com/dotnet/sdk:10.0 dotnet test --project tests/CopilotHere.UnitTests/CopilotHere.UnitTests.csproj`
