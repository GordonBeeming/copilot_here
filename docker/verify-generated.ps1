#!/usr/bin/env pwsh
# Verifies that generated Dockerfiles are up to date with images.json and snippets.
# Usage: pwsh docker/verify-generated.ps1
# Exit code 0 = up to date, 1 = stale (needs regeneration)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$generatorPath = Join-Path $scriptDir 'generate-dockerfiles.ps1'

Write-Host "Regenerating Dockerfiles to check for differences..."
& $generatorPath

# Use repo-relative paths for git commands (absolute paths can fail with "outside repository")
$repoRoot = git rev-parse --show-toplevel 2>&1
$generatedRelative = [System.IO.Path]::GetRelativePath($repoRoot, (Join-Path $scriptDir 'generated'))

Push-Location $repoRoot
try {
    $diff = git diff --exit-code $generatedRelative 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Generated Dockerfiles are out of date. Run 'pwsh docker/generate-dockerfiles.ps1' and commit the results."
        Write-Host $diff
        exit 1
    }

    # Check for untracked files in generated/
    $untracked = git ls-files --others --exclude-standard $generatedRelative
    if ($untracked) {
        Write-Error "Untracked generated Dockerfiles found. Run 'pwsh docker/generate-dockerfiles.ps1' and commit the results."
        Write-Host $untracked
        exit 1
    }

    # Check for tracked files that should have been deleted (stale Dockerfiles)
    $trackedFiles = git ls-files $generatedRelative | ForEach-Object { Split-Path $_ -Leaf }
    $generatedFiles = Get-ChildItem -Path (Join-Path $scriptDir 'generated') -Filter 'Dockerfile.*' -Name
    $staleFiles = $trackedFiles | Where-Object { $_ -notin $generatedFiles }
    if ($staleFiles) {
        Write-Error "Stale generated Dockerfiles found that are no longer in images.json: $($staleFiles -join ', '). Run 'pwsh docker/generate-dockerfiles.ps1' and commit the results."
        exit 1
    }
} finally {
    Pop-Location
}

Write-Host "Generated Dockerfiles are up to date."
