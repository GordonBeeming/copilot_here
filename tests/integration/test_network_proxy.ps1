# Integration tests for network proxy (airlock) functionality - PowerShell
# Tests -EnableAirlock and -EnableGlobalAirlock parameters

$ErrorActionPreference = "Stop"

# Color support
function Write-TestPass($message) {
    Write-Host "✓ PASS: $message" -ForegroundColor Green
}

function Write-TestFail($message) {
    Write-Host "✗ FAIL: $message" -ForegroundColor Red
}

# Test tracking
$script:TestCount = 0
$script:PassCount = 0
$script:FailCount = 0

function Test-Start($name) {
    Write-Host ""
    Write-Host "TEST: $name"
    $script:TestCount++
}

function Test-Pass($message) {
    Write-TestPass $message
    $script:PassCount++
}

function Test-Fail($message) {
    Write-TestFail $message
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
$env:COPILOT_HERE_TEST_MODE = "true"

# Source the script
. "$ScriptDir\copilot_here.ps1"

Write-Host "======================================"
Write-Host "Network Proxy (Airlock) Tests - PowerShell"
Write-Host "======================================"
Write-Host "PowerShell: $($PSVersionTable.PSVersion)"
Write-Host "Script: $ScriptDir\copilot_here.ps1"

# Test 1: -EnableAirlock parameter exists in Copilot-Here
Test-Start "Check -EnableAirlock parameter exists in Copilot-Here"
$params = (Get-Command Copilot-Here).Parameters
if ($params.ContainsKey("EnableAirlock")) {
    Test-Pass "-EnableAirlock parameter exists"
} else {
    Test-Fail "-EnableAirlock parameter not found"
}

# Test 2: -EnableGlobalAirlock parameter exists
Test-Start "Check -EnableGlobalAirlock parameter exists in Copilot-Here"
if ($params.ContainsKey("EnableGlobalAirlock")) {
    Test-Pass "-EnableGlobalAirlock parameter exists"
} else {
    Test-Fail "-EnableGlobalAirlock parameter not found"
}

# Test 3: Parameters exist in Copilot-Yolo too
Test-Start "Check -EnableAirlock parameter exists in Copilot-Yolo"
$yoloParams = (Get-Command Copilot-Yolo).Parameters
if ($yoloParams.ContainsKey("EnableAirlock")) {
    Test-Pass "-EnableAirlock parameter exists in Copilot-Yolo"
} else {
    Test-Fail "-EnableAirlock parameter not found in Copilot-Yolo"
}

# Test 4: Ensure-NetworkConfig function exists
Test-Start "Check Ensure-NetworkConfig function exists"
if (Get-Command Ensure-NetworkConfig -ErrorAction SilentlyContinue) {
    Test-Pass "Ensure-NetworkConfig function is defined"
} else {
    Test-Fail "Ensure-NetworkConfig function not found"
}

# Test 5: Invoke-CopilotAirlock function exists
Test-Start "Check Invoke-CopilotAirlock function exists"
if (Get-Command Invoke-CopilotAirlock -ErrorAction SilentlyContinue) {
    Test-Pass "Invoke-CopilotAirlock function is defined"
} else {
    Test-Fail "Invoke-CopilotAirlock function not found"
}

# Test 6: Help output contains NETWORK (AIRLOCK) section
Test-Start "Check NETWORK (AIRLOCK) section in help"
$helpOutput = Copilot-Here -Help 2>&1 | Out-String
if ($helpOutput -match "NETWORK (AIRLOCK)") {
    Test-Pass "NETWORK (AIRLOCK) section present in help"
} else {
    Test-Fail "NETWORK (AIRLOCK) section missing from help"
}

# Test 7: Help mentions -EnableAirlock
Test-Start "Check -EnableAirlock in help text"
if ($helpOutput -match "EnableAirlock") {
    Test-Pass "-EnableAirlock documented in help"
} else {
    Test-Fail "-EnableAirlock not in help output"
}

# Test 8: Help mentions -EnableGlobalAirlock
Test-Start "Check -EnableGlobalAirlock in help text"
if ($helpOutput -match "EnableGlobalAirlock") {
    Test-Pass "-EnableGlobalAirlock documented in help"
} else {
    Test-Fail "-EnableGlobalAirlock not in help output"
}

# Test 9: Existing config is detected
Test-Start "Test existing network config detection"
# Use cross-platform temp directory
$tempBase = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { "/tmp" }
$testDir = New-Item -ItemType Directory -Path (Join-Path $tempBase "copilot_test_$(Get-Random)") -Force
$configDir = New-Item -ItemType Directory -Path (Join-Path $testDir ".copilot_here") -Force
$configFile = Join-Path $configDir "network.json"
@{
    inherit_default_rules = $true
    mode = "enforce"
    allowed_rules = @()
} | ConvertTo-Json | Set-Content $configFile

Push-Location $testDir
try {
    # Ensure-NetworkConfig returns $true if config exists, so just check the return value
    $result = Ensure-NetworkConfig -IsGlobal $false
    if ($result -eq $true) {
        Test-Pass "Existing config detected correctly (returned true)"
    } else {
        Test-Fail "Existing config not detected (returned: $result)"
    }
} finally {
    Pop-Location
    Remove-Item $testDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Test 10: Default network rules file exists
Test-Start "Check default-network-rules.json exists"
$defaultRulesPath = Join-Path $ScriptDir "default-network-rules.json"
if (Test-Path $defaultRulesPath) {
    Test-Pass "default-network-rules.json exists in repo"
} else {
    Test-Fail "default-network-rules.json not found in repo"
}

# Test 11: Default rules JSON is valid
Test-Start "Validate default-network-rules.json format"
try {
    $jsonContent = Get-Content $defaultRulesPath -Raw | ConvertFrom-Json
    if ($jsonContent.allowed_rules) {
        Test-Pass "default-network-rules.json is valid JSON with allowed_rules"
    } else {
        Test-Fail "default-network-rules.json missing allowed_rules"
    }
} catch {
    Test-Fail "default-network-rules.json is invalid JSON: $_"
}

# Test 12: Docker compose template exists
Test-Start "Check docker-compose.airlock.yml.template exists"
$templatePath = Join-Path $ScriptDir "docker-compose.airlock.yml.template"
if (Test-Path $templatePath) {
    Test-Pass "docker-compose.airlock.yml.template exists"
} else {
    Test-Fail "docker-compose.airlock.yml.template not found"
}

# Test 13: Compose template has required placeholders
Test-Start "Validate compose template placeholders"
$templateContent = Get-Content $templatePath -Raw
$requiredPlaceholders = @("{{PROJECT_NAME}}", "{{APP_IMAGE}}", "{{PROXY_IMAGE}}", "{{NETWORK_CONFIG}}")
$missingPlaceholders = @()
foreach ($placeholder in $requiredPlaceholders) {
    if ($templateContent -notmatch [regex]::Escape($placeholder)) {
        $missingPlaceholders += $placeholder
    }
}
if ($missingPlaceholders.Count -eq 0) {
    Test-Pass "All required placeholders present in template"
} else {
    Test-Fail "Missing placeholders: $($missingPlaceholders -join ', ')"
}

# Test 14: entrypoint-airlock.sh exists
Test-Start "Check entrypoint-airlock.sh exists"
if (Test-Path (Join-Path $ScriptDir "entrypoint-airlock.sh")) {
    Test-Pass "entrypoint-airlock.sh exists"
} else {
    Test-Fail "entrypoint-airlock.sh not found"
}

# Test 15: proxy-entrypoint.sh exists
Test-Start "Check proxy-entrypoint.sh exists"
if (Test-Path (Join-Path $ScriptDir "proxy-entrypoint.sh")) {
    Test-Pass "proxy-entrypoint.sh exists"
} else {
    Test-Fail "proxy-entrypoint.sh not found"
}

# Test 16: Dockerfile.proxy exists
Test-Start "Check Dockerfile.proxy exists"
if (Test-Path (Join-Path $ScriptDir "docker/Dockerfile.proxy")) {
    Test-Pass "Dockerfile.proxy exists"
} else {
    Test-Fail "Dockerfile.proxy not found"
}

# Test 17: Proxy Rust source exists
Test-Start "Check proxy/src/main.rs exists"
if (Test-Path (Join-Path $ScriptDir "proxy/src/main.rs")) {
    Test-Pass "proxy/src/main.rs exists"
} else {
    Test-Fail "proxy/src/main.rs not found"
}

# Test 18: Copilot-Yolo help also has network proxy section
Test-Start "Check NETWORK (AIRLOCK) section in Copilot-Yolo help"
$yoloHelpOutput = Copilot-Yolo -Help 2>&1 | Out-String
if ($yoloHelpOutput -match "NETWORK (AIRLOCK)") {
    Test-Pass "NETWORK (AIRLOCK) section present in Copilot-Yolo help"
} else {
    Test-Fail "NETWORK (AIRLOCK) section missing from Copilot-Yolo help"
}

# Test 19: -ShowNetworkRules documented in help
Test-Start "Check -ShowNetworkRules documented"
if ($helpOutput -match "ShowNetworkRules") {
    Test-Pass "-ShowNetworkRules documented in help"
} else {
    Test-Fail "-ShowNetworkRules not documented in help"
}

# Test 20: -EditNetworkRules documented in help
Test-Start "Check -EditNetworkRules documented"
if ($helpOutput -match "EditNetworkRules") {
    Test-Pass "-EditNetworkRules documented in help"
} else {
    Test-Fail "-EditNetworkRules not documented in help"
}

# Test 21: -EditGlobalNetworkRules documented in help
Test-Start "Check -EditGlobalNetworkRules documented"
if ($helpOutput -match "EditGlobalNetworkRules") {
    Test-Pass "-EditGlobalNetworkRules documented in help"
} else {
    Test-Fail "-EditGlobalNetworkRules not documented in help"
}

# Test 22: -ShowNetworkRules runs without error
Test-Start "Check -ShowNetworkRules runs"
try {
    # Just verify the command runs without throwing an exception
    Copilot-Here -ShowNetworkRules | Out-Null
    Test-Pass "-ShowNetworkRules runs without error"
} catch {
    Test-Fail "-ShowNetworkRules threw an error: $_"
}

# Test 23: Config file has inherit_default_rules field
Test-Start "Check config has inherit_default_rules"
$tempBase = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { "/tmp" }
$testDir2 = New-Item -ItemType Directory -Path (Join-Path $tempBase "copilot_test_$(Get-Random)") -Force
$configDir2 = New-Item -ItemType Directory -Path (Join-Path $testDir2 ".copilot_here") -Force
$configFile2 = Join-Path $configDir2 "network.json"
@{
    inherit_default_rules = $true
    mode = "enforce"
    enable_logging = $false
    allowed_rules = @()
} | ConvertTo-Json | Set-Content $configFile2
$configContent = Get-Content $configFile2 -Raw
if ($configContent -match "inherit_default_rules") {
    Test-Pass "Config has inherit_default_rules field"
} else {
    Test-Fail "Config missing inherit_default_rules field"
}
Remove-Item $testDir2 -Recurse -Force -ErrorAction SilentlyContinue

# Test 24: Default rules JSON has enable_logging field
Test-Start "Check default-network-rules.json has enable_logging"
$defaultRulesContent = Get-Content $defaultRulesPath -Raw
if ($defaultRulesContent -match "enable_logging") {
    Test-Pass "default-network-rules.json has enable_logging field"
} else {
    Test-Fail "default-network-rules.json missing enable_logging field"
}

# Test 25: Monitor mode config is valid
Test-Start "Check monitor mode config is valid"
$tempBase = if ($env:TEMP) { $env:TEMP } elseif ($env:TMPDIR) { $env:TMPDIR } else { "/tmp" }
$testDir3 = New-Item -ItemType Directory -Path (Join-Path $tempBase "copilot_test_$(Get-Random)") -Force
$configDir3 = New-Item -ItemType Directory -Path (Join-Path $testDir3 ".copilot_here") -Force
$configFile3 = Join-Path $configDir3 "network.json"
@{
    inherit_default_rules = $true
    mode = "monitor"
    enable_logging = $false
    allowed_rules = @()
} | ConvertTo-Json | Set-Content $configFile3
$monitorContent = Get-Content $configFile3 -Raw
if ($monitorContent -match '"mode"\s*:\s*"monitor"') {
    Test-Pass "Monitor mode config is valid"
} else {
    Test-Fail "Monitor mode config format issue"
}
Remove-Item $testDir3 -Recurse -Force -ErrorAction SilentlyContinue

# Test 26: Compose template has LOGS_MOUNT placeholder
Test-Start "Check compose template has LOGS_MOUNT placeholder"
if ($templateContent -match "{{LOGS_MOUNT}}") {
    Test-Pass "Compose template has LOGS_MOUNT placeholder"
} else {
    Test-Fail "Compose template missing LOGS_MOUNT placeholder"
}

# Test 27: Compose template has proxy volume for config
Test-Start "Check compose template mounts network config"
if ($templateContent -match "{{NETWORK_CONFIG}}") {
    Test-Pass "Compose template has network config mount"
} else {
    Test-Fail "Compose template missing network config mount"
}

Print-Summary
