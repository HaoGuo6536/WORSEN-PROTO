---
id: PLAN-002
type: plan
title: Parallel contracts and integration
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Program coordinator
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-002 — Parallel contracts and integration

> LIVE within the user's previously approved [PLAN-001](PLAN-001-worsen-boilerplate.md) execution scope. This decomposition authorizes no additional feature scope. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md); see the [registry](../index.md). LIVE is approval, not dependency readiness or completion. Direction documentation is not present.

## 1. Objective

Make the approved boilerplate executable by independent workers without conflicting changes, competing Unity sessions, incompatible contracts, or missing integration. This is the sole owner of shared contracts, shared scene assembly, Session integration and final program acceptance. [PLAN-001](PLAN-001-worsen-boilerplate.md) retains gameplay values and overall scope; the child plans define assignment, dependencies and evidence.

## 2. Starting point

M0 is implemented: [checkpoint](../../Logs/AgentValidation/M0/verification.md), 70 passing Edit Mode tests, TagArena, input buffering and persistent services. Physical device testing remains unverified and graph query 5 has a documented false positive. Recheck this evidence against current source before relying on it. M1–M8 are not implemented merely because their plans exist.

The shared checkout contains substantial pre-existing uncommitted and untracked work, including M0. A worktree created from HEAD alone will not contain that baseline. Before delegating, provide and verify a complete agreed baseline in each isolated checkout, or use the shared-checkout publication protocol below. Do not mass-stage unrelated vendor/user files, switch the shared branch, or overwrite another worker's changes.

## 3. Changes

| Checkpoint | Coordinator output | Consumers |
|---|---|---|
| C0 — workspace admission | Named worker/checkout per plan, exact file claims, baseline/source hashes, testing protocol acknowledged | All |
| C1 — contract freeze | Compile-ready shared value types, interface/event signatures and test fixtures; decisions below resolved in source and recorded here | All worker plans may begin against this checkpoint |
| I1 — movement arena | Player + Level + minimal first-person view wired into TagArena; input/device and reload checks | 003, 004, 006, 010 |
| I2 — tag loop | Hunter/Chase + real Player hit/catch/death facts + audiovisual feedback + telemetry integrated; ≥30 measured chases | 003, 005, 006, 007, 010 |
| I3 — complete floor | Floor + Director + player health + results/restart integrated | 003, 007, 008, 009, 010 |
| V — acceptance | Full regression and human playtest evidence; unresolved failures remain explicit | PLAN-001 |

C1 is a prerequisite checkpoint, not completion of this entire coordinator plan. Integration checkpoints wait for the relevant worker deliverables; workers never wait for PLAN-002's final completion.

### Exclusive file ownership

The coordinator owns `Assets/Scripts/Core/**`, `Assets/Scripts/Session/**`, all `Assets/Scripts/Orchestrator/**`, `Assets/Editor/Scenes/**`, shared scene/build assets, shared test assembly/conformance infrastructure, `tools/coordination/**`, all assembly definitions, `Packages/**`, `ProjectSettings/**`, architecture/agent guides and the registry. PLAN-010 has the explicitly delegated Input subtree and its tests; it submits Run/Orchestrator changes to the coordinator.

Each worker owns only its plan's named system, tests, local generators and mirrored assets. The Level worker supplies arena content and builder code under its own paths; only the coordinator edits TagArenaSceneSetup and the actual shared scene. Only one worker writes a file at a time, including a plan's execution log. Shared-file changes are submitted as a proposed patch/application programming interface (API) request; the owner applies them under the lease. Transfer ownership explicitly with path, old/new owner and checkpoint recorded in §9; never infer transfer from inactivity.

### Contract decisions before parallel implementation

