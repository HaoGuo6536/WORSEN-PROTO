# ============================================================================
# Gate.ps1
# ============================================================================
# PURPOSE: Judge candidate test evidence against quarantines and the last promoted
#   baseline. Preserve unexecuted failures so selective runs cannot erase debt.
# ARCHITECTURAL ROLE: Integration tooling; pure verdict policy, no runtime layer.
# KEY RESPONSIBILITIES:
#   - Evaluate new failures and the cumulative blocking-failure ratchet.
#   - Merge observed test statuses while carrying forward unobserved history.
#   - Read/write promotion evidence and compare generated setup snapshots.
# DEPENDENCIES: PowerShell JSON/filesystem APIs; gate result and ledger schemas.
# USAGE NOTES: Dot-source; only actual Passed results clear a known failure.
# ============================================================================
# Gate verdict for integration candidates (dot-source). Pure functions over test summaries.
#
# Rules (fail closed):
#   - A failure matched by a live (unexpired) quarantine entry is non-blocking.
#   - Every other failure is blocking. A blocking failure absent from the last promoted
#     baseline is NEW, and any new failure fails the gate.
#   - The blocking count may not exceed the baseline's blocking count (ratchet).
#   - Missing results, zero tests, compile or setup failure fail the gate.
# Quarantine entries: { "match": "<glob on full test name>" | "category:<Name>", "owner", "reason",
#   "added", "expires" (yyyy-MM-dd) }. An expired entry stops matching, so its tests block
#   again until someone fixes them or renews the entry with a reason.

function Get-Quarantine([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return @() }
    return @((Get-Content -LiteralPath $path -Raw | ConvertFrom-Json).entries)
}

function Test-QuarantineMatch($entry, $failure, [datetime]$today) {
    if ($entry.expires -and ([datetime]$entry.expires) -lt $today.Date) { return $false }
    if ($entry.match -like 'category:*') { return @($failure.categories) -contains $entry.match.Substring(9) }
    return $failure.name -like $entry.match
}

function Get-LastPromoted([string]$ledger) {
    if (-not (Test-Path -LiteralPath $ledger)) { return $null }
    $last = $null
    foreach ($line in Get-Content -LiteralPath $ledger) {
        if (-not $line.Trim()) { continue }
        $e = $line | ConvertFrom-Json
        if ($e.promoted) { $last = $e }
    }
    return $last
}

function Get-GateVerdict($results, $quarantine, $baseline, [datetime]$today = (Get-Date)) {
    $reasons = @($results.issues | Where-Object { $_ })
    if (-not $results -or $results.total -le 0) {
        return [pscustomobject]@{ pass = $false; reasons = @('no test results or zero tests'); blocking = @(); quarantined = @(); new = @() }
    }
    $quarantined = @(); $blocking = @()
    foreach ($f in @($results.failures)) {
        if (@($quarantine | Where-Object { Test-QuarantineMatch $_ $f $today }).Count -gt 0) { $quarantined += $f.name } else { $blocking += $f.name }
    }
    $baseNames = if ($baseline) { @($baseline.failed_names) } else { @() }
    $new = @($blocking | Where-Object { $baseNames -cnotcontains $_ })
    $baseBlocking = if ($baseline) { [int]$baseline.blocking_count } else { 0 }
    if ($new.Count -gt 0) { $reasons += "$($new.Count) new failing test(s)" }
    # Modern baselines retain unobserved failures. Only executed passes can pay down
    # the ratchet; skipping or omitting a formerly failing test does not fix it.
    $ratchetCount = $blocking.Count
    if ($results.cases -and $baseline) {
        $next = Merge-TestBaseline $results $quarantine $baseline $today
        $ratchetCount = $next.blocking_count
    }
    if ($results.scope -eq 'selected' -and $null -eq $baseline.test_statuses) {
        $reasons += 'selected runs require a migrated leaf-status baseline (run full first)'
    }
    if ($results.scope -eq 'selected' -and (-not $results.cases -or @($results.cases).Count -ne $results.total)) { $reasons += 'selected run lacks leaf status evidence' }
    if ($ratchetCount -gt $baseBlocking) { $reasons += "blocking failures $ratchetCount exceed baseline $baseBlocking" }
    return [pscustomobject]@{ pass = ($reasons.Count -eq 0); reasons = $reasons; blocking = $blocking; quarantined = $quarantined; new = $new }
}

