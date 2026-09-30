---
id: PLAN-026
type: plan
title: Level diversity, gaps and multi-floor generation
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Level generation worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-003, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-026 — Level diversity, gaps and multi-floor generation

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.11](../specs/SPEC-004-horror-direction-content-proposals.md#211-level-and-room-design) (except cake traps), the §2.9 stair-ramp geometry, the §2.5 generation-retry row, and generator requests from other SPEC-004 plans. Continues the procedural work of [SPEC-003](../specs/SPEC-003-procedural-maps-level-progression.md). Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Generate floors that grow larger, taller and more varied with depth, without losing the reachability, reproducibility and validation rules of SPEC-003:

- **Geometry:** stairs are ramps under stepped visuals; rooms span one, two or three cells with irregular footprints; later floors are multi-storey, with every level change reachable by more than a staircase.
- **Gaps:** voids leave optional pockets.
- **Designed rooms:** execution puzzles and threshold-freeze rooms appear from configured rounds.
- **Themes:** swappable module sets, starting with two.
- **Service to other plans:** typed cake anchors, first-contact hunter spawns, stateful interactables and navigation areas.

## 2. Starting point

- `Domain/Procedural` exists with no registered plan. [`ProceduralController`](../../Assets/Scripts/Domain/Procedural/Controller/ProceduralController.cs) (326 lines) generates a castle floor around a four-door exit hub: 12 m cells on one storey, five castle families, and module kinds VaultPartition, WindowPartition, SlidePartition, TorchGallery, OpenStairHall, SplitLevelLibrary, BrokenCloister, BrokenGallery, MerchantRefuge and ExitHub. [`ProceduralConfig`](../../Assets/Scripts/Domain/Procedural/Config/ProceduralConfig.cs): 7 initial rooms, +2 per round, maximum 15.
- Stairs are stepped blocks ([`ProceduralCastlePresenter.Stairs`](../../Assets/Scripts/Domain/Procedural/Driver/ProceduralCastlePresenter.cs)). Cakes are lines of Flow anchors (lines 183–194). Hunter spawns are the rooms other than the spawn room (line 97), with no line-of-sight or distance check found [verify].
- [`ProceduralDriver`](../../Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs) validates reachability on the runtime NavMesh. SPEC-004 reports one failed assembly ends the run, with no retry.
- Room connections are openings; only the exit door is a physical door. SPEC-003's acceptance evidence (reproducible manifests, seed samples, growth measurements) is not recorded in the registry.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Stairs become one ramp collider under a stepped visual; wider landing collision; hunters use the ramps | Castle and geometry presenters; NavMesh check | — |
| 2 | Typed cake anchors (Precision, Detour, Risk, Vertical) with room for one to three per room | `ProceduralController` anchor emission | PLAN-019 rule |
| 3 | First-contact spawn guarantee (PLAN-015 rule); spawn-moving effects (Random Spawn) pass the same check | Spawn selection and validation | PLAN-015 |
| 4 | Generation retry: bounded retries with an incremented seed, then a reported fallback that is never counted as success | Procedural manager/controller; Expedition request | SPEC-003 §2.5 |
| 5 | Larger rooms: two- and three-cell modules and irregular footprints | Module catalogue, layout growth | — |
| 6 | Multi-floor buildings for later rounds: drops through broken floors, holes and shafts, climbable ledges, collapsed ramps, balconies; going up by climbing and rebounding; player-only shortcuts stay off hunter routes | Layout, geometry, off-mesh links, traversal access | PLAN-013 ledge climb |
| 7 | Gaps: missing cells, shafts, collapsed wings; required cakes and exit on the connected part; pockets beyond gaps optional | Layout and validation | 6 |
| 8 | Execution puzzles from a configured round (switches in order, a dimming lit path, a door open while moving, a timed vault sequence); never a wait state or a note | Puzzle modules, drivers | 5 |
| 9 | Threshold-freeze rooms: authored doorway setups (next cake through a door, hunter audible beyond, collapse behind) | Module rules | 5 |
| 10 | Themes as swappable module sets (families, materials, props, lights, sound zones, fog and hand looks); first slice of two themes | Theme config, catalogue | Theme choice (§6) |
| 11 | Stateful interactables: doors that open and close, lamps and torches, knockable props, breakable or thin partitions, threshold marks | Level/Procedural drivers; Core description (coordinator) | H1 world interactables |
| 12 | Navigation areas for the partition-ignoring hunter and ceiling movers | Bake and area setup | PLAN-016/017 |
| 13 | Shrine placement and gap edges for Passage | Placement pass | PLAN-025 |

## 4. Sequence

1. Reconcile existing generation with SPEC-003 §2. Record which requirements are met, with evidence (manifests, reachability tests) or as gaps. Run GitNexus upstream impact on `ProceduralController`, `ProceduralDriver`, the castle and geometry presenters and the spawn consumers.
2. **Wave 1:** 1 and 2 (playtest unblocker; SPEC-004 priority 2).
3. **Wave 2:** 3, 4 and 11.
4. **Wave 3:** 5, 6, 7, 12 and 13, then 8, 9 and 10.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification). SPEC-003's acceptance list applies in full: matching seeds reproduce layout manifests; a declared seed sample shows variation; measured growth between small and large configurations; validation of every required anchor, the exit and directional traversal; retained failed attempts.

- Generator tests: ramps replace stepped colliders; one to three typed anchors per room; spawns fail the check when visible or too close; retries increment the seed and report a fallback; gaps never disconnect required cakes or the exit; hunter routes exclude player-only shortcuts.
- The PLAN-014 sweep passes on the new modules and multi-floor layouts.
- Live HorrorRun on sample seeds per theme; screenshots of multi-floor drops and gaps; owner review of diversity.
- A fallback symptom to watch: any run whose layout comes from the fallback path is reported, not counted as generation.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| First two themes (castle plus hospital proposed) | SPEC-004 §5 | Content scope | Hao Guo |
| Generation budget for larger, taller floors | SPEC-003 §5 | Load time | Declare before measuring |
| NavMesh for multi-storey and ceiling movers | Technical | Hunter pathing | Spike first; PLAN-014 sweep |
| Round from which puzzles and freeze rooms appear | Open value | Pacing | Config; Hao Guo |
| SPEC-003 generator built without a registered plan | Governance | Unclear acceptance | Step 1 records the evidence |

## 7. Deferred follow-ups

Themes beyond the first two; boss floors; the forest and uncanny sequences from the GDD as content.

## 8. Definition of done

- [ ] Ramps, typed anchors, first-contact spawns and retry work live and in tests.
- [ ] Larger, multi-floor and gapped layouts pass SPEC-003 validation and the PLAN-014 sweep.
- [ ] Puzzles, freeze rooms and two themes ship behind configured rounds.
- [ ] Interactables and navigation areas serve PLAN-015/016/017/025.
- [ ] SPEC-003 acceptance evidence is recorded.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