| Boundary | C1 decision / required evidence |
|---|---|
| Existing M0 names | Preserve actual `InputFrame.Move/LookDelta/Held/Pressed/Released`, `InputButtons.LookBack`, `FramePublished`, `BeforeTick`, `TickAdvanced`, `PhaseChanged`. Reconcile conceptual PLAN-001 `OnFrame/OnPhaseChanged/lookBack` names through adapters/approved edits, never duplicate event streams. LookDelta is degrees, not a rate. |
| Tick ownership | Run Session remains the sole tick owner. Define ordered Player → Hunter → Chase → Floor → Director dispatch, input consumption once, and state publication after pose commit. No worker adds a competing FixedUpdate simulation loop. |
| State injection | Keep Core EntityContext as identity/shared randomness only. Keep Domain read-only views beside their states and inject them through typed Domain Factory/Manager initialization parameters or overloads. No service locator, relocation of mutable Domain state into Core, or Core-to-Domain reference. |
| Level/Player traversal | Player must remain Core-only. Define a Core traversal-surface interface/data contract implemented by the Level sub-driver, or neutral probe metadata. PlayerDriver resolves that contract, never concrete Domain.Level.LevelMarker. Validate with assembly/graph checks. |
| Cross-layer facts | Define Core payloads (identity, movement/chase/end enums or immutable data) for events consumed outside Domain; keep mutable/read-only Domain state behind its owning system. Presentation receives primitives. No Domain enum sneaks into a Core DTO (data transfer object). |
| Player/Level/Hunter | Freeze read-only pose/heading/velocity/speed/noise/health and level room/edge/anchor views, registry/spawn signatures, stable wall/room ids, sight probe, and Hunter belief/hit/chase facts. Workers provide fixtures against these contracts. |
| Floor/Director/Run | Freeze pickup/counter/exit/room-phase facts, delayed HintPayload, death/exit reason and RunSummary. Floor requires Level+Player; Director may read Player, Hunter, Chase and Floor. No reverse edges. |
| Telemetry/replay | Freeze tick-stamped chase outcomes, vault attempts/failures, look-back intervals, horizontal speed in m/s with explicit free-movement/chase tagging, input-lock start/end/reason and proximity samples; versioned input/probe records plus run seed/config provenance. Give stable definitions for measured acceptance denominators. |
| Passive inventory | PLAN-003 owns empty-slot inventory data in PlayerBehaviorState; coordinator owns a Core display snapshot/routing; PLAN-007 renders it. No item gameplay. |
| Shared level utility | Level and Floor use the same LevelGraphUtility; coordinator publishes it under Core/Utility with Core-only signatures. PLAN-004 supplies algorithm/tests, not a second Domain-local copy. |
| Marker registration/readiness | LevelMarker reports lifecycle records to its owning LevelDriver, then LevelManager updates LevelMarkerRegistry. No Driver/sub-driver directly calls a Registry. SceneRoot/Session invokes Level initialization before SceneReady; Domain never subscribes to a top-layer SceneRoot event. |
| View/router lifetime | Freeze eye/body pose, head-look, normalized-speed and discrete rebound/traversal facts. Rebound is an event/sample, not a new state added to Ground/Air/Slide/Vault/Stumble. Camera/PostFX/HUD/Results routers are scene-owned with paired subscriptions; persistent Audio/Telemetry routers rebind or consume persistent Session relays without retaining dead Domain publishers. Validate every repeated load. |
| Measurement semantics | Before 005/010, define Cornered classification and Lost-to-Confirmed reacquisition/grace/chase identity; do not invent an attack just to populate a metric. Test the 25-health threshold with arbitrary damage without adding a new damage source. |
| Precision control | Default movement is sprint; explicitly map the existing Sprint-named action to the intended hold-for-precision behavior, or approve a single compatible binding/API adjustment through the Input owner. Preserve 8/4 m/s and record the choice before Player/Input implementations diverge. |

These are engineering boundaries, not permission to change the original movement/chase tuning. Resolve design disagreements preserved in SPEC-002 only if they affect the assigned prototype behavior; retain PLAN-001's explicit chosen values and deferred scope.

## 4. Sequence

1. C0: inspect current work, assign owners, establish a recoverable baseline and obtain protocol acknowledgment from every active worker. Existing agents outside the protocol must pause shared-checkout writes before any testing.
2. C1: collect worker API proposals, inspect affected symbols with GitNexus, publish the minimal shared contracts and fixtures in one coordinator change. Compile and verify before opening the first parallel wave. Record signatures, paths and revision/hash evidence in §9.
3. Start PLAN-003, 004, 006, 007 and 010 against C1. PLAN-005, 008 and 009 may develop pure logic against the agreed fixtures as described in their plans; their live acceptance waits for real producers.
4. Integrate I1 under the lease. Each worker submits its own implementation/test evidence and reproducible setup entry point. Verify the real player/arena/first-person camera, not just mocks.
5. Integrate I2; connect one hunter and the feedback/telemetry channels. Retain the ≥30-chase tuning gate before declaring the tag loop proven.
6. Integrate I3; add FloorLoopSceneRoot/Setup, lifecycle transitions, death locking and restart via SceneFlowManager. Check repeated loads and one canonical persistent service.
7. V: run the final engineering and measured/human acceptance gates. Record source/config/seed/sample provenance and failures. Update relevant architecture roster/header facts with their owning changes; do not silently mark any entire plan complete.

### Testing admission

