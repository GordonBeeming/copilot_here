# PowerShell Install Script for copilot_here
# Downloads the script and runs the update function

# Set console output encoding to UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

# Download the main script
$scriptPath = "$env:USERPROFILE\.copilot_here.ps1"
Write-Host "📥 Downloading copilot_here.ps1..." -ForegroundColor Cyan
Invoke-WebRequest -Uri "https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/copilot_here.ps1" -OutFile $scriptPath
Write-Host "✅ Downloaded to: $scriptPath" -ForegroundColor Green

# Source the script to load functions
Write-Host "🔄 Loading copilot_here functions..." -ForegroundColor Cyan
Invoke-Expression (Get-Content $scriptPath -Raw)

# Run update to set up everything (binary, script, profiles)
Write-Host ""
Write-Host "📦 Running update..." -ForegroundColor Cyan
$null = Update-CopilotHere

# Run install-shells to set up shell integration
Write-Host ""
Write-Host "🔧 Setting up shell integration..." -ForegroundColor Cyan
copilot_here --install-shells

Write-Host ""
Write-Host "✅ Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "⚠️  Please restart your PowerShell session or run:" -ForegroundColor Yellow
Write-Host "   . `$PROFILE" -ForegroundColor Cyan
Write-Host ""
Write-Host "Then try: copilot_here --help" -ForegroundColor Yellow
