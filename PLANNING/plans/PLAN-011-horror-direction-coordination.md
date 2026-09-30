---
id: PLAN-011
type: plan
title: Horror direction execution map and shared contracts
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Coordinator role (assignee UNKNOWN — owner input needed); design authority Hao Guo
specs: [SPEC-004, SPEC-001, SPEC-002, SPEC-003]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-011 — Horror direction execution map and shared contracts

> Status: DRAFT since 2026-09-30. Umbrella for [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md), which is itself DRAFT, so nothing here or in its child plans is approved for execution. Engineering follows [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md). See the [registry](../index.md). `DOCUMENTATION/direction.md` is absent, so no direction item is linked.

## 1. Objective

Split SPEC-004 into fifteen child plans that can be run in parallel, with one owner for each file, a fixed set of shared contracts and named integration checkpoints. The priority "feel slice" should land first, and every SPEC-004 requirement should belong to exactly one plan or be recorded as deferred. This plan owns the shared Core and Session contracts, Orchestrators, scene roots, project settings, the coverage matrix and the conflict resolution with the LIVE boilerplate plans. It delivers no gameplay itself.

## 2. Starting point

Locations below were rechecked by text search on 2026-09-30. No Unity run and no GitNexus query was made for this decomposition, so caller sets and serialized wiring are unverified.

- SPEC-004's "today" columns come from the owner's 2026-09-30 code survey. The horror run already exists: [`HorrorRunSceneRoot`](../../Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs), Session systems `Expedition`, `HorrorEffects` and `Progression`, and a Domain `Procedural` castle generator. SPEC-003 therefore has generator code but no registered plan; PLAN-026 reconciles this.
- PLAN-001 and PLAN-002/003/005–010 are LIVE for the hand-built boilerplate. Several of their criteria conflict with SPEC-004 (§3.4). This decomposition does not edit them.
- Facts that constrain the shared contracts:
  - `ProgressionTraits` in [`ProgressionDefinitions.cs`](../../Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs) is an `int` flags enum with 26 of 32 bits used. The SPEC-004 catalogues (49 upgrades, 17 active general curses and 40 hunter curses, several stacking) cannot fit.
  - [`CueId`](../../Assets/Scripts/Core/Definitions/CueId.cs) has 67 identifiers; SPEC-004 §2.6 sets a budget of roughly three player, five per hunter and a handful of world cues.
  - [`NoiseEvent`](../../Assets/Scripts/Core/Definitions/NoiseEvent.cs) is produced only by Player and HorrorEffects. Occlusion exists only in the audio soundscape driver and the collapse-hand probe.
  - Hunter bodies require a `CapsuleCollider` and `Rigidbody` ([`HunterDriver.cs`](../../Assets/Scripts/Domain/Hunter/Driver/HunterDriver.cs)) that block the player.
  - The code has no persistence, but SPEC-004 needs lifetime-run gating, a persisted best depth and saved settings.
  - Only the exit door is a physical door; room connections are openings. Several hunter habits need doors, torches and props that can change state.

## 3. Changes

### 3.1 Child plans