1. Read the [lease guide](../../tools/coordination/README.md). Claim the repository-wide lease atomically with your task identity, plan id and purpose. Busy means no editor work; keep independent work moving or retry later. Do not enter Play Mode, refresh, build scenes, bake navigation, install packages or run tests before acquisition.
2. Assert your returned token. Verify the connected project path and actual editor state: no foreign Play Mode session, no running test/build, imports or compilation, and no unsaved user scene work that your operation would replace. Tool discovery alone proves none of these. If busy or unreadable, do not begin; retain the lease and coordinate with the active operator until the actual state is known and idle. Never disturb someone else's operation to pass admission.
3. Every save/publication into Assets, Packages or ProjectSettings of the open checkout also uses this lease and holds it through imports/compilation. An initial freeze acknowledgment covers already-running writers. Isolated worktrees may be edited independently; all Unity sessions for this repository still queue behind this single lease.
4. Hold ownership across refresh → compile → test/play → completed results → intended cleanup. Assert ownership before each tool mutation and heartbeat between operations. A heartbeat warning never authorizes stealing; a timed-out tool does not establish that Unity stopped.
5. Save output under `Logs/AgentValidation/<PLAN-ID>/<lease-token>/<run-name>/` in the tested checkout. Record exact source snapshot, test filter/counts/failures, console and scene evidence. A worker change arriving during a run invalidates its snapshot and requires a rerun after reconciliation.
6. Release in a finally-style cleanup only after confirming the owned operation finished and editor is idle. If operation state is uncertain, retain the lease, report owner/token and resolve the active operation; never release while a test might still be running. Restore only your temporary state. Do not force-stop another agent's/user's editor or auto-reclaim an old heartbeat.

The lock is cooperative; it cannot stop manual UI actions or a noncompliant tool caller. The coordinator enforces it through root instructions and worker admission. It supplements, and does not replace, Unity's project lock. Never open two editors against one checkout.

## 5. Verification

Before changing existing indexed symbols, run GitNexus upstream impact and inspect direct callers; handle unknown/partial results with source and serialized-reference evidence. Follow SPEC-001 §13: script headers, appropriate pure-layer tests, assembly compilation, ast-grep, current graph conformance, ArchitectureConformanceTests and project tests. A source-only pass does not establish scene wiring.

All Unity operations and saves into a checkout open in Unity use the [exclusive lease protocol](../../tools/coordination/README.md) and [PLAN-002 testing gate](PLAN-002-parallel-coordination.md#testing-admission). Acquire first; a free Status response is not ownership. Assert the token, verify the intended editor is idle, and wait for imports/compilation before testing. Use token-specific output paths, wait for completed results, restore only your changes, then release when idle. While another owner holds the lease, prepare patches outside imported paths or work in an isolated checkout; do not save into the tested checkout.

Validate C1 fixtures against concrete implementations at every integration checkpoint. Use a fresh graph snapshot; detect_changes can omit untracked sources and a long-lived MCP (Model Context Protocol) server can retain stale data. Hash/snapshot the included files and review the known query-5 member-reference false positive rather than relaxing checks. Serialize index writes and shared Git mutations through the coordinator; read-only searches and lint may run independently against stable snapshots.

At V, verify M1 speed/determinism, M3 chase statistics, M4 perception/comfort/look-back rates, M5 floor duration and M6 pressure gaps with PLAN-010 evidence. Automated substitutes do not establish subjective player perception or absence of discomfort. Log sample size; unmet or unmeasured acceptance remains open.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Uncommitted baseline | Integration | New worktrees miss M0/vendor prerequisites | Coordinator inventories and verifies before admission |
| Shared editor auto-import | Concurrency | A save recompiles during another test | Lease all publications and Unity operations |
| API drift | Dependency | Parallel branches compile separately but fail together | C1 fixtures and coordinator-only shared edits |
| Missing physical/human evidence | Verification | False completion claims | Run explicit checks; retain open gates if people/devices unavailable |
| Abandoned lease | Availability | Queue stays blocked | Diagnose owner/editor; manual verified recovery per guide, no expiry takeover |

## 7. Deferred follow-ups

PLAN-001's networking, procedural generation, shops/items/classes, multiple hunters and persisted settings remain deferred. Optional detection pull-out is a late experiment only after the first-person gate. This plan does not initiate documentation skills or their lifecycle operations.

## 8. Definition of done

- [ ] C0/C1 are recorded and every shared file has one owner.
- [ ] I1/I2/I3 run with real components, deterministic setup and correct reload/input lifecycle.
- [ ] Every child delivers its implementation and evidence; no fixture stands in for missing integration.
- [ ] V engineering and measured/human acceptance are met, or the program remains LIVE with explicit gaps.
- [ ] No Unity work ran without lease ownership; no foreign edits/settings were discarded.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-14 | Decomposition | Coordination plan written; C0/C1 and gameplay integration are not yet executed | [M0 baseline](../../Logs/AgentValidation/M0/verification.md) |