function Merge-TestBaseline($results, $quarantine, $baseline, [datetime]$today = (Get-Date)) {
    $states = [System.Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($state in @($baseline.test_statuses)) { if ($state) { $states[$state.name] = $state } }
    # Legacy ledgers recorded failures only. Seed them conservatively; the first
    # new gate is forced full so current results supply categories and statuses.
    foreach ($name in @($baseline.failed_names)) {
        if ($name -and -not $states.ContainsKey($name)) {
            $states[$name] = [pscustomobject]@{ name = $name; result = 'Failed'; categories = @(); blocking = $true }
        }
    }
    foreach ($case in @($results.cases)) {
        if (-not $case) { continue }
        # Skipped/inconclusive observations do not replace a known executed status.
        # Their current status remains in run evidence, not the baseline ratchet.
        if ($case.result -notin @('Passed', 'Failed') -and $states.ContainsKey($case.name)) { continue }
        $isBlocking = $case.result -eq 'Failed' -and @($quarantine | Where-Object { Test-QuarantineMatch $_ $case $today }).Count -eq 0
        $states[$case.name] = [pscustomobject]@{ name = $case.name; result = $case.result; categories = @($case.categories); blocking = $isBlocking }
    }
    $values = @($states.Values | Sort-Object name)
    [pscustomobject]@{
        test_statuses = $values
        failed_names = @($values | Where-Object { $_.result -eq 'Failed' } | ForEach-Object { $_.name })
        blocking_count = @($values | Where-Object { $_.blocking }).Count
        quarantined_count = @($values | Where-Object { $_.result -eq 'Failed' -and -not $_.blocking }).Count
    }
}

function Add-LedgerEntry([string]$ledger, [hashtable]$entry) {
    $dir = Split-Path -Parent $ledger
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $json = ([pscustomobject]$entry | ConvertTo-Json -Depth 6 -Compress)
    [System.IO.File]::AppendAllText($ledger, $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
}

# Setup drift evidence: which generated files a gate's setup steps rewrote. Not a verdict;
# it makes parity claims ("setup output unchanged") checkable in the gate evidence.
$script:DriftRoots = @('Assets/Resources', 'Assets/Prefabs', 'Assets/Scenes', 'Assets/Settings')
$script:DriftExtensions = @('.asset', '.prefab', '.unity', '.mat', '.mixer', '.controller', '.anim', '.wav', '.meta')

function Get-GeneratedSnapshot([string]$root, [string[]]$roots = $script:DriftRoots, [string[]]$extensions = $script:DriftExtensions) {
    $snap = @{}
    foreach ($r in $roots) {
        $dir = Join-Path $root $r
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        Get-ChildItem -LiteralPath $dir -Recurse -File | Where-Object { $extensions -contains $_.Extension.ToLowerInvariant() } | ForEach-Object {
            $rel = $_.FullName.Substring($root.TrimEnd('\', '/').Length + 1).Replace('\', '/')
            $snap[$rel] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
    $snap
}

function Compare-GeneratedSnapshot([hashtable]$before, [hashtable]$after) {
    $changed = @($after.Keys | Where-Object { $before.ContainsKey($_) -and $before[$_] -ne $after[$_] } | Sort-Object)
    $added = @($after.Keys | Where-Object { -not $before.ContainsKey($_) } | Sort-Object)
    $removed = @($before.Keys | Where-Object { -not $after.ContainsKey($_) } | Sort-Object)
    [pscustomobject]@{ changed = $changed; added = $added; removed = $removed; files = $after.Count }
}
