# Integration gate

`main` moves only after a candidate passes in Unity. This replaces the earlier flow that fast-forwarded and pushed `main` before running tests (audit finding L2-01).

| Script | Purpose |
|---|---|
| `integrate.ps1 -Branches a,b -Label batchN [-TestScope auto\|selected\|full] [-SetupSteps ...] [-CommitPaths ...]` | The gate. Offline pre-check (merge into `wt/integration`, compile, full headless pure tier, ast-grep, architecture checks); then, under the Unity lease, check out the candidate **detached** in the open checkout, import, run setup, commit generated `.meta` files and declared setup outputs onto the candidate, select native Edit Mode fixtures and evaluate the verdict. Pass: `main` moves to the candidate and is pushed with the worker branches, GitNexus re-indexes, and a build and smoke run is reported. Fail: the open checkout returns to `main` unless a run may still be active; the candidate is kept as `cand/<label>`, nothing is pushed. `-PrecheckOnly` stops after the offline step. |
| `Gate.ps1` / `Gate.Tests.ps1` | Verdict rules and their self-test. |
| `quarantine.json` | Known non-blocking failures: `match` (a full-name glob or `category:<Name>`), `owner`, `reason`, `added`, `expires`. An expired entry blocks again. |
| `compile.ps1` | Offline Roslyn compile of all seven assemblies against the warning baseline (no Unity). |
| `unity-setup.ps1` | Deterministic §10 setup methods (`Worsen.Editor.<System>.<Type>::<Method>`) and one-line snippets, with a structured ok/fail/unknown result per step. It fails on any failure. |
| `run-tests.ps1` | A standalone suite or fixture run under the lease, for diagnosis or an owner-present focused pass. |
| `await-results.ps1` | Waits for the NUnit XML and heartbeats the lease in-script; writes a JSON summary with categories. |
| `build-smoke.ps1` | Mono player build of HorrorRun plus a short headless run; reported in `evidence/build-ledger.jsonl`. |
| `restart-editor.ps1 [-ThresholdGB 20]` | Restarts the open editor when its committed memory exceeds the threshold (owner rule, 2026-09-30). The editor leaks about 2.5 GB per gate, and at 38 GB the suite took ~2.5 h instead of 8–12 min. It runs under the lease: refuse on a busy editor or an unsaved scene, back up and save the remaining dirty project assets (shaders excluded), exit directly, relaunch, wait for idle. The gate calls it before every Unity stage (`-RestartAboveGB`, 0 to skip). |
| `probe.ps1` | Read-only one-line C# probe. |
| `Common.ps1` | Lease, Synaptic bridge, editor state and console helpers. |
| `select-tests.py` / `test_select_tests.py` | Offline, fail-closed fixture selection from Git snapshots or a changed-path list; standard-library unit tests. |
| `TestSelection.ps1` / `TestResults.ps1` | Promotion safety-net policy, native request construction, NUnit leaf/coverage normalization. |
| `Assets/Editor/Testing/NativeTestRunnerSetup.cs` | One asynchronous native TestRunnerApi run, using checked reflection without new assembly references or vendor changes. |
| `selection-history.py` / `selection-history.json` | Reproducible batch20–24 historical selection evidence. |

## Selective native tests

The offline pure tier runs all its tests on every candidate except exhaustive `*SweepTests` fixtures, which run only in full native suites (owner, 2026-10-01). The native Edit Mode tier is selective; a selected run also drops `*SweepTests` fixtures, whose representative seeds stay in the ordinary fixtures. `-TestScope auto` is the default. `selected` requests selection but **cannot override** any full-suite requirement. Force a full run with:

```powershell
./tools/integration/integrate.ps1 -Branches @('wt/example') -Label batchN -TestScope full
```

The selector reads the **final committed candidate**, after setup output and generated metadata have been committed, against `base_main`. It does not use the current working tree when refs are supplied. For deleted/renamed types it scans both snapshots. No GitNexus result, partial or otherwise, narrows selection.

Selection includes:

