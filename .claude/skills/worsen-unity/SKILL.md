---
name: worsen-unity
description: Operate the open WORSEN Unity editor safely - Synaptic HTTP bridge (localhost:8086), the exclusive Unity lease, run_csharp quirks, setup/test/build scripts in tools/integration, and Unity CLI batch use. Use before any Unity inspection, mutation, test run, import, build or package change.
---

# Unity operations for WORSEN

Agents that are delegated workers never run Unity; this skill is for the coordinator or a directly working agent. The project is Unity 6000.3.12f1 (`ProjectSettings/ProjectVersion.txt`), usually open in the editor on the owner's machine.

## 1. The lease comes first

Acquire the exclusive lease with `tools/coordination/UnityTestLease.ps1` before anything that could disturb another operator or the owner:

- Play Mode, test runs and builds;
- scene, prefab or setup changes, NavMesh baking and package changes;
- asset refreshes or imports;
- saving files under `Assets/`, `Packages/` or `ProjectSettings/` in the open checkout.

Details and recovery: [tools/coordination/README.md](../../../tools/coordination/README.md).

- `Acquire` must succeed and return your token. A free status reading is not ownership. Never steal or clear another owner's lease.
- After acquiring, verify the editor:
  - it is the intended project (`Application.dataPath`);
  - it is not compiling, updating or in Play Mode;
  - no foreign test or build is running;
  - you know the dirty-scene state. Never save a scene you did not intend to change.
- Run `AssertOwner` before each mutation. Heartbeat **inside scripts**, never as separate model turns.
- Release only when the editor is idle. A tool timeout does not prove an operation stopped.
- The scripts in `tools/integration/` do all of this for you; prefer them over ad-hoc calls.

## 2. Synaptic HTTP bridge (the only supported control path)

Endpoint: `http://localhost:8086`. `GET /health`; discover with `GET /tools` or `GET /tools/category/<c>`; execute with `POST /execute {"tool": ..., "params": ...}`.

- `unity_run_csharp` runs one snippet. It must be a **single line with a single top-level `return <string>;` at the end**. A `return` inside `try`/`catch` is lost silently (`resultSet:false`), so assign to a variable and return once. Treat `resultSet:false` as failure, never as an empty success.
- Direct references to `Worsen.Editor.*` types do not compile in snippets. Use `System.Type.GetType("Worsen.Editor.<Ns>.<Type>, Worsen.Editor")` and invoke by reflection, or `tools/integration/unity-setup.ps1 -Steps 'Worsen.Editor.<Ns>.<Type>::<Method>'`.
- A snippet that triggers recompilation or a domain reload loses its result. Wait for the editor to settle, then re-read the state you expected to change.
- Console: `unity_console` read is broken in this build ("Unknown operation: CONSOLE"). Use `unity_analyze_console_logs` (`logType: error`), or `Get-ConsoleErrors` in `tools/integration/Common.ps1`.
- Tests: Synaptic's `unity_run_tests` route is broken. The scripts start `SynapticPro.TestRunner.NexusTestRunnerService.Execute("run","editmode",<filter>)` and read the NUnit XML that UnityValidationTools persists (`Logs/AgentValidation/active-output.txt` names the folder).
- A tool response's `success:true` is not evidence. Decode `result` and check for the expected value.

The `unity-synaptic` MCP transport is unreliable here. The old community `unityMCP` server was removed on 2026-09-30. A pilot of Unity's own CLI and `com.unity.pipeline` is approved but not yet adopted; until it passes, Synaptic HTTP is the only documented path.

## 3. Scripts (tools/integration)

| Need | Command (PowerShell) |
|---|---|
| Offline compile, no Unity | `tools/integration/compile.ps1 -Worktree <checkout> -RunName <new-name>` |
| Gate worker branches into main | `tools/integration/integrate.ps1 -Branches wt/a,wt/b -Label batchN [-SetupSteps ...] [-SetupSnippets ...] [-CommitPaths ...]` |
| Setup methods under the lease | `tools/integration/unity-setup.ps1 -Purpose <why> -Steps 'Worsen.Editor.X.Y::Method'` |
| A diagnostic or focused test run | `tools/integration/run-tests.ps1 -Purpose <why> [-TestName <fixture or test>]` |
| Build and smoke run | `tools/integration/build-smoke.ps1 -Label <label>` |
| Read-only probe | `tools/integration/probe.ps1 -Code 'return UnityEngine.Application.dataPath;'` |

The gate rules (candidate first, ratchet, quarantine, ledger) are in [tools/integration/README.md](../../../tools/integration/README.md). **While a gate runs, the open checkout is detached at the candidate: do not commit there.**

## 4. Unity CLI (separate process)

Use `C:/Program Files/Unity/Hub/Editor/6000.3.12f1/Editor/Unity.exe` only against a checkout that is **not** open in the editor; never open a second editor on the open project, and never close the owner's editor. Edit Mode tests: `-batchmode -projectPath <p> -runTests -testPlatform EditMode -testResults <fresh.xml> -logFile <log>` (no `-quit`). Compile check: `-batchmode -quit -projectPath <p> -logFile <log>`. `-executeMethod` only with an existing, inspected static editor method. Zero tests or a missing result file is not a pass.

## 5. Evidence

Report counts from the XML or JSON, never from a command's exit alone. Record gate runs in `evidence/gate-ledger.jsonl` (the gate does this) and cite that file rather than a `Logs/` path. The following tests are quarantined, with owner, reason and expiry, in `tools/integration/quarantine.json`:

- focus-dependent tests (`[Category("RequiresFocus")]`), which fail when Unity is not the foreground window;
- known failures.
