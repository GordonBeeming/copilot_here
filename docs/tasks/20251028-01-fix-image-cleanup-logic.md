# Fix Image Cleanup Logic and Prevent Unnecessary Re-pulls

**Date**: 2025-10-28  
**Type**: Bug Fix  
**Version**: 2025-10-28

## Problem Statement

The image cleanup logic had two critical issues:

1. **Broken cleanup with `<none>` tags**: The script was attempting to remove intermediate/dangling images with `<none>` tags, which would fail and clutter the output:
   ```
   🗑️  Removing: ghcr.io/gordonbeeming/copilot_here:<none>
   ⚠️  Failed to remove: ghcr.io/gordonbeeming/copilot_here:<none>
   ✓ Cleaned up 0 image(s)
   ```

2. **Unnecessary re-pulling**: Every run would remove ALL unused images (including the latest one) and then immediately pull the same image again, wasting bandwidth and time.

## Solution Approach

Implemented smart cleanup logic that:
- Filters out `<none>` tagged images from cleanup attempts
- Only removes images **older than 7 days**
- Always keeps the currently used image to avoid re-pulling
- Provides better user feedback about what's being removed and why

## Changes Made

### Files Modified
- `copilot_here.sh` - Bash/Zsh shell functions
- `copilot_here.ps1` - PowerShell functions  
- `README.md` - Manual installation sections for both shells

### Key Implementation Details

#### Bash/Zsh (`__copilot_cleanup_images`)
```bash
# Get cutoff timestamp (7 days ago)
local cutoff_date=$(date -d '7 days ago' +%s 2>/dev/null || date -v-7d +%s 2>/dev/null)

# Get all copilot_here images with the project label, excluding <none> tags
local all_images=$(docker images --filter "label=project=copilot_here" \
  --format "{{.Repository}}:{{.Tag}}|{{.CreatedAt}}" | grep -v ":<none>" || true)

# Parse creation date and compare
local image_date=$(date -d "$created_at" +%s 2>/dev/null || \
  date -j -f "%Y-%m-%d %H:%M:%S %z %Z" "$created_at" +%s 2>/dev/null)

if [ -n "$image_date" ] && [ "$image_date" -lt "$cutoff_date" ]; then
  # Remove old image
fi
```

#### PowerShell (`Remove-UnusedCopilotImages`)
```powershell
# Get cutoff timestamp (7 days ago)
$cutoffDate = (Get-Date).AddDays(-7)

# Get all copilot_here images, excluding <none> tags
$imagesToProcess = $allImages | Where-Object { $_ -notmatch ':<none>' }

# Parse creation date
$imageDate = [DateTime]::Parse($createdAt.Substring(0, 19))

if ($imageDate -lt $cutoffDate) {
  # Remove old image
}
```

### Cross-Platform Compatibility
- **Linux**: Uses GNU date with `-d` flag
- **macOS**: Fallback to BSD date with `-v` flag and `-j -f` for parsing
- **Windows**: Uses PowerShell `[DateTime]::Parse()`

## Testing Performed

- [x] Verified version updates in all files
- [x] Checked bash script syntax
- [x] Checked PowerShell script syntax
- [x] Confirmed `<none>` filtering logic
- [x] Validated date parsing for both Linux and macOS
- [x] Ensured current image is preserved

## Benefits

1. **No more failed removals**: `<none>` tagged images are filtered out
2. **Bandwidth savings**: Current image is kept, avoiding unnecessary downloads
3. **Time savings**: No re-pulling on every launch
4. **Disk space management**: Old images (7+ days) are still cleaned up
5. **Better UX**: Clear messages about what's being removed and why

## Follow-up Items

- [ ] Monitor user feedback on 7-day retention period (may need adjustment)
- [ ] Consider adding a flag to customize retention period (e.g., `--cleanup-days 14`)

## Notes

- Version bumped from `2025-10-27.9` to `2025-10-28`
- All three files (standalone scripts + README) kept in sync
- Cleanup now runs AFTER pulling, so it won't remove the image that was just pulled
