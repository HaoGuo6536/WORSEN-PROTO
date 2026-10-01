# ============================================================================
# run-tests.ps1
# ============================================================================
# PURPOSE: Run a standalone native fixture list in one asynchronous Unity run.
#   Preserve the legacy single-name/prefix diagnostic path without vendor edits.
# ARCHITECTURAL ROLE: Integration coordinator tooling; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Validate fixture-list input and acquire the exclusive Unity lease.
#   - Start one native run and collect exact completion/results evidence.
#   - Refuse missing coverage and release the lease only after editor idle.
# DEPENDENCIES: Common/TestSelection/TestResults, await-results, native Editor tool.
# USAGE NOTES: Coordinator only. No Git promotion; failures in native runs throw.
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Purpose,
    # Exact test full name or fixture prefix accepted by the in-editor runner; empty = full suite.
    [string]$TestName = '',
    [string[]]$Fixtures = @(),
    [string]$FixtureList = '',
    [string]$Plan = 'PLAN-011',
    [int]$TimeoutMinutes = 150,
    [string]$OutJson = ''
)
# Standalone Edit Mode run in the open editor under the Unity lease (outside the gate), for
# diagnosis or a focused owner-present pass. Waits for the complete NUnit XML, prints totals and
# failures, and releases the lease only once the editor is idle again. Does not move any ref.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\TestSelection.ps1"
. "$PSScriptRoot\TestResults.ps1"
if ($TestName -and ($FixtureList -or $PSBoundParameters.ContainsKey('Fixtures'))) { throw 'Use TestName or a fixture list, not both.' }
if ($FixtureList -and $PSBoundParameters.ContainsKey('Fixtures')) { throw 'Use Fixtures or FixtureList, not both.' }
if ($FixtureList) {
    $text = Get-Content -LiteralPath $FixtureList -Raw
    if ($text.TrimStart().StartsWith('{')) { $Fixtures = @(($text | ConvertFrom-Json).fixtures) }
    elseif ($text.TrimStart().StartsWith('[')) { $Fixtures = @($text | ConvertFrom-Json) }
    else { $Fixtures = @($text -split '\r?\n' | Where-Object { $_.Trim() }) }
}
if (($FixtureList -or $PSBoundParameters.ContainsKey('Fixtures')) -and $Fixtures.Count -eq 0) { throw 'Refusing an empty fixture list.' }
$request = $null
if (-not $TestName) {
    $scope = if ($Fixtures.Count -gt 0) { 'selected' } else { 'full' }
    $directory = Join-Path $script:Main ('Logs\AgentValidation\standalone\' + [guid]::NewGuid().ToString('N'))
    $request = New-NativeTestRequest $directory $scope $Fixtures
    if (-not $OutJson) { $OutJson = Join-Path $directory 'results.json' }
}
$token = Enter-UnityLease $Plan $Purpose
"Lease acquired: $($token.Substring(0,8))..."
$safe = $false
try {
    Assert-UnityLease $token
    $s = Get-EditorState
    if (-not (Test-EditorIsMain $s) -or -not (Test-EditorIdle $s) -or $s.Failed) { throw "Editor not idle, compile failed, or wrong project: $($s | ConvertTo-Json -Compress)" }
    $since = Get-Date
    if ($request) {
        "Start: " + (Invoke-UnityCsharp $request.code)
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'await-results.ps1') -Token $token -Since $since.ToString('o') -TimeoutMinutes $TimeoutMinutes -OutJson $OutJson -RunDirectory $request.directory -RunId $request.id
    } else {
        $name = $TestName.Replace('"', '')
        "Start: " + (Invoke-UnityCsharp ("return SynapticPro.TestRunner.NexusTestRunnerService.Execute(""run"", ""editmode"", ""$name"");"))
        & powershell -NoProfile -File (Join-Path $PSScriptRoot 'await-results.ps1') -Token $token -Since $since.ToString('o') -TimeoutMinutes $TimeoutMinutes -OutJson $OutJson
    }
    if ($LASTEXITCODE -ne 0) { throw 'No results; lease retained.' }
    $s = Wait-EditorIdle $token 10
    if (-not $s) { throw 'Results exist but the editor did not settle; lease retained.' }
    if ($s) { $safe = $true; if ($s.TimeScale -ne 1) { "WARNING: Time.timeScale is $($s.TimeScale) after the run" } }
    if ($request) {
        $results = Get-Content -LiteralPath $OutJson -Raw | ConvertFrom-Json
        $issues = @(Get-CoverageIssues $results $Fixtures)
        if ($results.total -le 0 -or $results.failed -gt 0 -or $issues.Count -gt 0) { throw "Standalone test run failed or incomplete: $($issues -join '; ')" }
    }
} finally {
    if ($safe) { Exit-UnityLease $token; 'Lease released.' } else { "LEASE RETAINED token=$token" }
}
