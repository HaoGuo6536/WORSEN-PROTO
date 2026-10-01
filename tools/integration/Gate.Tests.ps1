# ============================================================================
# Gate.Tests.ps1
# ============================================================================
# PURPOSE: Exercise verdict, carry-over, safety-net and NUnit evidence contracts
#   offline. Synthetic reports are test inputs, never reported as Unity evidence.
# ARCHITECTURAL ROLE: Integration tooling tests; no runtime layer.
# KEY RESPONSIBILITIES:
#   - Check full and partial ratchets, quarantine and status preservation.
#   - Check periodic selection policy and native request validation.
#   - Check NUnit leaf/suite coverage and generated-file drift detection.
# DEPENDENCIES: Gate.ps1, TestSelection.ps1, TestResults.ps1 and PowerShell.
# USAGE NOTES: Never contacts Unity; temporary test inputs are deleted in finally.
# ============================================================================
# Self-test for Gate.ps1 (plain PowerShell; run: powershell -NoProfile -File tools/integration/Gate.Tests.ps1).
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Gate.ps1"
. "$PSScriptRoot\TestSelection.ps1"
. "$PSScriptRoot\TestResults.ps1"
$failures = 0
function Check([string]$name, [bool]$ok) { if ($ok) { "PASS $name" } else { "FAIL $name"; $script:failures++ } }
function F([string]$n, [string[]]$c = @()) { [pscustomobject]@{ name = $n; categories = $c; message = '' } }
$today = [datetime]'2026-10-01'
$q = @(
    [pscustomobject]@{ match = 'category:RequiresFocus'; expires = '2026-12-31' },
    [pscustomobject]@{ match = 'Worsen.Tests.Flaky.*'; expires = '2026-12-31' },
    [pscustomobject]@{ match = 'Worsen.Tests.Old.*'; expires = '2026-09-01' }
)
$base = [pscustomobject]@{ label = 'b'; failed_names = @('A.x', 'A.y', 'Worsen.Tests.Old.z'); blocking_count = 3 }

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'Focus.t' @('RequiresFocus')), (F 'Worsen.Tests.Flaky.q')) }
$v = Get-GateVerdict $r $q $base $today
Check 'known failures below baseline pass' ($v.pass -and $v.blocking.Count -eq 1 -and $v.quarantined.Count -eq 2)

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'B.new')) }
$v = Get-GateVerdict $r $q $base $today
Check 'a new failure fails' (-not $v.pass -and $v.new -contains 'B.new')

$r = [pscustomobject]@{ total = 10; failures = @((F 'Worsen.Tests.Old.z')) }
$v = Get-GateVerdict $r $q $base $today
Check 'expired quarantine blocks but is known' ($v.pass -and $v.blocking -contains 'Worsen.Tests.Old.z')

$r = [pscustomobject]@{ total = 10; failures = @((F 'A.x'), (F 'A.y'), (F 'Worsen.Tests.Old.z')) }
$v = Get-GateVerdict $r $q ([pscustomobject]@{ failed_names = @('A.x', 'A.y', 'Worsen.Tests.Old.z'); blocking_count = 2 }) $today
Check 'ratchet: more blocking than baseline fails' (-not $v.pass)

$v = Get-GateVerdict ([pscustomobject]@{ total = 0; failures = @() }) $q $base $today
Check 'zero tests fail' (-not $v.pass)

$v = Get-GateVerdict ([pscustomobject]@{ total = 5; failures = @() }) $q $null $today
Check 'green with no baseline passes' ($v.pass)
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("gate-drift-" + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $tmp 'Assets/Resources/X') | Out-Null
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/a.asset') -Value 'a'
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/b.asset') -Value 'b'
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/c.png') -Value 'ignored'
    $s1 = Get-GeneratedSnapshot $tmp
    Check 'snapshot covers generated extensions only' ($s1.Count -eq 2 -and $s1.ContainsKey('Assets/Resources/X/a.asset'))
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/a.asset') -Value 'a2'
    Remove-Item -LiteralPath (Join-Path $tmp 'Assets/Resources/X/b.asset')
    Set-Content -LiteralPath (Join-Path $tmp 'Assets/Resources/X/d.prefab') -Value 'd'
    $d = Compare-GeneratedSnapshot $s1 (Get-GeneratedSnapshot $tmp)
    Check 'drift reports changed, added and removed' (($d.changed -join ',') -eq 'Assets/Resources/X/a.asset' -and ($d.added -join ',') -eq 'Assets/Resources/X/d.prefab' -and ($d.removed -join ',') -eq 'Assets/Resources/X/b.asset')
    $d = Compare-GeneratedSnapshot $s1 $s1
    Check 'identical snapshots show no drift' ($d.changed.Count + $d.added.Count + $d.removed.Count -eq 0)
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
function Case([string]$name, [string]$status, [bool]$blocking = $false) {
    [pscustomobject]@{ name = $name; result = $status; blocking = $blocking; fixture = $name.Substring(0, $name.LastIndexOf('.')); categories = @() }
}
function Result($cases) {
    [pscustomobject]@{ scope = 'selected'; total = @($cases).Count; cases = @($cases); issues = @()
        failures = @($cases | Where-Object { $_.result -eq 'Failed' }) }
}
$base = [pscustomobject]@{ failed_names = @('A.x', 'B.y'); blocking_count = 2
    test_statuses = @((Case 'A.x' 'Failed' $true), (Case 'B.y' 'Failed' $true), (Case 'C.z' 'Passed')) }
