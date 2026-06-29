# PowerShell uninstall script for copilot_here.
# Self-contained: removes the install by known paths and does NOT depend on the
# binary still working, so it cleans up even a half-broken install.
#
# Usage:
#   iex ([System.Text.Encoding]::UTF8.GetString((iwr -UseBasicParsing 'https://github.com/GordonBeeming/copilot_here/releases/download/cli-latest/uninstall.ps1').Content))
#   Add -Purge to also delete config dirs, -Yes to skip the confirmation prompt:
#   & ([scriptblock]::Create((iwr -UseBasicParsing '.../uninstall.ps1').Content)) -Purge -Yes

[CmdletBinding()]
param(
    [switch]$Purge,
    [switch]$Yes
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$home_ = if ($env:USERPROFILE) { $env:USERPROFILE } elseif ($env:HOME) { $env:HOME } else { [Environment]::GetFolderPath('UserProfile') }

$binDir = Join-Path (Join-Path $home_ ".local") "bin"
$binName = if ($env:USERPROFILE) { "copilot_here.exe" } else { "copilot_here" }
$bin = if ($env:COPILOT_HERE_BIN) { $env:COPILOT_HERE_BIN } else { Join-Path $binDir $binName }
$psScript = Join-Path $home_ ".copilot_here.ps1"
$markerStart = "# >>> copilot_here >>>"
$markerEnd = "# <<< copilot_here <<<"

Write-Host "🧹 Uninstalling copilot_here" -ForegroundColor Cyan
Write-Host "   This removes the binary, PowerShell script, cmd wrappers, and shell integration."
if ($Purge) {
    Write-Host "   -Purge: also deleting ~/.config/copilot_here and ~/.config/copilot-cli-docker"
}

if (-not $Yes) {
    $response = Read-Host "   Continue? [y/N]"
    if ($response -notmatch '^[yY]') {
        Write-Host "❌ Uninstall cancelled." -ForegroundColor Red
        return
    }
}

function Remove-CopilotProfileBlock {
    param([string]$ProfilePath)

    if (-not (Test-Path $ProfilePath)) { return }
    $content = Get-Content $ProfilePath -Raw -ErrorAction SilentlyContinue
    if ([string]::IsNullOrEmpty($content)) { return }
    if (-not ($content.Contains($markerStart) -or $content -match 'copilot_here\.ps1')) { return }

    if ($content.Contains($markerStart)) {
        $startIndex = $content.IndexOf($markerStart)
        $endIndex = $content.IndexOf($markerEnd, $startIndex)
        if ($endIndex -gt $startIndex) {
            $endIndex += $markerEnd.Length
            $before = $content.Substring(0, $startIndex)
            $after = if ($endIndex -lt $content.Length) { $content.Substring($endIndex) } else { "" }
            $content = $before + $after
        }
    }
    # Drop any stray sourcing lines left outside the block.
    $content = $content -replace '(?m)^.*copilot_here\.ps1.*$[\r\n]*', ''
    $content = $content.TrimEnd()
    if ($content.Length -gt 0) { $content += "`n" }

    $resolved = Resolve-Path $ProfilePath -ErrorAction SilentlyContinue
    $target = if ($resolved) { $resolved.Path } else { $ProfilePath }
    [System.IO.File]::WriteAllText($target, $content, [System.Text.Encoding]::UTF8)
    Write-Host "✓ Cleaned shell integration from $(Split-Path $ProfilePath -Leaf)"
}

function Remove-CopilotItem {
    param([string]$Path, [string]$Label)
    if (Test-Path $Path) {
        Remove-Item -Path $Path -Force -Recurse -ErrorAction SilentlyContinue
        Write-Host "✓ Removed $Label"
    }
}

# Stop any running containers so the binary isn't in use.
try {
    $running = docker ps --filter "name=copilot_here-" -q 2>$null
    if ($running) {
        Write-Host "🛑 Stopping copilot_here containers..."
        docker stop $running 2>&1 | Out-Null
    }
} catch { }

Remove-CopilotProfileBlock -ProfilePath (Join-Path $home_ "Documents\PowerShell\Microsoft.PowerShell_profile.ps1")
Remove-CopilotProfileBlock -ProfilePath (Join-Path $home_ "Documents\WindowsPowerShell\Microsoft.PowerShell_profile.ps1")
# Also handle Unix-style PowerShell profiles for pwsh on Linux/macOS.
Remove-CopilotProfileBlock -ProfilePath (Join-Path $home_ ".config/powershell/Microsoft.PowerShell_profile.ps1")

Remove-CopilotItem -Path (Join-Path $binDir "copilot_here.cmd") -Label "cmd wrapper (copilot_here.cmd)"
Remove-CopilotItem -Path (Join-Path $binDir "copilot_yolo.cmd") -Label "cmd wrapper (copilot_yolo.cmd)"
Remove-CopilotItem -Path $psScript -Label "PowerShell script (~/.copilot_here.ps1)"
Remove-CopilotItem -Path $bin -Label "binary ($bin)"

if ($Purge) {
    # Best-effort: remove copilot_here containers and pulled images so -Purge
    # matches the CLI's --purge. Skipped silently when docker isn't available.
    try {
        if (Get-Command docker -ErrorAction SilentlyContinue) {
            $containers = docker ps -aq --filter "name=copilot_here-" 2>$null
            if ($containers) { docker rm -f $containers 2>&1 | Out-Null; Write-Host "✓ Removed copilot_here containers" }
            $images = docker images --filter=reference='ghcr.io/gordonbeeming/copilot_here*' -q 2>$null | Select-Object -Unique
            if ($images) { docker rmi -f $images 2>&1 | Out-Null; Write-Host "✓ Removed copilot_here images" }
        }
    } catch { }
    Remove-CopilotItem -Path (Join-Path $home_ ".config/copilot_here") -Label "config (~/.config/copilot_here)"
    Remove-CopilotItem -Path (Join-Path $home_ ".config/copilot-cli-docker") -Label "config (~/.config/copilot-cli-docker)"
} else {
    Write-Host "• Kept config dirs (re-run with -Purge to remove them)"
}

Write-Host ""
Write-Host "✅ copilot_here uninstalled." -ForegroundColor Green
Write-Host "   Restart PowerShell or open a new terminal to clear the loaded function."
Write-Host "   Installed via WinGet / dotnet tool? Remove it with that package manager."
