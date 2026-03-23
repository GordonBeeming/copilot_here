#!/usr/bin/env pwsh
# Generates Dockerfiles from images.json and snippet files.
# Usage: pwsh docker/generate-dockerfiles.ps1

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$configPath = Join-Path $scriptDir 'images.json'
$snippetsDir = Join-Path $scriptDir 'snippets'
$generatedDir = Join-Path $scriptDir 'generated'

if (-not (Test-Path $configPath)) {
    Write-Error "Configuration file not found: $configPath"
    exit 1
}

$config = Get-Content $configPath -Raw | ConvertFrom-Json

if (-not (Test-Path $generatedDir)) {
    New-Item -ItemType Directory -Path $generatedDir | Out-Null
}

$imageNames = $config.images.PSObject.Properties.Name
foreach ($imageName in $imageNames) {
    $image = $config.images.$imageName
    $base = $image.base
    $snippets = $image.snippets

    $lines = @()
    $lines += "# Auto-generated from docker/images.json - DO NOT EDIT MANUALLY"
    $lines += "# To modify, edit docker/snippets/*.Dockerfile or docker/images.json"
    $lines += "# then run: pwsh docker/generate-dockerfiles.ps1"
    $lines += ""
    $lines += "# Use a slim Node.js base image, which gives us ``npm``."
    $lines += "FROM $base"

    foreach ($snippet in $snippets) {
        $snippetPath = Join-Path $snippetsDir "$snippet.Dockerfile"
        if (-not (Test-Path $snippetPath)) {
            Write-Error "Snippet file not found: $snippetPath"
            exit 1
        }

        $lines += ""
        $lines += "# --- snippet: $snippet ---"
        $snippetContent = (Get-Content $snippetPath -Raw).TrimEnd()
        $lines += $snippetContent
    }

    $outputPath = Join-Path $generatedDir "Dockerfile.$imageName"
    $content = ($lines -join "`n") + "`n"
    Set-Content -Path $outputPath -Value $content -NoNewline

    Write-Host "Generated: Dockerfile.$imageName ($($snippets.Count) snippets)"
}

Write-Host ""
Write-Host "Generated $($imageNames.Count) Dockerfiles in $generatedDir"
