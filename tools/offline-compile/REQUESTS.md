# Requests to other owners — PLAN-011 pure-test tier (L2-08, L5-01)

These are proposals, not changes to the owners' files. All implementation in this worker stays in `tools/offline-compile/**`.

## 1. `tools/delegation/common-rules.md` — Required checks before you finish

Insert the following item after the existing compile check; renumber lint accordingly. Exact proposed wording:

> **Headless pure tests (must run):** After a successful fresh compile, run `powershell -NoProfile -File "WORKTREE/tools/offline-compile/Run-PureTests.ps1" -Worktree "WORKTREE" -CompileRun <the-successful-compile-RunName> -RunName <task>-pure-<nnn> -Filter '<anchored regex of the fully qualified fixtures you added or touched>'`. Include dependent fixtures affected by changed public behavior. Use a new RunName each time. Read `Logs/AgentValidation/PLAN-002/offline-compile/<RunName>/summary.json` and `results.log`; report `PURE_RESULT`, wall time, the exact filter and the evidence path. Any pure failure, timeout, harness error, incomplete result or empty selection blocks hand-off. Environment and skipped cases are not passes: list them and request coordinator Unity verification; an all-environment/skipped selection must be reported as “no pure coverage,” never “tests passed.” Do not weaken assertions, add skips or substitute engine behavior merely to obtain a green headless result. Recompile and rerun if the touched sources changed after the compile snapshot. This tier never starts or contacts Unity and does not replace the coordinator's full Unity gate.

Example anchored fixture regex: `^Worsen[.]Tests[.]Player[.](PlayerControllerTests|PlayerMoverPresenterTests)[.]`.

## 2. `tools/integration/integrate.ps1` — offline candidate pre-check

Owner: integration coordinator. Insert this block immediately after `$entry.compile_ok = ($compileExit -eq 0)` (currently line 55), before lint/architecture checks and before any Unity lease or checkout publication. Use the candidate's versioned runner, the exact successful candidate compile run, and a unique pure run. Do not run the open checkout's test assembly.

```powershell
$entry.pure_ok = $false
if ($entry.compile_ok) {
    $pureCompileRun = "int-$Label-$stamp"
    $pureRun = "pure-$Label-$stamp"
    $pureScript = Join-Path $int 'tools/offline-compile/Run-PureTests.ps1'
    $pureRoot = Join-Path $int "Logs/AgentValidation/PLAN-002/offline-compile/$pureRun"
    $pureSummaryPath = Join-Path $pureRoot 'summary.json'
    try {
        if (-not (Test-Path -LiteralPath $pureScript)) { throw 'Candidate lacks the versioned headless runner.' }
        $pureOut = & powershell -NoProfile -File $pureScript -Worktree $int -CompileRun $pureCompileRun -RunName $pureRun
        $pureExit = $LASTEXITCODE
        $pureOut | Set-Content -LiteralPath (Join-Path $out 'pure-results.log') -Encoding UTF8
        $pureOut | ForEach-Object { Log "  $_" }
        if (-not (Test-Path -LiteralPath $pureSummaryPath)) { throw 'No completed headless summary.' }
        $pure = Get-Content -LiteralPath $pureSummaryPath -Raw | ConvertFrom-Json
        $compiledTests = Join-Path $int "Logs/AgentValidation/PLAN-002/offline-compile/$pureCompileRun/bin/Worsen.Tests.dll"
        $compiledHash = (Get-FileHash -LiteralPath $compiledTests -Algorithm SHA256).Hash
        $enumerated = @($pure.Cases).Count
        $total = $pure.Totals.passed + $pure.Totals.failed + $pure.Totals.environment + $pure.Totals.skipped
        $outcomesAgree = $true
        foreach ($kind in @('passed', 'failed', 'environment', 'skipped')) {
            if (@($pure.Cases | Where-Object { $_.Outcome -eq $kind }).Count -ne $pure.Totals.$kind) {
                $outcomesAgree = $false
            }
        }
        $entry.pure_ok = ($pureExit -eq 0 -and $pure.ExitCode -eq 0 -and
            $pure.CompileRun -eq $pureCompileRun -and -not $pure.SelfTest -and
            [string]::IsNullOrEmpty($pure.Filter) -and $pure.TestAssemblySHA256 -eq $compiledHash -and
            $pure.Total -gt 0 -and $pure.Total -eq $enumerated -and $total -eq $enumerated -and
            $outcomesAgree -and $pure.Totals.failed -eq 0 -and $pure.Totals.passed -gt 0 -and
            @($pure.InfrastructureErrors).Count -eq 0)
        $entry.pure = [ordered]@{
            total = $pure.Total; passed = $pure.Totals.passed; failed = $pure.Totals.failed
            environment = $pure.Totals.environment; skipped = $pure.Totals.skipped
            pure_fraction = $pure.PureFraction; wall_seconds = $pure.TotalWallSeconds
            evidence = $pureRoot; assembly_sha256 = $pure.TestAssemblySHA256
        }
        Copy-Item -LiteralPath $pureSummaryPath -Destination (Join-Path $out 'pure-summary.json')
    } catch {
        $entry.pure_ok = $false
        $entry.pure_error = $_.Exception.Message
        Log "Headless pre-check error: $($entry.pure_error)"
    }
}
```