- Fixtures declared in changed test files (not merely the file's basename), plus consumers of shared test helpers to a fixed point. Nested helpers are represented by their enclosing type so private names like `Fixture` do not select unrelated systems.
- All fixtures for changed production systems, by folder and namespace convention.
- Tests containing declared type identifiers, including reflection strings, plus **one production/editor consumer hop**. This is lexical dependency analysis, not proof of complete semantic reachability; collisions over-select. Comments are not references.
- Integration, routing, wiring, setup and scene fixtures (including all Expedition, Run, Scenes, HorrorRun and SceneFlow fixtures) for Orchestrator, Session, Core definitions, setup code, scenes, prefabs and Resources changes, or their directly affected wiring consumers.
- Three always-included, no-Play-Mode-entry smoke fixtures: `Worsen.Tests.Architecture.ArchitectureConformanceTests`, `Worsen.Tests.Infrastructure.FixtureTimeSetUpTests`, and `Worsen.Tests.Run.RunFactRelayWiringTests`. The last is a one-test native Manager/channel integration smoke, not a full gameplay playtest.

Full runs are mandatory:

- Every fifth **successful promotion** (`-FullSuiteEvery 5`, provisional), and whenever four promotions have accumulated since a recorded full run. Failed attempts do not advance the counter; a failed due-full gate remains due.
- On `-TestScope full`, or whenever the selector says `full`.
- For asmdef/asmref/DLL/compiler-response changes, `Packages/`, `ProjectSettings/`, test Infrastructure, `FixtureTimeSetUp`, the native runner, integration/offline-test tooling, vendor-reference content, and unclassified paths. Shaders and arbitrary art/tool changes currently fall back to full rather than claiming a dependency mapping.
- When fixture discovery is ambiguous (including inherited, generic or nested fixtures), mandatory smoke is missing, source/ref reading fails, or selector execution fails.
- On first use or a legacy ledger without leaf statuses, to migrate the baseline safely.
- No longer for executed setup alone (owner, 2026-10-01): the files setup rewrote (setup drift) are passed back through the selector with `--changed-files` and their fixtures are added; full is used only when that selection says full or fails. A source-only setup change still uses the selector's widened integration selection.

Documentation-only paths outside Assets are explicitly classified; other unknown paths are not silently ignored. Large mixed batches can legitimately select the full suite.

Offline preview (no Unity, no lease):

```text
python tools/integration/select-tests.py --base main --candidate wt/example
python tools/integration/select-tests.py --changed-files changes.json --output selection.json
```

`changes.json` is a JSON array of exact repository-relative POSIX paths; a UTF-8 newline-separated list is also accepted. Deleted paths without an available base widen to full. JSON includes `scope`, `fixtures`, `fixture_reasons`, `full_reasons`, `all_fixtures`, inventory count and changed paths. A `full` scope means unfiltered Edit Mode execution, not "run only the listed discoverable fixtures."

Standalone fixture lists use the same native runner:

```powershell
./tools/integration/run-tests.ps1 -Purpose 'Focused regression' -Fixtures @('Worsen.Tests.Audio.AudioMixerSetupTests', 'Worsen.Tests.Run.RunFactRelayWiringTests')
./tools/integration/run-tests.ps1 -Purpose 'Focused regression' -FixtureList fixtures.json
```

`-FixtureList` accepts a JSON fixture array, selector JSON's `fixtures` property, or newline-separated names. It is an explicit list, not gate policy: for an unfiltered standalone run omit all filters. Do not pass a selector's `full` output as a subset expecting it to run the whole suite. Empty lists, patterns, namespace prefixes and individual test method names are rejected by the native path. Existing `-TestName` retains the vendor's single-name/prefix diagnostic path, mutually exclusive with lists. No vendor code is modified.

### Native evidence and reloads

The coordinator sends one request JSON beneath `Logs/AgentValidation`. The Editor tool builds one `Filter.groupNames` array of escaped, anchored full-fixture regexes and executes TestRunnerApi **once**, asynchronously (`runSynchronously=false`, so UnityTest coroutines are not filtered out). Full runs omit the assembly and group filters. Checked reflection is necessary because Worsen.Editor has no TestRunner/Worsen.Tests assembly references; API drift is a hard failure, not permission to add references.

The existing `UnityValidationTools` callback remains the sole native NUnit serializer. The runner verifies its registration, temporarily directs it to a new run-specific directory, and uses SessionState to survive domain reloads. Only after the observer's matching finished marker and complete XML summary exist does it atomically publish `result-native.xml` and `complete.json` carrying the coordinator request ID. The previous observer destination is restored. This avoids duplicating callback machinery or changing test infrastructure/vendor sources.

`await-results.ps1 -RunDirectory ... -RunId ...` requires that exact completion/XML pair. Without those options its legacy discovery path still works. The parser retains leaf identities, inherited categories and statuses, detects count mismatches, cancellation and suite-only failures, and checks every requested fixture appears in the results. Explicit ignored cases remain visibly **skipped**, not passes (notably the existing HorrorRunFogWiringTests); a run with no executed cases fails. Missing or malformed evidence never clears a failure. The first coordinator verification must cover a multi-fixture run, a Play Mode/domain-reload fixture such as AudioMixerPlayModeTests, the full migration gate and then a selective gate.

An ambiguous start error or timeout retains the checkout and lease. Do not retry while a request is pending. After confirming no tests are active, preserve the request/output/error files. A coordinator can inspect `SessionState.GetString("Worsen.Testing.PendingRequest", "")`; manual recovery must restore `active-output.txt` from `Worsen.Testing.PreviousOutput` (or remove it if `Worsen.Testing.HadPreviousOutput` is false) before clearing those three SessionState keys. Never clear them under an active run.

### Historical dry run

`python tools/integration/selection-history.py --output tools/integration/selection-history.json` produced:

| Gate | Reconstructed base | Final candidate | Scope | Fixtures | Estimated native time saved |
|---|---|---|---|---|---|
| batch20 | e2e3f10f | 64ec667b | full | 345/345 | 0 min |
| batch21 | edbe1925 | 175d4081 | full | 347/347 | 0 min |
| batch22 | 697d99a4 | 775f5fe4 | full | 347/347 | 0 min |
| batch23 | 88b9a20c | 9a95aa00 | full | 350/350 | 0 min |
| batch24 | 88b9a20c | c977783e | full | 351/351 | 0 min |

`cand/batch22` and `cand/batch24` do not exist in this checkout's refs; their uniquely named gate commits were used explicitly, not fabricated refs. The checked-in ledger ends at batch13. Bases were reconstructed from the first non-merge parent before each contiguous worker merge train; full hashes, fallback provenance and all reasons are in the JSON. Batches20–22 include assembly/test-infrastructure changes and many unmapped assets; batches23–24 include an unmapped shader. No selection saving is claimed for these broad candidates. A narrower real test-only change (`adc1efd`) selects 10 current fixtures, including its shared helper consumers and smoke. Actual native timing savings remain unmeasured; the report's optional estimate is only a uniform-fixture-cost proxy against the owner's 15–25 minute full-suite range, not a benchmark.

Offline self-tests:

```text
python -m unittest discover -s tools/integration -p test_*.py -v
powershell -NoProfile -File tools/integration/Gate.Tests.ps1
```

## Verdict

- A failure matched by a live quarantine entry does not block.
- Any other failure is **blocking**. A blocking failure that the last promoted ledger entry did not have is **new**, and one new failure fails the gate.
- The cumulative blocking count may not exceed the last promoted count (a ratchet: it can only fall). Omitted, skipped or inconclusive tests preserve their last known executed status. Only an explicit `Passed` result clears a prior failure; even full runs do not silently retire deleted/renamed failures. Legacy failure-only history is conservatively retained where the migration full run cannot resolve it; those unresolved entries may require owner review.
- Missing results, zero tests, a Unity compile failure or a setup failure fail the gate.
- Setup drift is evidence, not a verdict. `setup-drift.json` lists the generated files (`.asset`, `.prefab`, `.unity`, `.mat`, `.mixer`, `.controller`, `.anim`, `.wav`, `.meta` under `Assets/Resources`, `Prefabs`, `Scenes` and `Settings`) that the setup steps changed, added or removed. A refactor that claims identical setup output should show none.

Every run appends one line to [evidence/gate-ledger.jsonl](../../evidence/gate-ledger.jsonl): base, candidate, compile, lint, setup, observed test totals, scope, requested fixtures/reasons, actual fixture names, run identity, coverage issues, verdict, promotion and push. `run_failed_names`/`run_blocking_count`/`run_quarantined_count` describe only this run. `test_statuses`, `failed_names`, `blocking_count` and `quarantined_count` carry the cumulative baseline. Only promoted entries become the next baseline. Unrun quarantine entries are not reclassified on expiry; if they fail on a later run, expiry applies then. `test-selection.json` retains the decision next to native evidence. A promoted full run records `full_suite_coverage`: itself plus the promotions accumulated since the last recorded full run. That means the cumulative resulting tree was tested, not that historical hashes were replayed.

## Operating rules

- **While a gate runs, the open checkout is detached at the candidate. Do not commit there** (a commit would land on the candidate); work in a worktree instead. The gate refuses to promote if HEAD moved and saves the stray commit as `rescue/<label>-<stamp>`.
- The gate refuses to switch the open checkout if the active scene has unsaved changes, the editor is busy, or the checkout is not on `main`. It keeps the lease when the editor state is unknown.
- Focus-dependent Play Mode tests carry `[Category("RequiresFocus")]` and are quarantined. Run them in an owner-present pass: `run-tests.ps1 -TestName <fixture>` with Unity in the foreground.
- Fix a red candidate forward. Base the fix worker on `cand/<label>` and gate `cand/<label>` plus the fix branch together.
- **Timed-out suite.** `-TestTimeoutMinutes` (default 240) bounds the wait for the results XML. Play-mode tests dominate the suite, at about 100 s of domain reload each. If the wait expires, the gate fails with `fail-no-results`. It **leaves the open checkout on the candidate and keeps the lease**, because switching branches under a still-running suite would import `main` mid-run.
  Recovery:
  1. Confirm in Unity that the test run has ended and the editor is idle.
  2. Evaluate the late results XML with `Gate.ps1`, or re-run the gate.
  3. Check out `main` and release the lease.
