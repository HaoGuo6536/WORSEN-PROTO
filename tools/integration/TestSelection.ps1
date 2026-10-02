# ============================================================================
# TestSelection.ps1
# ============================================================================
# PURPOSE: Apply gate safety-net policy to conservative selector output and build
#   run-specific native runner requests. No Unity call is made by these helpers.
# ARCHITECTURAL ROLE: Integration tooling; coordinator policy, no runtime layer.
# KEY RESPONSIBILITIES:
#   - Fail closed on selector errors, missing baseline or a due full-suite run.
#   - Reuse complete full runs, including rejected candidates, with provenance.
#   - Count native gate attempts for the periodic safety net.
#   - Persist fixture reasons and construct a safely quoted runner invocation.
# DEPENDENCIES: select-tests.py, Gate.ps1, TestResults.ps1, Python, Git, gate ledger.
# USAGE NOTES: Dot-source with Common/Gate/TestResults. 'selected' cannot override full policy.
# ============================================================================
function Resolve-TestSelection($selection, [string]$requested, $entries, [int]$interval = 5, $testedBaseline = $null) {
    if ($interval -lt 1) { throw 'Full-suite interval must be positive.' }
    if ($requested -notin @('auto', 'selected', 'full')) { throw 'Invalid requested scope.' }
    $gates = @($entries | Where-Object { $_.test_run_id -or $_.test_scope -or $_.tests.total -gt 0 -or $_.verdict -eq 'fail-no-results' })
    $reasons = @($selection.full_reasons | Where-Object { $_ })
    if ($selection.scope -notin @('selected', 'full') -or ($selection.scope -eq 'selected' -and -not $selection.fixtures)) { $reasons += 'invalid/empty selector result' }
    if ($selection.scope -eq 'full' -and $reasons.Count -eq 0) { $reasons += 'selector requires full' }
    if ($requested -eq 'full') { $reasons += 'explicit -TestScope full' }
    $ordinal = $gates.Count + 1
    if (-not $testedBaseline) { $reasons += 'bootstrap/migrate complete full-run baseline' }
    $sinceFull = @()
    foreach ($gate in $gates) {
        if ($testedBaseline -and $gate.test_run_id -ceq $testedBaseline.run_id) { $sinceFull = @() }
        else { $sinceFull += [pscustomobject]@{ label = $gate.label; candidate = $gate.candidate_final } }
    }
    if ($ordinal % $interval -eq 0 -or $sinceFull.Count -ge ($interval - 1)) { $reasons += "periodic full suite: native gate $ordinal, interval $interval" }
    $scope = if ($reasons.Count -gt 0) { 'full' } else { 'selected' }
    $fixtures = @($selection.fixtures)
    $fixtureReasons = $selection.fixture_reasons
    if ($scope -eq 'full') {
        $fixtures = @($selection.all_fixtures)
        if (-not $fixtures -or $fixtures.Count -eq 0) {
            $fixtures = @('Worsen.Tests.Architecture.ArchitectureConformanceTests', 'Worsen.Tests.Infrastructure.FixtureTimeSetUpTests', 'Worsen.Tests.Run.RunFactRelayWiringTests')
        }
        $fixtureReasons = @{}
        foreach ($fixture in $fixtures) { $fixtureReasons[$fixture] = @('full-suite policy (see full_reasons)') }
    }
    [pscustomobject]@{
        scope = $scope
        fixtures = $fixtures; fixture_reasons = $fixtureReasons
        all_fixtures = @($selection.all_fixtures)
        full_reasons = @($reasons | Sort-Object -Unique); selector_scope = $selection.scope
        requested_scope = $requested; gate_number = $ordinal; full_suite_interval = $interval
        promotion_number = @($entries | Where-Object { $_.promoted }).Count + 1
        gates_since_full = $sinceFull
    }
}

