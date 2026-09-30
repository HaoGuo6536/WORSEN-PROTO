---
id: PLAN-012
type: plan
title: Direction-independent survey fixes
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Coordinator role (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-012 — Direction-independent survey fixes

> Status: DRAFT since 2026-09-30. Implements [SPEC-004 §2.8](../specs/SPEC-004-horror-direction-content-proposals.md#28-smaller-fixes-independent-of-direction) items 3, 4, 7, 8, 9 and 10. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Close six contained defects from the 2026-09-30 survey that hold whichever direction wins. The configured Sealed Sills value is the one used. One UseItem press produces exactly one flashlight toggle. Telemetry records progression. A normal build boots the horror run. Stale comments match the code. The four chase and room cues are audible in HorrorRun. The other §2.8 items belong to PLAN-013 (items 1–2), PLAN-020 (hint line, empty slots) and PLAN-023/025 (Interact).

## 2. Starting point

| Item | Location found on 2026-09-30 |
|---|---|
| Sealed Sills | [`ExpeditionSessionController.OptionalWindowMultiplier()`](../../Assets/Scripts/Session/Expedition/Controller/ExpeditionSessionController.cs) tests the trait against a fixed value; [`HorrorEffectsConfig.sealedWindowMultiplier`](../../Assets/Scripts/Session/HorrorEffects/Config/HorrorEffectsConfig.cs) is read by `HorrorEffectsController.OptionalWindowMultiplier`, which SPEC-004 reports is never consumed [verify callers] |
| UseItem | [`HorrorOrchestrator.OnInput`](../../Assets/Scripts/Orchestrator/HorrorOrchestrator.cs) toggles the flashlight on a UseItem press, and [`HorrorEffectsController`](../../Assets/Scripts/Session/HorrorEffects/Controller/HorrorEffectsController.cs) also consumes the press |
| Telemetry | [`TelemetryDefinitions.cs`](../../Assets/Scripts/Core/Definitions/TelemetryDefinitions.cs) has no round, wallet, choice or per-floor seed kinds |
| Build order | [`EditorBuildSettings.asset`](../../ProjectSettings/EditorBuildSettings.asset): TagArena 0, SampleScene 1, FloorLoop 2, HorrorRun 3 |
| Stale comments | [`SceneFlowManager.cs`](../../Assets/Scripts/Session/SceneFlow/Manager/SceneFlowManager.cs) line 28 says FloorLoop is unavailable; [`RunPhase.cs`](../../Assets/Scripts/Core/Definitions/RunPhase.cs) line 22 says later milestones will supply exit and collapse facts |
| Cue gating | [`AudioOrchestrator`](../../Assets/Scripts/Orchestrator/AudioOrchestrator.cs) gates the chase-start, lose, death and room-telegraph cues while an expedition is active; it is unconfirmed that the expansion path plays all four |

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Read the Sealed Sills multiplier from one configured source; delete the literal | `ExpeditionSessionController`, `HorrorEffectsController`, `HorrorEffectsConfig` | — |
| 2 | Route UseItem to a single consumer; the other reads the resulting flashlight state | `HorrorOrchestrator`, `HorrorEffectsController` | — |
| 3 | Add telemetry kinds for round start/end, wallet, each choice (threat, curse, purchase) and per-floor seed | `TelemetryDefinitions` (Core, coordinator), Telemetry presenter/CSV, Session publishers | H1 if the Core change is batched |
| 4 | Put HorrorRun at build index 0; leave the test scenes buildable after it | `EditorBuildSettings.asset` | PLAN-020 title scene later takes index 0 |
| 5 | Correct both stale comments and the headers they sit in | `SceneFlowManager`, `RunPhase` | — |
| 6 | Confirm or add chase-start, lose, death and room-telegraph cues on the expedition path | `AudioOrchestrator`; audio path owned by PLAN-021 | Coordinate with PLAN-021 cue budget |

Item 3 changes a Core definition, so the coordinator applies it. The Telemetry subtree is delegated to this plan's worker for the duration of this plan. Item 6 only confirms or restores the four cues; their final form follows PLAN-021's budget.

## 4. Sequence

1. Run GitNexus upstream impact on `OptionalWindowMultiplier` (both), `HorrorOrchestrator.OnInput`, the UseItem handler in `HorrorEffectsController` and the `AudioOrchestrator` cue handlers. Record callers.
2. Items 1, 2 and 5: small commits with tests.
3. Item 3: propose the telemetry fields at H1, then implement the publishers and CSV columns.
4. Item 6: in HorrorRun, trigger each of the four events and record whether the cue plays; fix only missing routing.
5. Item 4: change build order under the lease, then build and boot once.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Expedition/HorrorEffects tests: a changed `HorrorEffectsConfig` value changes the window multiplier; no literal remains (`rg "0\.4f" Assets/Scripts/Session/Expedition`).
- A test that feeds one UseItem press and asserts one toggle, with orchestrator and effects state agreeing after 1, 2 and 3 presses. A live check in HorrorRun confirms the beam and hunter light response agree.
- Telemetry tests: each new kind is written once per event; a captured HorrorRun session contains round, wallet, choice and seed rows.
- The build boots HorrorRun. Record the build log and first-scene evidence.
- A live record of the four cues playing. If a cue plays only through a legacy path, report that as a fallback.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Which Sealed Sills path is live | Unverified | Fix the wrong one | GitNexus and runtime check first |
| SampleScene in the build list | Scope | Not in SPEC-004 | Report only; owner decides |
| Cue fix collides with PLAN-021 budget | Ownership | Double edits in `AudioOrchestrator` | Coordinator serializes |

## 7. Deferred follow-ups

The Interact binding and HUD hint line (PLAN-020, PLAN-023, PLAN-025); empty inventory slots (PLAN-020, PLAN-024); air control and lunge chaining (PLAN-013).

## 8. Definition of done

- [ ] Sealed Sills reads its configured value; tests prove it.
- [ ] One UseItem press produces one toggle in tests and live.
- [ ] Telemetry records round, wallet, choices and per-floor seed in a real HorrorRun capture.
- [ ] A normal build boots HorrorRun (or the PLAN-020 title scene, once present).
- [ ] Both stale comments are corrected.
- [ ] All four cues are recorded playing in HorrorRun.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
