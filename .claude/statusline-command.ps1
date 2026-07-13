$input_json = [Console]::In.ReadToEnd() | ConvertFrom-Json

$modelName = $input_json.model.display_name
if (-not $modelName) { $modelName = "Claude" }

$ctx = $input_json.context_window
$usedPct = $ctx.used_percentage
$totalTokens = $ctx.total_input_tokens
$windowSize = $ctx.context_window_size

# ANSI color codes (dim-friendly for terminal)
$esc = [char]27
$dim = "$esc[2m"
$reset = "$esc[0m"
$cyan = "$esc[36m"
$yellow = "$esc[33m"
$red = "$esc[31m"
$green = "$esc[32m"

$parts = @()
$parts += "$dim$cyan$modelName$reset"

if ($null -ne $usedPct) {
    $usedPctRounded = [math]::Round($usedPct)

    if ($usedPctRounded -ge 80) {
        $pctColor = $red
    } elseif ($usedPctRounded -ge 50) {
        $pctColor = $yellow
    } else {
        $pctColor = $green
    }

    $parts += "$dim$pctColor$usedPctRounded% ctx$reset"
}

if ($null -ne $totalTokens -and $null -ne $windowSize) {
    $totalK = [math]::Round($totalTokens / 1000, 1)
    $windowK = [math]::Round($windowSize / 1000, 0)
    $parts += "$dim$($totalK)k/$($windowK)k tok$reset"
} elseif ($null -ne $totalTokens) {
    $totalK = [math]::Round($totalTokens / 1000, 1)
    $parts += "$dim$($totalK)k tok$reset"
}

$statusLine = $parts -join "$dim | $reset"
Write-Output $statusLine
