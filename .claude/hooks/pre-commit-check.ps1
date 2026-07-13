# pre-commit-check.ps1
# Claude Code PreToolUse hook: safety rule violation check before git commit
# Exit 0 = allow, Exit 2 = block (stderr message sent to Claude as reason)

$input_json = [Console]::In.ReadToEnd()
$data = $input_json | ConvertFrom-Json

$command = $data.tool_input.command
if ($command -notmatch 'git\s+commit') { exit 0 }

$violations = @()
$warnings = @()

$staged = git diff --cached --name-only --diff-filter=ACM 2>$null | Where-Object { $_ -match '\.cs$' }

if (-not $staged) { exit 0 }

foreach ($file in $staged) {
    if (-not (Test-Path $file)) { continue }
    $content = Get-Content $file -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }
    $filename = Split-Path $file -Leaf

    # NetworkVariable direct assignment outside Server/Host context
    $netVarAssign = [regex]::Matches($content, '\.Value\s*=(?!=)')
    if ($netVarAssign.Count -gt 0 -and $content -notmatch 'IsServer|IsHost') {
        $warnings += "NET-WARN [WARN] $file : NetworkVariable.Value assignment without IsServer/IsHost check ($($netVarAssign.Count) occurrences)"
    }

    # Debug.Log left in production code (not in #if DEBUG)
    $debugLogs = [regex]::Matches($content, 'Debug\.Log\(')
    if ($debugLogs.Count -gt 3) {
        $warnings += "PERF-WARN [WARN] $file : Excessive Debug.Log() calls ($($debugLogs.Count)) - consider removing or wrapping in #if DEBUG"
    }

    # new WaitForSeconds without caching
    $wfsMatches = [regex]::Matches($content, 'new\s+WaitForSeconds\s*\(')
    if ($wfsMatches.Count -gt 0) {
        $warnings += "PERF-WARN [WARN] $file : new WaitForSeconds() $($wfsMatches.Count) occurrences - cache as static readonly"
    }
}

if ($warnings.Count -gt 0) {
    $warningMsg = $warnings -join "`n"
    [Console]::Error.WriteLine("=== Safety Warnings ===`n$warningMsg")
}

if ($violations.Count -gt 0) {
    $violationMsg = $violations -join "`n"
    [Console]::Error.WriteLine("=== Safety Violations (BLOCKED) ===`n$violationMsg")
    exit 2
}

exit 0