| Plan | Scope (SPEC-004 sections) | Primary owned paths |
|---|---|---|
| [PLAN-012](PLAN-012-direction-independent-survey-fixes.md) | Direction-independent survey fixes (§2.8 items 3, 4, 7–10) | Coordinator paths, Telemetry |
| [PLAN-013](PLAN-013-movement-traversal-hit-recovery.md) | Movement, traversal and hit recovery (§2.9 rows 1–6, §2.4 grace, §2.8 items 1–2, §2.6 fail forward) | `Domain/Player/**` |
| [PLAN-014](PLAN-014-hunter-corner-stall-investigation.md) | Hunter corner-stall investigation (§2.9 row 7) | Hunter route/steering presenters, navigation build settings |
| [PLAN-015](PLAN-015-hunter-behaviour-felt-intelligence.md) | Hunter behaviour and felt intelligence (§2.2 table and prose) | `Domain/Hunter`, `Chase`, `Director` (framework) |
| [PLAN-016](PLAN-016-hunter-roster-foundation.md) | Roster foundation and first three threats (§2.2 build order, briefs, novelty gating) | Hunter archetype content, threat catalogue |
| [PLAN-017](PLAN-017-hunter-roster-expansion.md) | Roster expansion and non-hunter threats (§2.15–§2.17 remainder) | Archetype modules added after PLAN-016 |
| [PLAN-018](PLAN-018-collapse-fog-grab-hands.md) | Collapse fog and grab hands (§2.10, §2.5 collapse rows) | Floor collapse subtree, fog field |
| [PLAN-019](PLAN-019-cakes-arrow-cake-traps.md) | Cakes, the white arrow and cake traps (§2.5, §2.18, §2.11 traps) | Floor collection and guidance |
| [PLAN-020](PLAN-020-run-interface-menus-results.md) | In-run interface, title, pause and results (§2.3, §2.5, §2.6, §2.7) | HUD, Results, menus, shelter display |
| [PLAN-021](PLAN-021-silence-first-audio-hearing.md) | Silence-first audio and shared hearing (§2.3, §2.5, §2.6) | `Presentation/Audio/**` |
| [PLAN-022](PLAN-022-camera-catch-degradation-lighting.md) | Camera feel, the catch, degradation and lighting (§2.2, §2.3, §2.5, §2.6, §2.18) | Camera, PostFX, Horror, Environment lighting |
| [PLAN-023](PLAN-023-selection-curses-stakes.md) | Selection cadence, curses and stakes (§2.4, §2.12, §2.13, progression events) | `Session/Progression`, `Session/HorrorEffects` |
| [PLAN-024](PLAN-024-shop-upgrades-consumables.md) | Shop, upgrades and consumables (§2.12, §2.13, §2.18 flashlight) | Shop/inventory subtree, shop screen |
| [PLAN-025](PLAN-025-shrines.md) | Shrines (§2.14) | New shrine system |
| [PLAN-026](PLAN-026-level-diversity-gaps-multifloor.md) | Level diversity, gaps and multi-floor generation (§2.11, §2.9 ramps, SPEC-003) | `Domain/Procedural/**`, Level |