function Get-CompleteFullBaseline([string]$root, $entries) {
    # Cumulative test_statuses cannot prove a full run: they may contain old leaves.
    $rows = @($entries)
    for ($i = $rows.Count - 1; $i -ge 0; $i--) {
        $e = $rows[$i]
        if ($e.test_scope -ne 'full' -or -not $e.test_run_id -or -not $e.candidate_final -or
            $e.setup_ok -ne $true -or $e.compile_ok -ne $true -or @($e.coverage_issues | Where-Object { $_ }).Count -gt 0) { continue }
        try {
            $raw = Invoke-Native python (Join-Path $PSScriptRoot 'select-tests.py') --root $root --base $e.candidate_final --candidate $e.candidate_final
            if ($script:NativeExit -ne 0) { throw 'baseline inventory process failed' }
            $inventory = ($raw -join "`n") | ConvertFrom-Json
            if ($inventory.scope -ne 'selected' -or -not $inventory.all_fixtures) { throw 'baseline inventory unavailable/ambiguous' }
            $results = $e.full_native_results
            $source = 'ledger full_native_results'
            if (-not $results) {
                $directory = Join-Path $e.evidence ('native-' + $e.test_run_id)
                $request = Get-Content -LiteralPath (Join-Path $e.evidence ('request-' + $e.test_run_id + '.json')) -Raw | ConvertFrom-Json
                $complete = Get-Content -LiteralPath (Join-Path $directory 'complete.json') -Raw | ConvertFrom-Json
                $xml = Join-Path $directory 'result-native.xml'
                if ($request.run_id -cne $e.test_run_id -or $request.scope -ne 'full' -or $complete.run_id -cne $e.test_run_id -or $complete.scope -ne 'full' -or
                    [IO.Path]::GetFullPath($complete.xml) -cne [IO.Path]::GetFullPath($xml)) { throw 'full-run identity mismatch' }
                $results = Read-TestSummary $xml
                $source = $xml
            }
            if (@(Get-CompleteEvidenceIssues $results $inventory.all_fixtures).Count -gt 0) { throw 'incomplete full-run evidence' }
            $driftPaths = @($e.setup_drift_paths)
            if ($null -eq $e.setup_snapshot -and $null -eq $e.setup_drift_paths) {
                if ($e.setup_drift) {
                    $drift = Get-Content -LiteralPath (Join-Path $e.evidence 'setup-drift.json') -Raw | ConvertFrom-Json
                    if ($null -eq $drift.changed -or $null -eq $drift.added -or $null -eq $drift.removed) { throw 'invalid setup drift' }
                    $driftPaths = @($drift.changed) + @($drift.added) + @($drift.removed)
                } else { $driftPaths = @() } # Legacy gate did not run setup.
            }
            # Coverage is proved above by raw full leaves only. Carry unresolved
            # historical failure debt separately so a later full run cannot erase
            # a deleted/ignored failing test by becoming the next coverage anchor.
            $debt = [pscustomobject]@{ test_statuses = @($e.test_statuses | Where-Object { $_.result -eq 'Failed' }); failed_names = @($e.failed_names) }
            $states = Merge-TestBaseline $results @() $debt
            return [pscustomobject]@{ label = $e.label; run_id = $e.test_run_id; candidate = $inventory.candidate
                source = $source; test_statuses = @($states.test_statuses); ignoredFixtures = @($results.ignoredFixtures)
                all_fixtures = @($inventory.all_fixtures); setup_snapshot = $e.setup_snapshot
                setup_drift_paths = @($driftPaths | Where-Object { $_ }); evidence = $e.evidence }
        } catch { Write-Warning "Full baseline $($e.label) rejected: $($_.Exception.Message)" }
    }
    return $null
}

function Get-SelectionSetupPaths($baseline, [hashtable]$currentSnapshot, [string[]]$currentDrift) {
    if ($baseline -and $null -ne $baseline.setup_snapshot -and $null -ne $currentSnapshot) {
        $before = @{}
        if ($baseline.setup_snapshot -is [System.Collections.IDictionary]) {
            foreach ($key in $baseline.setup_snapshot.Keys) { $before[$key] = $baseline.setup_snapshot[$key] }
        } else {
            foreach ($p in $baseline.setup_snapshot.PSObject.Properties) { $before[$p.Name] = $p.Value }
        }
        $delta = Compare-GeneratedSnapshot $before $currentSnapshot
        return @($delta.changed) + @($delta.added) + @($delta.removed)
    }
    # Legacy evidence has paths but no hashes: union, never subtract matching names.
    return @(@($baseline.setup_drift_paths) + @($currentDrift) | Where-Object { $_ } | Sort-Object -Unique)
}

