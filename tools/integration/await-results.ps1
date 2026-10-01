# ============================================================================
# await-results.ps1
# ============================================================================
# PURPOSE: Wait for complete NUnit evidence while retaining the Unity lease.
#   Exact native run identity prevents stale results from satisfying a new gate.
# ARCHITECTURAL ROLE: Integration coordinator tooling; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Wait for atomic native completion or support legacy observer XML discovery.
#   - Heartbeat the lease and emit normalized leaf-status/result evidence.
# DEPENDENCIES: Common.ps1, TestResults.ps1 and native/validation observer output.
# USAGE NOTES: Exit 2 means no results; never releases a lease itself.
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][datetime]$Since,
    [int]$TimeoutMinutes = 120,
    [string]$OutJson = '',
    # Optional exact native-run identity; legacy observer runs remain supported.
    [string]$RunDirectory = '',
    [string]$RunId = ''
)
# Waits for UnityValidationTools to write a complete NUnit result XML newer than $Since,
# heartbeating the Unity lease meanwhile (in this script, not in model turns). Prints totals
# and failures, and writes a structured summary (with each failure's categories, including
# inherited fixture categories) to $OutJson. Exit 2 = no result; never releases the lease.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\TestResults.ps1"
if ([bool]$RunDirectory -ne [bool]$RunId) { throw 'RunDirectory and RunId must be supplied together.' }
$dir = if ($RunDirectory) { $RunDirectory } else { Join-Path $script:Main ((Get-Content (Join-Path $script:Main 'Logs\AgentValidation\active-output.txt') -Raw).Trim()) }
$deadline = (Get-Date).AddMinutes($TimeoutMinutes); $beat = Get-Date; $xml = $null
while ((Get-Date) -lt $deadline) {
    if ($RunId) {
        $complete = Join-Path $dir 'complete.json'
        if (Test-Path -LiteralPath $complete) {
            $marker = Get-Content -LiteralPath $complete -Raw | ConvertFrom-Json
            $expected = Join-Path $dir 'result-native.xml'
            if ($marker.run_id -cne $RunId -or [IO.Path]::GetFullPath($marker.xml) -ne [IO.Path]::GetFullPath($expected)) { throw 'Native completion identity mismatch.' }
            $xml = Get-Item -LiteralPath $expected
            if ($xml.LastWriteTime -le $Since) { throw 'Native result predates this request.' }
        }
    } else {
        $xml = Get-ChildItem -LiteralPath $dir -Filter 'result-*.xml' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -gt $Since } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($xml) { Start-Sleep -Seconds 3 }
    }
    if ($xml) { break }
    if (((Get-Date) - $beat).TotalSeconds -gt 45) { Invoke-LeaseCommand Heartbeat $Token | Out-Null; $beat = Get-Date }
    Start-Sleep -Seconds 10
}
if (-not $xml) { "TIMEOUT: no result XML after $TimeoutMinutes min; lease retained."; exit 2 }
$summary = Read-TestSummary $xml.FullName
if ($OutJson) { $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutJson -Encoding UTF8 }
"RESULT {0}" -f $xml.FullName
"total={0} passed={1} failed={2} skipped={3} inconclusive={4} result={5} duration={6}s" -f $summary.total, $summary.passed, $summary.failed, $summary.skipped, $summary.inconclusive, $summary.result, $summary.duration
foreach ($f in $summary.failures) { "FAILED {0}{1}: {2}" -f $f.name, $(if ($f.categories) { ' [' + ($f.categories -join ',') + ']' } else { '' }), $f.message }
foreach ($s in $summary.skippedNames) { "SKIPPED $s" }
foreach ($issue in $summary.issues) { "INVALID $issue" }
