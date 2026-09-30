# Integration gate

`main` moves only after a candidate passes in Unity. This replaces the earlier flow that fast-forwarded and pushed `main` before running tests (audit finding L2-01).

| Script | Purpose |
|---|---|
| `integrate.ps1 -Branches a,b -Label batchN [-SetupSteps ...] [-CommitPaths ...]` | The gate. Offline pre-check (merge into `wt/integration`, compile, ast-grep, architecture checks); then, under the Unity lease, check out the candidate **detached** in the open checkout, import, run setup, commit generated `.meta` files and declared setup outputs onto the candidate, run the full Edit Mode suite and evaluate the verdict. Pass: `main` moves to the candidate and is pushed with the worker branches, GitNexus re-indexes, and a build and smoke run is reported. Fail: the open checkout returns to `main`, the candidate is kept as `cand/<label>`, nothing is pushed. `-PrecheckOnly` stops after the offline step. |
| `Gate.ps1` / `Gate.Tests.ps1` | Verdict rules and their self-test. |
| `quarantine.json` | Known non-blocking failures: `match` (a full-name glob or `category:<Name>`), `owner`, `reason`, `added`, `expires`. An expired entry blocks again. |
| `compile.ps1` | Offline Roslyn compile of all seven assemblies against the warning baseline (no Unity). |
| `unity-setup.ps1` | Deterministic §10 setup methods (`Worsen.Editor.<System>.<Type>::<Method>`) and one-line snippets, with a structured ok/fail/unknown result per step. It fails on any failure. |
| `run-tests.ps1` | A standalone suite or fixture run under the lease, for diagnosis or an owner-present focused pass. |
| `await-results.ps1` | Waits for the NUnit XML and heartbeats the lease in-script; writes a JSON summary with categories. |
| `build-smoke.ps1` | Mono player build of HorrorRun plus a short headless run; reported in `evidence/build-ledger.jsonl`. |
| `probe.ps1` | Read-only one-line C# probe. |
| `Common.ps1` | Lease, Synaptic bridge, editor state and console helpers. |

## Verdict

- A failure matched by a live quarantine entry does not block.
- Any other failure is **blocking**. A blocking failure that the last promoted ledger entry did not have is **new**, and one new failure fails the gate.
- The blocking count may not exceed the last promoted count (a ratchet: it can only fall).
- Missing results, zero tests, a Unity compile failure or a setup failure fail the gate.

Every run appends one line to [evidence/gate-ledger.jsonl](../../evidence/gate-ledger.jsonl): base, candidate, compile, lint, setup, test totals, failing names, blocking and quarantined counts, new failures, verdict, promotion and push.

## Operating rules

- **While a gate runs, the open checkout is detached at the candidate. Do not commit there** (a commit would land on the candidate); work in a worktree instead. The gate refuses to promote if HEAD moved and saves the stray commit as `rescue/<label>-<stamp>`.
- The gate refuses to switch the open checkout if the active scene has unsaved changes, the editor is busy, or the checkout is not on `main`. It keeps the lease when the editor state is unknown.
- Focus-dependent Play Mode tests carry `[Category("RequiresFocus")]` and are quarantined. Run them in an owner-present pass: `run-tests.ps1 -TestName <fixture>` with Unity in the foreground.
- Fix a red candidate forward. Base the fix worker on `cand/<label>` and gate `cand/<label>` plus the fix branch together.
