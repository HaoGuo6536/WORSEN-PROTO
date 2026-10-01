# ============================================================================
# TestSelection.ps1
# ============================================================================
# PURPOSE: Apply gate safety-net policy to conservative selector output and build
#   run-specific native runner requests. No Unity call is made by these helpers.
# ARCHITECTURAL ROLE: Integration tooling; coordinator policy, no runtime layer.
# KEY RESPONSIBILITIES:
#   - Fail closed on selector errors, missing baseline or a due full-suite run.
#   - Count successful promotions, never attempts, for the periodic safety net.
#   - Persist fixture reasons and construct a safely quoted runner invocation.
# DEPENDENCIES: select-tests.py, Python, Git, existing promotion ledger.
# USAGE NOTES: Dot-source after Common.ps1. 'selected' cannot override full policy.
# ============================================================================
function Resolve-TestSelection($selection, [string]$requested, $promotions, [int]$interval = 5) {
    if ($interval -lt 1) { throw 'Full-suite interval must be positive.' }
    if ($requested -notin @('auto', 'selected', 'full')) { throw 'Invalid requested scope.' }
    $promotions = @($promotions | Where-Object { $_.promoted })
    $reasons = @($selection.full_reasons | Where-Object { $_ })
    if ($selection.scope -notin @('selected', 'full') -or ($selection.scope -eq 'selected' -and -not $selection.fixtures)) { $reasons += 'invalid/empty selector result' }
    if ($selection.scope -eq 'full' -and $reasons.Count -eq 0) { $reasons += 'selector requires full' }
    if ($requested -eq 'full') { $reasons += 'explicit -TestScope full' }
    $ordinal = $promotions.Count + 1
    $last = $promotions | Select-Object -Last 1
    if (-not $last -or $null -eq $last.test_statuses) { $reasons += 'bootstrap/migrate leaf-status baseline' }
    $sinceFull = @()
    foreach ($promotion in $promotions) {
        if ($promotion.test_scope -eq 'full') { $sinceFull = @() }
        else { $sinceFull += [pscustomobject]@{ label = $promotion.label; candidate = $(if ($promotion.candidate_final) { $promotion.candidate_final } else { $promotion.candidate }) } }
    }
    if ($ordinal % $interval -eq 0 -or $sinceFull.Count -ge ($interval - 1)) { $reasons += "periodic full suite: promotion $ordinal, interval $interval" }
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
        requested_scope = $requested; promotion_number = $ordinal; full_suite_interval = $interval
        promotions_since_full = $sinceFull
    }
}

function Get-CandidateTestSelection([string]$root, [string]$base, [string]$candidate, [string]$ledger,
                                    [string]$requested, [int]$interval, [string]$output) {
    try {
        $raw = Invoke-Native python (Join-Path $PSScriptRoot 'select-tests.py') --root $root --base $base --candidate $candidate
        if ($script:NativeExit -ne 0) { throw 'selector process failed' }
        $selection = ($raw -join "`n") | ConvertFrom-Json
        if (-not $selection.schema_version -or -not $selection.fixture_reasons) { throw 'invalid selector schema' }
    } catch {
        $selection = [pscustomobject]@{ scope = 'full'; fixtures = @(); fixture_reasons = @{}; full_reasons = @("selector error: $($_.Exception.Message)") }
    }
    $promotions = @()
    if (Test-Path -LiteralPath $ledger) {
        $promotions = @(Get-Content -LiteralPath $ledger | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json } | Where-Object { $_.promoted })
    }
    $resolved = Resolve-TestSelection $selection $requested $promotions $interval
    $resolved | Add-Member -NotePropertyName base -NotePropertyValue $base
    $resolved | Add-Member -NotePropertyName candidate -NotePropertyValue $candidate
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
