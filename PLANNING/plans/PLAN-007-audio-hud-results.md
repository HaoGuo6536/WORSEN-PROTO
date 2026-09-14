---
id: PLAN-007
type: plan
title: Audio and interface communicate chase and run outcomes
status: LIVE
created: 2026-09-14
updated: 2026-09-14
owner: Audio/HUD/Results worker
specs: [SPEC-001, SPEC-002]
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-007 — Audio, HUD, and results

> LIVE since 2026-09-14. Implements [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md) and [SPEC-002](../specs/SPEC-002-worsen-game-design.md), through [PLAN-001 M4](PLAN-001-worsen-boilerplate.md#m4--detection-beat-look-back-first-person-readability--1-week), [M5](PLAN-001-worsen-boilerplate.md#m5--floor-loop--2-weeks), [M7](PLAN-001-worsen-boilerplate.md#m7--health-injury-hud--1-week), and [M8](PLAN-001-worsen-boilerplate.md#m8--tuning-harness-built-alongside-m3-finished-here). See the [registry](../index.md).
> This is a bounded decomposition of the previously approved boilerplate execution, with unchanged scope. This planning operation implements nothing and records no milestone as complete.

## 1. Objective

Make chase proximity and run progress readable through audio and a minimal user interface (UI), including the heads-up display (HUD), then show an authoritative run summary and request restart. Detection, health, collection, collapse, run-end rules, and scene loading remain with their Domain/Session owners. Keep the player moving and numeric health hidden.

## 2. Starting point

M0 supplies an [input service](../../Assets/Scripts/Presentation/Input/Manager/InputManager.cs), a [run tick/phase publisher](../../Assets/Scripts/Session/Run/Manager/RunSessionManager.cs), [SceneFlowManager](../../Assets/Scripts/Session/SceneFlow/Manager/SceneFlowManager.cs), and a [UI Toolkit DebugOverlayDriver](../../Assets/Scripts/Presentation/DebugOverlay/Driver/DebugOverlayDriver.cs) with mirrored UI Toolkit markup (UXML), theme and panel assets. Audio/HUD/Results source does not exist at decomposition time. The existing run phase event is `PhaseChanged`; parent-plan `OnPhaseChanged` examples and the future RunSummary delivery must be reconciled by the coordinator, not assumed implemented.

Entry requires **C1: the Core contract freeze in [PLAN-002](PLAN-002-parallel-coordination.md)**, not completion of PLAN-002. Pure audio/display work may run beside PLAN-003 Player, PLAN-005 Hunter/Chase, PLAN-006 Camera/PostFX, and PLAN-008 Floor. Live event integration waits for their real publishers and coordinator-owned Session/routing changes. No direction document exists; omit direction identifiers.

## 3. Changes

| # | Change | Exclusive worker ownership | Depends on |
|---|--------|----------------------------|------------|
| 1 | Audio Service, persistent: Manager, Driver, AudioMixPresenter, DriverState, AudioDriverConfig | `Assets/Scripts/Presentation/Audio/**` | C1 cue/primitive contracts |
| 2 | HUD Service, scene-owned: Manager, UI Toolkit Driver, DriverConfig, Presenter/DriverState for restore timing | `Assets/Scripts/Presentation/HUD/**` | C1 count/exit/direction contracts |
| 3 | Results Service, scene-owned: Manager, UI Toolkit Driver, DriverConfig, tested formatting Presenter if needed | `Assets/Scripts/Presentation/Results/**` | C1 RunSummary/restart contract |
| 4 | Tests, system config generators, UI and sound assets | `Assets/Editor/Tests/{Audio,HUD,Results}/**`, `Assets/Editor/{Audio,HUD,Results}/**`, `Assets/Resources/ScriptableObjects/Presentation/{Audio,HUD,Results}/**`, `Assets/Resources/UI/Presentation/{HUD,Results}/**`, `Assets/Resources/Audio/Presentation/Audio/**` | Changes 1–3 |

**Excluded ownership:** Core, Domain, Session, Orchestrator, assemblies, packages, global scenes/setup, and architecture/registry changes. PLAN-002 owns shared edits and integration. Do not modify Input or DebugOverlay while using them as reference implementations. Presentation references Core only, has no gameplay Controller/BehaviorState, and never subscribes to a foreign system from its Driver.

Preserve the parent's behavior without moving its rules into the interface:

| Concern | Audio/HUD/Results behavior |
|---------|---------------------------|
| Detection | On confirmed chase, play the chase sting through a small priority mixer; reduce HUD to **counter + exit state only**. PLAN-006 supplies the synchronized **+12°/0.08 s/0.4 s** camera beat; do not implement camera math here. |
| Chase lost | Play the lose cue; restore the regular HUD over **0.5 s**, with interrupted restoration handled by Presenter state. |
| Proximity | AudioMixPresenter maps incoming `closeness` **0–1** to breath and hunter-layer gains. PLAN-005 owns the **4 m → 1, 20 m → 0** mapping and rear/front weighting **1.0/0.5**; no distance display or duplicate sensing. |
| Sound layers | Unity Audio only: priority-managed presence/detection/chase/lose/death cues and breath/hunter layers; movement facts drive footsteps. Clip references, gains, fades, and priorities belong to AudioDriverConfig. Presenter returns mixer decisions; Driver plays them. |
| Health | Critical-health breathing from supplied health facts; numeric health remains hidden. PLAN-003 owns **100 initial health**, **50 lunge damage**, **100/50/25/0** states, and the **5%** injured speed reduction; PLAN-006 owns injury vignette/death snap. |
| Floor HUD | Display cake count, Locked/Open exit state, and empty item slots. Render the supplied direction cue; PLAN-008 chooses the nearest uncollected cake by NavMesh path distance every **0.5 s**, then substitutes the exit cue. **No Golden Cake direction cue.** |
| Collapse | Play supplied room-telegraph cues with the parent **6 s** telegraph; PLAN-008 owns schedule, room closure and FloorManager → own FloorDriver lighting commands. Coordinator routes published audio facts only; this worker does not alter scene lighting. |
| Results | Display authoritative run time, cakes, Golden Cakes, chase statistics, and end reason from Core RunSummary. Restart publishes a UI fact; only Session/SceneFlow decides/reset/loads. No stopping interaction is added to collection or the exit. |

Freeze the following exact Manager surface and event hand-off at C1; any new enums/DTOs are authored by PLAN-002 in Core:

| Publisher fact / owner | Command or outgoing event |
|------------------------|---------------------------|
| PLAN-005 `OnChaseStarted` / `OnChaseLost` | `AudioManager.PlayCue(CueId)` using agreed chase/lose identifiers; `HUDManager.SetChaseMode(bool)` |
| PLAN-005 `OnProximityChanged(float)` | `AudioManager.SetProximity(float closeness)`; Presenter computes both gains internally |
| PLAN-003 movement/speed/health/death facts | `AudioManager.SetMovementState(MovementState)`, `SetSpeedNormalized(float)`, `SetInjury(int currentHealth,int maxHealth)`, and `PlayCue(CueId)` |
| PLAN-008 count/exit/direction/room facts | `HUDManager.SetCount(int collected,int total)`, `SetExitState(ExitState)`, `SetDirection(Vector3 direction,bool visible)`; `AudioManager.PlayCue(CueId)` for agreed exit/telegraph cues |
| PLAN-003 passive empty-slot inventory, routed as a Core snapshot by the coordinator | `HUDManager.SetItemSlots(int emptySlotCount)`; no inventory gameplay or item use |
| Coordinator-owned authoritative run completion | `ResultsManager.Show(RunSummary summary)`; return path `ResultsManager.RestartRequested` is an instance `Action` event |

Hand PLAN-002 exact serialized fields, Resources paths, document/panel/theme assets, initialization/teardown details, and these signatures. It alone creates AudioOrchestrator/HUDOrchestrator/ResultsOrchestrator, registers them in SPEC-001, wires scene roots, supplies real RunSummary, and routes restart to SceneFlowManager. UI Drivers publish click facts to their Manager; they never call Session or any singleton.

## 4. Sequence

1. Confirm C1 and source reality. Classify files; use GitNexus query/context and upstream impact before changing existing symbols. Missing/new targets require scoped reference checks, and HIGH/CRITICAL impact requires a warning before edits.
2. Implement/test pure AudioMixPresenter arbitration, gain/fade response, and reset behavior with explicit `dt`; define clip fallback diagnostics without silent successful playback claims.
3. Implement HUD restore timing and results formatting with Presenter/DriverState only where needed. Test interrupted chase transitions, unavailable cues, counts, empty slots, summary formatting, and restart fact routing.
4. Build thin Managers/Drivers and owned config generators under `Worsen/Audio/...`, `Worsen/HUD/...`, and `Worsen/Results/...`. Pair subscriptions/teardown; serialize references first, use mirrored Resources fallback, and report missing assets visibly.
5. Deliver UI Toolkit UXML, Unity Style Sheet (USS), theme and panel contracts and audio setup details to PLAN-002. Bind the current document root after disable/re-enable; persistent Audio must release scene-owned sources/targets across loads.
6. After real publishers exist, verify detection/lose/proximity/health and full Floor results/restart with coordinator wiring. Run joint acceptance with PLAN-006 and PLAN-010 telemetry, retaining failures and missing evidence as open gates.

## 5. Verification

Follow [the exclusive Unity protocol](../../tools/coordination/README.md) and [PLAN-002 testing admission](PLAN-002-parallel-coordination.md#testing-admission). Before any Unity mutation or save/publication into Assets, Packages or ProjectSettings of a checkout open in Unity, acquire the exclusive lease, assert its token and verify the actual editor is idle. The coordinator first obtains pause/protocol acknowledgments from already-active writers; cooperating writers then acquire the same lease for each publication, without a new global acknowledgment for every save. While another owner holds it, prepare patches outside imported paths or edit an isolated checkout not open in Unity. Do not overlap tests.

Assert the token before each operation and heartbeat between operations. Use unique `Logs/AgentValidation/PLAN-007/<lease-token>/<UTC-timestamp>/` results/logs. Wait for actual completion, inspect fresh totals/failures/skips and logs, and release in `finally` only when Unity is idle. A started, timed-out, zero-test, or missing-result run is not a pass; a still-running operation retains its lease. Use the protocol's actual commands and recovery rules.

- Run `AudioMixPresenterTests` plus any HUD/Results Presenter tests. Cover priority contention, interrupted fades, finite/bounded gains, critical breathing, **0.5 s** HUD restore, formatting and retained/reset state.
- Complete SPEC-001 §13: headers, tests, assembly compilation, ast-grep, refreshed GitNexus and saved conformance queries, ArchitectureConformanceTests and project tests. Coordinate shared index refresh through PLAN-002.
- Verify actual audible clips/gain changes, HUD counter/exit-only chase mode, restoration, hidden health, empty item slots, and live Floor direction switching. Missing clip warnings, a silent fallback, synthetic samples, or labels that never receive real events do not establish completion.
- Joint M4 acceptance with PLAN-006: cover the screen for **1 s** during pursuit; near/mid/far distance identification from audio plus proximity feedback is **≥70%**. Record covered/audio-only and full-feedback conditions distinctly. Support its **≥1 look-back/chase**, **≤10 percentage-point** extra in-chase vault failures, and **15-minute** comfort probes; PLAN-010 owns measurement infrastructure.
- With PLAN-008/002, a full first sweep lasts **2–4 minutes** with **no stopping for an interaction**. On both death and exit, results match the actual summary; one restart request leads to one authorized scene load and fresh run state, with no stale audio, duplicate listeners, or recreated-document failures.
- Capture/view actual UI and listen to actual output. If an immediate screenshot tool produces black pixels, use verified end-of-render capture; valid label/layout state alone is not a rendered visual pass.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|------|------|--------|--------------------|
| Final sound assets are unavailable | Content | Mixer tests pass while the experience stays silent | Audio worker names temporary audible cues and missing assets; real listening evidence required. |
| Parent event names differ from M0 | Contract | Summary/phase or restart wiring appears complete but never fires | PLAN-002 freezes adapters and exact Core payloads at C1; worker never edits Session. |
| Critical breathing masks hunter distance | Acceptance | Hidden health feedback damages the chase read | Test layer priorities and gains with PLAN-006's joint probe; retain tuning evidence. |
| Persistent Audio caches scene targets | Lifecycle | Reloads leave stale sources or doubled sound | Explicit release/rebind hand-off and duplicate/restart checks. |
| Shared checkout auto-import | Coordination | Corrupts another worker's validation window | Exclusive lease plus acknowledged writer freeze; isolated edits only during another lease. |

## 7. Deferred follow-ups

FMOD/Wwise, persistent settings, inventory/item gameplay, shops, permanent minimaps, numeric health displays, and new run rules remain outside scope. PLAN-010 owns telemetry CSV, recording/playback, and the tuning window; expose read-only serialized DriverConfig fields for that integration without editing its files. PLAN-006 owns camera/post-processing and the optional late detection snap.

## 8. Definition of done

- [ ] Audio/HUD/Results stacks and own configs/assets exist, with pure tests and no forbidden dependencies or runtime SO setters.
- [ ] Real chase/player/floor/run publishers reach agreed commands through coordinator-owned routing; restart emits one fact and Session performs the load.
- [ ] Detection/lose cues, proximity and critical breathing, counter/exit-only chase HUD, **0.5 s** restoration, empty slots, and correct results work visibly/audibly.
- [ ] Joint **≥70%** distance-band probe and **2–4 minute** no-stop floor flow have evidence; shared look-back/comfort measurements are handed to PLAN-006/010.
- [ ] Disable/re-enable/reload cleanup, deterministic setup/fallback diagnostics, SPEC-001 gates, lease-scoped completed results, and coordinator acceptance are recorded.

## 9. Execution log

| Date | Step | Result | Evidence |
|------|------|--------|----------|
| 2026-09-14 | Decomposition | LIVE child scope inherits the prior PLAN-001 execution approval; planning only, no implementation or completion claim. | Parent M4/M5/M7/M8, SPEC-001/002, M0 source links above. |
