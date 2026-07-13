# post-edit-check.ps1
# Claude Code PostToolUse hook: sync alerts after editing core files
# Exit 0 = always succeed (warnings only, never blocks)

$input_json = [Console]::In.ReadToEnd()
$data = $input_json | ConvertFrom-Json

$filePath = $data.tool_input.file_path
if (-not $filePath) { exit 0 }

$filename = Split-Path $filePath -Leaf
$alerts = @()

switch -Regex ($filename) {
    'AbsoluteZeroTurnManager\.cs' {
        $alerts += "[SYNC] AbsoluteZeroTurnManager modified -> Check AZDemoUI sync (turn state display)"
        $alerts += "       Update Docs/GAME_SYSTEMS.md if turn flow changed"
    }
    'AZDemoUI\.cs' {
        $alerts += "[SYNC] AZDemoUI modified -> Verify AbsoluteZeroTurnManager state references"
    }
    'NetworkManager|RelayManager|SessionManager|LobbyManager' {
        $alerts += "[SYNC] Network infrastructure modified -> Update Docs/NETWORK_ARCHITECTURE.md"
    }
}

if ($alerts.Count -gt 0) {
    $msg = $alerts -join "`n"
    [Console]::Error.WriteLine("=== Post-Edit Sync Alert ===`n$msg")
}

exit 0
