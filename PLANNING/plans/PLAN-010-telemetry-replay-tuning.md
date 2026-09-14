---
id: PLAN-010
type: plan
title: Telemetry replay and tuning
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Instrumentation worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-010 — Telemetry replay and tuning

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Bring M8 instrumentation forward so other plans can measure their exit criteria: telemetry, deterministic input/probe replay and an Editor tuning window. This workstream owns the existing Input service extensions; Player and coordinator workers request changes rather than editing it concurrently.

## 2. Starting point

M0 has a live input buffer and run seed/tick, pure buffer tests and a debug overlay. It has no recordings, telemetry file writer or tuning window. Actual physical input delivery remains to be verified. Begin after [C1](PLAN-002-parallel-coordination.md#3-changes), in parallel with Player, Level and presentation; integration expands as producers land.

## 3. Changes

| # | Owned area | Output |
|---|---|---|
| 1 | `Assets/Scripts/Presentation/Telemetry/**`, its mirrored resources and `Assets/Editor/Tests/Telemetry/**` | Manager, Driver, Presenter/DriverState/Config; one comma-separated values (CSV) file per session under persistentDataPath |
| 2 | `Assets/Scripts/Presentation/Input/**`, `Assets/Editor/Tests/Input/**`, mirrored Input configs and `Assets/Editor/Input/**` | InputRecorder sub-driver, Live/Playback source selection, versioned record/playback and existing buffer regression protection |
| 3 | `Assets/Editor/Tuning/**`, `Assets/Editor/Telemetry/**` | TuningWindow and instrumentation setup/analysis tools |

Core record/summary payloads, Session seed distribution, producer events, DebugOverlay and TelemetryOrchestrator belong to PLAN-002 or their feature owners. Request missing samples from those owners. Never read Domain state directly from Presentation or build gameplay RunSummary from telemetry.

### Data contract and measurements

- Tick-stamp per-chase duration/outcome (Lunge/Cornered/Lost), per-tick horizontal speed in m/s tagged free-movement/in-chase, input-lock start/end/reason, look-back start/duration/catch within 1 s after release, vault attempts/failures tagged in-chase/free, heat/proximity gaps and floor time. Player supplies lock facts; coordinator freezes/routes them. Exclude vertical velocity and chase samples from M1 free-speed percentiles.
- Agree chase identity, grace/reacquisition and Cornered classification before writing reports. Record incomplete/unknown outcomes separately; never silently bias denominators.
- Record run seed, config/profile values or immutable snapshot hashes, source revision/hash, fixed timestep, record-schema version and capture start/end/completeness.
- InputRecorder belongs to PlayerInputDriver; InputManager exposes source selection. Playback preserves held/pressed/released masks and look degrees exactly, disables live mixing, and restores live input/focus gates on completion/failure.
- PLAN-003 supplies tick-aligned MovementProbe records; replay pairs them with InputFrame to test Controller state trajectory without a scene. Physics itself is not claimed deterministic from input alone.
- Coordinator distributes the same seeded Random to factories/Floor/Director in fixed order. Record seed plus consumption-order/source/config provenance; a seed alone cannot reproduce changed code/tuning.
- TuningWindow at `Worsen/Tuning/Open Tuning Window` uses SerializedObject to edit Config/Profile/DriverConfig assets. No runtime immediate-mode graphical user interface (GUI) or public ScriptableObject setters. Discover current assets rather than maintaining a conflicting shared registry.

## 4. Sequence

1. C1: freeze schemas with all publishers and deliver minimal fixture-backed recording/report application programming interfaces (APIs).
2. Implement pure telemetry aggregation/tests, file output/error reporting and InputRecorder/source selection with startup/focus/scene gating tests.
3. I1: validate physical keyboard/mouse/gamepad input, record real input/probes and deliver M1 deterministic replay/free-speed report.
4. I2: wire coordinator-owned TelemetryOrchestrator to real Hunter/Chase/player events, detect missing streams and deliver ≥30-chase and M4 perception/look-back/vault-rate reports.
5. I3: add real Floor/Director measurements and run-end flush; provide acceptance reports to each owner. Implement tuning window without changing gameplay defaults.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

TelemetryPresenterTests covers window boundaries, catch-within-1-second definition, missing data, event ordering/duplicates, rates/percentiles and completed/incomplete session flush. Extend Input tests for live/playback isolation, end/failure/focus/scene reset, short taps and single-consumption edges. Test output path permission failure explicitly; never claim a run was recorded if file creation/flush failed.

Reports must support M1 free-movement horizontal p90 speed ≥9 m/s, verb-transition input locks ≤0.35 s and identical replay; M3 ≥30 chases with median 12–25 s, loss ≥40%, lunge-catch ≥60%; M4 ≥70% near/mid/far recognition, look-back ≥1/chase and chase vault-failure increase ≤10 percentage points; M5 first sweep 2–4 min; M6 pressure-gap bound. Human comfort/perception answers are recorded as such, not generated from telemetry. Keep raw data beside derived reports.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Event drift | Integration | Plausible but wrong reports | C1 versions, missing-stream detection, producer fixture/live comparisons |
| Input recording affects gameplay | Regression | Duplicate/missed edges | Single source and preserved M0 regression tests |
| Ignored device events in tool context | Verification | False hardware-input pass | Real focused Game view/device session; state-buffer injection is a separate check |
| Reproducibility claim too broad | Evidence | Same seed differs with code/config/physics | Log source/config/probe provenance and scope the claim |
| Conflicting tuning changes | Ownership | Acceptance cannot be reproduced | Coordinate exact assets; lease edits and retain before/after snapshots |

## 7. Deferred follow-ups

No network replay/prediction, cloud analytics, dashboards, persistent user settings or generalized benchmarking service.

## 8. Definition of done

- [ ] Recording/playback preserves real input semantics and pure replay reproduces Player state.
- [ ] Real event streams produce complete files and honest derived reports; missing/failure paths are visible.
- [ ] Editor tuning updates only intended serialized assets and preserves architectural restrictions.
- [ ] Each consumer receives its measurement evidence; subjective acceptance is not falsely automated.
- [ ] Coordinator routing and seed integration are verified under the testing lease.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned early to support measured gates; awaits C1 | [Coordinator](PLAN-002-parallel-coordination.md) |
