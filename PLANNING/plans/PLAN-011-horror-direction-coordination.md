---
id: PLAN-011
type: plan
title: Horror direction execution map and shared contracts
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Coordinator role (assignee UNKNOWN — owner input needed); design authority Hao Guo
specs: [SPEC-004, SPEC-001, SPEC-002, SPEC-003, SPEC-005]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-011 — Horror direction execution map and shared contracts

> Status: LIVE since 2026-09-30, approved by Hao Guo together with SPEC-004 and every child plan. Umbrella for [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md). Engineering follows [SPEC-001](../specs/SPEC-001-project-architecture-guidelines.md). See the [registry](../index.md). `DOCUMENTATION/direction.md` is absent, so no direction item is linked.

## 1. Objective

Split SPEC-004 into fifteen child plans that can be run in parallel, with one owner for each file, a fixed set of shared contracts and named integration checkpoints. The priority "feel slice" should land first, and every SPEC-004 requirement should belong to exactly one plan or be recorded as deferred. This plan owns the shared Core and Session contracts, Orchestrators, scene roots, project settings, the coverage matrix and the conflict resolution with the LIVE boilerplate plans. It delivers no gameplay itself.

## 2. Starting point

Locations below were rechecked by text search on 2026-09-30. No Unity run and no GitNexus query was made for this decomposition, so caller sets and serialized wiring are unverified.

- SPEC-004's "today" columns come from the owner's 2026-09-30 code survey. The horror run already exists: [`HorrorRunSceneRoot`](../../Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs), Session systems `Expedition`, `HorrorEffects` and `Progression`, and a Domain `Procedural` castle generator. SPEC-003 therefore has generator code but no registered plan; PLAN-026 reconciles this.
- PLAN-001 to PLAN-003 and PLAN-005 to PLAN-010 were the LIVE hand-built boilerplate plans. Several of their criteria conflicted with SPEC-004 (§3.4); at H0 on 2026-09-30 the owner directed that they be archived, and they are now SUPERSEDED.
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

