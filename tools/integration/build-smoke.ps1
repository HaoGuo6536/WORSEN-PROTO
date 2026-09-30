param(
    [string]$Label = 'manual',
    [string]$Plan = 'PLAN-011',
    [int]$BuildTimeoutMinutes = 40,
    [int]$SmokeSeconds = 25
)
# Player build plus a short smoke run, reported (not yet blocking) after each promoted batch.
#  1. Under the Unity lease: Worsen.Editor.Horror.HorrorBuildValidation.QueueBuild (Mono,
#     StandaloneWindows64, HorrorRun), then wait for its completed.json.
#  2. Lease released; launch the player headless (-batchmode -nographics) for $SmokeSeconds,
#     then stop it and scan its log for exceptions.
# Appends a record to evidence/build-ledger.jsonl.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\Gate.ps1"
$record = [ordered]@{ label = $Label; started = (Get-Date).ToString('o'); commit = ((Invoke-Git $script:Main rev-parse HEAD) | Select-Object -Last 1) }
$token = Enter-UnityLease $Plan "Build + smoke $Label"
$safe = $false; $output = $null
try {
    Assert-UnityLease $token
    $s = Get-EditorState
    if (-not (Test-EditorIsMain $s) -or -not (Test-EditorIdle $s) -or $s.Failed -or $s.Dirty) { throw "Editor not ready for a build: $($s | ConvertTo-Json -Compress)" }
    $output = Invoke-UnityCsharp 'var t = System.Type.GetType("Worsen.Editor.Horror.HorrorBuildValidation, Worsen.Editor"); string r = (string)t.GetMethod("QueueBuild").Invoke(null, null); return r;'
    "Build queued: $output"
    $done = Join-Path $output 'completed.json'
    $deadline = (Get-Date).AddMinutes($BuildTimeoutMinutes); $beat = Get-Date
    while (-not (Test-Path -LiteralPath $done) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 10
        if (((Get-Date) - $beat).TotalSeconds -gt 45) { Invoke-LeaseCommand Heartbeat $token | Out-Null; $beat = Get-Date }
    }
    if (-not (Test-Path -LiteralPath $done)) { throw "Build did not finish within $BuildTimeoutMinutes min; lease retained." }
    $build = Get-Content -LiteralPath $done -Raw | ConvertFrom-Json
    $record.build = [ordered]@{ result = $build.result; errors = $build.errors; warnings = $build.warnings; seconds = $build.durationSeconds; bytes = $build.bytes }
    "Build: $($build.result) errors=$($build.errors) warnings=$($build.warnings) $([math]::Round($build.durationSeconds))s"
    $s = Wait-EditorIdle $token 10
    if ($s) { $safe = $true }
} finally {
    if ($safe) { Exit-UnityLease $token } else { "LEASE RETAINED token=$token" }
}
if ($record.build.result -eq 'Succeeded') {
    $exe = Join-Path $output 'WORSEN.exe'; $log = Join-Path $output 'smoke-player.log'
    $p = Start-Process -FilePath $exe -ArgumentList @('-batchmode', '-nographics', '-logFile', "`"$log`"") -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds $SmokeSeconds
    $alive = -not $p.HasExited
    if ($alive) { Stop-Process -Id $p.Id -Force }
    $text = if (Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Raw } else { '' }
    $exceptions = ([regex]::Matches($text, '(?m)^\w*Exception[:\s]|NullReferenceException|Crash!!!')).Count
    $record.smoke = [ordered]@{ ran_seconds = $SmokeSeconds; alive_at_end = $alive; exit = $(if ($alive) { $null } else { $p.ExitCode }); exceptions = $exceptions; log = $log }
    "Smoke: alive=$alive exceptions=$exceptions log=$log"
}
$record.ended = (Get-Date).ToString('o')
Add-LedgerEntry (Join-Path $script:Main 'evidence\build-ledger.jsonl') $record
