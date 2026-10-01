param(
    [Parameter(Mandatory = $true)][string[]]$Branches,
    [Parameter(Mandatory = $true)][string]$Label,
    [string]$Plan = 'PLAN-011',
    # Deterministic setup methods ("Worsen.Editor.<System>.<Type>::<Method>") run on the candidate before tests.
    [string[]]$SetupSteps = @(),
    [string[]]$SetupSnippets = @(),
    # Extra paths (git pathspecs) produced by setup to commit onto the candidate, besides .meta files.
    [string[]]$CommitPaths = @(),
    # Wait for the Edit Mode results XML. Play-mode tests dominate (about 100 s of domain reload
    # each): batch 12 needed 116 minutes and batch 13 more than 150.
    [int]$TestTimeoutMinutes = 240,
    [switch]$PrecheckOnly,
    [switch]$NoPush,
    [switch]$NoReindex,
    [switch]$NoBuild
)
# Fail-closed integration gate.
#  1. Offline: merge the branches into wt/integration (reset to main), compile all assemblies,
#     ast-grep, architecture checks. Nothing in the open checkout changes.
#  2. Under the Unity lease: check out the candidate DETACHED in the open checkout (main does
#     not move), refresh, run setup, commit generated .meta files and setup outputs onto the
#     candidate, run the full Edit Mode suite and evaluate the gate (Gate.ps1).
#  3. PASS: move main to the candidate, attach, push main and worker branches, re-index GitNexus.
#     FAIL: check main out again (the editor re-imports the old state), keep the candidate as
#     cand/<Label> for fix workers, and push nothing.
#  Every run appends one line to evidence/gate-ledger.jsonl.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Common.ps1"
. "$PSScriptRoot\Gate.ps1"
$main = $script:Main
$int = Join-Path (Split-Path -Parent $main) 'WORSEN-wt\integration'
$ledger = Join-Path $main 'evidence\gate-ledger.jsonl'
$quarantineFile = Join-Path $PSScriptRoot 'quarantine.json'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$out = Join-Path $main "Logs\AgentValidation\integration\$Label-$stamp"
New-Item -ItemType Directory -Force -Path $out | Out-Null
function Log($m) { $line = "[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $m; $line; Add-Content -LiteralPath (Join-Path $out 'integration.log') -Value $line }
$entry = [ordered]@{ label = $Label; started = (Get-Date).ToString('o'); branches = $Branches; plan = $Plan; promoted = $false; pushed = $false; evidence = $out }

# ---------- 1. offline pre-check ----------
if (-not (Test-Path -LiteralPath $int)) {
    Invoke-Git $main worktree add -q -b wt/integration $int main | Out-Null
    cmd /c mklink /J "$int\Library" "$main\Library" | Out-Null
}
Invoke-Git $int checkout -q wt/integration | Out-Null
Invoke-Git $int reset -q --hard main | Out-Null
# A file committed raw although .gitattributes routes it through LFS reads as modified right after the
# reset (the clean filter turns it into a pointer), and a merge that touches it then refuses. When its
# raw bytes equal the committed blob it is a filter artifact, not an edit: remove only the worktree copy
# of this throwaway checkout; the index (and so the candidate) is unchanged and the next reset restores it.
foreach ($line in @(Invoke-Git $int -c core.quotepath=off status --porcelain)) {
    if ($line -notmatch '^ M (.+)$') { continue }
    $f = $Matches[1].Trim('"')
    $raw = (Invoke-Git $int hash-object --no-filters -- $f) | Select-Object -Last 1
    $blob = (Invoke-Git $int rev-parse "HEAD:$f") | Select-Object -Last 1
    if ($raw -eq $blob) { Remove-Item -LiteralPath (Join-Path $int $f) -Force; Log "Filter artifact (raw blob under an LFS rule), removed from the integration worktree only: $f" }
}
$mainHead = (Invoke-Git $main rev-parse HEAD) | Select-Object -Last 1
$entry.base_main = $mainHead
Log "Candidate base: main $mainHead"
foreach ($b in $Branches) {
    Log "Merging $b"
    Invoke-Native git.exe -C $int merge --no-ff -q -m "Merge $b into main`n`n$script:Attribution" $b | ForEach-Object { Log "  $_" }
    if ($script:NativeExit -ne 0) { Invoke-Native git.exe -C $int merge --abort | Out-Null; throw "Merge conflict for $b; resolve it on a branch and rerun. Nothing published." }
}
$compileOut = & powershell -NoProfile -File (Join-Path $PSScriptRoot 'compile.ps1') -Worktree $int -RunName "int-$Label-$stamp"
$compileExit = $LASTEXITCODE; $compileOut | ForEach-Object { Log "  $_" }
$entry.compile_ok = ($compileExit -eq 0)
# Headless pure tier (no Unity): every pure test in the candidate's compiled test assembly must
# pass. Engine-bound cases are "environment", not passes; they run in the Unity suite below.
$pureScript = Join-Path $int 'tools\offline-compile\Run-PureTests.ps1'
if ($entry.compile_ok -and (Test-Path -LiteralPath $pureScript)) {
    $pureRun = "pure-$Label-$stamp"
    $pureOut = & powershell -NoProfile -File $pureScript -Worktree $int -CompileRun "int-$Label-$stamp" -RunName $pureRun
    $pureExit = $LASTEXITCODE
    $pureOut | Set-Content -LiteralPath (Join-Path $out 'pure-results.log') -Encoding UTF8
    $pureLine = @($pureOut | Where-Object { $_ -match '^PURE_RESULT ' }) | Select-Object -Last 1
    Log "  $pureLine"
    $pureSummary = Join-Path $int "Logs\AgentValidation\PLAN-002\offline-compile\$pureRun\summary.json"
    if (Test-Path -LiteralPath $pureSummary) {
        $pure = Get-Content -LiteralPath $pureSummary -Raw | ConvertFrom-Json
        $entry.pure = [ordered]@{ total = $pure.Total; passed = $pure.Totals.passed; failed = $pure.Totals.failed; environment = $pure.Totals.environment; skipped = $pure.Totals.skipped }
        $entry.pure_ok = ($pureExit -eq 0 -and $pure.Totals.failed -eq 0 -and $pure.Totals.passed -gt 0 -and @($pure.InfrastructureErrors).Count -eq 0)
    } else { $entry.pure_ok = $false }
} else { $entry.pure_ok = $null }
Push-Location $int; $lint = Invoke-Native ast-grep scan; $lintExit = $script:NativeExit; Pop-Location
$lint | Set-Content -LiteralPath (Join-Path $out 'ast-grep.txt')
$entry.lint_ok = ($lintExit -eq 0 -and -not ($lint | Where-Object { $_ -match '^(error|warning)\[' }))
$checks = Join-Path $main 'tools\checks\architecture.py'
if (Test-Path -LiteralPath $checks) {
    $arch = Invoke-Native python $checks --root $int; $archExit = $script:NativeExit
    $arch | Set-Content -LiteralPath (Join-Path $out 'architecture-checks.txt'); $arch | Select-Object -Last 5 | ForEach-Object { Log "  $_" }
    $entry.arch_ok = ($archExit -eq 0)
} else { $entry.arch_ok = $null }
$candidate = (Invoke-Git $int rev-parse HEAD) | Select-Object -Last 1
$entry.candidate = $candidate
$changed = @(Invoke-Git $int diff --name-only "$mainHead..$candidate")
$changed | Set-Content -LiteralPath (Join-Path $out 'changed-files.txt')
if (-not $entry.compile_ok -or -not $entry.lint_ok -or $entry.arch_ok -eq $false -or $entry.pure_ok -eq $false) {
    $entry.verdict = 'fail-precheck'; Add-LedgerEntry $ledger $entry
    throw "Offline pre-check failed (compile=$($entry.compile_ok) lint=$($entry.lint_ok) arch=$($entry.arch_ok) pure=$($entry.pure_ok)); see $out. Nothing published."
}
Log "Offline pre-check passed; candidate $candidate"
if ($PrecheckOnly) { Log 'Precheck only; nothing published.'; return }

# ---------- 2. Unity gate on the detached candidate ----------
$token = Enter-UnityLease $Plan "Gate $Label`: candidate $($candidate.Substring(0,8)) import, setup, Edit Mode suite"
Log "Lease acquired ($($token.Substring(0,8))...)"
$safe = $false; $onCandidate = $false; $keepCandidate = $false; $verdict = $null
try {
    Assert-UnityLease $token
    $s = Get-EditorState
    Log ("Editor before: main={0} idle={1} failed={2} scene={3} dirty={4} timeScale={5}" -f (Test-EditorIsMain $s), (Test-EditorIdle $s), $s.Failed, $s.Scene, $s.Dirty, $s.TimeScale)
    if (-not (Test-EditorIsMain $s)) { throw 'Connected editor is not the main project.' }
    if (-not (Test-EditorIdle $s)) { throw 'Editor busy before the gate; lease retained for coordination.' }
    if ($s.Dirty) { throw 'Active scene has unsaved changes (possibly the owner''s); refusing to switch the checkout.' }
    $branch = (Invoke-Git $main rev-parse --abbrev-ref HEAD) | Select-Object -Last 1
    if ($branch -ne 'main') { throw "Open checkout is on '$branch', expected main." }
    Invoke-Git $main checkout -q --detach $candidate | Out-Null
    $onCandidate = $true
    Log "Open checkout detached at candidate; main unchanged at $mainHead"
    Assert-UnityLease $token
    Invoke-UnityCsharp 'UnityEditor.AssetDatabase.Refresh(); return "refresh-requested";' | Out-Null
    $s = Wait-EditorIdle $token 20
    if (-not $s) { throw 'Editor did not settle within 20 minutes; lease retained.' }
    $console = Get-ConsoleErrors; $console | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'console-errors.json')
    $entry.console_errors = $console.analysis.errors
    Log ("Imported: compileFailed={0} consoleErrors={1}" -f $s.Failed, $console.analysis.errors)
    if ($s.Failed) { $entry.verdict = 'fail-compile'; throw 'Unity reports script compilation failed on the candidate.' }

    if ($SetupSteps.Count + $SetupSnippets.Count -gt 0) {
        $before = Get-GeneratedSnapshot $main
        # In-process call: `powershell -File` would split the string arrays into positional arguments.
        $global:LASTEXITCODE = 0
        $setupOut = & (Join-Path $PSScriptRoot 'unity-setup.ps1') -Purpose "Gate $Label setup" -Steps $SetupSteps -Snippets $SetupSnippets -Token $token -OutJson (Join-Path $out 'setup.json') -ContinueOnError
        $setupExit = $LASTEXITCODE; $setupOut | ForEach-Object { Log "  $_" }
        $entry.setup_ok = ($setupExit -eq 0)
        # Evidence, not a verdict: which generated files the setup rewrote (parity claims are checkable).
        $drift = Compare-GeneratedSnapshot $before (Get-GeneratedSnapshot $main)
        $drift | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $out 'setup-drift.json')
        $entry.setup_drift = [ordered]@{ changed = $drift.changed.Count; added = $drift.added.Count; removed = $drift.removed.Count; files = $drift.files }
        Log ("Setup drift: changed={0} added={1} removed={2} of {3} generated files (setup-drift.json)" -f $drift.changed.Count, $drift.added.Count, $drift.removed.Count, $drift.files)
    } else { $entry.setup_ok = $true }

    # Commit Unity-generated .meta files for merged paths and declared setup outputs onto the candidate.
    $wanted = @{}
    foreach ($f in $changed) { $wanted["$f.meta"] = $true; $d = Split-Path -Parent $f; while ($d) { $wanted[($d.Replace('\', '/')) + '.meta'] = $true; $d = Split-Path -Parent $d } }
    $untracked = @((Invoke-Git $main -c core.quotepath=off status --porcelain --untracked-files=all) | Where-Object { $_ -match '^\?\? (.+\.meta)$' } | ForEach-Object { ($_ -replace '^\?\? ', '').Trim('"') })
    $metas = @($untracked | Where-Object { $wanted.ContainsKey($_) })
    # Checked and lock-tolerant: batch 14's adds silently failed on a stale index.lock, so the
    # candidate was tested without its meta files ever being committed.
    if ($metas.Count -gt 0) { Invoke-Git $main add -- @metas | Out-Null }
    if ($CommitPaths.Count -gt 0) { Invoke-Git $main add -- @CommitPaths | Out-Null }
    Invoke-Native git.exe -C $main diff --cached --quiet | Out-Null
    if ($script:NativeExit -ne 0) {
        Invoke-Git $main commit -q -m "Gate $Label`: Unity meta files and setup outputs`n`n$script:Attribution" | Out-Null
        $candidate = (Invoke-Git $main rev-parse HEAD) | Select-Object -Last 1
        Log "Gate commit $candidate ($($metas.Count) meta file(s), setup paths: $($CommitPaths -join ', '))"
    }
    $entry.candidate_final = $candidate

    Assert-UnityLease $token
    # Unity's SceneView repaint throws NullReferenceException during Play Mode tests in a fresh
    # editor (batch 14: 7 tests failed on that engine-only log). Close Scene views for the
    # suite and reopen one afterwards through Window/General/Scene (GetWindow cannot create a window
    # from Synaptic's execution context; verified 2026-09-30).
    $sceneViews = Invoke-UnityCsharp 'int n = 0; foreach (UnityEditor.SceneView sv in new System.Collections.ArrayList(UnityEditor.SceneView.sceneViews)) { if (sv != null) { sv.Close(); n++; } } return "closed=" + n;'
    $entry.scene_views_closed = [int]($sceneViews -replace '^closed=', '')
    Log "Scene views closed for the suite: $sceneViews"
    Log 'Running full Edit Mode suite'
    $since = Get-Date
    $start = Invoke-UnityCsharp 'return SynapticPro.TestRunner.NexusTestRunnerService.Execute("run", "editmode", "");'
    Log "Test start: $start"
    $resultsJson = Join-Path $out 'editmode-results.json'
    $waitOut = & powershell -NoProfile -File (Join-Path $PSScriptRoot 'await-results.ps1') -Token $token -Since $since.ToString('o') -TimeoutMinutes $TestTimeoutMinutes -OutJson $resultsJson
    $waitExit = $LASTEXITCODE
    $waitOut | Set-Content -LiteralPath (Join-Path $out 'editmode-summary.txt')
    $waitOut | Select-Object -First 3 | ForEach-Object { Log "  $_" }
    if ($waitExit -ne 0) {
        # Unity may still be running the suite. Switching the checkout now would import main's
        # files mid-run, so leave the candidate checked out and the lease held.
        $entry.verdict = 'fail-no-results'; $keepCandidate = $true
        throw "No test results within $TestTimeoutMinutes min. The open checkout stays on the candidate and the lease is retained until the run is confirmed finished (README: 'Timed-out suite')."
    }
    $s = Wait-EditorIdle $token 10
    if ($s -and $s.TimeScale -ne 1) { Log "WARNING: Time.timeScale is $($s.TimeScale) after the suite (a test leaked it)" }
    if ($entry.scene_views_closed -gt 0) {
        try { Log ("Scene view restored: " + (Invoke-UnityCsharp 'bool ok = UnityEditor.EditorApplication.ExecuteMenuItem("Window/General/Scene"); return "menu=" + ok + " sceneViews=" + UnityEditor.SceneView.sceneViews.Count;')) } catch { Log "WARNING: could not reopen the Scene view: $($_.Exception.Message)" }
    }
    # Nobody may commit in the open checkout while it is detached for the gate; a moved HEAD
    # means the tested tree is not the candidate, so keep that work on a rescue branch and stop.
    $headNow = (Invoke-Git $main rev-parse HEAD) | Select-Object -Last 1
    if ($headNow -ne $candidate) {
        Invoke-Git $main branch -f "rescue/$Label-$stamp" $headNow | Out-Null
        Invoke-Git $main reset -q --soft $candidate | Out-Null
        $entry.verdict = 'fail-head-moved'
        throw "HEAD moved during the gate ($headNow); saved as rescue/$Label-$stamp; not promoting."
    }
    $results = Get-Content -LiteralPath $resultsJson -Raw | ConvertFrom-Json
    $baseline = Get-LastPromoted $ledger
    $verdict = Get-GateVerdict $results (Get-Quarantine $quarantineFile) $baseline
    $entry.tests = [ordered]@{ total = $results.total; passed = $results.passed; failed = $results.failed; skipped = $results.skipped }
    $entry.failed_names = @($results.failures | ForEach-Object { $_.name })
    $entry.blocking_count = $verdict.blocking.Count
    $entry.quarantined_count = $verdict.quarantined.Count
    $entry.new_failures = $verdict.new
    $entry.baseline_label = if ($baseline) { $baseline.label } else { $null }
    $pass = $verdict.pass -and $entry.setup_ok
    $entry.verdict = if ($pass) { 'pass' } elseif (-not $entry.setup_ok) { 'fail-setup' } else { 'fail-tests' }
    Log ("Gate verdict: {0}; blocking={1} (baseline {2}) quarantined={3} new={4} {5}" -f $entry.verdict, $verdict.blocking.Count, $(if ($baseline) { $baseline.blocking_count } else { 0 }), $verdict.quarantined.Count, $verdict.new.Count, ($verdict.reasons -join '; '))
    foreach ($n in $verdict.new) { Log "  NEW FAILURE $n" }

    if ($pass) {
        Invoke-Git $main update-ref refs/heads/main $candidate $mainHead | Out-Null
        Invoke-Git $main checkout -q main | Out-Null
        $onCandidate = $false; $entry.promoted = $true
        Log "PROMOTED: main $mainHead -> $candidate"
    } else {
        Invoke-Git $main branch -f "cand/$Label" $candidate | Out-Null
        Invoke-Git $main checkout -q main | Out-Null
        $onCandidate = $false
        Log "NOT PROMOTED: candidate kept as cand/$Label; open checkout back on main $mainHead"
        Invoke-UnityCsharp 'UnityEditor.AssetDatabase.Refresh(); return "refresh-requested";' | Out-Null
    }
    $s = Wait-EditorIdle $token 20
    if ($s -and (Test-EditorIdle $s)) { $safe = $true }
} catch {
    Log "ERROR: $($_.Exception.Message)"
    if (-not $entry.verdict) { $entry.verdict = 'fail-error' }
    if ($onCandidate -and $keepCandidate) {
        try { Invoke-Git $main branch -f "cand/$Label" (Invoke-Git $main rev-parse HEAD | Select-Object -Last 1) | Out-Null } catch { }
        Log "Open checkout left on the candidate (cand/$Label): the suite may still be running"
    } elseif ($onCandidate) {
        try { Invoke-Git $main branch -f "cand/$Label" (Invoke-Git $main rev-parse HEAD | Select-Object -Last 1) | Out-Null; Invoke-Git $main checkout -q main | Out-Null; Log "Restored open checkout to main; candidate kept as cand/$Label" } catch { Log "RESTORE FAILED: $($_.Exception.Message)" }
    }
    throw
} finally {
    $entry.ended = (Get-Date).ToString('o')
    if ($safe) { Exit-UnityLease $token; Log 'Lease released.' } else { Log "LEASE RETAINED (token $token). Verify the editor, then release." }
    if ($entry.promoted -and -not $NoPush) {
        $refs = @('main') + @((Invoke-Git $main for-each-ref --format='%(refname)' refs/heads/wt/) | Where-Object { $_ -ne 'refs/heads/wt/integration' } | ForEach-Object { "${_}:${_}" })
        $push = Invoke-Native git.exe -C $main push origin @refs
        $entry.pushed = ($script:NativeExit -eq 0); $push | Select-Object -Last 3 | ForEach-Object { Log "  push: $_" }
    }
    Add-LedgerEntry $ledger $entry
    Log "Ledger: $ledger"
}
if ($entry.promoted -and -not $NoReindex) {
    Push-Location $main; $idx = Invoke-Native node .gitnexus/run.cjs analyze --index-only; Pop-Location
    $idx | Select-Object -Last 2 | ForEach-Object { Log "  gitnexus: $_" }
}
if ($entry.promoted -and -not $NoBuild) {
    # Reported, not blocking (owner decision 2026-09-30); see evidence/build-ledger.jsonl.
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'build-smoke.ps1') -Label $Label -Plan $Plan | ForEach-Object { Log "  build: $_" }
}
Log "Evidence: $out"
if (-not $entry.promoted) { exit 1 }
