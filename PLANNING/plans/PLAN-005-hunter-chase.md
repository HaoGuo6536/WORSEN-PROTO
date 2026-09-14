---
id: PLAN-005
type: plan
title: Hunter behavior and chase rules
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Hunter and Chase worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-005 — Hunter behavior and chase rules

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Deliver M3's single-hunter tag loop: observable sensing/belief, goal-oriented action planning (GOAP), inertial steering, committed lunges, chase confirmation/loss and proximity facts. Keeping Hunter and Chase under one worker avoids competing changes to their tightly coupled state/event contracts.

## 2. Starting point

M0 has no Hunter/Chase/Player/Level. Begin pure development after [C1](PLAN-002-parallel-coordination.md#3-changes) using agreed Player/Level fixtures; integrated behavior waits for PLAN-003/004 and coordinator I1. Measured M3 acceptance also requires PLAN-010 telemetry and I2 feedback.

## 3. Changes

| # | Owned area | Output |
|---|---|---|
| 1 | `Assets/Scripts/Domain/Hunter/**` | Manager/Factory/Registry, Controller, BehaviorState/read-only view, HunterProfile, Definitions, GoapPlannerUtility |
| 2 | Hunter Driver subtree | Navigation mesh (NavMesh) path probes, sight/contact probes, HunterSteeringPresenter, DriverState/Config |
| 3 | `Assets/Scripts/Domain/Chase/**` | Manager, Controller, BehaviorState/read-only view, ChaseConfig/Definitions |
| 4 | `Assets/Editor/{Hunter,Chase}/**`, `Assets/Editor/Tests/{Hunter,Chase}/**`, `Assets/Prefabs/Hunter/**`, mirrored Domain Hunter/Chase config assets | Generators, tests and archetype |

Core payloads, EntityContext, Run hit routing, Orchestrators and shared scenes are coordinator-owned. Hunter reads Player/Level through injected Domain views; Core EntityContext remains Core-only. Hunter never calls Player; its hit event carries identities and damage for Session routing.

### Required behavior and starting values

- Acceleration 20 m/s², turn rate 240°/s; chase speed 1.12× player sprint.
- Lunge windup/active/recovery 0.25/0.30/0.80 s, distance 4 m, speed 18 m/s; recovery speed zero. Controller decides phases, Presenter computes motion, Driver checks contacts and Manager resolves EntityId.
- Sight cone 110°, range 30 m; sensor evaluation every 4 ticks. Driver probes head/chest/hips, any visible sample suffices; cone/range decisions are pure.
- Hearing range 18 m; read tick-stamped Player noises, loudness with distance falloff. Belief stores last known position/tick/confidence, decays over 8 s; delayed imprecise Director hints enter through ReceiveHint.
- GOAP keys: PlayerVisible, PlayerHeard, HasBelief, BeliefFresh, InLungeRange, LoopDetected, HasHint. Goals CatchPlayer > FindPlayer > Patrol. Actions Patrol, InvestigateHint, Chase, Lunge, SearchLastKnown, CutOff; Stalk optional, BreakLoop v2. A* utility replans on changed keys/action failure; keep the initial planner small (~100 lines target).
- NavMesh supplies paths only; Presenter turns corners into acceleration/turn-limited steering. No NavMeshAgent steering that bypasses these tunables.
- Chase confirmation requires continuous sight 0.3 s. Loss requires BOTH no sight for 2.5 s AND distance >14 m; Lost grace 1.5 s before None.
- Closeness: distance 4→20 m maps 1→0, rear weight 1.0/front weight 0.5 during Confirmed/Lost. Emit primitive changes for feedback.
- Registry/state collection supports N hunters, but ship and test gameplay with one.

## 4. Sequence

1. C1: freeze sight/proximity/hit/chase identity contracts and typed Factory injection. Resolve the reacquisition/grace accounting and Cornered outcome questions with the coordinator before telemetry classification.
2. Implement pure sensing/belief/GOAP/chase tests with fixture Player/Level state.
3. Implement Driver probes and steering, deterministic Hunter prefab/generator; provide setup entry point to coordinator.
4. Integrate real Player/Level via I2; verify hard-cut gain versus straight-corridor loss and costly missed lunges.
5. Connect PLAN-006/007 feedback and PLAN-010 measurements through coordinator-owned routers. Run the measured acceptance set, tune only owned Config/Profile values with provenance.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

Run HunterControllerTests, GoapPlannerUtilityTests, HunterSteeringPresenterTests and ChaseControllerTests. Cover threshold equality, all three sight samples, hearing age, hint age/radius, goal changes, unreachable paths, lunge contact multiplicity, reset/reuse, AND loss rule, grace/reacquisition and closeness bounds.

Measure ≥30 real chases: median 12–25 s; ≥40% end by loss rather than catch; ≥60% of catches are lunges rather than cornered catches. Record denominator and an agreed Cornered classification; do not claim 100% by leaving other catches unclassified. Show hard cuts gaining ground in the mid room and straight corridors losing ground. Human description must support “tag”; metrics alone do not establish feel.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Lost→Confirmed and chase identity | Design accounting | Inflates chase/loss counts | Coordinator freezes reacquisition and grace semantics before recording |
| Cornered outcome lacks explicit trigger | Design accounting | Catch ratio could become tautological | Define classification from actual accepted gameplay, not an invented attack |
| Stale/fake player view | Integration | Sensors pass fixtures but miss live player | Real pose/noise/probe tests at I2 |
| Runtime route/scene edits | Ownership | Breaks other workers | Submit to coordinator only |

## 7. Deferred follow-ups

BreakLoop v2, advanced stalking, multiple hunters, behavior trees and new attack types are deferred. Director policy belongs to PLAN-009; health rules belong to PLAN-003.

## 8. Definition of done

- [ ] One live Hunter follows paths with inertial steering and committed, correctly routed lunge hits.
- [ ] Sensing/belief/GOAP/chase tests pass and no forbidden Domain dependencies exist.
- [ ] ≥30-chase metrics and human route/feel evidence meet M3.
- [ ] Feedback/telemetry observe real events and all unresolved acceptance gaps remain explicit.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned; awaits C1, then real Player/Level integration | [Coordinator](PLAN-002-parallel-coordination.md) |
