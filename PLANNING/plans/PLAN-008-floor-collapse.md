---
id: PLAN-008
type: plan
title: Floor collection and collapse
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Floor worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-008 — Floor collection and collapse

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Deliver M5's moving collection loop, exit opening, farthest-first collapse and optional Golden Cake second sweep. Keep Floor implementation separate from Session run orchestration and presentation so those owners can work concurrently.

## 2. Starting point

FloorLoop does not yet exist. Begin pure Floor work after [C1](PLAN-002-parallel-coordination.md#3-changes) using agreed Level/Player fixtures; live pickup/navigation/lethality waits for PLAN-003/004 and coordinator I3. Results and HUD are PLAN-007; Director is PLAN-009.

## 3. Changes

| # | Owned area | Output |
|---|---|---|
| 1 | `Assets/Scripts/Domain/Floor/**` | FloorManager, Controller, BehaviorState/read-only view, Config, Definitions |
| 2 | Floor Driver subtree | FloorDriver, CakePickup and RoomCollapseVolume sub-drivers; room telegraph lighting and blockers |
| 3 | `Assets/Editor/Floor/**`, `Assets/Editor/Tests/Floor/**`, `Assets/Prefabs/Floor/**`, mirrored Domain Floor config assets | Content builders, tests, pickup/room assets |

Do not edit Run/SceneFlow, shared Core graph/helpers, SceneRoots, Orchestrators, shared scenes or FloorLoopSceneSetup. Send facts, summaries and a local content builder to PLAN-002. Floor depends only on Level and Player plus Core.

- Select cake anchors using type weights from read-only Level data and the run's injected Random. Generate stable seeded placement.
- CakePickup reports collider contact to its Driver/Manager; Manager resolves EntityId and Controller applies collection once to the shared counter. No interaction stop/button. Exit starts Locked and opens on required collection.
- Direction cue: actual navigation mesh (NavMesh) path length to uncollected cakes every 0.5 s, through Driver queries; publish direction as Core/primitive data. Exit cue replaces cake cue after opening. Golden Cakes get no directional cue.
- Use shared Core LevelGraphUtility breadth-first distances from exit; schedule rooms farthest-first at configured interval. Telegraph for 6 s, then Closed.
- FloorDriver owns physical room warning lights, blockers, hunter navigation exclusion and lethal volume arming. Publish room-phase facts for PLAN-007 audio via coordinator routing; no Presentation dependency.
- Spawn Golden Cakes at original anchors on exit opening; separate wallet count. Reaching exit ends the run through Session; Floor never loads scenes or shows results.
- Contact/damage/death/exit signals use C1 identities and reasons. Coordinator builds RunSummary and orchestrates input locking/restart; presentation never owns gameplay totals.

## 4. Sequence

1. C1: freeze Floor snapshot/counter/exit/room-phase/contact events and anchor/path schemas; provide fixtures to Director and presentation.
2. Implement pure collection/exit/collapse/second-sweep logic and seeded tests.
3. Build pickup and room Driver paths, repeatable local prefab/content generation, telegraph lights and collision/nav changes.
4. Coordinator adds FloorLoopSceneRoot/Setup and Session phase integration at I3. Integrate actual Player and Level, then wire HUD/audio/results.
5. Verify full seeded run, optional second sweep, lethal closure, exit/death summaries and repeatable restart; hand off source/config/seed evidence.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

FloorControllerTests: duplicate contacts, anchor selection weights/seed reproducibility, disconnected rooms, distance tie ordering, exact telegraph/closure timing, safe transition ordering, Golden Cake separation and cue selection. Verify real triggers, lights, blockers and hunters refusing closed rooms; a changed NavMesh cost alone is not proof of impassability.

Measure first sweep 2–4 minutes with no stop to interact. Test both exit success and solo death; results and restart must reflect gameplay facts and reset the new run. Verify no Golden Cake arrow and no fallback dummy anchors.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Closed-room path remains usable | Integration | Collapse has no effect on Hunter | Inspect real navigation and path attempts |
| Room lights unowned | Coverage | Telegraph unreadable | FloorDriver owns physical warning lights; 007 owns audio |
| Run/Results concurrent changes | Ownership | Multiple end paths or wrong totals | Coordinator owns Session; 007 consumes summary |
| Golden Cake cue | Design | Undermines risk/second sweep | Explicitly no Golden Cake direction cue |

## 7. Deferred follow-ups

No economy/shop, procedural placement outside hand-authored anchors, save/load, or full multiplayer collapse policy.

## 8. Definition of done

- [ ] Seeded collection opens exit, starts visible/audible collapse and offers optional Golden Cakes.
- [ ] Real paths, blockers, lethal contacts and exit/death routes work with correct ownership.
- [ ] Pure tests and full 2–4-minute first-sweep acceptance pass.
- [ ] I3 results/restart reflect real counters and preserve persistent-service uniqueness.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned; awaits C1 and live Player/Level integration | [Coordinator](PLAN-002-parallel-coordination.md) |
