---
id: PLAN-009
type: plan
title: Director pressure and relief
status: SUPERSEDED
created: 2026-09-14
updated: 2026-09-30
owner: Director worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: PLAN-015, PLAN-022
source: none
evidence: none
archived: 2026-09-30
---

# PLAN-009 — Director pressure and relief

> SUPERSEDED and archived on 2026-09-30 at the owner's direction, when SPEC-004 and PLAN-011 to PLAN-026 became LIVE. Successor: [PLAN-015](../../plans/PLAN-015-hunter-behaviour-felt-intelligence.md) and [PLAN-022](../../plans/PLAN-022-camera-catch-degradation-lighting.md). Unchecked criteria below were not met and are not carried forward unless the successor restates them. The original status line follows as history.
> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../../specs/SPEC-002-worsen-game-design.md); see the [registry](../../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Deliver M6 pressure pacing through delayed, imprecise hints and relief spacing, plus one movement-pressure intrusion. Director influences Hunter belief without directly controlling Hunter perception or bypassing goal-oriented action planning (GOAP).

## 2. Starting point

Director does not exist. After [C1](PLAN-002-parallel-coordination.md#3-changes), pure rules may be built against Player/Hunter/Chase/Floor fixtures in parallel. Real validation waits for PLAN-003/005/008 and coordinator I3; intrusion visuals wait for PLAN-006.

## 3. Changes

| # | Owned area | Output |
|---|---|---|
| 1 | `Assets/Scripts/Domain/Director/**` | Manager, Controller, BehaviorState/read-only view if consumed, Config and Definitions |
| 2 | `Assets/Editor/Director/**`, `Assets/Editor/Tests/Director/**`, mirrored Domain Director config assets | Generator, deterministic pacing tests |

Core HintPayload, shared Run tick, Orchestrators and scenes belong to PLAN-002. Director may read Player/Hunter/Chase/Floor in the declared order, call HunterManager.ReceiveHint through HunterRegistry, and publish intrusion facts. Never add the reverse dependency or read Presentation telemetry.

- Run Session ticks Director each fixed tick; Controller accumulates explicit delta time and evaluates at 2 Hz.
- Maintain heat, relief and a bounded timestamped player-position buffer.
- Start heatThreshold 20 s; hint uses position from 3 s ago with radius 8 m. It must not silently use current position when history is missing; define deterministic warmup behavior.
- No hint while relief <10 s. Tighten cadence after the Floor exit opens through configured policy.
- Hunter receives HintPayload age/radius and chooses InvestigateHint through its own belief/planner.
- Stationary/slow for >3 s yields a 2 s intrusion; Manager publishes duration and PostFXOrchestrator routes visuals. No additional threat/attack scope.

## 4. Sequence

1. C1: agree time units, history/warmup, player assignment, hint/proximity events and Floor read-only contract. Supply fixtures for telemetry/PostFX.
2. Implement history, heat/relief and cadence rules with deterministic tests and explicit injected randomness where used.
3. Add Manager wiring and generator; coordinator adds ordered tick/scene/visual routing.
4. Integrate real Hunter and Floor; inspect hint timestamp/radius, relief suppression and end-of-run/reset behavior.
5. With PLAN-010, measure actual pressure gaps and human wandering reports across full floors.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

DirectorControllerTests covers 2 Hz evaluation with variable tick batches, history boundaries/warmup, delayed location accuracy, radius/seed reproducibility, relief equality, exit-state cadence, stationary intrusion, multiple registry entries and scene reset.

Over a full floor, maximum gap between hunter proximity events (<20 m) must not exceed heatThreshold + hintAge + measured travel time. Log how travel time and event windows are measured; do not subtract missing samples. Testers report no wandering stretches. Hint emission alone does not prove the Hunter can reach the player.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Live rather than historical position | Design | Omniscient Hunter | Assert exact sampled timestamp/age |
| Inaccessible hint route | Integration | Pace guarantee fails despite emitted hints | Real path/travel/proximity measurement |
| Relief reset/accounting | Contract | Pressure stacks unfairly | Use frozen chase identity/end facts |
| Evaluation duplicated in Update | Architecture | Timing depends on rendering | Single Run tick; explicit dt only |

## 7. Deferred follow-ups

No boss cadence, player personalization, adaptive difficulty, extra systemic enemies or new hunter actions.

## 8. Definition of done

- [ ] Hints use delayed imprecise information, proper relief and floor-dependent cadence.
- [ ] Intrusion is routed to actual visuals and ends/reset correctly.
- [ ] Pure tests plus full-floor pressure-gap and human wandering acceptance pass.
- [ ] No forbidden dependency or competing tick exists.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned; awaits C1 and later I3 integration | [Coordinator](PLAN-002-parallel-coordination.md) |
| 2026-09-14 | Published implementation and bounded verification | Director pressure, history, relief and cadence are published under Session ownership. Default intrusion reaches real visuals and expires. An arranged default-threshold historical hint produces a three-second-old uncertain belief, next-tick investigation and more than one metre of actual Hunter motor travel. | [Requirement and live evidence matrix](../../../Logs/AgentValidation/GoalCompletion/requirements.md) |
| 2026-09-14 | Remaining acceptance; no archive | LIVE: relief transitions, exit cadence, repeated seeded timing and the full-floor proximity-gap bound with measured travel still need complete evidence. Hint input capture was incomplete; participant reports of no wandering remain required. | [Remaining acceptance](../../../Logs/AgentValidation/GoalCompletion/current-evidence/remaining-acceptance.md) |
| 2026-09-15 | Complete-cohort pressure observations | All 1,721 pressure boundaries and 11 hints reconstruct without differences. Nine interior gaps have maximum 18.383334292 s; 30 boundary-censored gaps have maximum 25.933334686 s. Nine hints have later near poses and two remain censored. All nine complete interior gaps are below the 23-second base threshold before nonnegative travel. Participant no-wandering reports remain open. | [Checkpoint evidence](../../../Logs/AgentValidation/GoalCompletion/planning-update/proposal-005/evidence-matrix.md) |
| 2026-09-15 | Current engineering verified; acceptance remains open | Full regression reports 563 passed / 0 failed / 2 Explicit skips / 565 total, including all 13 architecture cases. Independent native/config/graph/capture audits pass. The later rendered pointer check and both idle/observer/lease closeouts are complete. LIVE: preserve historical failures and unmet numeric/participant/device criteria. No archive is justified. | [Final checkpoint](../../../Logs/AgentValidation/GoalCompletion/planning-update/proposal-005/evidence-matrix.md) |
| 2026-09-30 | Superseded and archived | Owner directed archiving the old LIVE plans; successor PLAN-015, PLAN-022. Acceptance items still open here are not claimed. | [Registry](../../index.md#approval-and-boilerplate-archive-on-2026-09-30) |
