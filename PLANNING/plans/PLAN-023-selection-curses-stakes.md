---
id: PLAN-023
type: plan
title: Selection cadence, curses and stakes
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Progression worker, delegated Session/Progression and Session/HorrorEffects (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-023 — Selection cadence, curses and stakes

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.4](../specs/SPEC-004-horror-direction-content-proposals.md#24-stakes-and-the-make-it-worse-ladder) (except the grace window, PLAN-013); the §2.12 selection rows; the §2.13 general curses; the §2.2 hidden-mutation cadence; and the Session side of the §2.9 health rule. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Turn "Make it Worse" into a ladder the player climbs by choice:

- A selection round every two rounds, offering a hunter and a curse together. Duplicates are allowed, there is no cap, and every chosen threat is active.
- General curses that remove information, tools, time and safety, stated plainly.
- An honourable early bail, an optional in-run worsen verb, and persistent consequences.
- Progression events roughly every eight floors, including hidden mutations with tells.

This plan also replaces the flags-based effect model with a catalogue that other plans extend.

## 2. Starting point

- [`ProgressionDefinitions.cs`](../../Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs): phases Dormant, ChooseThreat, ChooseCurse, Generating, Exploring, Shop, Ended, GenerationFailed. `ProgressionTraits` is an `int` flags enum near capacity (PLAN-011 §2).
- [`ProgressionConfig`](../../Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs): threat and curse offered every combat round; `_shopInterval = 2`; `_maximumActiveThreats = 5`; a hunter id is active once. Curses (echo-debt, afterimage, restless-masonry, gilded-hunger, borrowed-footsteps, unquiet-flame, sealed-sills) are mostly stat changes.
- [`ProgressionSessionController`](../../Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs) (427 lines) records health and ends the run at zero; health carries between rounds. The wallet persists across floors and dies with the player.
- The exit opens only after the last required cake; there is no bail. Interact is bound and never read. No persistence exists.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Effect catalogue replaces the flags: identifier, kind (threat, curse, upgrade, consumable), fear axis, availability floor, requirements (a hunter present, a prerequisite curse), stack cap. Publish the read-only active-effects view | Core contract (coordinator); Progression config and controller; HorrorEffects | H1 effect identity |
| 2 | Selection every two rounds: hunter and curse together; pure floors between; the shop keeps its own cadence; decline stays mandatory | Progression controller, phases | 1 |
| 3 | Duplicates, no cap, all chosen threats active on every floor | Progression, spawn requests | H1 threat kinds; PLAN-016 registry |
| 4 | Hunter curses offered only once that hunter is in the run; stack to a cap | Offer rules | 1; PLAN-016/017 entries |
| 5 | General curses from §2.13, excluding deferred Wagered Haul, as catalogue entries; each effect implemented by its owner (table below) | Catalogue data; requests | 1 |
| 6 | Early bail: the exit can open early at a cost (most of the wallet or a permanent curse) | Progression; request to PLAN-019 | Cost choice (§6) |
| 7 | Worsenings as removals: card copy states plainly what is removed; catalogue validation warns when an entry only raises hunter numbers | Catalogue validation test | 1 |
| 8 | In-run worsen verb on Interact: optional mid-floor action raising reward and pressure together | Progression, Interact routing (Input request), Floor or Environment request | Design (§6) |
| 9 | Persistent hell: a curse that stays until climbed out of, or debt that compounds on death | Progression plus persistence store | H1 persistence; design (§6) |
| 10 | Progression events about every eight floors: hazard, hunter upgrade, extra hunter, random, or hidden mutation with a tell and "something is different" | Event scheduler; PLAN-015 mutation hook | SPEC-004 §5 shrine cadence |
| 11 | Health: full each floor; carry-over removed from Session | Progression health path | PLAN-013 regeneration |
| 12 | Retire or rewrite the current general curses and old trait flags | Config, HorrorEffects | Owner decision (§6) |
| 13 | Stakes climb faster than lethality: top rungs survivable by a skilled player | Playtest criterion | — |

| General curse | Effect owner |
|---|---|
| No Look-Back | PLAN-013, PLAN-022 |
| Silent Presence | PLAN-021 |
| Hidden Count | PLAN-020 |
| Darker Floors | PLAN-022, PLAN-026 |
| Random Spawn | PLAN-026 (first-contact check applies) |
| Shuffled Collapse, Faster Collapse | PLAN-018 (via PLAN-019 ordering) |
| Nothing??? | PLAN-015 mutation hook |
| Thin Skin, Short Grace, Slow Mend, No Regen, Rough Start, Short Burst, Heavy Legs | PLAN-013 |
| Spent Pockets | PLAN-024 |
| Greedy Door | PLAN-019 |

Prerequisite chains are data: No Regen requires Slow Mend; Heavy Legs requires Short Burst.

## 4. Sequence

1. Run GitNexus upstream impact on `ProgressionTraits` (every consumer), `ProgressionSessionController`, `ProgressionConfig` and `HorrorEffectsController`. Expect CRITICAL; warn before editing.
2. **Wave 1:** 6 (priority 6), using the existing model if change 1 is not yet frozen.
3. **Wave 2:** 1 and 11, retiring placeholders jointly with PLAN-016.
4. **Wave 3:** 2, 3, 4, 5, 7 and 12, then 10, 8 and 9 once their designs are approved.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- `ProgressionSessionController` tests:
  - cadence over twelve rounds; duplicates and no cap;
  - hunter curses only with that hunter; stack caps; prerequisites;
  - early bail cost applied exactly once;
  - health reset per floor; progression event cadence (seeded).
- Catalogue validation: every entry has an axis, floor and requirements; no two entries share an identifier; deferred entries are not offered.
- Live HorrorRun over several rounds: selection, bail, a mutation with its tell, curses taking effect.
- Owner playtest judges whether top rungs are survivable and catastrophic to lose.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Early bail cost: wallet share or permanent curse | Design | Stakes | Hao Guo |
| Worsen verb design | Design | Scope | Hao Guo; stays unbuilt until approved |
| Persistent hell form | Design | Persistence | Hao Guo |
| Keep, rewrite or retire the current general curses | Owner decision | Catalogue | Hao Guo |
| Shrine and event cadence on the same floor | SPEC-004 §5 | Double worsening | Hao Guo; coordinate with PLAN-025 |
| "Lower Gravity" named in §2.16 only | Spec gap | Missing entry | PLAN-011 §6 item 4 |

## 7. Deferred follow-ups

Wagered Haul and the wallet wager; decline a hunter; bring-in stakes (co-op).

## 8. Definition of done

- [ ] The effect catalogue replaces flags with conformance, and all consumers are migrated.
- [ ] Selection cadence, duplicates, no cap and hunter-gated curses work live.
- [ ] Every active general curse is implemented by its owner and tested.
- [ ] Early bail, progression events and health reset work live; the worsen verb and persistent hell are delivered or explicitly deferred by the owner.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Early bail | Early bail hold and wallet penalty, wired through door, run session and run summary | Commits e95b37a, 8be19cc |
