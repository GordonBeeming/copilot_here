#!/usr/bin/env pwsh
# Verifies that generated Dockerfiles are up to date with images.json and snippets.
# Usage: pwsh docker/verify-generated.ps1
# Exit code 0 = up to date, 1 = stale (needs regeneration)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$generatorPath = Join-Path $scriptDir 'generate-dockerfiles.ps1'

Write-Host "Regenerating Dockerfiles to check for differences..."
& $generatorPath

$diff = git diff --exit-code (Join-Path $scriptDir 'generated') 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Generated Dockerfiles are out of date. Run 'pwsh docker/generate-dockerfiles.ps1' and commit the results."
    Write-Host $diff
    exit 1
}

# Also check for untracked files in generated/
$untracked = git ls-files --others --exclude-standard (Join-Path $scriptDir 'generated')
if ($untracked) {
    Write-Error "Untracked generated Dockerfiles found. Run 'pwsh docker/generate-dockerfiles.ps1' and commit the results."
    Write-Host $untracked
    exit 1
}

Write-Host "Generated Dockerfiles are up to date."
