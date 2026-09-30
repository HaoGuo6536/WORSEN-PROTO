---
id: PLAN-016
type: plan
title: Hunter roster foundation and first three threats
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Hunter roster worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001, SPEC-002]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-016 — Hunter roster foundation and first three threats

> Status: DRAFT since 2026-09-30. Implements [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.2 (build order, one-page brief, partition-ignoring hunter, novelty on schedule), the first three entries of §2.15 and their §2.16 curses, and the §2.17 systemic slot. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Remove the five placeholder hunters and their fifteen curses. Establish the one-page brief, threat kinds and an archetype extension point, so each new hunter's rules live in their own module. Ship the first three threats in the Game Design Document (GDD) order:

1. One physical pursuer, tuned alone in the arena until its four tunables feel like tag and its approach frightens without art.
2. The partition-ignoring hunter.
3. One non-physical systemic threat expressed by the Director.

Add depth and lifetime-run gating, so later hunters arrive as novelty.

## 2. Starting point

- [`ProgressionConfig`](../../Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs) lists five threats: watcher, rusher, lurker, hexer and thorncaller. [`ProgressionTraits`](../../Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs) holds their 15 curse flags. [`HunterController`](../../Assets/Scripts/Domain/Hunter/Controller/HunterController.cs) applies them through `Cursed(...)` multipliers (lines 139–172).
- `HunterAttackStyle` covers Lunge, Projectile and GroundSpikes. Every archetype runs through one 473-line controller configured by profile flags. Ten very different hunters would turn it into a monolith.
- `_maximumActiveThreats = 5`; a hunter id can be active once. All five are available from round one. No persistence exists.
- Retained machinery per SPEC-004 §3: planner, fact model, factory, registry, motor, attack and animation configs, trait flags, light hooks, animation driver and Final IK. PLAN-015 adds behaviours and hooks.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Brief template and briefs as design authority: a new DRAFT spec ("Hunter briefs") via `$docs-plans new spec`, one section per hunter, approved before modelling | `PLANNING/specs/` (planning operation) | Hao Guo invokes |
| 2 | Archetype extension point: per-archetype rules module (controller + config) under `Domain/Hunter/Archetypes/<Name>/`, consulted by the shared controller; architecture review against SPEC-001 §2/§12 | Hunter Manager/Controller, new folders, assembly check | PLAN-015 hooks; SPEC-001 amendment if needed |
| 3 | Retire placeholders: remove the five profiles, their curses and traits; migrate tests to fixture archetypes | Hunter configs, `ProgressionConfig`, `HunterController`, tests | H1 effect identity |
| 4 | Threat kinds, duplicates, no cap, always active, in the registry and factory (distinct identity per duplicate) | `HunterRegistry`, `HunterFactory`, spawn requests | H1 threat kinds; PLAN-023 selection |
| 5 | First physical pursuer: brief, four tunables tuned in TagArena before art, habits, environment habit, sound set, mutation pool, §2.16 curses | New archetype module, profile, assets | 1, 2; PLAN-015 wave 1–2 |
| 6 | Partition-ignoring hunter (Weaver or Skip): paths through optional doors, vault windows and thin partitions | Archetype module; navigation area or agent type from PLAN-026 | 5; PLAN-026 request |
| 7 | Systemic threat expressed by the Director (the Ticking, the Stare, or a §2.17 candidate) | Archetype or Director module; guidance threat arrow if collectable | 5; PLAN-019 guidance |
| 8 | Novelty gating: availability by floor depth and lifetime runs | Threat catalogue data; Session persistence | H1 persistence |
| 9 | Silhouette and animation: readable against fog at look-back speed; non-human, masked or cloth-covered; stop-motion sample rate where chosen | Art placeholders, animation config | PLAN-022 fog and lighting |

**Requests.** Selection and offers (PLAN-023). Per-archetype cue slots and mix priority (PLAN-021). Navigation areas and breakable or optional partitions (PLAN-026). Threat arrow for collectable threats (PLAN-019, PLAN-020).

## 4. Sequence

1. Hao Guo chooses the three threats (§6) and approves the briefs spec.
2. Run GitNexus upstream impact on `HunterController`, `HunterFactory`, `HunterRegistry`, `ProgressionTraits` consumers and `ProgressionConfig`. Expect HIGH or CRITICAL; warn before editing.
3. Build change 2 with one fixture archetype. Change 3 retires the placeholders in the same window, so no build ships both.
4. Change 5 in TagArena: tune the four tunables alone until owner playtest accepts "tag", before any art.
5. Changes 6 and 7, then 4, 8 and 9, and integrate at H3.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Architecture conformance proves each archetype module depends only on permitted layers. A graph check shows the shared controller has no per-archetype branches added.
- Tests per archetype: its rule, counterplay condition and tell; each §2.16 curse's effect and stacking cap; duplicate instances keep distinct identities and are all active; gating hides unavailable threats.
- PLAN-014 sweep passes for every new archetype, including partition-ignoring paths.
- Arena evidence: four tunables recorded with provenance; owner playtest notes for the first pursuer before art.
- A fallback symptom to watch: a placeholder profile or trait still reachable from a build means retirement was partial.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Which §2.15 hunter is the first pursuer (candidates: Echo, Ram, Herald, Mannequin, Blinder) | Owner decision | Everything after step 3 | Hao Guo at H0 |
| Weaver or Skip as partition-ignoring; Skip is an annoyance type | Owner decision | Navigation work | Hao Guo |
| Which systemic threat is third (Ticking, Stare, §2.17 candidate) | Owner decision | Director work | Hao Guo |
| Mannequin rule direction and Faithless Arrow (SPEC-004 §5) | Open question | Brief content | Hao Guo, if chosen |
| Archetype modules may need a SPEC-001 taxonomy amendment | Architecture | Policy change | Amend SPEC-001 and its checks with reason, not relax them |
| Removing placeholders reopens LIVE PLAN-005 evidence | Governance | History | PLAN-011 §3.4 note |

## 7. Deferred follow-ups

The remaining seven hunters, their curses and the §2.17 candidates (PLAN-017). Decline-a-hunter stays deferred.

## 8. Definition of done

- [ ] Briefs spec approved for the three threats; placeholders and their curses removed with tests migrated.
- [ ] Archetype extension point passes conformance and hosts all three threats.
- [ ] Each threat has its rule, habits, environment habit, sound set, mutation pool and §2.16 curses, with tests and live evidence.
- [ ] Duplicates, no cap and always-active behaviour work in HorrorRun.
- [ ] Depth and lifetime gating work across runs using the persistence store.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
