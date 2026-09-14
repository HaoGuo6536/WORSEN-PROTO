---
id: PLAN-003
type: plan
title: Player movement and health
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Player worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-003 — Player movement and health

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Deliver PLAN-001 M1 movement and M7 player health in one owned workstream, avoiding two agents editing PlayerController/BehaviorState. Keep the full sprint → slide → slide-jump → vault → rebound → landing chain, look-back steering and injury/death facts. [PLAN-001 §3](PLAN-001-worsen-boilerplate.md#3-milestones-build-order-each-gates-the-next) owns gameplay values.

## 2. Starting point

M0 has InputFrame/MovementProbe and a tick source but no Player. Begin implementation only after [PLAN-002 C1](PLAN-002-parallel-coordination.md#3-changes) freezes contracts. Player and Level can then progress in parallel. Live traversal waits for PLAN-004 surfaces; first-person camera integration waits for PLAN-006 and coordinator I1. Health logic can be tested without Hunter; deliver real hit/catch/death facts with PLAN-005 at I2 for valid chase measurements. I3 adds full floor/results/restart integration.

## 3. Changes

| # | Owned files / behavior | Dependency |
|---|---|---|
| 1 | `Assets/Scripts/Domain/Player/**`: Manager, Factory, Registry, Controller, BehaviorState/read-only view, Profile, Definitions | C1 |
| 2 | PlayerDriver, PlayerMoverPresenter, DriverState, PlayerMoverDriverConfig, limb stand-in | C1 Core traversal contract; PLAN-004 live geometry |
| 3 | Health, injury, ApplyHit, health/death events in the same Player system | C1 event schema |
| 4 | `Assets/Editor/Tests/Player/**`, `Assets/Editor/Player/**`, mirrored `Resources/ScriptableObjects/Domain/Player/**`, `Assets/Prefabs/Player/**` | Own source |

Do not edit Core, Session, Input, Orchestrator, global scene setup or Camera. Submit required routing/contract patches to PLAN-002. Player depends on Core only; PlayerDriver may resolve the agreed Core surface contract, never Domain.Level types.

### Required behavior

- State machine: Ground, Air, Slide, Vault, Stumble. Start sprint 8 m/s, precision walk 4; acceleration 60 m/s². Profile MaxDesignSpeed 14 m/s.
- Jump buffer/coyote 0.1 s, vertical 5.5 m/s; air steering 25 m/s² capped at existing horizontal speed.
- Slide entry ≥6 m/s, +2 boost, decay toward sprint over ~1.2 s; jump preserves horizontal velocity. Driver capsule height 50%.
- Vault lock 0.25 s, mantle 0.35 s with clearance and preserved entry speed. Probe through stable metadata; no physics in Controller.
- Rebound: Air, wall within 0.6 m, facing angle ≤45°, jump window 0.15 s, reflect velocity +3 m/s upward, 0.4 s cooldown and no same-wall chain.
- Landing vertical impact <12 m/s retains 100% horizontal; 12–18 retains 60% with 0.2 s soft stumble; >18 retains 30% with 0.5 s stumble. Distinguish stumble duration from input lock; the ≤0.35 s verb-transition lock criterion is not silently relaxed.
- Look-back held: forward input retained, lateral steering ×0.35, body heading frozen while look delta turns the head; body look resumes on release. Verbs remain usable.
- Noise ring buffer: sprint low, slide mid, vault/rebound/land high, tick-stamped and readable without engine objects. Publish a Core rebound/traversal fact with direction/timing for camera roll and telemetry; rebound remains a transition/event rather than a new movement state.
- Hidden health starts 100; lunge damage 50; meaningful states 100/50/25/0, injury max-speed penalty 5%. Publish health/death facts once; Controller owns hit rules. Coordinator routes Hunter hits and ends the run.
- PlayerBehaviorState contains passive inventory data with empty slots, as required by the parent. C1 defines its Core display snapshot; coordinator routes it to PLAN-007. No item effects, collection, equipment or inventory gameplay is added.
- Factory owns spawning/identity/teardown; Manager performs probe → decide → move → CommitPose → publish. Driver owns capsule casts; Presenter owns projection/interpolation math.

## 4. Sequence

1. Agree pose/velocity/heading/health/noise and movement-result signatures at C1; document units and stable wall identity.
2. Implement ground/jump/slide and pure replay tests before advanced traversal. Deliver minimum player fixture to PLAN-005/008 and coordinator.
3. Add Driver collision/probes, vault/rebound/landing and look-back; integrate against PLAN-004 real surfaces through the coordinator.
4. Add health/death rules and tests in this same ownership window. Expose facts needed by PLAN-006/007 without adding Presentation dependencies.
5. Supply deterministic Player generator/prefab assembly entry points; coordinator wires I1/I3. Hand off source snapshot and regression evidence.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

Run PlayerControllerTests and PlayerMoverPresenterTests: threshold boundaries, tap/coyote windows, same-wall cooldown, head/body separation, invalid probes, collision projection, death idempotence, pooled-state reset and deterministic InputFrame+MovementProbe replay. Check capsule clearance, slopes/edges, short traversal, step/ground snapping and all movement chains in the actual arena. No static test substitutes for casts.

Use PLAN-010 recordings: 90th-percentile free horizontal speed ≥9 m/s; no verb-transition input lock >0.35 s; recorded input/probes yield identical state trajectories. Coordinate M4 look-back/vault-rate measurements; a visible camera turn without steering rules is a fallback failure.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Level type coupling | Architecture | Breaks Player→Core-only contract | C1 neutral surface contract; graph checks |
| Mantle/stumble semantics | Design wording | Could confuse state duration with hard input lock | Record lock availability explicitly; coordinator resolves any real conflict |
| Missing real device/camera test | Verification | Input math may pass without playable movement | I1 physical-device/first-person check |
| Health and movement parallel edits | Ownership | Lost changes | This worker owns both, no separate health writer |

## 7. Deferred follow-ups

No networking/prediction implementation, stamina, combat attacks, item mechanics or additional movement verbs. Camera effects and health presentation belong to 006/007.

## 8. Definition of done

- [ ] M1 chain and M7 health rules work through the real tick/Driver/Factory path.
- [ ] Threshold and deterministic replay tests pass; pure state contains no engine objects.
- [ ] Speed/input-lock acceptance and live collision checks are recorded.
- [ ] NoPlayer/placeholder telemetry is replaced only after real integration.
- [ ] Owned files and coordinator handoff are verified; no shared-file conflicts.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned; awaits C1 | [Coordinator](PLAN-002-parallel-coordination.md) |
