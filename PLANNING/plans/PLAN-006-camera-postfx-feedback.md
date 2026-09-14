---
id: PLAN-006
type: plan
title: Camera and post-processing communicate pursuit without obscuring movement
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Camera/PostFX worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-006 — Camera and post-processing feedback

> LIVE since 2026-09-14. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md), through [PLAN-001 M4](PLAN-001-worsen-boilerplate.md#m4--detection-beat-look-back-first-person-readability--1-week), [M6](PLAN-001-worsen-boilerplate.md#m6--director-v1--15-weeks), and [M7](PLAN-001-worsen-boilerplate.md#m7--health-injury-hud--1-week). See the [registry](../index.md).
> This is a bounded decomposition of the previously approved boilerplate execution, with unchanged scope. This planning operation implements nothing and records no milestone as complete.

## 1. Objective

Provide readable first-person camera feedback and post-processing for confirmed pursuit, look-back, movement, injury, and death. Keep all game decisions with their owning Domain/Session systems; these Presentation systems receive facts and apply tunable visual responses.

## 2. Starting point

M0 has a static camera built by [TagArenaSceneSetup](../../Assets/Editor/Scenes/TagArenaSceneSetup.cs), a persistent [RunSessionManager](../../Assets/Scripts/Session/Run/Manager/RunSessionManager.cs), and a working [DebugOverlay Driver stack](../../Assets/Scripts/Presentation/DebugOverlay/Driver/DebugOverlayDriver.cs). That overlay demonstrates explicit initialization and document rebinding; it does not implement this plan's camera or effects. No Camera/PostFX system source exists at decomposition time. Recheck Cinemachine and Universal Render Pipeline package versions before implementation; package configuration belongs to the coordinator.

Entry requires **C1: the Core contract freeze in [PLAN-002](PLAN-002-parallel-coordination.md)**, not completion of PLAN-002. Pure Presenters and tests can proceed against that agreed contract alongside PLAN-003 Player, PLAN-005 Hunter/Chase, and PLAN-009 Director. Live event integration waits for their real publishers and coordinator wiring. No direction document exists; do not invent direction identifiers.

## 3. Changes

| # | Change | Exclusive worker ownership | Depends on |
|---|--------|----------------------------|------------|
| 1 | Camera Service, scene-owned: Manager, Driver, CameraFeedbackPresenter, DriverState, CameraDriverConfig | `Assets/Scripts/Presentation/Camera/**` | C1; player head/pose contract |
| 2 | PostFX Service, scene-owned: Manager, Driver, PostFXPresenter, necessary DriverState, PostFXDriverConfig | `Assets/Scripts/Presentation/PostFX/**` | C1; scalar feedback contracts |
| 3 | Pure tests, config generators, system-specific assets | `Assets/Editor/Tests/{Camera,PostFX}/**`, `Assets/Editor/{Camera,PostFX}/**`, `Assets/Resources/ScriptableObjects/Presentation/{Camera,PostFX}/**`, `Assets/Resources/Prefabs/Presentation/{Camera,PostFX}/**` | Changes 1–2 |

**Excluded ownership:** Core, Domain, Session, Orchestrator, assemblies, packages, global scenes, scene setup, and architecture/registry edits belong to PLAN-002 or the named worker. Request coordinator changes; do not edit those files. Presentation references Core only. Each Manager owns its Driver; visual math belongs in the Presenter, transient data in DriverState, and every tunable in its own DriverConfig. Cinemachine and volume application programming interfaces (APIs) stay inside Drivers.

Preserve these starting values and behavior from the parent:

| Response | Camera/PostFX implementation |
|----------|------------------------------|
| Detection beat | Camera field of view (FOV) kick **+12° over 0.08 s**, decaying over **0.4 s**, with a Driver-owned Cinemachine impulse. PLAN-007 owns the synchronous sting and HUD reduction. |
| Proximity | Map incoming `closeness` 0–1 to peripheral distortion. PLAN-005 alone computes the **4 m → 1, 20 m → 0** distance mapping and rear/front **1.0/0.5** weighting during Confirmed/Lost; do not duplicate it here. |
| Look-back | Ease head yaw to **160° over 0.12 s**; pitch clamp **±20°**; return over **0.15 s**, with optional **0.1 s** re-acquire blur on release. PLAN-003 owns body heading and the **0.35** steering authority; movement verbs remain available. |
| Readability | Base **horizontal FOV 95°**, adding **8°** at normalized maximum speed; slide roll **±6°**, rebound roll **±10°**. Define horizontal-to-camera-lens conversion in the Presenter so aspect ratio does not silently change the intended FOV. |
| Settings | CameraDriverConfig exposes FOV, tilt on/off, and punch intensity; PostFXDriverConfig exposes re-acquire blur on/off and effect tuning. No persisted settings Session. |
| Intrusion/injury/death | Display the supplied Director intrusion duration (**2 s** initial desaturation/static), injury vignette from health facts, and `PlayDeathSnap(killerPos)` toward the hunter. PLAN-003 owns health **100/50/25/0**, damage, and the **5%** injured speed reduction. |
| Optional experiment, last | Disabled-by-default CameraDriverConfig toggle for a **0.5 s** third-person detection pull-out returning to first-person before the hunter closes. Build only after the first-person beat completes a full M3 set of **≥30 measured chases**. |

The coordinator freezes this exact command hand-off at C1, resolving Core enum names without importing Domain definitions:

| Incoming fact / owner | Target Manager command |
|-----------------------|------------------------|
| PLAN-005 `OnChaseStarted`; `OnProximityChanged(float)` | `CameraManager.PlayDetectionKick()`; `PostFXManager.SetPeripheralDistortion(float closeness)` |
| PLAN-003 `OnLookBackChanged(bool)` | `CameraManager.SetLookBack(bool)`; on release, `PostFXManager.PlayReacquireBlur()` |
| PLAN-003 movement and normalized-speed facts | `CameraManager.SetMovementState(MovementState)`; `CameraManager.SetSpeedNormalized(float)`; C1 defines a Core representation |
| PLAN-003 discrete rebound/traversal fact | C1 freezes a separate primitive/Core command for the ±10° rebound response; do not infer rebound from the Ground/Air/Slide/Vault/Stumble enum or add a new movement state |
| PLAN-003 `OnHealthChanged(EntityId,int,int)`; `OnDied(EntityId,Vector3)` | `PostFXManager.SetInjury(int currentHealth,int maxHealth)`; `CameraManager.PlayDeathSnap(Vector3 killerPosition)` |
| PLAN-009 `OnIntrusion(float seconds)`; optional confirmed chase experiment | `PostFXManager.PlayIntrusion(float seconds)`; `CameraManager.PlayDetectionSnap()` |

Supply serialized own-Driver fields, head-anchor/pose binding requirements, prefab/volume/config paths, initialization/teardown signatures, and the above commands to PLAN-002. It alone creates CameraOrchestrator/PostFXOrchestrator, registers them in SPEC-001, and assembles scene-owned head rig/volume wiring. PLAN-003 owns `PlayerLimbStandIn` hands/feet for vault and slide; this worker verifies visibility without editing that sub-driver.

## 4. Sequence

1. Confirm C1, current source and package evidence, and ownership. Classify new files and run GitNexus query/context plus upstream impact on existing targets before each code change; missing/new targets require scoped reference evidence, not an all-clear. Warn before HIGH/CRITICAL changes.
2. Build and test CameraFeedbackPresenter/DriverState and PostFXPresenter/DriverState against explicit time and primitive samples. Include response cancellation/reset, combined effects, horizontal FOV conversion, and disabled comfort settings.
3. Implement thin Managers and Drivers with paired lifecycle, serialized references first, mirrored Resources fallback, and visible missing-asset diagnostics. Scene-owned services have no singleton; reloading resets effect state and releases owned objects.
4. Add own config generators and assets under `Worsen/Camera/...` and `Worsen/PostFX/...`. Hand deterministic rig and volume construction details to PLAN-002 rather than changing global setup or render settings.
5. Coordinate live hookup after real Player/Chase/Director publishers exist; verify look-back, detection, injury, death, and reloads. Preserve the first-person baseline before the optional snap experiment.
6. Run joint acceptance with PLAN-007 and PLAN-010 telemetry, record settings and outcomes, and return evidence to PLAN-002. Unmet measurements remain open gates.

## 5. Verification

Follow [the exclusive Unity protocol](../../tools/coordination/README.md) and [PLAN-002 testing admission](PLAN-002-parallel-coordination.md#testing-admission). Before any Unity mutation or save/publication into Assets, Packages or ProjectSettings of a checkout open in Unity, acquire the exclusive lease, assert its token and verify the actual editor is idle. The coordinator first obtains pause/protocol acknowledgments from already-active writers; cooperating writers then acquire the same lease for each publication, without a new global acknowledgment for every save. While another owner holds it, prepare patches outside imported paths or edit an isolated checkout not open in Unity. Do not overlap tests.

Assert the token before each operation and heartbeat between operations. Use unique `Logs/AgentValidation/PLAN-006/<lease-token>/<UTC-timestamp>/` logs/results, wait for actual completed results, read totals/failures/skips and timestamps, and release in `finally` only after the editor is idle. Started, timed-out, zero-test, or missing-result runs do not pass; do not release a still-running lease. The protocol's commands and recovery rules are authoritative.

- Run `CameraFeedbackPresenterTests` and `PostFXPresenterTests`; cover exact timing endpoints, pitch/yaw bounds, speed normalization, effect composition, optional toggles, and reset behavior with explicit `dt`.
- Complete SPEC-001 §13: headers, pure tests, assembly compilation, ast-grep, fresh GitNexus index and saved conformance queries, ArchitectureConformanceTests and project tests. Coordinate shared index refresh through PLAN-002.
- Verify actual first-person rig, Cinemachine impulse, volume settings, and scene reload teardown. Missing assets must produce a visible diagnostic; flat FOV/no impulse, unchanged volumes, or synthetic publisher samples cannot establish live integration.
- Joint M4 probe with PLAN-007: during a chase, cover the screen for **1 s**; testers identify near/mid/far distance from audio plus proximity feedback **≥70%** of trials. Record audio-only versus full-feedback conditions instead of attributing visible effects to the covered interval.
- With PLAN-003/005/010, average look-back use is **≥1 per chase**, and in-chase vault failures exceed free-movement failures by **no more than 10 percentage points**. Preserve the parent's measured M3 chase-set requirement for the optional snap.
- At default settings, collect **15-minute** play sessions with **no motion-sickness reports**; record sample size and reports rather than declaring universal comfort. Confirm hands/feet and affordances remain readable at **8–14 m/s**.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|------|------|--------|--------------------|
| Missing player pose, movement, or chase contracts | Dependency | Fake integration or forbidden references | Freeze at C1; coordinator owns Core and real routing. |
| Horizontal FOV versus lens vertical FOV | Technical | Incorrect baseline or excessive motion | Camera worker tests aspect-ratio conversion; no direct lens math in Manager. |
| Third-person detection disagreement in SPEC-002 | Optional scope | Obscures the required first-person beat | Keep toggle off, build last, record separate experiment results. |
| Comfort settings and hidden health readability | Acceptance | Effects can obscure traversal or reveal numeric health | Coordinate PLAN-007; no health numbers, explicit comfort measurements. |
| Shared-editor automatic import | Coordination | Invalidates another worker's verification | Lease plus writer freeze; no shared-checkout edits during another lease. |

## 7. Deferred follow-ups

Persisted user settings, alternate camera modes beyond the optional parent experiment, new render packages, player health/movement rules, hunter-distance calculations, and telemetry infrastructure remain outside this plan. PLAN-010 owns recording/playback and the tuning window; expose serialized config fields for it without editing that tool. Coordinator owns packages and all cross-system integration.

## 8. Definition of done

- [ ] Camera/PostFX own their complete, tested presentation stacks and designer configs without forbidden dependencies.
- [ ] Real publisher events reach the exact agreed Manager commands through coordinator-owned routing; scene reloads remove stale targets and effects.
- [ ] Required M4 values, M6 intrusion, and M7 injury/death presentation work; optional snap is either tested after the prerequisite set or explicitly left disabled/deferred.
- [ ] Joint perception, look-back/vault, and comfort measurements have evidence meeting their thresholds; missing human evidence remains unchecked.
- [ ] Deterministic setup hand-off, serialized-first fallback diagnostics, SPEC-001 gates, lease-scoped completed results, and coordinator acceptance are recorded.

## 9. Execution log

| Date | Step | Result | Evidence |
|------|------|--------|----------|
| 2026-09-14 | Decomposition | LIVE child scope inherits the prior PLAN-001 execution approval; planning only, no implementation or completion claim. | Parent M4/M6/M7, SPEC-001/002, M0 source links above. |
