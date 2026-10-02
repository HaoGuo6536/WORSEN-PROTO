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
# Batch 37: a fixture that ran now defines its leaves; its renamed or deleted old leaves are dropped,
# while fixtures that did not run keep their recorded failures.
$renamedBase = [pscustomobject]@{ failed_names = @('A.old', 'B.y'); blocking_count = 2
    test_statuses = @((Case 'A.old' 'Failed' $true), (Case 'B.y' 'Failed' $true)) }
$next = Merge-TestBaseline (Result @((Case 'A.new' 'Passed'))) @() $renamedBase $today
Check 'rerun fixture drops its renamed leaves but keeps failures of other fixtures' ($next.failed_names.Count -eq 1 -and $next.failed_names -contains 'B.y' -and -not ($next.test_statuses.name -contains 'A.old'))
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
Check 'unrun expired quarantine blocks under current policy' (-not (Get-GateVerdict $r $q $baseQ $today).pass)
$r = Result @((Case 'Worsen.Tests.Old.z' 'Failed'))
Check 'executed expired quarantine increases ratchet and blocks' (-not (Get-GateVerdict $r $q $baseQ $today).pass)
$r.scope = 'full'
$next = Merge-TestBaseline $r $q $baseQ $today
Check 'full runs use the same carry-over semantics' ($next.blocking_count -eq 1)

$selection = [pscustomobject]@{ scope = 'selected'; fixtures = @('Worsen.Tests.X.Tests'); fixture_reasons = @{ 'Worsen.Tests.X.Tests' = @('changed') }; full_reasons = @() }
$gates = @(1..3 | ForEach-Object { [pscustomobject]@{ label = "b$_"; promoted = $false; test_run_id = "run$_"; test_scope = $(if ($_ -eq 1) { 'full' } else { 'selected' }); candidate_final = "hash$_" } })
$tested = [pscustomobject]@{ run_id = 'run1'; test_statuses = @((Case 'A.x' 'Failed'), (Case 'B.y' 'Passed')) }
Check 'complete rejected full baseline enables selection' ((Resolve-TestSelection $selection 'auto' $gates 5 $tested).scope -eq 'selected')
$gates += [pscustomobject]@{ label = 'b4'; promoted = $false; test_run_id = 'run4'; test_scope = 'selected'; candidate_final = 'hash4' }
$policy = Resolve-TestSelection $selection 'selected' $gates 5 $tested
Check 'rejected native attempts never force a periodic full suite' ($policy.scope -eq 'selected' -and $policy.gate_number -eq 5 -and $policy.gates_since_full.Count -eq 0)
$gates += [pscustomobject]@{ label = 'failed-precheck'; promoted = $false }
Check 'offline-only failures do not consume native intervals' ((Resolve-TestSelection $selection 'auto' $gates 5 $tested).gate_number -eq 5)
$gates += [pscustomobject]@{ label = 'failed-full'; test_run_id = 'run5'; test_scope = 'full'; promoted = $false }
Check 'an incomplete full run does not reset the promotion count' ((Resolve-TestSelection $selection 'auto' $gates 5 $tested).scope -eq 'selected')
$promoted = @($gates) + @(1..5 | ForEach-Object { [pscustomobject]@{ label = "p$_"; promoted = $true; test_run_id = "prun$_"; test_scope = 'selected'; candidate_final = "phash$_" } })
$policy = Resolve-TestSelection $selection 'auto' $promoted 5 $tested
Check 'five promotions since the reused full run force a full suite' ($policy.scope -eq 'full' -and $policy.gates_since_full.Count -eq 5)
Check 'explicit full is honored' ((Resolve-TestSelection $selection 'full' @($gates[0]) 5 $tested).scope -eq 'full')
Check 'missing ledger starts full' ((Resolve-TestSelection $selection 'auto' @()).scope -eq 'full')
$selection.scope = 'full'; $selection.full_reasons = @('asmdef')
Check 'selected cannot override selector full' ((Resolve-TestSelection $selection 'selected' @($gates[0]) 5 $tested).scope -eq 'full')

