param(
    [Parameter(Mandatory = $true)][string]$RunName,
    [string]$ProjectRoot = (Get-Location).Path,
    [string]$AstGrep = 'C:/Users/Hao Guo/AppData/Roaming/npm/ast-grep.ps1'
)

# Read-only lint of captured sources with verbatim copies of repository rules.
$ErrorActionPreference = 'Stop'
if ($RunName -notmatch '^[a-zA-Z0-9_-]+$') { throw 'RunName must be a simple directory name.' }
$ProjectRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$runRoot = Join-Path $ProjectRoot ('Logs/AgentValidation/PLAN-002/offline-compile/' + $RunName)
$snapshotRoot = Join-Path $runRoot 'snapshot'
$sourceManifestPath = Join-Path $runRoot 'source-manifest.json'
if (-not (Test-Path -LiteralPath $sourceManifestPath)) { throw 'Compile snapshot is missing.' }
if (Test-Path -LiteralPath (Join-Path $runRoot 'ast-grep-summary.json')) { throw 'Refusing to replace lint evidence.' }
$ruleDir = 'tools/ast-grep/rules'
$ruleFiles = @('sgconfig.yml') + @(Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $ruleDir) -Recurse -File | ForEach-Object {
    $_.FullName.Substring($ProjectRoot.Length + 1).Replace('\', '/')
})
$rulesManifest = @()
foreach ($relative in $ruleFiles) {
    $source = Join-Path $ProjectRoot $relative
    $target = Join-Path $snapshotRoot $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    $before = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $source -Destination $target
    $copied = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($before -ne $copied -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $copied) {
        throw "Rule changed during capture: $source"
    }
    $rulesManifest += [pscustomobject]@{ RelativePath = $relative; SHA256 = $copied }
}
$rulesManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runRoot 'ast-grep-rule-manifest.json') -Encoding UTF8
$sourceManifest = @(Get-Content -LiteralPath $sourceManifestPath -Raw | ConvertFrom-Json)
$invalidSources = @($sourceManifest | Where-Object {
    (Get-FileHash -LiteralPath $_.SnapshotPath -Algorithm SHA256).Hash -ne $_.SHA256
})
if ($invalidSources.Count -gt 0) { throw 'Captured source integrity check failed.' }
$version = (& $AstGrep --version) -join [Environment]::NewLine
$outputPath = Join-Path $runRoot 'ast-grep.json'
$inspectPath = Join-Path $runRoot 'ast-grep-inspect.txt'
$command = '& "' + $AstGrep + '" scan --config sgconfig.yml --json --inspect summary --no-ignore parent --no-ignore vcs Assets'
$command | Set-Content -LiteralPath (Join-Path $runRoot 'ast-grep.command.txt') -Encoding UTF8
Push-Location -LiteralPath $snapshotRoot
try {
    & $AstGrep scan --config sgconfig.yml --json --inspect summary --no-ignore parent --no-ignore vcs Assets 2> $inspectPath | Set-Content -LiteralPath $outputPath -Encoding UTF8
    $lintExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}
$findings = @(Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json)
$summary = [pscustomobject]@{
    Scope = 'Static ast-grep rules on preserved compile snapshot; no runtime or Unity test evidence.'
    Version = $version; Command = $command; WorkingDirectory = $snapshotRoot
    ExitCode = $lintExitCode; Findings = $findings.Count
    SourceFiles = @($sourceManifest | Where-Object { $_.RelativePath.EndsWith('.cs') }).Count
    RuleFiles = $rulesManifest.Count - 1
    FindingsByRule = @($findings | Group-Object ruleId | Select-Object Name, Count)
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runRoot 'ast-grep-summary.json') -Encoding UTF8
Write-Output "$version`: exit=$lintExitCode findings=$($findings.Count) rules=$($summary.RuleFiles) sources=$($summary.SourceFiles)"
Get-Content -LiteralPath $inspectPath | Write-Output
foreach ($finding in $findings) {
    Write-Output ($finding.ruleId + ': ' + $finding.file + ':' + ($finding.range.start.line + 1) + ' ' + $finding.message)
}
exit $lintExitCode