Ownership follows [PLAN-002 §3](PLAN-002-parallel-coordination.md#exclusive-file-ownership): the coordinator owns `Core/**`, `Session/**` contracts, `Orchestrator/**`, scene setup, assemblies, `Packages/**` and `ProjectSettings/**`. It explicitly delegates Session subtrees to PLAN-023/024. One writer per file per wave. Every other change is submitted to the path's owner as a request; each child lists its requests in §3.

### 3.2 Checkpoints

| Checkpoint | Coordinator output | Consumers |
|---|---|---|
| H0 — admission | SPEC-004 set LIVE (or the owner approves named plans); §3.4 conflicts and §6 spec inconsistencies decided; worker, checkout and baseline hash per plan; Unity lease protocol acknowledged | All |
| H1 — contract freeze | §3.3 contracts compiled with fixtures; graph conformance passes | All |
| H2 — feel slice | Wave 1 integrated in HorrorRun; owner playtest notes recorded | 013, 015, 019–023, 026 |
| H3 — horror floor | Wave 2 integrated: fog, felt intelligence, first threats, audio budget, catch | 013–016, 018–022 |
| H4 — progression loop | Wave 3 integrated: selection, curses, shop, shrines, level diversity, roster expansion | 017, 023–026 |
| HV — acceptance | Full regression and owner playtest against each child's §8 | All |

### 3.3 Contract decisions at H1

| Boundary | Decision to freeze |
|---|---|
| Effect identity | Replace `ProgressionTraits` flags with catalogue identifiers plus stack count and cap. Consumers receive one read-only view of active effects. Each rule's implementation stays with its owning system. |
| Threat kinds | `Pursuer`, `Annoyance`, `Systemic`. Duplicates allowed, no active-threat cap, every chosen threat active on every floor. The Director manages pressure and hints, never presence. |
| Shared hearing | One occlusion and falloff model derived from the level graph and portal state, as a pure Core utility, used by hunter hearing, Director hint radius and the audio mix. `NoiseEvent` gains a source kind. Every environmental sound is a noise event; interface sounds and music are not. |
| Hit and grace | A hit fact carries severity (light or heavy) and source (lunge, hand, projectile, scream). Player owns the grace window and boost. Hunter pass-through uses a coordinator-owned physics layer swap (`TagManager` and collision matrix). |
| Guidance targets | Floor publishes typed guidance targets: white arrow (required cakes, then exit), threat arrow (keys, collector items), golden sense, exit through walls. The white arrow never targets a trap or Mimic except under a named curse (§6 item 8). |
| Cue budget | Replace the flat `CueId` list with per-owner cue slots: player (3), per hunter archetype (presence, detection, chase layer, attack timing, death sting), a small world set, and interface cues outside the run. Each cue carries variation metadata and a timing-is-a-tell flag. |
| Hunter brief | Each archetype declares inertia, commitment, speed ratio and loss rule; three or four habits including one environment habit; a mutation pool with tells; and a sound set. |
| Progression events | Kinds: environmental hazard, hunter upgrade, extra hunter, random, hidden mutation. A mutation fact carries a tell identifier and never a text description. |
| Persistence | A versioned, Session-owned store for lifetime runs, best depth, unlocked hunters and settings. Domain never reads it directly. |
| World interactables | Core description for stateful doors, lights and knockable props, so hunters, shrines, micro-events and curses act on shared objects produced by PLAN-026. |
| Floor split | PLAN-019 owns `FloorController`, `FloorDriver`, `FloorPresenter`, `CakePickup`, `FloorExitDoor*`, `FloorLumenGlow` and `FloorConfig`. PLAN-018 owns `RoomCollapse*`, `FloorHand*` and the new fog field. |

### 3.4 Conflicts with LIVE boilerplate plans

| LIVE item | SPEC-004 position | Plan |
|---|---|---|
| [PLAN-003](PLAN-003-player-movement-health.md) look-back steering ×0.35, frozen heading, free head scan | Fixed snap behind, no scanning, steering continues | 013, 022 |
| PLAN-003 health: lunge 50, states 100/50/25/0, no grace | Grace, severity-scaled boost, full health each floor, regeneration | 013 |
| PLAN-003 vault lock 0.25 s, mantle 0.35 s | Physics mantle with live look, steering in the last third | 013 |
| [PLAN-005](PLAN-005-hunter-chase.md) Stalk optional; investigation at chase speed | Stalk required; walk while investigating | 015 |
| PLAN-005/[006](PLAN-006-camera-postfx-feedback.md) death camera snap and fade | Held close-up, hard sting, hard cut | 022 |
| [PLAN-007](PLAN-007-audio-hud-results.md) chase label, health display, compass | Removed; one white arrow | 020 |
| [PLAN-008](PLAN-008-floor-collapse.md) collapse closure and lethal rooms | Exit room never collapses; rubber-band wall; grab then throw | 018 |

Player, Hunter and Floor code is shared by every scene, so limiting SPEC-004 to HorrorRun would need parallel configuration paths. Recommendation: at H0, add a dated note to each affected LIVE criterion pointing to its successor plan. The existing evidence stays as history. This is the owner's decision.

### 3.5 Coverage matrix

| SPEC-004 | Plan |
|---|---|
| §2.1 conclusions and GDD conflicts | No separate work; applied through the rows below and §3.4 |
| §2.2 slow approach, Stalk, Retreat, lunge stagger, habits, four tunables, felt intelligence 1–6, region hints, environment habits, Final IK, stop-motion | 015 |
| §2.2 first-contact spawn rule | 015 defines it; 026 enforces it in generation |
| §2.2 partition-ignoring hunter, build order, briefs, novelty on schedule | 016 |
| §2.2 per-hunter sound identity | 021 slots and mix; 016/017 per-hunter assets |
| §2.2 hidden mutations | 023 cadence; 015 hook; 016/017 pools |
| §2.2 the catch | 022 (sting 021, cut 020) |
| §2.3 HUNTED label, hidden health, hidden count display | 020 |
| §2.3 clean all-clear, false positives | 021 |
| §2.3 arrow decision | 019 data; 020 drawing |
| §2.3 look-back snap | 013 rules and input; 022 camera |
| §2.3 occluded player noise, crouch | 011 contract; 013 emitters and crouch; 015 hearing and hint radius; 021 mix |
| §2.4 early bail, worsenings, worsen verb, persistent hell, stakes vs lethality | 023 |
| §2.4 hit grace | 013 |
| §2.5 cake density, creation horror, golden count data | 019; display 020 |
| §2.5 combo sting | 021 |
| §2.5 exit soft-lock, hands, deep dark | 018 |
| §2.5 generation retry | 026 |
| §2.5 results | 020; telemetry fields 012 |
| §2.5 micro-events | 022 |
| §2.6 silence, binaural and occlusion, minimal audio, shared hearing, unplayed cues, breathing, heartbeat | 021 |
| §2.6 title, pause and settings | 020 |
| §2.6 embodiment | hands 013; landing dip 022; breathing 021 |
| §2.6 degradation layer, startle budget | 022 |
| §2.6 fail forward | 013 rules; 022 camera |
| §2.7 | 020 |
| §2.8 items 1–2 / 3, 4, 7–10 / 5 / 6 | 013 / 012 / 020 removes hint, 023 and 025 use Interact / 020 hides slots, 024 fills them |
| §2.9 rows 1–6, on-hit boost, regeneration | 013 (ramp colliders 026) |
| §2.9 row 7 | 014 |
| §2.10 | 018 |
| §2.11 rooms, multi-floor, ramps, puzzles, freeze rooms, diversity, gaps | 026 |
| §2.11 cake traps | 019 |
| §2.12 relics, flashlight, expensive shop | 024 |
| §2.12 selection cadence, duplicates, no cap | 023 |
| §2.13 upgrades and consumables / general curses | 024 / 023 |
| §2.14 | 025 |
| §2.15, §2.16 | 016 (first three), 017 (rest) |
| §2.17 | 016 (systemic slot), 017 (other candidates) |
| §2.18 arrow / flashlight / lighting | 019 and 020 / 024 / 022 |
| Deferred rows (§2.4, §2.5, §2.12, §2.13, §2.14) | §7 |

## 4. Sequence

1. **H0.** Wait for SPEC-004 approval. Resolve §3.4 and §6, then assign workers and baselines. PLAN-012, and PLAN-014 steps 1–3 (capturing evidence), may start once H0 is met.
2. **H1.** Collect the children's contract proposals, run GitNexus upstream impact on each changed Core symbol, then publish the §3.3 contracts and fixtures in one coordinator change.
3. **Wave 1: feel slice.** This covers SPEC-004 §4's six priorities and three playtest unblockers. 013 grace and look-back rules; 026 stair ramps and typed cake anchors; 019 arrow fix and cake density; 015 walk-speed approach and Stalk; 021 presence loops through occlusion and a music floor that never resolves; 020 removal of the chase label and health display; 022 look-back camera and the held catch; 023 early bail. Integrate at **H2**.
4. **Wave 2.** Remaining work in 013, 014 (fix), 015, 016, 018, 019, 020, 021 and 022. Integrate at **H3**.
5. **Wave 3.** 017, the rest of 023, 024, 025 and the rest of 026. Integrate at **H4**, then **HV**.

With four agent slots, run the coordinator plus three workers. Rotate ready work; do not weaken dependencies.

## 5. Verification

These common gates apply to every child plan:

- Run GitNexus upstream impact before editing an indexed symbol. Warn on HIGH or CRITICAL; treat UNKNOWN or partial results as unresolved.
- Complete SPEC-001 §13: script headers, pure-layer tests, assembly compilation, `ast-grep scan`, the saved graph conformance queries, `ArchitectureConformanceTests` and the project suite.
- Acquire the [Unity lease](../../tools/coordination/README.md) before any Unity operation or save into an open checkout, and follow the [PLAN-002 testing admission](PLAN-002-parallel-coordination.md#testing-admission). Write output under `Logs/AgentValidation/<PLAN-ID>/<lease-token>/<run>/`.
- Check wiring changes live in HorrorRun; a static pass does not prove a feature is connected. Report any fallback path that fires as a failure.
- Feel rows are accepted by owner playtest, recorded with date and build. Metrics support but do not establish feel.

For this plan: every SPEC-004 §2 requirement maps to a plan or to §7; the H1 contracts compile with fixtures and pass conformance; and each checkpoint has retained evidence.

## 6. Risks and open questions

| Item | Type | Proposed resolution / owner |
|---|---|---|
| 1. §2.3 "hide the whole HUD in a chase" vs §2.18 arrow "always active, including during chases" | Spec conflict | Arrow exempt; Hao Guo at H0 |
| 2. §2.5 "second grab fatal" vs §2.10 "no special fatal-grab rule" | Spec conflict | §2.10 (later rule set) wins; Hao Guo |
| 3. §2.12 "no relic is a percentage" vs §2.13 flat and multiplier upgrades | Spec conflict | Catalogue wins; the rule applies to rule-changing upgrades; Hao Guo |
| 4. §2.16 names "Lower Gravity", absent from §2.13, and "Wagered Haul", deferred | Spec gap | Confirm or drop Lower Gravity; Hao Guo |
| 5. §2.17 cites "the Marionette already listed"; no Marionette exists | Spec gap | Name it or delete the reference; Hao Guo |
| 6. §2.5 golden count on the HUD vs §2.7 target of arrow, count and slots only | Spec conflict | Hao Guo |
| 7. §2.4 "wager the wallet" row not marked deferred; §3 defers it | Spec wording | Treat as deferred |
| 8. SPEC-004 §5 open questions (Mannequin, Faithless Arrow, numbers, themes, shrine vs event cadence) | Design | Owners named in each child |
| 9. Boilerplate conflicts (§3.4) | Governance | Hao Guo at H0 |
| 10. Shared checkout has a large uncommitted baseline; worktrees from HEAD miss it | Integration | Verify baseline before delegating |
| 11. New persistence, spatializer package and physics layers | Shared settings | Coordinator only, under lease |

## 7. Deferred follow-ups

Deferred by SPEC-004: declining a hunter at selection; bring-in items between runs; the wallet wager and Wagered Haul; the Shrine of Purgatory revive; co-op and networking. Not covered: survivor classes beyond the flashlight; the shelter and shop screen redesign beyond PLAN-020/024; permanent unlocks beyond hunter gating. Each needs a new DRAFT plan or spec.

## 8. Definition of done

- [ ] H0: SPEC-004 LIVE or named plans approved; §3.4 and §6 decisions recorded; owners assigned.
- [ ] H1: §3.3 contracts compiled with fixtures; conformance and project tests pass.
- [ ] H2, H3 and H4 each have integration evidence and owner playtest notes.
- [ ] Every child plan is COMPLETED, SUPERSEDED or CANCELLED with a reason.
- [ ] The coverage matrix is re-audited against the final SPEC-004 with no requirement unassigned.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Decomposition | PLAN-011 to PLAN-026 registered DRAFT; no code changed | [Registry](../index.md#spec-004-decomposition-on-2026-09-30) |