$r = Result @((Case 'B.y' 'Passed'))
$v = Get-GateVerdict $r @() $null $today $tested
Check 'green selected run cannot forgive rejected full failures' (-not $v.pass -and $v.blocking -contains 'A.x')
$v = Get-GateVerdict $r @() ([pscustomobject]@{ failed_names = @('A.x'); blocking_count = 1 }) $today $tested
Check 'even previously known unselected candidate failures block' (-not $v.pass)
$live = @([pscustomobject]@{ match = 'A.x'; expires = '2026-12-31' })
Check 'live quarantine applies to unselected failure' ((Get-GateVerdict $r $live $null $today $tested).pass)
Check 'expired quarantine applies to unselected failure' (-not (Get-GateVerdict $r $live $null ([datetime]'2027-01-01') $tested).pass)
foreach ($status in @('Skipped', 'Inconclusive')) {
    Check "$status retry preserves rejected full failure" (-not (Get-GateVerdict (Result @((Case 'A.x' $status))) @() $null $today $tested).pass)
}
$r = Result @((Case 'A.x' 'Passed'))
Check 'fresh passing retry clears rejected full failure' ((Get-GateVerdict $r @() $null $today $tested).pass)
$composed = Merge-TestedResults $r @() $tested $today
Check 'composition preserves fixture identity and native totals' ($composed.total -eq 2 -and $r.total -eq 1 -and $composed.cases[1].fixture -eq 'B')
Check 'composition does not mutate baseline' ($tested.test_statuses[0].result -eq 'Failed')
$oldSetup = [pscustomobject]@{ setup_snapshot = [pscustomobject]@{ a = 'one'; removed = 'old' }; setup_drift_paths = @('irrelevant') }
$paths = @(Get-SelectionSetupPaths $oldSetup @{ a = 'two'; added = 'new' } @('irrelevant'))
Check 'between-run setup hash changes additions deletions are selected' (($paths | Sort-Object) -join ',' -eq 'a,added,removed')
Check 'identical setup hashes do not repeat old drift' (@(Get-SelectionSetupPaths $oldSetup @{ a = 'one'; removed = 'old' } @('a')).Count -eq 0)
$paths = @(Get-SelectionSetupPaths ([pscustomobject]@{ setup_drift_paths = @('a', 'b') }) @{} @('b', 'c'))
Check 'legacy setup paths are unioned not subtracted' (($paths -join ',') -eq 'a,b,c')

