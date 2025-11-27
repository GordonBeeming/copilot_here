# GitHub info extraction tests for PowerShell
# Tests the Get-GitHubInfo function with various URL formats

$ErrorActionPreference = "Stop"

# Color support
function Write-TestPass { param($Message) Write-Host "✓ PASS: $Message" -ForegroundColor Green }
function Write-TestFail { param($Message) Write-Host "✗ FAIL: $Message" -ForegroundColor Red }

$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0

function Test-Start {
    param($Name)
    Write-Host ""
    Write-Host "TEST: $Name"
    $script:TestCount++
}

function Test-Pass {
    param($Message)
    Write-TestPass $Message
    $script:PassCount++
}

function Test-Fail {
    param($Message)
    Write-TestFail $Message
    $script:FailCount++
}

function Print-Summary {
    Write-Host ""
    Write-Host "======================================"
    Write-Host "TEST SUMMARY"
    Write-Host "======================================"
    Write-Host "Total Tests: $script:TestCount"
    Write-Host "Passed: $script:PassCount" -ForegroundColor Green
    if ($script:FailCount -gt 0) {
        Write-Host "Failed: $script:FailCount" -ForegroundColor Red
    } else {
        Write-Host "Failed: $script:FailCount"
    }
    Write-Host "======================================"
    
    if ($script:FailCount -gt 0) {
        exit 1
    }
}

# Setup
$ScriptDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$TestDir = Join-Path ([System.IO.Path]::GetTempPath()) "copilot_here_test_$(Get-Random)"
New-Item -ItemType Directory -Path $TestDir -Force | Out-Null

# Cleanup on exit
$CleanupScript = {
    if (Test-Path $TestDir) {
        Remove-Item -Recurse -Force $TestDir -ErrorAction SilentlyContinue
    }
}

try {
    Write-Host "======================================"
    Write-Host "GitHub Info Extraction Tests (PowerShell)"
    Write-Host "======================================"
    Write-Host "Script: $ScriptDir\copilot_here.ps1"
    Write-Host ""

    # Source the script
    . "$ScriptDir\copilot_here.ps1"

    # Helper to test URL parsing by creating a mock git repo
    function Test-UrlParsing {
        param(
            [string]$Url,
            [string]$ExpectedOwner,
            [string]$ExpectedRepo,
            [string]$TestName
        )
        
        # Create a temporary git repo with the specified remote
        $repoDir = Join-Path $TestDir "test_repo_$(Get-Random)"
        New-Item -ItemType Directory -Path $repoDir -Force | Out-Null
        Push-Location $repoDir
        
        git init -q 2>$null
        git remote add origin $Url 2>$null
        
        # Call the production function
        $result = Get-GitHubInfo
        
        Pop-Location
        Remove-Item -Recurse -Force $repoDir -ErrorAction SilentlyContinue
        
        if ($result -and $result.Owner -eq $ExpectedOwner -and $result.Repo -eq $ExpectedRepo) {
            Test-Pass $TestName
            return $true
        } else {
            $actualOwner = if ($result) { $result.Owner } else { "<null>" }
            $actualRepo = if ($result) { $result.Repo } else { "<null>" }
            Test-Fail "$TestName (expected: $ExpectedOwner|$ExpectedRepo, got: $actualOwner|$actualRepo)"
            return $false
        }
    }

    # Test SSH URL format
    Test-Start "Parse SSH URL (git@github.com:owner/repo.git)"
    Test-UrlParsing "git@github.com:GordonBeeming/copilot_here.git" "GordonBeeming" "copilot_here" "SSH URL with .git"

    Test-Start "Parse SSH URL without .git suffix"
    Test-UrlParsing "git@github.com:GordonBeeming/copilot_here" "GordonBeeming" "copilot_here" "SSH URL without .git"

    # Test HTTPS URL format
    Test-Start "Parse HTTPS URL (https://github.com/owner/repo.git)"
    Test-UrlParsing "https://github.com/GordonBeeming/copilot_here.git" "GordonBeeming" "copilot_here" "HTTPS URL with .git"

    Test-Start "Parse HTTPS URL without .git suffix"
    Test-UrlParsing "https://github.com/GordonBeeming/copilot_here" "GordonBeeming" "copilot_here" "HTTPS URL without .git"

    # Test with different owner/repo names
    Test-Start "Parse URL with hyphenated names"
    Test-UrlParsing "git@github.com:my-org/my-awesome-repo.git" "my-org" "my-awesome-repo" "Hyphenated names"

    Test-Start "Parse URL with underscores"
    Test-UrlParsing "https://github.com/my_org/my_repo.git" "my_org" "my_repo" "Underscored names"

    Test-Start "Parse URL with numbers"
    Test-UrlParsing "git@github.com:user123/repo456.git" "user123" "repo456" "Names with numbers"

    # Test placeholder replacement using production code
    Test-Start "Placeholder replacement in network config"
    $configFile = Join-Path $TestDir "network.json"
    @'
{
  "allowed_paths": ["/agents/{{GITHUB_OWNER}}/{{GITHUB_REPO}}"]
}
'@ | Set-Content $configFile -Encoding UTF8

    # Create a git repo context
    $repoDir = Join-Path $TestDir "placeholder_test"
    New-Item -ItemType Directory -Path $repoDir -Force | Out-Null
    Push-Location $repoDir
    git init -q 2>$null
    git remote add origin "git@github.com:TestOwner/TestRepo.git" 2>$null

    # Call the production function to process the config
    $processed = Get-ProcessedNetworkConfig -ConfigFile $configFile

    if ($processed -and (Test-Path $processed)) {
        $content = Get-Content $processed -Raw
        Remove-Item $processed -ErrorAction SilentlyContinue
        
        if ($content -match '"/agents/TestOwner/TestRepo"') {
            Test-Pass "Placeholders replaced correctly"
        } else {
            Test-Fail "Placeholders not replaced (got: $content)"
        }
    } else {
        Test-Fail "Processed config file not created"
    }

    Pop-Location

    # Test with no git repo (should return null)
    Test-Start "Handle non-git directory gracefully"
    $nonGitDir = Join-Path $TestDir "not_a_repo"
    New-Item -ItemType Directory -Path $nonGitDir -Force | Out-Null
    Push-Location $nonGitDir

    $result = Get-GitHubInfo
    if ($null -eq $result) {
        Test-Pass "Returns null for non-git directory"
    } else {
        Test-Fail "Should return null for non-git directory (got: $($result.Owner)|$($result.Repo))"
    }

    Pop-Location

    # Test current repo (where we actually are)
    Test-Start "Extract info from current repository"
    Push-Location $ScriptDir
    $result = Get-GitHubInfo
    if ($result -and $result.Owner -and $result.Repo) {
        Test-Pass "Extracted owner=$($result.Owner), repo=$($result.Repo)"
    } else {
        Test-Fail "No result from current repository"
    }
    Pop-Location

    Print-Summary

} finally {
    & $CleanupScript
}
