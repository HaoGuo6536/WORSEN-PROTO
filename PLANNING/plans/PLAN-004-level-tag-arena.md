---
id: PLAN-004
type: plan
title: Level graph and tag arena
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Level worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-004 — Level graph and tag arena

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Deliver M2's hand-built traversal cluster, typed markers, read-only level graph and repeatable content generation. Develop independently of Player logic after [C1](PLAN-002-parallel-coordination.md#3-changes); the coordinator owns the shared scene and setup.

## 2. Starting point

M0 TagArena contains a simple floor/camera and persistent services. No Level implementation or navigation/traversal cluster exists. PLAN-003 and this plan exchange the frozen Core surface/probe contract; neither worker edits the other's system.

## 3. Changes

| # | Owned area | Output |
|---|---|---|
| 1 | `Assets/Scripts/Domain/Level/**` | LevelManager, LevelMarkerRegistry, BehaviorState/read-only view, Definitions, LevelDriver and LevelMarker sub-driver |
| 2 | `Assets/Editor/Level/**`, `Assets/Editor/Tests/Level/**` | Marker drawer, cluster generation entry point, graph tests |
| 3 | `Assets/Greybox/TagArena/**`, `Assets/Prefabs/Level/**`, mirrored `Resources/ScriptableObjects/Domain/Level/**` if needed | Geometry/material/prefab content |

Core LevelGraph, shared Core/Utility/LevelGraphUtility, neutral traversal contracts, shared TagArena.unity, navigation integration in TagArenaSceneSetup and build settings belong to PLAN-002. Supply the LevelGraphUtility implementation/test proposal to the coordinator for publication under Core because both Level and Floor consume it; never create a duplicate Domain-local helper. Submit patches/data; do not write shared files directly.

- Low connector; mid braided room with ≥2 exits and micro-loop; tall atrium with vertical line and one-way drop to the mid room.
- Long diagonal sightline, hunter-only straight corridor and player-only clutter route.
- Markers: VaultSurface, SlideGate, ReboundSurface, OneWayDrop, LosBreak (line-of-sight break), CakeAnchor Flow/Precision/Detour/Risk/Vertical, HunterLink, ExitMarker.
- Marker registration/unregistration follows OnEnable/OnDisable through LevelMarker → owning LevelDriver → LevelManager → LevelMarkerRegistry. A sub-driver never calls a Registry directly. Reconcile initially enabled markers during explicit LevelManager initialization before graph assembly and SceneReady handoff. Stable room/anchor/link ids survive deterministic rebuild.
- Read-only rooms/edges/anchors and breadth-first topological distances use the shared pure Core LevelGraphUtility and Core LevelGraph data; Level has no Domain dependencies. SceneRoot/Session calls initialization downward; Level never subscribes to top-layer SceneReady.
- Baked navigation mesh (NavMesh); NavMeshLinks correspond to HunterLink markers and are recreated by setup.
- Readability at 8–14 m/s: consistent waist-height vault/lit edges, rebound stripes, clear slide silhouettes and one-way-drop lips.

## 4. Sequence

1. C1: agree Core graph/surface schema with coordinator and Player worker; provide graph fixtures for Hunter/Floor/Director.
2. Implement marker registration and submit shared graph utility to the coordinator with connected/disconnected/one-way edge tests; wait for that source publication before compiling dependent changes.
3. Build geometry/materials and a local content builder. Coordinator invokes it from TagArenaSceneSetup under its ownership.
4. Under the Unity lease, integrate and bake NavMesh with the coordinator; verify registered markers, links and deterministic rebuild identity.
5. Traverse with real PLAN-003 Player and view from PLAN-006; tune affordance geometry within M2 scope, then hand off I1 evidence.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

LevelGraphUtilityTests covers room/anchor identity, distances, inaccessible nodes, directional links and deterministic ordering. Inspect actual NavMesh routes and hunter/player-only alternatives. Play every traversal line at design speeds; record any hesitation, blocked shortcut or wrong marker/probe. Gizmos and mock graphs alone are not acceptance. Rebuild twice without changing globally unique identifiers (GUIDs) or creating duplicate registrants.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Shared scene collision | Concurrency | Worker overwrites another system's wiring | Coordinator alone publishes shared scene/setup |
| Concrete LevelMarker in PlayerDriver | Architecture | Forbidden Player→Level edge | Core surface contract implemented by this sub-driver |
| NavMesh-only route success | Integration | Player collision/affordance still broken | Real Player traversal and Hunter path checks |
| Missing markers/disconnected graph | Fallback | Floor/Director quietly use defaults | Fail/report setup gaps; no invented fallback anchors |

## 7. Deferred follow-ups

No procedural generation, extra clusters/biomes or ProBuilder dependency unless coordinator explicitly selects it for this scope. Primitive generation is sufficient.

## 8. Definition of done

- [ ] Required cluster topology, marker classes and distinct routes exist.
- [ ] Graph tests and concrete navigation/traversal checks pass.
- [ ] Scene assembly uses a repeatable content builder; coordinator integrates it.
- [ ] Player remains Core-only and downstream consumers receive valid frozen graph data.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Work assigned; awaits C1 | [Coordinator](PLAN-002-parallel-coordination.md) |