$tmp = Join-Path ([IO.Path]::GetTempPath()) ('gate-results-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $tmp | Out-Null
    $path = Join-Path $tmp 'result.xml'
    $xml = '<test-run total="2" passed="1" failed="1" skipped="0" inconclusive="0" result="Failed" duration="1.25"><test-suite type="TestFixture" fullname="Worsen.Tests.X.Tests" result="Failed"><properties><property name="Category" value="RequiresFocus"/></properties><test-case fullname="Worsen.Tests.X.Tests.A" result="Passed"/><test-case fullname="Worsen.Tests.X.Tests.B" result="Failed"><failure><message><![CDATA[broken]]></message></failure></test-case></test-suite></test-run>'
    [IO.File]::WriteAllText($path, $xml)
    $r = Read-TestSummary $path
    Check 'NUnit parser retains all leaves and inherited categories' ($r.total -eq 2 -and $r.cases.Count -eq 2 -and $r.failures[0].categories -contains 'RequiresFocus' -and $r.failures[0].message -eq 'broken' -and $r.issues.Count -eq 0)
    Check 'complete failed full evidence is reusable' (@(Get-CompleteEvidenceIssues $r @('Worsen.Tests.X.Tests')).Count -eq 0)
    $r.total = 3
    Check 'stored full evidence rejects missing leaves' (@(Get-CompleteEvidenceIssues $r @('Worsen.Tests.X.Tests')).Count -gt 0)
    $r = Read-TestSummary $path
    $r.cases[1].name = $r.cases[0].name
    Check 'stored full evidence rejects duplicate leaves' (@(Get-CompleteEvidenceIssues $r @('Worsen.Tests.X.Tests')).Count -gt 0)
    $r = Read-TestSummary $path
    $r.failed = 0
    Check 'stored full evidence rejects lying aggregates' (@(Get-CompleteEvidenceIssues $r @('Worsen.Tests.X.Tests')).Count -gt 0)
    $r = Read-TestSummary $path
    $r.result = 'Cancelled'
    Check 'stored full evidence rejects incomplete run' (@(Get-CompleteEvidenceIssues $r @('Worsen.Tests.X.Tests')).Count -gt 0)
    $r = Read-TestSummary $path
    # Mock only Git inventory discovery; synthetic NUnit evidence stays local.
    $script:mockReuse = $true
    function Invoke-Native {
        $script:NativeExit = 0
        $json = '{"schema_version":1,"scope":"selected","candidate":"c0","all_fixtures":["Worsen.Tests.X.Tests"],"fixtures":["Worsen.Tests.X.Tests"],"fixture_reasons":{"Worsen.Tests.X.Tests":["smoke"]},"full_reasons":[],"base":"c0","baseline_reused":true,"baseline_rule":"test ancestry"}'
        if (-not $script:mockReuse) { $json = $json.Replace('"baseline_reused":true', '"baseline_reused":false') }
        $json
    }
    $entry = @{ label = 'rejected-full'; test_scope = 'full'; test_run_id = 'full-id'; candidate_final = 'c0'
        setup_ok = $true; compile_ok = $true; coverage_issues = @(); promoted = $false
        full_native_results = $r; setup_snapshot = @{}; setup_drift_paths = @() }
    $ledger = Join-Path $tmp 'ledger.jsonl'
    Add-LedgerEntry $ledger $entry
    $rows = @(Get-Content -LiteralPath $ledger | ForEach-Object { $_ | ConvertFrom-Json })
    $b = Get-CompleteFullBaseline $tmp $rows
    Check 'ledger round trip bootstraps non-promoted full with provenance' ($b.label -eq 'rejected-full' -and $b.run_id -eq 'full-id' -and $b.test_statuses.Count -eq 2 -and $b.test_statuses[1].fixture -eq 'Worsen.Tests.X.Tests')
    $entry.test_statuses = @((Case 'Removed.Tests.OldFailure' 'Failed'))
    $bDebt = Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)
    Check 'a newer full anchor cannot erase deleted historical failure debt' ($bDebt.test_statuses.name -contains 'Removed.Tests.OldFailure')
    $entry.test_statuses = @((Case 'Worsen.Tests.X.Tests.A' 'Failed'))
    $bDebt = Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)
    Check 'explicit full pass clears historical failure debt' (@($bDebt.test_statuses | Where-Object { $_.name -eq 'Worsen.Tests.X.Tests.A' })[0].result -eq 'Passed')
    $entry.test_statuses = @()
    $s = Get-CandidateTestSelection $tmp main c1 $ledger auto 5 (Join-Path $tmp 'selection.json') @{} @()
    Check 'selection wrapper uses full baseline rather than promotion status' ($s.scope -eq 'selected' -and $s.base -eq 'c0' -and $s.tested_baseline.run_id -eq 'full-id')
    $script:mockReuse = $false
    $s = Get-CandidateTestSelection $tmp main unrelated $ledger auto 5 (Join-Path $tmp 'unrelated.json') @{} @()
    Check 'incompatible candidate cannot reuse evidence for a selected verdict' ($s.scope -eq 'full' -and $null -eq $s.tested_baseline)
    $script:mockReuse = $true
    $entry.test_scope = 'selected'
    Check 'selected composite cannot bootstrap full coverage' ($null -eq (Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)))
    $entry.test_scope = 'full'; $entry.full_native_results.issues = @('suite-only failure')
    Check 'bad full run falls back to previous complete full' ((Get-CompleteFullBaseline $tmp @($rows[0], [pscustomobject]$entry)).label -eq 'rejected-full')
    $entry.full_native_results = $null; $entry.test_statuses = $rows[0].full_native_results.cases; $entry.evidence = $tmp
    Check 'cumulative statuses alone cannot prove full coverage' ($null -eq (Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)))
    # Exercise legacy result-file bootstrap with an exact completion/request pair.
    $nativeDir = Join-Path $tmp 'native-full-id'
    New-Item -ItemType Directory -Path $nativeDir | Out-Null
    $nativePath = Join-Path $nativeDir 'result-native.xml'
    [IO.File]::WriteAllText($nativePath, $xml)
    @{ run_id = 'full-id'; scope = 'full'; xml = $nativePath } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $nativeDir 'complete.json')
    @{ run_id = 'full-id'; scope = 'full' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tmp 'request-full-id.json')
    Check 'legacy matching completed XML bootstraps rejected full' ((Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)).source -eq $nativePath)
    @{ run_id = 'wrong-id'; scope = 'full'; xml = $nativePath } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $nativeDir 'complete.json')
    Check 'mismatched completion cannot bootstrap' ($null -eq (Get-CompleteFullBaseline $tmp @([pscustomobject]$entry)))
    $r = Read-TestSummary $path
    Check 'coverage accepts exact fixture identity' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Tests')).Count -eq 0)
    Check 'coverage rejects a missing fixture despite nonzero total' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Missing')).Count -eq 1)
    $r = Result @((Case 'Worsen.Tests.X.Tests.A' 'Passed'), (Case 'Worsen.Tests.Scenes.HorrorRunFogWiringTests.Ignored' 'Skipped'))
    Check 'existing explicit ignored fixture is evidence, not a missing fixture or pass' (@(Get-CoverageIssues $r @('Worsen.Tests.Scenes.HorrorRunFogWiringTests')).Count -eq 0 -and $r.cases[1].result -eq 'Skipped')
    $r = Result @((Case 'Worsen.Tests.X.Tests.A' 'Skipped'))
    Check 'all skipped is not an executed run' (@(Get-CoverageIssues $r @('Worsen.Tests.X.Tests')) -contains 'no executed test cases')
    [IO.File]::WriteAllText($path, $xml.Replace('total="2"', 'total="3"'))
    Check 'count mismatch is invalid evidence' ((Read-TestSummary $path).issues -contains 'aggregate mismatch: total')
    [IO.File]::WriteAllText($path, $xml.Replace('type="TestFixture"', 'type="TestFixture" label="Cancelled"'))
    Check 'cancelled suite is not complete reusable coverage' ((Read-TestSummary $path).issues -contains 'cancelled/not-run suite: Worsen.Tests.X.Tests')
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
