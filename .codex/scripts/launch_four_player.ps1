param(
    [string]$PlayerPath = "$env:LOCALAPPDATA/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe",
    [switch]$DebugUi,
    [switch]$LocalTransport,
    [switch]$CaptureStartup,
    [int]$Width = 960,
    [int]$Height = 540
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $PlayerPath)) { throw "Build the development player first: $PlayerPath" }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runToken = [Guid]::NewGuid().ToString('N')
$runDirectory = Join-Path $projectRoot "output/manual-four-player/$((Get-Date).ToString('yyyyMMdd-HHmmss'))-$($runToken.Substring(0, 6))"
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$coordinationFile = Join-Path $runDirectory 'lobby-code.tmpdata'
$launched = @()
try {
    foreach ($index in 0..3) {
        $role = if ($index -eq 0) { 'host' } else { 'client' }
        $label = if ($index -eq 0) { 'host' } else { "client$index" }
        $arguments = @('-screen-fullscreen', '0', '-force-d3d11', '-screen-width', $Width,
            '-screen-height', $Height, '--az-visual-flow', '1', '--az-manual-play', '1',
            '--az-role', $role, '--az-run', $runToken, '--az-debug-ui', [int]$DebugUi.IsPresent,
            '--az-manual-capture', [int]$CaptureStartup.IsPresent,
            '--az-services-profile', "azm-$index-$($runToken.Substring(0, 8))", '-logFile', "`"$(Join-Path $runDirectory "$label.log")`"")
        if (-not $LocalTransport) {
            $arguments += @('--az-relay-flow', '1', '--az-coordination-file', "`"$coordinationFile`"")
        }
        # These four visible windows are the requested interactive players.
        $process = Start-Process -FilePath $PlayerPath -ArgumentList $arguments -WindowStyle Normal -PassThru
        $launched += [pscustomobject]@{ role = $label; processId = $process.Id; log = "$label.log" }
        if ($index -eq 0 -and -not $LocalTransport) {
            $deadline = (Get-Date).AddSeconds(65)
            while (-not (Test-Path -LiteralPath $coordinationFile)) {
                if ($process.HasExited) { throw "Host exited. See $runDirectory/host.log" }
                if ((Get-Date) -gt $deadline) { throw "Relay startup timed out. See $runDirectory/host.log" }
                Start-Sleep -Milliseconds 500
            }
        }
    }
} catch {
    # Only clean up this invocation's processes when launch itself fails.
    foreach ($entry in $launched) { Stop-Process -Id $entry.processId -ErrorAction SilentlyContinue }
    throw
}
[pscustomobject]@{ runDirectory = $runDirectory; relay = -not $LocalTransport; debugUi = $DebugUi.IsPresent; players = $launched } |
    ConvertTo-Json -Depth 4 | Tee-Object -FilePath (Join-Path $runDirectory 'launch.json')
Write-Host 'Four manual players launched. Use Alt+Tab or arrange the windows; Escape opens return-to-lobby.'
