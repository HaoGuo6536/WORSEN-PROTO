# Offline compile and pure-test harness

Compiles the project's assemblies with Unity's bundled Roslyn (`DotNetSdkRoslyn/csc.dll`) and reference assemblies, without starting Unity. Moved here from `Logs/AgentValidation/PLAN-002/offline-compile/` so the gate is versioned; evidence still goes to `Logs/AgentValidation/PLAN-002/offline-compile/<RunName>/` (git-ignored).

| Script | Purpose |
|---|---|
| `Compile-Staged.ps1 -Plans @() -Through Tests -RunName <name> -ProjectRoot <checkout>` | Snapshot `.cs`/`.asmdef` inputs, compile Core → Tests in order with references derived from each asmdef, and record hashes and drift. Usually called through `tools/integration/compile.ps1`. |
| `Lint-Snapshot.ps1` | ast-grep over a preserved snapshot. |
| `Run-PureTests.ps1` + `ManagedPureRunner.cs` + `PureTestPolicy.cs` | Discovers the compiled Edit Mode assembly with NUnit reflection and executes the headless tier on the bundled .NET runtime. |
| `Run-ManagedPure.ps1` | Compatibility entry point for the historic five-fixture filter; preserves `SnapshotName`/`ProjectRoot`, now prints `PURE_RESULT`. |
| `Test-PureRunner.ps1` + `PureTestSelfTests.cs` | Compiles a separate deliberately red fixture and verifies each classification, lifecycle, timeout and filter result. Never adds tests to `Worsen.Tests`. |

Requirements: Unity 6000.3.12f1 installed at the Hub default path, and a checkout whose `Library/ScriptAssemblies` exists (worktrees junction the main `Library`). Each run name must be new; the harness refuses to overwrite evidence.

## Run the headless tier

From the worker checkout, first obtain a successful full compile snapshot:

```powershell
powershell -NoProfile -File tools/integration/compile.ps1 -Worktree "C:/path/to/worktree" -RunName worker-compile-001
powershell -NoProfile -File tools/offline-compile/Run-PureTests.ps1 -Worktree "C:/path/to/worktree" -CompileRun worker-compile-001 -RunName worker-pure-001
```

The runner reads the selected compile run's `bin/Worsen.Tests.dll`, not `Library/ScriptAssemblies/Worsen.Tests.dll`. Dependencies are copied from the compile reference manifest, checking captured and copied SHA-256 hashes; `Library` is only read. Unity's duplicate same-identity `UnityEngine.dll`/`UnityEditor.dll` forwarding facades use the canonical `Managed/` copy. The unused subfolder facade is explicitly recorded in `dependency-manifest.json`; other conflicting dependency identities are rejected.

`-Filter` matches a case's fully qualified NUnit name by case-insensitive substring OR regular expression. Use full fixture names or an anchored expression for precise ownership. Exclusions remain exclusions even if explicitly selected. Examples:

```powershell
# One fixture
powershell -NoProfile -File tools/offline-compile/Run-PureTests.ps1 -Worktree "C:/path/to/worktree" -CompileRun worker-compile-001 -RunName worker-pure-002 -Filter Worsen.Tests.Player.PlayerControllerTests
# The old five fixtures, through the generic entry point
powershell -NoProfile -File tools/offline-compile/Run-PureTests.ps1 -Worktree "C:/path/to/worktree" -CompileRun worker-compile-001 -RunName worker-pure-003 -Filter '^Worsen[.]Tests[.](Level[.](LevelGraphUtilityTests|LevelControllerTests)|Player[.]PlayerMoverPresenterTests|Run[.]RunSessionControllerTests|SceneFlow[.]SceneFlowControllerTests)[.]'
```

Normal output is one `PURE_RESULT passed=N failed=N environment=N skipped=N` line followed by each failure, message and stack. Environment details are in the JSON, not hidden or counted as passes. Exit codes:

- `0`: nonempty selection, complete run, no pure failures. A selection entirely classified environment/skipped can return zero; check `Pure` and do not claim those cases were verified.
- `1`: one or more pure failures, including timeouts or inconclusive results.
- `2`: harness/input/discovery failure or zero selected cases. Missing results are never a pass.

The script never launches `Unity.exe`, uses a Unity bridge/service, or acquires a Unity lease. It runs only bundled `dotnet.exe` for Roslyn, the monitor, and its managed child. Test current directory and NUnit `WorkDirectory` are the evidence directory. This is trusted project-test execution, not a filesystem/network sandbox.

## Semantics and classification

