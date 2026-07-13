# pre-compact-save.ps1
# PreCompact hook: capture working state before context compaction
# Outputs systemMessage so AI and user are aware of modified files

$input_json = [Console]::In.ReadToEnd()

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"

$modified = @()
try {
    $diff = git diff --name-only 2>$null
    if ($diff) { $modified += $diff }
    $staged = git diff --cached --name-only 2>$null
    if ($staged) { $modified += $staged }
} catch {}

$modified = $modified | Select-Object -Unique

if ($modified.Count -gt 0) {
    $fileList = ($modified | Select-Object -First 15) -join ", "
    $msg = "Pre-compaction snapshot ($timestamp). Modified files: $fileList. Update ACTIVE_CONTEXT.md with current work state before compaction."

    [Console]::Error.WriteLine("[Hook] Compaction at $timestamp - $($modified.Count) modified file(s)")
    foreach ($f in ($modified | Select-Object -First 10)) {
        [Console]::Error.WriteLine("  - $f")
    }

    $json = @{ systemMessage = $msg } | ConvertTo-Json -Compress
    Write-Output $json
} else {
    [Console]::Error.WriteLine("[Hook] Compaction at $timestamp - no modified files")
}

exit 0
