param(
    [Parameter(Mandatory = $true)][string]$Purpose,
    # Exact test full name or fixture prefix accepted by the in-editor runner; empty = full suite.
    [string]$TestName = '',
    [string]$Plan = 'PLAN-011',
    [int]$TimeoutMinutes = 150,
    [string]$OutJson = ''
)
# Standalone Edit Mode run in the open editor under the Unity lease (outside the gate), for
# diagnosis or a focused owner-present pass. Waits for the complete NUnit XML, prints totals and
# failures, and releases the lease only once the editor is idle again. Does not move any ref.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
$token = Enter-UnityLease $Plan $Purpose
"Lease acquired: $($token.Substring(0,8))..."
$safe = $false
try {
    Assert-UnityLease $token
    $s = Get-EditorState
    if (-not (Test-EditorIsMain $s) -or -not (Test-EditorIdle $s) -or $s.Failed) { throw "Editor not idle, compile failed, or wrong project: $($s | ConvertTo-Json -Compress)" }
    $since = Get-Date
    $name = $TestName.Replace('"', '')
    "Start: " + (Invoke-UnityCsharp ("return SynapticPro.TestRunner.NexusTestRunnerService.Execute(""run"", ""editmode"", ""$name"");"))
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'await-results.ps1') -Token $token -Since $since.ToString('o') -TimeoutMinutes $TimeoutMinutes -OutJson $OutJson
    if ($LASTEXITCODE -ne 0) { throw 'No results; lease retained.' }
    $s = Wait-EditorIdle $token 10
    if ($s) { $safe = $true; if ($s.TimeScale -ne 1) { "WARNING: Time.timeScale is $($s.TimeScale) after the run" } }
} finally {
    if ($safe) { Exit-UnityLease $token; 'Lease released.' } else { "LEASE RETAINED token=$token" }
}