$r = Result @((Case 'C.z' 'Passed'))
$v = Get-GateVerdict $r @() $base $today
$next = Merge-TestBaseline $r @() $base $today
Check 'partial green does not clear omitted failures' ($v.pass -and $v.blocking.Count -eq 0 -and $next.failed_names.Count -eq 2 -and $next.blocking_count -eq 2)
$r = Result @((Case 'A.x' 'Passed'))
$next = Merge-TestBaseline $r @() $base $today
Check 'executed pass clears only its own baseline failure' ($next.failed_names.Count -eq 1 -and $next.failed_names -contains 'B.y' -and $next.test_statuses.Count -eq 3)
$r = Result @((Case 'A.x' 'Skipped'))
$next = Merge-TestBaseline $r @() $base $today
Check 'skipped retry cannot clear a known failure' ($next.failed_names.Count -eq 2 -and $next.blocking_count -eq 2)
$r = Result @((Case 'A.x' 'Inconclusive'))
Check 'inconclusive retry cannot clear a failure' ((Merge-TestBaseline $r @() $base $today).failed_names.Count -eq 2)
$r = Result @((Case 'C.z' 'Skipped'))
Check 'skipped retry preserves a known passing status too' ((Merge-TestBaseline $r @() $base $today).test_statuses[2].result -eq 'Passed')
$r = Result @((Case 'A.x' 'Failed'))
Check 'known partial failure passes without counting omitted tests as run' ((Get-GateVerdict $r @() $base $today).pass)
$r = Result @((Case 'C.z' 'Failed'))
Check 'previously passed test failing in partial run is new' (-not (Get-GateVerdict $r @() $base $today).pass)
$r = Result @((Case 'D.new' 'Failed'))
Check 'new partial failure cannot use omitted baseline budget' (-not (Get-GateVerdict $r @() $base $today).pass)
$r = Result @((Case 'C.z' 'Passed'))
$r.issues = @('fixture did not execute: B')
Check 'coverage mismatch blocks an otherwise green result' (-not (Get-GateVerdict $r @() $base $today).pass)
$r = Result @((Case 'C.z' 'Passed'))
Check 'legacy baseline requires a full migration run' (-not (Get-GateVerdict $r @() ([pscustomobject]@{ failed_names = @(); blocking_count = 0 }) $today).pass)
$baseQ = [pscustomobject]@{ failed_names = @('Worsen.Tests.Old.z'); blocking_count = 0
    test_statuses = @((Case 'Worsen.Tests.Old.z' 'Failed' $false)) }
$r = Result @((Case 'C.z' 'Passed'))
Check 'unrun expired quarantine is not reclassified as a current failure' ((Get-GateVerdict $r $q $baseQ $today).pass)
$r = Result @((Case 'Worsen.Tests.Old.z' 'Failed'))
Check 'executed expired quarantine increases ratchet and blocks' (-not (Get-GateVerdict $r $q $baseQ $today).pass)
$r.scope = 'full'
$next = Merge-TestBaseline $r $q $baseQ $today
Check 'full runs use the same carry-over semantics' ($next.blocking_count -eq 1)

