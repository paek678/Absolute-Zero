param(
    [Parameter(Mandatory=$true)][ValidateSet('transitions','replay','corpus')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [int]$Seed = 3609,
    [ValidateSet('late-fan','mixed')][string]$HumanPolicy = 'late-fan',
    [ValidateRange(30,600)][int]$MatchTimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$runDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $runDirectory) { throw "Use a fresh evidence directory: $runDirectory" }
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$executable = Join-Path $env:TEMP 'AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build the fixed test player first.' }
$token = [guid]::NewGuid().ToString('N').Substring(0,8)
$processes = @()
try {
    $peerCount = if ($Mode -eq 'transitions') { 4 } else { 1 }
    for ($peer = 0; $peer -lt $peerCount; $peer++) {
        $argumentText = '-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -force-d3d11 --plan036-output "' + $runDirectory + '" -logFile "' + (Join-Path $runDirectory ('peer' + $peer + '.log')) + '" '
        if ($Mode -eq 'transitions') {
            $argumentText += '--plan036-mode-transitions --plan036-peer ' + $peer + ' --az-services-profile azp36-' + $peer + '-' + $token
        } else {
            $argumentText += '--plan036-full-match --plan036-seed ' + $Seed + ' --plan036-human-policy ' + $HumanPolicy + ' --plan036-match-timeout ' + $MatchTimeoutSeconds
            if ($Mode -eq 'corpus') { $argumentText += ' --plan036-corpus' }
        }
        $processes += Start-Process -FilePath $executable -ArgumentList $argumentText -WindowStyle Hidden -PassThru
    }
    $processes | Select-Object Id,StartTime | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'processes.json')
    $budgetSeconds = if ($Mode -eq 'transitions') { 720 } elseif ($Mode -eq 'replay') { 4 * $MatchTimeoutSeconds + 180 } else { $MatchTimeoutSeconds + 180 }
    $deadline = (Get-Date).AddSeconds($budgetSeconds)
    while (($processes | Where-Object { -not $_.HasExited }).Count -gt 0 -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
} finally {
    foreach ($ownedProcess in $processes) {
        if (-not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id; $ownedProcess.WaitForExit() }
    }
    $processes | Select-Object Id,ExitCode | ConvertTo-Json | Set-Content (Join-Path $runDirectory 'exit.json')
}
$reports = @(Get-ChildItem -LiteralPath $runDirectory -Filter '*report.json' | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$passed = $reports.Count -eq $peerCount -and ($reports | Where-Object { -not $_.passed }).Count -eq 0 -and ($processes | Where-Object { $_.ExitCode -ne 0 }).Count -eq 0
$summary = [pscustomobject]@{ passed=$passed; mode=$Mode; seed=$Seed; humanPolicy=$HumanPolicy; matchTimeoutSeconds=$MatchTimeoutSeconds; reports=$reports.Count; checks=($reports | ForEach-Object { $_.checks.Count } | Measure-Object -Sum).Sum; failures=@($reports | Where-Object { -not $_.passed } | ForEach-Object { $_.failure }) }
$summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runDirectory 'summary.json')
$summary | ConvertTo-Json -Depth 5
if (-not $passed) { exit 1 }
