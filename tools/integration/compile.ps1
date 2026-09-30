param(
    [Parameter(Mandatory = $true)][string]$Worktree,
    [Parameter(Mandatory = $true)][string]$RunName
)
# Offline Roslyn compile of every WORSEN assembly (Core..Tests) from a checkout or worktree,
# with no Unity process. Prints per-assembly results against the recorded warning baseline
# and exits 1 on any error. Evidence: <Worktree>/Logs/AgentValidation/PLAN-002/offline-compile/<RunName>.
$ErrorActionPreference = 'Stop'
$harness = Join-Path $PSScriptRoot '..\offline-compile\Compile-Staged.ps1'
$Worktree = (Resolve-Path -LiteralPath $Worktree).Path
if (-not (Test-Path -LiteralPath (Join-Path $Worktree 'Library\ScriptAssemblies'))) { throw "Checkout has no Library (junction) with ScriptAssemblies: $Worktree" }
& $harness -Plans @() -Through Tests -RunName $RunName -ProjectRoot $Worktree | Out-Null
$summaryPath = Join-Path $Worktree "Logs\AgentValidation\PLAN-002\offline-compile\$RunName\summary.json"
$summary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
# Existing CS0649 warnings on serialized fields; a rise in any assembly is a regression.
$baseline = @{ 'Worsen.Core' = 0; 'Worsen.Domain' = 41; 'Worsen.Session' = 0; 'Worsen.Presentation' = 34; 'Worsen.Orchestrator' = 70; 'Worsen.Editor' = 0; 'Worsen.Tests' = 0 }
$failed = $false
foreach ($r in $summary.Results) {
    $b = $baseline[$r.Assembly]; $delta = $r.Warnings - $b; $flag = ''
    if ($r.Errors -gt 0 -or $r.ExitCode -ne 0) { $flag = '  <-- ERRORS'; $failed = $true }
    elseif ($delta -gt 0) { $flag = "  <-- $delta new warning(s)" }
    '{0}: exit={1} sources={2} errors={3} warnings={4} (baseline {5}){6}' -f $r.Assembly, $r.ExitCode, $r.Sources, $r.Errors, $r.Warnings, $b, $flag
}
$runDir = Split-Path -Parent $summaryPath
Get-ChildItem -LiteralPath $runDir -Filter '*.log' -ErrorAction SilentlyContinue | ForEach-Object {
    $lines = @(Get-Content -LiteralPath $_.FullName | Where-Object { $_ -match ': error CS' })
    if ($lines.Count -gt 0) { "--- errors in $($_.Name)"; $lines | Select-Object -First 40 }
}
"Evidence: $runDir"
if ($failed) { exit 1 }
