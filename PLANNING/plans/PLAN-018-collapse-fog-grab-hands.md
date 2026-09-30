---
id: PLAN-018
type: plan
title: Collapse fog and grab hands
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Collapse worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-018 — Collapse fog and grab hands

> Status: DRAFT since 2026-09-30. Implements [SPEC-004 §2.10](../specs/SPEC-004-horror-direction-content-proposals.md#210-collapse-fog-and-grabs) and the §2.5 rows for exit soft-lock, hands as a real cost and deep dark in collapse. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Make collapse read as a dark front advancing through the building, not cells switching on, and make the fog dangerous. Room progress still decides which room dies next. The exit room never collapses. Fog leaks through doorways and thickens as neighbouring rooms begin to die. A room about to collapse pulses faster and faster. A collapsed room is a pitch-black mouth that springs the player back. Hands at the boundary grab, damage and throw, reliably, in play.

## 2. Starting point

- [`RoomCollapseVolume.cs`](../../Assets/Scripts/Domain/Floor/Driver/RoomCollapseVolume.cs) (238 lines): one volume per room with cracks, clipped mist at the far corners and pooled hand objects whose colliders are disabled.
- [`FloorHandController.cs`](../../Assets/Scripts/Domain/Floor/Controller/FloorHandController.cs): warning, escapable slow, one hit per grab and explicit lethal confirmation. Unit tests pass, but in play the grab chain does not fire (SPEC-004 §2.10); the cause is not identified.
- [`FloorController`](../../Assets/Scripts/Domain/Floor/Controller/FloorController.cs) orders collapse by distance to the exit (line 57). `ContactExit` refuses a Closed exit room (line 192), so a Closed exit room soft-locks the run.
- Fog is black from 8 to 24 m across the whole floor (SPEC-004 §2.5). The fog and horror presentation lives in [`HorrorAtmosphereDriver`](../../Assets/Scripts/Presentation/Horror/Driver/HorrorAtmosphereDriver.cs) and [`EnvironmentDriver`](../../Assets/Scripts/Presentation/Environment/Driver/EnvironmentDriver.cs) [verify which owns distance fog].
- LIVE [PLAN-008](PLAN-008-floor-collapse.md) defines collapse closure and lethal behaviour that this plan replaces (PLAN-011 §3.4).

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Exit room never collapses; fog and hands may reach its doorways | Request to PLAN-019 (`FloorController`) | — |
| 2 | Rebuild the grab chain: a trigger at the fog boundary; entering starts a grab; leaving the radius within the grace escapes; otherwise damage, throw away from the fog, cooldown | `FloorHandController`, `RoomCollapseVolume`, hand state | H1 hit and grace contract |
| 3 | Debug overlay shows hand state per room | DebugOverlay request (coordinator) | 2 |
| 4 | Grab rules: set damage then throw; lethal only when the damage kills; no special fatal-grab rule | Hand controller, config | 2; PLAN-011 §6 item 2 |
| 5 | Warning: fog pulses faster and faster before a room collapses | Collapse presenter; audio request to PLAN-021 | — |
| 6 | Collapsed room: rubber-band wall that stretches and springs the player out; touching it starts a grab | Volume, controller | 2 |
| 7 | Hands reach toward remaining cakes as collapse progresses, then snatch them or draw them into the fog; publish a cake-loss fact | Hand presenter; fact to PLAN-019 | — |
| 8 | Fog density field: low-resolution 3D texture written per room, blurred across portals; dark, not white; tendrils along floors and door frames, never ceilings; rounded black mouth on collapsed doorways; faint cold glow only where thin | New Presentation fog field and pass | Portal topology from Core `LevelGraph`; performance budget |
| 9 | Deep dark: fog near distance and torch budget tied to collapse phase; the sweep stays readable | Environment/Horror presentation | 8; PLAN-022 lighting |
| 10 | Hand visuals: a mass of thin black arms pouring from the doorway, after the Gate of Truth reference | Art placeholder, prefab | 8 |
| 11 | Hooks: Wax Ward, Wax Heart, Low Profile, Faster Collapse, Shuffled Collapse (escape route guaranteed), and shared hearing for grabs | Effect view reads; noise events | H1; PLAN-023/024 catalogues |

The domain side (boundary, grab state, cake loss) stays in `Domain/Floor`. The density texture and fog pass are Presentation and read only published collapse progress and portal data (SPEC-001 layering).

## 4. Sequence

1. Run GitNexus upstream impact on `FloorHandController`, `RoomCollapseVolume`, `RoomCollapsePresenter` and the `FloorController` collapse ordering.
2. Reproduce the broken grab chain live under the lease. Record why the tested chain does not fire in play before rebuilding it.
3. Changes 1, 2, 3, 4 and 6 (safety and function first). Then 5 and 7.
4. Spike change 8 in an isolated scene with a declared frame-time budget. Commit to volumetric or screen-space only after the spike. Then 9, 10 and 11.
5. Integrate at H3.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- `FloorHandController` tests: enter, escape inside grace, damage and throw direction, cooldown, kill only by damage, rubber-band contact starting a grab, and Wax Ward breaking the next grab.
- Collapse ordering tests: the exit room is never selected; Shuffled Collapse keeps an escape route.
- A live HorrorRun recording: a grab fires, damages and throws, with the debug overlay showing each state. Passing unit tests without this recording do not satisfy the plan.
- Fog: frame-time measurements against the declared budget on the target machine; screenshots of leaks, the collapsed mouth and the thin-edge glow. Owner review of "front, not tiles".
- A fallback symptom to watch: hands animate but the player takes no damage means the runtime chain is still broken.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Fog field performance in the Universal Render Pipeline | Technical | Frame time | Spike and budget first |
| Unknown cause of the broken grab chain | Investigation | Rebuild could repeat the fault | Step 2 before rebuilding |
| Pulse rate, damage, throw distance, spring force | Open values | Balance | Config; Hao Guo |
| Hands versus cake traps and Mimics | Design | Overlapping rules | PLAN-019/017 agree the order |
| Replaces LIVE PLAN-008 criteria | Governance | History | PLAN-011 §3.4 |

## 7. Deferred follow-ups

Per-theme fog and hand looks (PLAN-026 themes); the lingering-room threat (PLAN-017).

## 8. Definition of done

- [ ] The exit room never collapses in tests and live.
- [ ] The grab chain works live with debug evidence; the grab rules and rubber-band wall hold.
- [ ] Pulse warning, cake snatch and deep-dark tie-in are visible in HorrorRun.
- [ ] The fog field meets its declared budget and passes owner review.
- [ ] Curse and upgrade hooks pass their tests.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
