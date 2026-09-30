# ============================================================================
# Test-PureRunner.ps1
# ============================================================================
# PURPOSE:
#   Validates the deliberately red harness fixture against exact expected results.
#   A failing runner exit is expected; this script returns success only if every
#   classification, timeout continuation and evidence accounting check matches.
# ARCHITECTURAL ROLE: Offline verification test tool; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Run the self-test assembly independently of Worsen.Tests.
#   - Assert exact named case outcomes and namespace/fixture accounting.
#   - Check filtering, empty selection and evidence overwrite refusal.
# DEPENDENCIES: Run-PureTests.ps1 and successful compile evidence.
# USAGE NOTES: All outputs remain under a new validation run; no Unity invocation.
# ============================================================================
param(
    [Parameter(Mandatory = $true)][string]$Worktree,
    [Parameter(Mandatory = $true)][string]$CompileRun,
    [Parameter(Mandatory = $true)][string]$RunName
)
$ErrorActionPreference = 'Stop'
$Worktree = (Resolve-Path -LiteralPath $Worktree).Path
$runner = Join-Path $PSScriptRoot 'Run-PureTests.ps1'
$root = Join-Path $Worktree "Logs/AgentValidation/PLAN-002/offline-compile/$RunName"
& powershell -NoProfile -File $runner -Worktree $Worktree -CompileRun $CompileRun -RunName $RunName -SelfTest -TimeoutSeconds 1 |
    Set-Variable -Name captured
if ($LASTEXITCODE -ne 1) { throw "Expected intentionally failing self-test exit 1; got $LASTEXITCODE. $captured" }
$summaryPath = Join-Path $root 'summary.json'
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
$expected = @{}
foreach ($name in @('Pass', 'Expected(2)', 'Field(3)', 'Property(4)', 'Method(5)', 'Values(1)', 'Values(2)', 'ZAfterTimeout', 'ClassifierBoundaries')) {
    $expected["OfflineHarness.SelfTest.Cases.$name"] = 'passed'
}
foreach ($name in @('AssertionFail', 'OrdinaryNull', 'OrdinaryMissingMethod', 'OrdinarySecurity', 'MissingFile', 'Timeout', 'WrongExpected(2)')) {
    $expected["OfflineHarness.SelfTest.Cases.$name"] = 'failed'
}
foreach ($name in @('Engine', 'Editor')) { $expected["OfflineHarness.SelfTest.Cases.$name"] = 'environment' }
foreach ($name in @('Ignored', 'Explicit', 'Focus', 'Coroutine')) { $expected["OfflineHarness.SelfTest.Cases.$name"] = 'skipped' }
foreach ($name in @('BadOneTimeSetup.Never', 'BadOneTimeTeardown.Pass', 'MixedFailure.Fail', 'TimeoutOneTimeSetup.Never',
    'TimeoutOneTimeTeardown.Pass', 'TimeoutSetup.Never', 'TimeoutTeardown.Pass', 'MixedOneTimeEngineTeardown.Fail', 'MixedOneTimeAssertionTeardown.Engine', 'BadSource.Never')) {
    $expected["OfflineHarness.SelfTest.$name"] = 'failed'
}
foreach ($name in @('EngineOneTimeSetup.Never', 'EngineOneTimeTeardown.Pass', 'EngineSource.Never')) { $expected["OfflineHarness.SelfTest.$name"] = 'environment' }
foreach ($name in @('IgnoredFixture.Never', 'ExplicitFixture.Never', 'FocusFixture.Never', 'UnityLifecycle.Never')) { $expected["OfflineHarness.SelfTest.$name"] = 'skipped' }
if ($summary.ExitCode -ne 1 -or @($summary.InfrastructureErrors).Count -ne 0 -or $summary.Total -ne $expected.Count -or
    @($summary.Cases).Count -ne $expected.Count -or -not $summary.SelfTest) { throw 'Self-test summary is incomplete.' }