The installed NUnit framework's `NUnitTestAssemblyRunner` and `DefaultTestAssemblyBuilder` supply reflection discovery, `[Test]`, `[TestCase]` including `ExpectedResult`, static field/property/method `[TestCaseSource]`, `[Values]`, inherited setup/teardown and one-time hooks. No alternative NUnit package is downloaded. Parameter combinations and expected results are evaluated by NUnit, not approximated by the harness. An unavailable or invalid case source produces NUnit's single discovery-error case; its unknown data cardinality is not invented.

`[Ignore]`, `[Explicit]`, `[UnityTest]` and inherited `[Category("RequiresFocus")]` are counted as skipped. Fixtures requiring `[UnitySetUp]`/`[UnityTearDown]` coroutine hooks are also skipped: executing their regular `[Test]` bodies without those hooks would produce false failures. Coroutine-only methods omitted by NUnit are counted separately by reflection. A filter does not force Explicit tests to run.

Engine environment results are limited to:

- `SecurityException`/`MissingMethodException` for ECall/InternalCall engine failures. The exact CLR diagnostic `ECall methods must be packaged into a system module.` is accepted even when rejection happens at JIT time before an engine frame can enter the stack; otherwise engine stack/internal-call metadata is required.
- `UnityException` referring to the main thread or editor.
- `NullReferenceException` whose first actual stack frame is in `UnityEngine.*`/`UnityEditor.*`, not merely a later engine frame.
- `UnityEditor` API dependencies: conservative managed Intermediate Language call tracing through the fixture, its hooks/constructors and reachable project code records the exact editor member without invoking it. Branches are not evaluated, so conditional editor calls may conservatively exclude a case. Reflection/virtual dispatch is not fully resolved; executed editor exception stacks are checked too.

Ordinary null references, security errors, missing methods/files/assemblies, assertion failures and invalid test data remain failures. Mixed assertion plus engine-dependent teardown remains a failure, including one-time teardown. Fixture setup/teardown errors are propagated to selected descendants rather than lost behind NUnit's aggregate counts. Environment classification does not emulate or replace engine APIs. Some Unity value-type methods (for example native-backed geometry operations) cannot run on standalone .NET either.

Every selected runnable case, including its setup/teardown, has a provisional 10-second inactivity deadline (`-TimeoutSeconds`, range 0.1–600). One-time hooks and discovery have the same bound. The monitor polls at 20 ms; filter regex matching is bounded to one second. Test output does not reset the watchdog. On timeout the monitor terminates only its owned harness child, records failure, and resumes unfinished cases in a fresh child. One-time hooks repeat for fixtures resumed after a timeout, and test identities must stay stable; this is intentionally not uninterrupted NUnit execution. NUnit uses zero parallel test workers. Assembly-local tests expecting Unity focus, player-loop frames, domain reload or native state still require coordinator verification.

## Evidence and harness self-test

All run artifacts stay under `Logs/AgentValidation/PLAN-002/offline-compile/<RunName>/`:

- `summary.json`: per-case outcomes/messages/stacks, totals by first namespace below `Worsen.Tests`, fixture totals, majority-environment fixture list, pure fraction, test assembly hash, compile run, exit code, execution and total wall time.
- `results.log`: exact printed results; failures retain their messages.
- Dependency and harness-source SHA-256 manifests, copied input assemblies and sources, compiler response/log files, runtime configuration and invocation arguments.
- Per-attempt event journals, completed-case exclusions and raw completed NUnit result XML. Interrupted attempts retain their partial journals.

Run the checked self-test (a compile snapshot is used only to provide existing dependencies):

```powershell
powershell -NoProfile -File tools/offline-compile/Test-PureRunner.ps1 -Worktree "C:/path/to/worktree" -CompileRun worker-compile-001 -RunName harness-self-001
```

The underlying deliberately red assembly is `PureTestSelfTests.dll`; it is never compiled into `Worsen.Tests`. The verifier expects every named outcome, five timeout paths (body, setup, teardown, both one-time hooks), continued execution, NUnit source/Values/ExpectedResult behavior, lifecycle failure precedence, filter/exclusion behavior, correct accounting and immutable overwrite refusal. `self-test-verification.json` and `SELF_TEST_OK` establish success; the underlying runner exit 1 is expected. Direct `Run-PureTests.ps1 -SelfTest` is also available but is not by itself a green verification.

## Measured coverage and owner hand-off

See [MEASUREMENT.md](MEASUREMENT.md) for the full-worktree measurement, all namespace totals, the environment-dominated fixture list and acceptance-target shortfall. See [REQUESTS.md](REQUESTS.md) for exact proposed worker instructions and the candidate integration gate. Those owners' files were not edited. Headless evidence supplements, never replaces, the coordinator's full Unity Edit Mode gate and architecture/scene validation.
