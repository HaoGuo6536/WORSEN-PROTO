param(
    [Parameter(Mandatory = $true)][string]$Token,
    [Parameter(Mandatory = $true)][datetime]$Since,
    [int]$TimeoutMinutes = 120,
    [string]$OutJson = ''
)
# Waits for UnityValidationTools to write a complete NUnit result XML newer than $Since,
# heartbeating the Unity lease meanwhile (in this script, not in model turns). Prints totals
# and failures, and writes a structured summary (with each failure's categories, including
# inherited fixture categories) to $OutJson. Exit 2 = no result; never releases the lease.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
$dir = Join-Path $script:Main ((Get-Content (Join-Path $script:Main 'Logs\AgentValidation\active-output.txt') -Raw).Trim())
$deadline = (Get-Date).AddMinutes($TimeoutMinutes); $beat = Get-Date; $xml = $null
while ((Get-Date) -lt $deadline) {
    $xml = Get-ChildItem -LiteralPath $dir -Filter 'result-*.xml' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -gt $Since } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($xml) { Start-Sleep -Seconds 3; break }
    if (((Get-Date) - $beat).TotalSeconds -gt 45) { Invoke-LeaseCommand Heartbeat $Token | Out-Null; $beat = Get-Date }
    Start-Sleep -Seconds 10
}
if (-not $xml) { "TIMEOUT: no result XML after $TimeoutMinutes min; lease retained."; exit 2 }
[xml]$doc = Get-Content -LiteralPath $xml.FullName -Raw
$run = $doc.'test-run'
function Get-Categories($node) {
    $cats = @()
    while ($node -and $node.LocalName -ne '#document') {
        foreach ($p in $node.SelectNodes("properties/property[@name='Category']")) { $cats += $p.value }
        $node = $node.ParentNode
    }
    return @($cats | Select-Object -Unique)
}
$failures = @()
foreach ($c in $doc.SelectNodes("//test-case[@result='Failed']")) {
    $msg = ($c.failure.message.'#cdata-section' + $c.failure.message.InnerText) -replace '\s+', ' '
    $failures += [pscustomobject]@{ name = $c.fullname; categories = (Get-Categories $c); message = $msg.Substring(0, [Math]::Min(400, $msg.Length)) }
}
$skipped = @($doc.SelectNodes("//test-case[@result='Skipped']") | ForEach-Object { $_.fullname })
$summary = [pscustomobject]@{
    xml = $xml.FullName; total = [int]$run.total; passed = [int]$run.passed; failed = [int]$run.failed
    skipped = [int]$run.skipped; inconclusive = [int]$run.inconclusive; result = $run.result; duration = [double]$run.duration
    failures = $failures; skippedNames = $skipped
}
if ($OutJson) { $summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutJson -Encoding UTF8 }
"RESULT {0}" -f $xml.FullName
"total={0} passed={1} failed={2} skipped={3} inconclusive={4} result={5} duration={6}s" -f $run.total, $run.passed, $run.failed, $run.skipped, $run.inconclusive, $run.result, $run.duration
foreach ($f in $failures) { "FAILED {0}{1}: {2}" -f $f.name, $(if ($f.categories) { ' [' + ($f.categories -join ',') + ']' } else { '' }), $f.message }
foreach ($s in $skipped) { "SKIPPED $s" }