$selection = [pscustomobject]@{ scope = 'selected'; fixtures = @('Worsen.Tests.X.Tests'); fixture_reasons = @{ 'Worsen.Tests.X.Tests' = @('changed') }; full_reasons = @() }
$promotions = @(1..3 | ForEach-Object { [pscustomobject]@{ label = "b$_"; promoted = $true; test_scope = $(if ($_ -eq 1) { 'full' } else { 'selected' }); test_statuses = @(); candidate_final = "hash$_" } })
Check 'auto normally selects' ((Resolve-TestSelection $selection 'auto' $promotions).scope -eq 'selected')
$promotions += [pscustomobject]@{ label = 'b4'; promoted = $true; test_scope = 'selected'; test_statuses = @(); candidate_final = 'hash4' }
$policy = Resolve-TestSelection $selection 'selected' $promotions
Check 'fifth promotion forces full even with selected request' ($policy.scope -eq 'full' -and $policy.promotion_number -eq 5 -and $policy.promotions_since_full.Count -eq 3)
$promotions += [pscustomobject]@{ label = 'failed-attempt'; promoted = $false }
Check 'failed attempts do not consume promotion intervals' ((Resolve-TestSelection $selection 'auto' $promotions).promotion_number -eq 5)
Check 'explicit full is honored' ((Resolve-TestSelection $selection 'full' @($promotions[0])).scope -eq 'full')
Check 'missing ledger starts full' ((Resolve-TestSelection $selection 'auto' @()).scope -eq 'full')
$selection.scope = 'full'; $selection.full_reasons = @('asmdef')
Check 'selected cannot override selector full' ((Resolve-TestSelection $selection 'selected' @($promotions[0])).scope -eq 'full')

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('gate-results-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $path = Join-Path $tmp 'result.xml'
    $xml = '<test-run total="2" passed="1" failed="1" skipped="0" inconclusive="0" result="Failed" duration="1.25"><test-suite type="TestFixture" fullname="Worsen.Tests.X.Tests" result="Failed"><properties><property name="Category" value="RequiresFocus"/></properties><test-case fullname="Worsen.Tests.X.Tests.A" result="Passed"/><test-case fullname="Worsen.Tests.X.Tests.B" result="Failed"><failure><message><![CDATA[broken]]></message></failure></test-case></test-suite></test-run>'
    [IO.File]::WriteAllText($path, $xml)
    $r = Read-TestSummary $path
    Check 'NUnit parser retains all leaves and inherited categories' ($r.total -eq 2 -and $r.cases.Count -eq 2 -and $r.failures[0].categories -contains 'RequiresFocus' -and $r.failures[0].message -eq 'broken' -and $r.issues.Count -eq 0)
    Check 'coverage accepts exact fixture identity' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Tests')).Count -eq 0)
    Check 'coverage rejects a missing fixture despite nonzero total' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Missing')).Count -eq 1)
    $r = Result @((Case 'Worsen.Tests.X.Tests.A' 'Passed'), (Case 'Worsen.Tests.Scenes.HorrorRunFogWiringTests.Ignored' 'Skipped'))
    Check 'existing explicit ignored fixture is evidence, not a missing fixture or pass' (@(Get-CoverageIssues $r @('Worsen.Tests.Scenes.HorrorRunFogWiringTests')).Count -eq 0 -and $r.cases[1].result -eq 'Skipped')
    $r = Result @((Case 'Worsen.Tests.X.Tests.A' 'Skipped'))
    Check 'all skipped is not an executed run' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Tests')) -contains 'no executed test cases')
    [IO.File]::WriteAllText($path, $xml.Replace('total="2"', 'total="3"'))
    Check 'count mismatch is invalid evidence' ((Read-TestSummary $path).issues -contains 'aggregate mismatch: total')
    [IO.File]::WriteAllText($path, '<test-run total="1" passed="1" failed="0" skipped="0" inconclusive="0" result="Failed" duration="1"><test-suite type="TestFixture" fullname="X" result="Failed"><test-case fullname="X.A" result="Passed"/></test-suite></test-run>')
    Check 'fixture teardown failure with passing leaves blocks' ((Read-TestSummary $path).issues.Count -gt 0)
    $request = New-NativeTestRequest $tmp 'selected' @('Worsen.Tests.X.Tests', 'Worsen.Tests.Y.Tests')
    $payload = Get-Content -LiteralPath $request.path -Raw | ConvertFrom-Json
    Check 'one native request carries the entire list and run identity' ($payload.fixtures.Count -eq 2 -and $payload.run_id -eq $request.id -and $request.code.Contains('NativeTestRunnerSetup.Run'))
    $caught = $false
    try { New-NativeTestRequest $tmp 'selected' @() | Out-Null } catch { $caught = $true }
    Check 'empty native selection is rejected' $caught
    $caught = $false
    try { New-NativeTestRequest $tmp 'selected' @('^Worsen.Tests.X.*') | Out-Null } catch { $caught = $true }
    Check 'regex is not accepted as a fixture name' $caught
    # Exercise the real waiter in its immediately-complete branch. No heartbeat,
    # lease operation, Unity process or HTTP request is performed by this test.
    $native = Join-Path $tmp 'native'
    New-Item -ItemType Directory -Path $native | Out-Null
    $nativeXml = Join-Path $native 'result-native.xml'
    [IO.File]::WriteAllText($nativeXml, $xml)
    @{ run_id = 'offline-test'; xml = $nativeXml } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $native 'complete.json')
    $outJson = Join-Path $tmp 'waiter.json'
    $waitOutput = & powershell -NoProfile -File (Join-Path $PSScriptRoot 'await-results.ps1') -Token 'offline-no-lease' -Since '2000-01-01' -RunDirectory $native -RunId 'offline-test' -OutJson $outJson
    $waitExit = $LASTEXITCODE
    $waitSummary = Get-Content -LiteralPath $outJson -Raw | ConvertFrom-Json
    Check 'actual waiter parses only the exact completed native result' ($waitExit -eq 0 -and $waitSummary.total -eq 2 -and @($waitOutput | Where-Object { $_ -like 'RESULT *' }).Count -eq 1)
} finally { Remove-Item -LiteralPath $tmp -Recurse -Force -ErrorAction SilentlyContinue }
"GATE_TEST_RESULT failures=$failures"
if ($failures -gt 0) { exit 1 }