Ownership follows [PLAN-002 §3](../archive/plans/PLAN-002-parallel-coordination.md#exclusive-file-ownership): the coordinator owns `Core/**`, `Session/**` contracts, `Orchestrator/**`, scene setup, assemblies, `Packages/**` and `ProjectSettings/**`. It explicitly delegates Session subtrees to PLAN-023/024. One writer per file per wave. Every other change is submitted to the path's owner as a request; each child lists its requests in §3.

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

Amended 2026-09-30: the hearing and persistence requirements in the historical table below are superseded. Shared acoustic attenuation remains, but hunter hearing admits player movement, Firecracker and player-triggered cake traps, never Pacification, world audio (including grabs/pickups) or false positives. Explicit hunter-rule broadcasts remain separately typed mechanics, never inferred from playback. Test negative ingress at Hunter, Director and Session routes. Persistence serves settings and run history/best depth; no lifetime prerequisite, persistent curse or debt is required.

Amended 2026-09-30: [SPEC-005](../specs/SPEC-005-hunter-briefs.md) is LIVE. Only the ten new hunters enter run selection: Echo/Weaver/Ticking from round 1, Ram/Mannequin 4, Mimic/Blinder 5, Skip/Herald/Stare 6. Gates govern first admission only; all retained instances stay active. Generator capacity must cover the complete request, including duplicates and Nothing extras, or fail/retry explicitly; a roster shortfall is never a successful floor. Track module source, generated/bound assets, fact consumers and live evidence separately. Collector guidance in the old table is superseded by the declined candidate decision.

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

### 3.4 Conflicts with the boilerplate plans (resolved at H0)

| LIVE item | SPEC-004 position | Plan |
|---|---|---|
| [PLAN-003](../archive/plans/PLAN-003-player-movement-health.md) look-back steering ×0.35, frozen heading, free head scan | Fixed snap behind, no scanning, steering continues | 013, 022 |
| PLAN-003 health: lunge 50, states 100/50/25/0, no grace | Grace, severity-scaled boost, full health each floor, regeneration | 013 |
| PLAN-003 vault lock 0.25 s, mantle 0.35 s | Physics mantle with live look, steering in the last third | 013 |
| [PLAN-005](../archive/plans/PLAN-005-hunter-chase.md) Stalk optional; investigation at chase speed | Stalk required; walk while investigating | 015 |
| PLAN-005/[006](../archive/plans/PLAN-006-camera-postfx-feedback.md) death camera snap and fade | Held close-up, hard sting, hard cut | 022 |
| [PLAN-007](../archive/plans/PLAN-007-audio-hud-results.md) chase label, health display, compass | Removed; one white arrow | 020 |
| [PLAN-008](../archive/plans/PLAN-008-floor-collapse.md) collapse closure and lethal rooms | Exit room never collapses; rubber-band wall; grab then throw | 018 |

Resolved at H0 on 2026-09-30: the owner directed that the old LIVE plans be archived. PLAN-001 to PLAN-003 and PLAN-005 to PLAN-010 are SUPERSEDED by the plans named in the registry. Their unchecked criteria are not carried forward unless a successor restates them, and their evidence remains as history.

### 3.5 Coverage matrix

Amended 2026-09-30: the obsolete early-bail/persistent-hell and unused-Interact assignments below are superseded: PLAN-023 removes bail and Bail Bond, while shrine Interact belongs to PLAN-025. The run-scoped worsen options in [SPEC-006](../specs/SPEC-006-worsen-verb-persistent-hell.md) remain DRAFT/not approved; persistent hell is dropped, not deferred implementation. SPEC-005 governs PLAN-016/017 roster briefs, PLAN-023 admission/curses, PLAN-024 hunter-gated upgrades and PLAN-021 sounds. PLAN-017 records collector, lingering-room and touch-screamer as declined; Faithless Arrow is the ordinary Mimic curse, not another threat. [PLAN-027](PLAN-027-audit-remediation-structural-refactors.md) owns audit remediation, not additional gameplay scope.

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

Amended 2026-09-30: Wave 1's “023 early bail” is superseded by removal of the physical, input and completion path and Bail Bond. No SPEC-006 implementation is admitted without a later owner approval of a run-scoped option; no persistent-hell work. Historical wave numbering above is distinct from the audit's WP wave 1 recorded in §9.

## 5. Verification

These common gates apply to every child plan:

Amended 2026-09-30: §5 and every child's §8 require evidence tied to the exact integrated hash, setup version, fresh result counts, build seed and owner-review date. Earlier log entries and accepted branches are history, not proof of current exits. Headless unit tests cannot establish rendering, physics, input focus, audio or owner feel. Retain failed gates and distinguish accepted work from promoted work.

- Run GitNexus upstream impact before editing an indexed symbol. Warn on HIGH or CRITICAL; treat UNKNOWN or partial results as unresolved.
- Complete SPEC-001 §13: script headers, pure-layer tests, assembly compilation, `ast-grep scan`, the saved graph conformance queries, `ArchitectureConformanceTests` and the project suite.
- Acquire the [Unity lease](../../tools/coordination/README.md) before any Unity operation or save into an open checkout, and follow the [PLAN-002 testing admission](../archive/plans/PLAN-002-parallel-coordination.md#testing-admission). Write output under `Logs/AgentValidation/<PLAN-ID>/<lease-token>/<run>/`.
- Check wiring changes live in HorrorRun; a static pass does not prove a feature is connected. Report any fallback path that fires as a failure.
- Feel rows are accepted by owner playtest, recorded with date and build. Metrics support but do not establish feel.

For this plan: every SPEC-004 §2 requirement maps to a plan or to §7; the H1 contracts compile with fixtures and pass conformance; and each checkpoint has retained evidence.

## 6. Risks and open questions

| Item | Type | Proposed resolution / owner |
|---|---|---|
| 1. §2.3 "hide the whole HUD in a chase" vs §2.18 arrow "always active, including during chases" | Spec conflict | Decided 2026-09-30: arrow stays visible during chases; other HUD chrome hides |
| 2. §2.5 "second grab fatal" vs §2.10 "no special fatal-grab rule" | Spec conflict | Decided 2026-09-30: §2.10 rules win; no special fatal-grab rule |
| 3. §2.12 "no relic is a percentage" vs §2.13 flat and multiplier upgrades | Spec conflict | Decided 2026-09-30: catalogue multipliers and flat upgrades allowed; the rule applies to rule-changing upgrades |
| 4. §2.16 names "Lower Gravity", absent from §2.13, and "Wagered Haul", deferred | Spec gap | Decided 2026-09-30: drop Lower Gravity; Wagered Haul stays deferred |
| 5. §2.17 cites "the Marionette already listed"; no Marionette exists | Spec gap | Decided 2026-09-30: delete the Marionette reference |
| 6. §2.5 golden count on the HUD vs §2.7 target of arrow, count and slots only | Spec conflict | Decided 2026-09-30: golden count shown as a quiet second figure |
| 7. §2.4 "wager the wallet" row not marked deferred; §3 defers it | Spec wording | Decided 2026-09-30: treat as deferred |
| 8. SPEC-004 §5 open questions (Mannequin, Faithless Arrow, numbers, themes, shrine vs event cadence) | Design | Numbers: provisional values in config, listed for owner tuning (decided 2026-09-30). Mannequin rule, Faithless Arrow, themes and shrine cadence: ask when the owning plan reaches them |
| 9. Boilerplate conflicts (§3.4) | Governance | Decided 2026-09-30: old plans archived as SUPERSEDED (§3.4) |
| 10. Shared checkout has a large uncommitted baseline; worktrees from HEAD miss it | Integration | Verify baseline before delegating |
| 11. New persistence, spatializer package and physics layers | Shared settings | Coordinator only, under lease |
| 12. Push policy after H0 ("pushed only on request") | Governance | Decided 2026-09-30 (owner, later that day): push `main` and every worker branch except `wt/integration` to origin after each integration batch, as a saved state; never force-push |
| 13. Integration order: `main` moved and was pushed before tests (audit L2-01) | Process | Decided 2026-09-30: `main` moves only through the fail-closed gate in `tools/integration/` (candidate first, ratchet, quarantine with expiry, `evidence/gate-ledger.jsonl`) |
| 14. SPEC-004 §5 and plan open questions answered on 2026-09-30 | Design | Recorded in each owning plan's open-question table (PLAN-014, 016, 017, 019, 021 to 026); Echo's per-shrine changed outcome (PLAN-025) and the worsen verb and persistent hell designs (SPEC-006, DRAFT) remain open |
| 15. Public repository | Governance | Decided 2026-09-30: never commit Asset Store or other third-party content; project assets stay uncommitted for now; [VENDOR.md](../../VENDOR.md) records sources |

Amended 2026-09-30: items 8 and 14 are superseded where they request decisions already settled: Mannequin moves only in darkness while unobserved, lit rooms are refuges and Wick always freezes it; Faithless Arrow is ordinary Mimic curse content; four distinct themes are Castle/Hospital/School/Basement; shrine/event fear-axis exclusion is removed. Echo repeats the last shrine at its normal cost, with no changed-outcome/doubling table. SPEC-006 is not approved and persistent hell is dropped. Final IK's vendor assembly definition is approved; Steam Audio 4.8.1 is imported with routing pending; the High Definition Render Pipeline (HDRP) is removed. Remaining owner questions are tuning, pending run-scoped worsen design/binding and actual play acceptance, not these settled policies.

## 7. Deferred follow-ups

Deferred by SPEC-004: declining a hunter at selection; bring-in items between runs; the wallet wager and Wagered Haul; the Shrine of Purgatory revive; co-op and networking. Not covered: survivor classes beyond the flashlight; the shelter and shop screen redesign beyond PLAN-020/024; permanent unlocks beyond hunter gating. Each needs a new DRAFT plan or spec.

## 8. Definition of done

- [x] H0: SPEC-004 LIVE or named plans approved; §3.4 and §6 decisions recorded; owners assigned.
- [ ] H1: §3.3 contracts compiled with fixtures; conformance and project tests pass.
- [ ] H2, H3 and H4 each have integration evidence and owner playtest notes.
- [ ] Every child plan is COMPLETED, SUPERSEDED or CANCELLED with a reason.
- [ ] The coverage matrix is re-audited against the final SPEC-004 with no requirement unassigned.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Decomposition | PLAN-011 to PLAN-026 registered DRAFT; no code changed | [Registry](../index.md#spec-004-decomposition-on-2026-09-30) |
| 2026-09-30 | H0 admission | Owner approved SPEC-004 (and SPEC-003) and PLAN-011 to PLAN-026 as LIVE; archived PLAN-001 to PLAN-003 and PLAN-005 to PLAN-010 as SUPERSEDED; accepted §6 items 1–7 with provisional numbers; first threats Echo, Weaver and Ticking; verified work lands as local commits on main and is pushed only on request; placeholder art and voice; ask before any package or asset download. Execution: Claude coordinates; workers are Hermes one-shot subagents on gpt-6-astra (high, xhigh for hard tasks) and gpt-6.1-sol (high, clearly defined tasks), in isolated worktrees, integrated by the coordinator under the Unity lease | [Registry](../index.md#approval-and-boilerplate-archive-on-2026-09-30) |
| 2026-09-30 | H1 contracts and integration batches 1–5 | Shared Core contracts landed (1685893). Five integration batches merged worker branches into local main under the Unity lease; full Edit Mode suites: batch 1 1126/1135, batch 2 1194/1209, batch 4 1323/1342, batch 5 1375/1395 passing. Remaining failures are tracked per owning plan; four focus-dependent capture tests fail only when Unity is not the foreground window | Commits 1685893, e6e297f, acb8e29, 7bdabbd, 46781b9, 3da4714; `Logs/AgentValidation/PLAN-011/integration/` |
| 2026-09-30 | Integration batches 6–11 | Full Edit Mode suites: batch 6 1428/1457, batch 7 1497/1549, batch 8 1723/1783 (a leaked `Time.timeScale = 0` caused most failures; fixed), batch 9 1882/1918, batch 10 2085/2114, batch 11 2290/2329 passing (35 failed, 4 skipped; 7 failures are focus-dependent). `main` was fast-forwarded and pushed before each suite ran | Coordinator tracker; batch 11 failures are the bootstrap baseline in [evidence/gate-ledger.jsonl](../../evidence/gate-ledger.jsonl) |
| 2026-09-30 | Audit follow-up: gates versioned | Fail-closed integration gate, delegation scripts with run records and scope checks, hooks with LFS, vendor ignores and VENDOR.md committed (7e7c4bd); instructions slimmed to a 6 KB AGENTS.md plus on-demand skills; SPEC-001 amended (Session run-rules layer and edges, bounded Manager engine calls, responsibility alarms, legacy-scene compatibility layer, Appendix A ledger) | `tools/integration/README.md`, `tools/delegation/README.md`, SPEC-001 §8b, §13f, Appendix A |
| 2026-09-30 | Amended 2026-09-30: batch 12 gate | fail-setup, not promoted; setup array invocation failed. Blocking failures 25 against baseline 28, including 6 new failures; a lower total did not permit promotion. Candidate retained; main unchanged | [Gate ledger](../../evidence/gate-ledger.jsonl), label `batch12`; exact candidate and counts in ledger |
| 2026-09-30 | Amended 2026-09-30: audit WP wave 1 | WP-I, WP-P, WP-H, WP-A, WP-U, WP-F, WP-M and WP-C accepted with the headless tier on each. Acceptance is not promotion or H2/H3/H4 owner acceptance; integrated candidate still needs the gate | Coordinator tracker 2026-09-30; branches `wt/wp-routing`, `wt/wp-progression`, `wt/wp-hunter`, `wt/wp-audio`, `wt/wp-ui`, `wt/wp-floor`, `wt/wp-player`, `wt/wp-present`; [gate ledger](../../evidence/gate-ledger.jsonl) retains failed batch13 prechecks separately |
| 2026-09-30 | Amended 2026-09-30: theme restyle | Four-theme restyles and predefined room templates assigned: Gothic Castle, 1970s Hospital, post-war School, industrial boiler-room Basement; distinct architecture and palettes, not swapped props. Kit v1 exports accepted; Castle partial and runtime import/template wiring/visual acceptance still outstanding | Coordinator tracker 2026-09-30; kit bases `477e7ff`, `ad3e066`; [PLAN-026](PLAN-026-level-diversity-gaps-multifloor.md) |
