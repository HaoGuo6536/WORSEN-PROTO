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
    $reasons = @()
    if (-not $results -or $results.total -le 0) {
        return [pscustomobject]@{ pass = $false; reasons = @('no test results or zero tests'); blocking = @(); quarantined = @(); new = @() }
    }
    $quarantined = @(); $blocking = @()
    foreach ($f in @($results.failures)) {
        if (@($quarantine | Where-Object { Test-QuarantineMatch $_ $f $today }).Count -gt 0) { $quarantined += $f.name } else { $blocking += $f.name }
    }
    $baseNames = if ($baseline) { @($baseline.failed_names) } else { @() }
    $new = @($blocking | Where-Object { $baseNames -notcontains $_ })
    $baseBlocking = if ($baseline) { [int]$baseline.blocking_count } else { 0 }
    if ($new.Count -gt 0) { $reasons += "$($new.Count) new failing test(s)" }
    if ($blocking.Count -gt $baseBlocking) { $reasons += "blocking failures $($blocking.Count) exceed baseline $baseBlocking" }
    return [pscustomobject]@{ pass = ($reasons.Count -eq 0); reasons = $reasons; blocking = $blocking; quarantined = $quarantined; new = $new }
}

function Add-LedgerEntry([string]$ledger, [hashtable]$entry) {
    $dir = Split-Path -Parent $ledger
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $json = ([pscustomobject]$entry | ConvertTo-Json -Depth 6 -Compress)
    [System.IO.File]::AppendAllText($ledger, $json + "`n", (New-Object System.Text.UTF8Encoding($false)))
}