function Get-CandidateTestSelection([string]$root, [string]$base, [string]$candidate, [string]$ledger,
                                    [string]$requested, [int]$interval, [string]$output,
                                    [hashtable]$setupSnapshot = $null, [string[]]$setupDrift = @()) {
    $entries = @()
    if (Test-Path -LiteralPath $ledger) {
        $entries = @(Get-Content -LiteralPath $ledger | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
    }
    $tested = Get-CompleteFullBaseline $root $entries
    try {
        $extra = @(Get-SelectionSetupPaths $tested $setupSnapshot $setupDrift)
        $pathsFile = $output + '.setup-paths.json'
        ConvertTo-Json -InputObject @($extra) | Set-Content -LiteralPath $pathsFile -Encoding UTF8
        $arguments = @((Join-Path $PSScriptRoot 'select-tests.py'), '--root', $root, '--base', $base, '--candidate', $candidate, '--extra-changes', $pathsFile)
        if ($tested) { $arguments += @('--tested-candidate', $tested.candidate) }
        $raw = Invoke-Native python @arguments
        if ($script:NativeExit -ne 0) { throw 'selector process failed' }
        $selection = ($raw -join "`n") | ConvertFrom-Json
        if (-not $selection.schema_version -or -not $selection.fixture_reasons) { throw 'invalid selector schema' }
    } catch {
        $selection = [pscustomobject]@{ scope = 'full'; fixtures = @(); fixture_reasons = @{}; full_reasons = @("selector error: $($_.Exception.Message)") }
    }
    if (-not $selection.baseline_reused) { $tested = $null }
    if ($tested) {
        foreach ($fixture in @($selection.all_fixtures)) {
            if ($tested.all_fixtures -cnotcontains $fixture -and $selection.fixtures -cnotcontains $fixture) {
                $selection.fixtures += $fixture
                $selection.fixture_reasons | Add-Member -NotePropertyName $fixture -NotePropertyValue @('not covered by full baseline')
            }
        }
    }
    $resolved = Resolve-TestSelection $selection $requested $entries $interval $tested
    $resolved | Add-Member -NotePropertyName base -NotePropertyValue $selection.base
    $resolved | Add-Member -NotePropertyName candidate -NotePropertyValue $candidate
    $resolved | Add-Member -NotePropertyName base_main -NotePropertyValue $base
    $resolved | Add-Member -NotePropertyName baseline_rule -NotePropertyValue $selection.baseline_rule
    $resolved | Add-Member -NotePropertyName tested_baseline -NotePropertyValue $tested
    $resolved | Add-Member -NotePropertyName changed_files -NotePropertyValue @($selection.changed_files)
    $resolved | Add-Member -NotePropertyName ignored_gate_files -NotePropertyValue @($selection.ignored_gate_files)
    $resolved | Add-Member -NotePropertyName setup_paths -NotePropertyValue @($extra)
    $resolved | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $output -Encoding UTF8
    return $resolved
}

function New-NativeTestRequest([string]$directory, [string]$scope, [string[]]$fixtures) {
    if ($scope -notin @('selected', 'full') -or ($scope -eq 'selected' -and $fixtures.Count -eq 0)) { throw 'Invalid native selection.' }
    foreach ($fixture in $fixtures) {
        if ($fixture -cnotmatch '^Worsen\.Tests\.[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+$') { throw "Expected a full fixture name, not a test/prefix/regex: $fixture" }
    }
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $path = Join-Path $directory "request-$id.json"
    $output = Join-Path $directory "native-$id"
    @{ run_id = $id; scope = $scope; fixtures = @($fixtures); output = $output } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
    # C# verbatim string: backslashes are literal and embedded quotes are doubled.
    $literal = $path.Replace('"', '""')
    [pscustomobject]@{ id = $id; directory = $output; path = $path
        code = "return Worsen.Editor.Testing.NativeTestRunnerSetup.Run(@""$literal"");" }
}