$seen = @{}
foreach ($case in $summary.Cases) {
    if ($seen.ContainsKey($case.Name) -or -not $expected.ContainsKey($case.Name) -or $case.Outcome -ne $expected[$case.Name]) {
        throw "Unexpected classification: $($case.Name) = $($case.Outcome); expected $($expected[$case.Name])"
    }
    $seen[$case.Name] = $true
}
foreach ($outcome in @('passed', 'failed', 'environment', 'skipped')) {
    $count = @($summary.Cases | Where-Object { $_.Outcome -eq $outcome }).Count
    if ($count -ne $summary.Totals.$outcome -or $count -ne $summary.Namespaces.'OfflineHarness.SelfTest'.$outcome) { throw "Bad $outcome accounting." }
    $fixtureTotal = ($summary.Fixtures.PSObject.Properties.Value | ForEach-Object { $_.$outcome } | Measure-Object -Sum).Sum
    if ($fixtureTotal -ne $count) { throw "Bad fixture $outcome accounting." }
}
$timeouts = @($summary.Cases | Where-Object { $_.Message -like '*timeout after 1000 ms*' })
if ($timeouts.Count -ne 5) { throw 'Missing timeout/lifecycle evidence.' }
$hash = (Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash
& powershell -NoProfile -File $runner -Worktree $Worktree -CompileRun $CompileRun -RunName $RunName | Out-Null
if ($LASTEXITCODE -ne 2 -or (Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash -ne $hash) { throw 'Overwrite refusal failed.' }
foreach ($probe in @(
    @{ Suffix = 'substring'; Filter = 'Cases.Pass'; Expected = 1 },
    @{ Suffix = 'regex'; Filter = 'Cases[.](Pass|Expected[(]2[)])$'; Expected = 2 },
    @{ Suffix = 'explicit'; Filter = 'Cases.Explicit'; Expected = 0 },
    @{ Suffix = 'empty'; Filter = 'NoSuchFixture_HeadlessProbe'; Expected = 0 }
)) {
    $probeName = "$RunName-$($probe.Suffix)"
    & powershell -NoProfile -File $runner -Worktree $Worktree -CompileRun $CompileRun -RunName $probeName -SelfTest -Filter $probe.Filter | Out-Null
    $probeExit = $LASTEXITCODE
    $probeSummary = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $root) "$probeName/summary.json") -Raw | ConvertFrom-Json
    $wantExit = if ($probe.Suffix -eq 'empty') { 2 } else { 0 }
    if ($probeExit -ne $wantExit -or $probeSummary.Totals.passed -ne $probe.Expected -or $probeSummary.Totals.failed -ne 0) {
        throw "Filter probe failed: $($probe.Suffix)"
    }
    if ($probe.Suffix -eq 'explicit' -and $probeSummary.Totals.skipped -ne 1) { throw 'Explicit filter forced execution.' }
    if ($probe.Suffix -eq 'empty' -and $probeSummary.Total -ne 0) { throw 'Empty filter selected tests.' }
}
$children = @(Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" | Where-Object { $_.CommandLine -like "*$RunName*ManagedPureRunner*" })
if ($children.Count -ne 0) { throw 'Harness left a child process running.' }
$verified = [ordered]@{ Passed = $true; Cases = $expected.Count; Totals = $summary.Totals; Timeouts = 5;
    Checks = @('exact classifications', 'inherited lifecycle', 'one-time failure precedence', 'five timeout paths and continuation',
        'namespace/fixture totals', 'overwrite refusal', 'substring filter', 'regex filter', 'explicit exclusion', 'empty-selection exit', 'no surviving child');
    SummarySHA256 = $hash }
$verified | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'self-test-verification.json') -Encoding UTF8
"SELF_TEST_OK cases=$($expected.Count) passed=$($summary.Totals.passed) failed=$($summary.Totals.failed) environment=$($summary.Totals.environment) skipped=$($summary.Totals.skipped) timeouts=5"
