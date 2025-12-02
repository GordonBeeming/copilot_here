# Cross-platform CLI integration tests (PowerShell)
# 
# These tests verify the CLI binary works correctly across platforms.
# They DO NOT require Docker - they test CLI functionality only.
#
# Usage: .\tests\integration\test_cli.ps1 [-CliPath <path>]

param(
    [string]$CliPath = ""
)

$ErrorActionPreference = "Continue"

# Test counters
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0

function Write-TestStart {
    param([string]$Name)
    Write-Host ""
    Write-Host "TEST: $Name" -ForegroundColor Blue
    $script:TestCount++
}

function Write-TestPass {
    param([string]$Message)
    Write-Host "✓ PASS: $Message" -ForegroundColor Green
    $script:PassCount++
}

function Write-TestFail {
    param([string]$Message)
    Write-Host "✗ FAIL: $Message" -ForegroundColor Red
    $script:FailCount++
}

function Write-Summary {
    Write-Host ""
    Write-Host "======================================"
    Write-Host "CLI INTEGRATION TEST SUMMARY"
    Write-Host "======================================"
    Write-Host "Total Tests: $script:TestCount"
    Write-Host "Passed: $script:PassCount" -ForegroundColor Green
    if ($script:FailCount -gt 0) {
        Write-Host "Failed: $script:FailCount" -ForegroundColor Red
    } else {
        Write-Host "Failed: $script:FailCount"
    }
    Write-Host "======================================"
    
    return $script:FailCount -eq 0
}

# CLI binary path
$script:CliBinary = ""
$script:RepoRoot = ""
$script:TestDir = ""

function Setup-Cli {
    Write-Host "🔧 Setting up CLI binary..."
    
    $scriptPath = $PSScriptRoot
    $script:RepoRoot = (Get-Item "$scriptPath/../..").FullName
    
    if ($CliPath -and (Test-Path $CliPath)) {
        $script:CliBinary = $CliPath
        Write-Host "   Using provided CLI: $script:CliBinary"
    } else {
        # Build CLI from source
        Write-Host "   Building CLI from source..."
        
        $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
        if (-not $dotnetCmd) {
            Write-Host "❌ .NET SDK not found. Install it or provide -CliPath" -ForegroundColor Red
            exit 1
        }
        
        $publishDir = Join-Path $script:RepoRoot "publish/cli-test"
        New-Item -ItemType Directory -Force -Path $publishDir | Out-Null
        
        $csproj = Join-Path $script:RepoRoot "app/CopilotHere.csproj"
        $result = & dotnet publish $csproj -c Release -o $publishDir --nologo -v q 2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Failed to build CLI: $result" -ForegroundColor Red
            exit 1
        }
        
        # Find binary
        $exePath = Join-Path $publishDir "CopilotHere.exe"
        $binPath = Join-Path $publishDir "CopilotHere"
        
        if (Test-Path $exePath) {
            $script:CliBinary = $exePath
        } elseif (Test-Path $binPath) {
            $script:CliBinary = $binPath
        } else {
            Write-Host "❌ CLI binary not found after build" -ForegroundColor Red
            exit 1
        }
        
        Write-Host "   Built CLI: $script:CliBinary"
    }
}

function Setup-TestDir {
    $script:TestDir = Join-Path ([System.IO.Path]::GetTempPath()) "copilot_here-test-$([System.Guid]::NewGuid().ToString('N').Substring(0,8))"
    New-Item -ItemType Directory -Force -Path $script:TestDir | Out-Null
    New-Item -ItemType Directory -Force -Path (Join-Path $script:TestDir ".copilot_here") | Out-Null
}

function Cleanup {
    if ($script:RepoRoot) {
        $publishDir = Join-Path $script:RepoRoot "publish/cli-test"
        if (Test-Path $publishDir) {
            Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
        }
    }
    if ($script:TestDir -and (Test-Path $script:TestDir)) {
        Remove-Item -Recurse -Force $script:TestDir -ErrorAction SilentlyContinue
    }
}

# ============================================================================
# TEST CASES
# ============================================================================

