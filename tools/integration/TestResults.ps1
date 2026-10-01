# ============================================================================
# TestResults.ps1
# ============================================================================
# PURPOSE: Parse native NUnit evidence without treating omitted tests as passes.
#   Shared by the lease-aware waiter and offline gate self-tests.
# ARCHITECTURAL ROLE: Integration tooling; result normalization, no runtime layer.
# KEY RESPONSIBILITIES:
#   - Preserve every leaf status, fixture identity and inherited category.
#   - Validate aggregate counts and expose suite-only failures/cancelled runs.
# DEPENDENCIES: PowerShell and System.Xml; no Unity or network calls.
# USAGE NOTES: Dot-source. Malformed XML throws; incomplete evidence fails closed.
# ============================================================================
function Read-TestSummary([string]$path) {
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($path, $settings)
    $doc = New-Object System.Xml.XmlDocument
    try { $doc.Load($reader) } finally { $reader.Dispose() }
    $run = $doc.DocumentElement
    if ($run.LocalName -ne 'test-run') { throw 'Expected an NUnit test-run root.' }
    $cases = @(); $failures = @(); $issues = @()
    foreach ($c in $doc.SelectNodes('//test-case')) {
        $cats = @(); $node = $c; $fixture = $c.GetAttribute('classname')
        while ($node -and $node.LocalName -ne '#document') {
            foreach ($p in $node.SelectNodes("properties/property[@name='Category']")) { $cats += $p.GetAttribute('value') }
            if (-not $fixture -and $node.LocalName -eq 'test-suite' -and $node.GetAttribute('type') -eq 'TestFixture') { $fixture = $node.GetAttribute('fullname') }
            $node = $node.ParentNode
        }
        $name = $c.GetAttribute('fullname'); $status = $c.GetAttribute('result')
        if (-not $name -or $status -notin @('Passed', 'Failed', 'Skipped', 'Inconclusive')) { $issues += 'unnamed or unfinished test case' }
        if ($c.GetAttribute('label') -in @('Cancelled', 'Canceled', 'NotRun')) { $issues += "cancelled/not-run case: $name" }
        $categories = @($cats | Select-Object -Unique)
        $cases += [pscustomobject]@{ name = $name; result = $status; fixture = $fixture; categories = $categories }
        if ($status -eq 'Failed') {
            $messageNode = $c.SelectSingleNode('failure/message')
            $message = if ($messageNode) { $messageNode.InnerText -replace '\s+', ' ' } else { '' }
            $failures += [pscustomobject]@{ name = $name; categories = $categories; message = $message.Substring(0, [Math]::Min(400, $message.Length)) }
        }
    }
    # A fixture ignored at fixture or parameterized-method level is emitted as a skipped suite with no leaf
    # cases (HorrorRunFogWiringTests, batch 31b); record it so coverage treats it as present and skipped.
    $ignoredFixtures = @($doc.SelectNodes("//test-suite[@type='TestFixture' and @result='Skipped']") | ForEach-Object { $_.GetAttribute('fullname') })
    foreach ($suite in $doc.SelectNodes("//test-suite[@result='Failed']")) {
        if ($suite.SelectNodes(".//test-case[@result='Failed']").Count -eq 0) { $issues += ('suite-only failure: ' + $suite.GetAttribute('fullname')) }
    }
    $totals = @{}
    foreach ($status in @('Passed', 'Failed', 'Skipped', 'Inconclusive')) {
        $key = $status.ToLowerInvariant()
        $totals[$key] = @($cases | Where-Object { $_.result -eq $status }).Count
        if (-not $run.HasAttribute($key) -or [int]$run.GetAttribute($key) -ne $totals[$key]) { $issues += "aggregate mismatch: $key" }
    }
    if (-not $run.HasAttribute('total') -or [int]$run.GetAttribute('total') -ne $cases.Count) { $issues += 'aggregate mismatch: total' }
    if (@($cases.name | Select-Object -Unique).Count -ne $cases.Count) { $issues += 'duplicate test full names' }
    $result = $run.GetAttribute('result')
    # Unity's runner reports a run whose failures are all in child tests as 'Failed(Child)'.
    if ($result -eq 'Failed(Child)') { $result = 'Failed' }
    if ($result -notin @('Passed', 'Failed') -or ($result -eq 'Failed' -and $totals.failed -eq 0)) { $issues += "incomplete or suite-only root result: $result" }
    [pscustomobject]@{
        xml = $path; total = $cases.Count; passed = $totals.passed; failed = $totals.failed
        skipped = $totals.skipped; inconclusive = $totals.inconclusive; result = $result
        duration = [double]::Parse($run.GetAttribute('duration'), [Globalization.CultureInfo]::InvariantCulture)
        failures = $failures; skippedNames = @($cases | Where-Object { $_.result -eq 'Skipped' } | ForEach-Object { $_.name })
        cases = $cases; issues = $issues; ignoredFixtures = $ignoredFixtures
    }
}

function Get-CoverageIssues($results, [string[]]$fixtures) {
    $issues = @($results.issues)
    if (-not $results.cases -or @($results.cases).Count -ne $results.total) { $issues += 'missing leaf status evidence' }
    if (@($results.cases | Where-Object { $_.result -in @('Passed', 'Failed') }).Count -eq 0) { $issues += 'no executed test cases' }
    foreach ($fixture in $fixtures) {
        $matched = @($results.cases | Where-Object { $_.fixture -ceq $fixture -or $_.fixture.StartsWith($fixture + '(') })
        # Existing explicit ignores (e.g. HorrorRunFogWiringTests) are reported as
        # skipped, not passed. Missing fixtures are different: the filter omitted them.
        if ($matched.Count -eq 0 -and @($results.ignoredFixtures) -cnotcontains $fixture) { $issues += "fixture absent from results: $fixture" }
    }
    return $issues
}