Change the existing pre-check rejection condition (currently line 69) to:

```powershell
if (-not $entry.compile_ok -or -not $entry.pure_ok -or -not $entry.lint_ok -or $entry.arch_ok -eq $false) {
```

Keep its existing `fail-precheck` ledger write and throw; add `pure=$($entry.pure_ok)` to the exception message. Leave the later Unity gate intact. Environment/skipped cases are retained in the ledger but do not count as headless passes or waive Unity verification. Reject an all-environment/skipped candidate via `passed -gt 0`.

Do not initially make `PureFraction >= 0.5` an integration gate: the measured baseline is only 31.45%, so that would reject every unchanged candidate. Record this coverage shortfall explicitly, improve the fixtures through their owners, then approve a coverage threshold/baseline policy separately. Zero pure failures remains a fail-closed headless gate now.

## 3. Pure coverage target — fixture/system owners, approval needed

The measured run verifies 733 of 2,331 discovered cases headlessly. At least 433 additional cases must become genuinely executable as pure tests to reach half of this unchanged denominator. Engine-backed configuration setup, `SerializedObject` authoring and native-backed Unity value-type operations are the main obstacles; bypassing engine constructors, fabricating native results or simply relabeling these outcomes is not an acceptable fix.

Exact first targets (no edits made here):

- `Assets/Editor/Tests/Player/PlayerControllerTests.cs`, `PlayerControllerTests.SetUp`/`TearDown`: request approved plain-data configuration seams for pure rule cases instead of ScriptableObject creation/destruction; retain separate Unity tests for config serialization/wiring.
- `Assets/Editor/Tests/Progression/ProgressionSessionControllerTests.cs`, `ProgressionSessionControllerTests.SetUp`/`SetConfigField`: request approved plain-data input seams and genuinely pure rule fixtures, while keeping Unity Config authoring coverage.
- `Assets/Editor/Tests/Procedural/ProceduralControllerTests.cs`, `ProceduralControllerTests.SetUp`/`Generate`: request separation of pure generation inputs/geometry math from ScriptableObject creation and `SerializedObject` setup.
- `Assets/Editor/Tests/Architecture/ArchitectureConformanceTests.cs`, `ArchitectureConformanceTests.AssetsRoot`: request an explicit source-root input independent of `Application.dataPath`, with dedicated tests. It is currently environment, not a headless architecture pass.

These are scope-expansion requests, not authorization to edit tests, runtime contracts, or architecture. Review the complete majority-environment fixture list in `MEASUREMENT.md` and `summary.json` before selecting a migration batch. Static/editor discovery limitations and conditional editor-call exclusions also need owner judgement; the Unity suite remains the source of native/lifecycle acceptance.

## 4. Graph/index ownership

The main-project GitNexus index has no target for the offline runner or its old wrapper/private classifier (`UNKNOWN`). Only read-only queries were authorized there, and reindexing this worktree would modify paths outside this worker's owned scope. Coordinator: refresh the authoritative index after integration, without modifying the worker's scope or claiming graph conformance from missing targets.