function Test-Version {
    Write-TestStart "CLI shows version"
    
    $result = & $script:CliBinary --version 2>&1 | Out-String
    
    if ($result -match "\d+\.\d+\.\d+") {
        Write-TestPass "Version: $($result.Trim().Split("`n")[0])"
    } else {
        Write-TestFail "Did not show version: $result"
    }
}

function Test-Help {
    Write-TestStart "CLI shows help"
    
    $result = & $script:CliBinary --help 2>&1 | Out-String
    
    if ($result -match "GitHub Copilot CLI") {
        Write-TestPass "Help text includes description"
    } else {
        Write-TestFail "Help missing description: $result"
    }
}

function Test-HelpShowsOptions {
    Write-TestStart "CLI help shows main options"
    
    $result = & $script:CliBinary --help 2>&1 | Out-String
    
    $options = @("--dotnet", "--playwright", "--mount", "--no-pull", "--yolo")
    $missing = @()
    
    foreach ($option in $options) {
        if ($result -notmatch [regex]::Escape($option)) {
            $missing += $option
        }
    }
    
    if ($missing.Count -eq 0) {
        Write-TestPass "All main options present"
    } else {
        Write-TestFail "Missing options: $($missing -join ', ')"
    }
}

function Test-HelpShowsCommands {
    Write-TestStart "CLI help shows commands"
    
    $result = & $script:CliBinary --help 2>&1 | Out-String
    
    $commands = @("--list-mounts", "--list-images", "--show-image", "--enable-airlock")
    $missing = @()
    
    foreach ($cmd in $commands) {
        if ($result -notmatch [regex]::Escape($cmd)) {
            $missing += $cmd
        }
    }
    
    if ($missing.Count -eq 0) {
        Write-TestPass "All main commands present"
    } else {
        Write-TestFail "Missing commands: $($missing -join ', ')"
    }
}

function Test-ListImages {
    Write-TestStart "CLI lists available images"
    
    $result = & $script:CliBinary --list-images 2>&1 | Out-String
    
    if (($result -match "latest") -and ($result -match "dotnet")) {
        Write-TestPass "Image list includes expected variants"
    } else {
        Write-TestFail "Image list incomplete: $result"
    }
}

function Test-ShowImage {
    Write-TestStart "CLI shows current image config"
    
    $result = & $script:CliBinary --show-image 2>&1 | Out-String
    
    if ($result -match "latest") {
        Write-TestPass "Shows image configuration"
    } else {
        Write-TestFail "Did not show image config: $result"
    }
}

function Test-ListMountsEmpty {
    Write-TestStart "CLI lists mounts (empty)"
    
    Push-Location $script:TestDir
    try {
        $result = & $script:CliBinary --list-mounts 2>&1 | Out-String
        
        # Should run without error even with no mounts configured
        if ($result -match "mount" -or $LASTEXITCODE -eq 0) {
            Write-TestPass "List mounts works with empty config"
        } else {
            Write-TestFail "List mounts failed: $result"
        }
    } finally {
        Pop-Location
    }
}

function Test-ShowAirlockRules {
    Write-TestStart "CLI shows airlock rules"
    
    Push-Location $script:TestDir
    try {
        $result = & $script:CliBinary --show-airlock-rules 2>&1 | Out-String
        
        # Should run without error
        if ($result -match "airlock|rules|no.*config") {
            Write-TestPass "Shows airlock rules info"
        } else {
            Write-TestFail "Did not show airlock info: $result"
        }
    } finally {
        Pop-Location
    }
}

function Test-DotnetAlias {
    Write-TestStart "CLI accepts -d9 alias"
    
    $result = & $script:CliBinary -d9 --help 2>&1 | Out-String
    
    if ($result -notmatch "unrecognized") {
        Write-TestPass "-d9 alias accepted"
    } else {
        Write-TestFail "-d9 not recognized: $result"
    }
}

function Test-YoloFlag {
    Write-TestStart "CLI accepts --yolo flag"
    
    $result = & $script:CliBinary --yolo --help 2>&1 | Out-String
    
    if ($result -notmatch "unrecognized") {
        Write-TestPass "--yolo flag accepted"
    } else {
        Write-TestFail "--yolo not recognized: $result"
    }
}

function Test-PassthroughHelp {
    Write-TestStart "CLI passes --help2 through"
    
    $result = & $script:CliBinary --help2 2>&1 | Out-String
    
    if ($result -notmatch "unrecognized.*help2") {
        Write-TestPass "--help2 recognized for passthrough"
    } else {
        Write-TestFail "--help2 not handled: $result"
    }
}

# ============================================================================
# MAIN
# ============================================================================

function Main {
    Write-Host "========================================"
    Write-Host "    CLI INTEGRATION TESTS (PowerShell)"
    Write-Host "========================================"
    Write-Host "    Platform: $([System.Environment]::OSVersion.Platform) $([System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture)"
    Write-Host "========================================"
    
    try {
        Setup-Cli
        Setup-TestDir
        
        # Run tests
        Test-Version
        Test-Help
        Test-HelpShowsOptions
        Test-HelpShowsCommands
        Test-ListImages
        Test-ShowImage
        Test-ListMountsEmpty
        Test-ShowAirlockRules
        Test-DotnetAlias
        Test-YoloFlag
        Test-PassthroughHelp
        
        # Call Write-Summary and capture result explicitly
        $null = Write-Summary
        
        # Exit based on fail count
        if ($script:FailCount -gt 0) {
            exit 1
        }
        exit 0
    } finally {
        Cleanup
    }
}

Main
