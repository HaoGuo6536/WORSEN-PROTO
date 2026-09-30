---
id: PLAN-017
type: plan
title: Hunter roster expansion and non-hunter threats
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Hunter roster workers, one per archetype (assignees UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-017 — Hunter roster expansion and non-hunter threats

> Status: DRAFT since 2026-09-30. Implements the [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.15 hunters not shipped by PLAN-016, their §2.16 curses, and the remaining §2.17 non-hunter threat candidates. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Add the rest of the requested roster behind depth gates, so each arrival returns real unknown for a few runs. Each hunter ships with its approved brief, four tunables, habits, environment habit, sound set, mutation pool with tells and §2.16 curses. Also add the approved non-hunter threat candidates. Every archetype lives in its own module from PLAN-016, so separate workers can build different hunters in parallel.

## 2. Starting point

- Starts after PLAN-016 establishes the extension point, retires placeholders and ships three threats. The seven or eight remaining hunters depend on PLAN-016's choice.
- Herald audio is present and fixed by SPEC-004: `Assets/External/Audio/Mangled_Screams_free/Sounds/ms_mangled_scream_03.wav` and `sb_mangled_scream_01.wav` to `sb_mangled_scream_03.wav`.
- No voice lines exist for the Stare's "I see you" and "Find me" [assumed; search before recording new assets].
- Room lighting state exists only as floor and collapse data (`FloorConfig`, `RoomCollapseVolume`). A per-room "lit" contract for the Mannequin is not established.

## 3. Changes

| Hunter (kind) | New mechanics needed | Depends on |
|---|---|---|
| The Echo (pursuer) | Record the player's path; replay with a fixed delay; never backtrack or take an untaken route; blocked by closed doors, collapse and drops | Player pose history view; PLAN-026 doors |
| The Mannequin (pursuer) | Move only in darkness and unobserved; freeze while lit or looked at; rare light-failure subversion; silent | Per-room lit state (PLAN-022/026); view-cone check; SPEC-004 §5 rule direction |
| The Stare (annoyance) | Spawn near the player; stay at the edge of view; hold the look to scare away; respawn cadence; chase with a harder loss rule on failure; "Find me" subversion | Voice assets; PLAN-015 loss rule per archetype |
| The Weaver (pursuer) | Ceiling traversal; warned web projectile that slows; Reposition action with a swept clear line before shooting; small projectile collider | Ceiling navigation approach; PLAN-013 slow hook |
| The Ram (pursuer) | Wind-up, straight charge with no turning, hard stop on walls | Partition Breaker needs breakable partitions (PLAN-026) |
| The Mimic (annoyance) | Pose as an uncollected cake; excluded from white-arrow targets; bite hold | PLAN-019 guidance exclusion; Faithless Arrow decision |
| The Skip (annoyance) | Count doorway and vault-window uses per floor; silent invisible teleport to intercept; cooldown; mark tell | Level interactables and marks (PLAN-026) |
| The Blinder (pursuer) | Floor traps and a thrown projectile that blind for a while; hiss and tick tells | Blind effect (PLAN-022); Muffled Dark (PLAN-021) |
| The Ticking (annoyance) | Follows behind and winds down; dormant while ticking; hunts when stopped; keys spawn near the player on a timer, marked by a threat arrow | PLAN-019 threat arrow; PLAN-020 drawing |
| The Herald (pursuer) | Every scream is a noise event and a Director hint to every hunter; attack scream in a radius with low damage and deafening; fixed audio mapping with pitch variation except the attack | Shared hearing (H1); PLAN-021 deafen and priority |

| §2.17 candidate | Form | Note |
|---|---|---|
| Cadence-like collector: offerings on a timer, enrages past a count | Systemic threat with threat arrow | Clock and count in one sentence |
| Lingering rule: staying in a room too long cracks it early | Systemic threat | Needs a PLAN-018 early-collapse request |
| Screamer: silence by touch before it calls every hunter | Annoyance | Uses shared hearing |
| Counterfeit arrow: points somewhere wrong for a few seconds | Curse, not a body | Implemented through PLAN-023 and PLAN-019 |

Each hunter also gets: its brief section in the PLAN-016 briefs spec; a per-archetype cue set within the PLAN-021 budget; its §2.16 curses (3–5 each) as catalogue entries with stacking caps; a depth or lifetime gate; and a PLAN-014 sweep pass.

## 4. Sequence

1. Hao Guo approves each brief. §2.17 candidates also need approval, since SPEC-004 lists them as candidates rather than approved rows.
2. For each hunter or threat, in the order Hao Guo sets, as one small child effort: run GitNexus impact on any shared hook it needs; build the module against fixtures; add curses; run the sweep; integrate under the lease.
3. Arena check without art; add art only after the rule reads.
4. Integrate at H4 with PLAN-023 selection and gating.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Per archetype: rule tests (for example, the Echo never backtracks, the Mannequin never moves while observed, the Ram cannot turn mid-charge, the Mimic is never a white-arrow target without Faithless Arrow); counterplay works; every tell is present; every curse applies and stacks to its cap.
- The Herald: a scream produces one noise event per listener, using the shared occlusion model; deafening muffles the mix; attack scream pitch stays fixed.
- PLAN-014 sweep per archetype, including ceiling and teleport movers.
- Live HorrorRun with several archetypes active, including duplicates. Owner playtest judges legibility.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Mannequin: dark-only or light-only | SPEC-004 §5 | Refuge rooms | Hao Guo, before the brief |
| Faithless Arrow breaks arrow trust | SPEC-004 §5 | Curse catalogue | Hao Guo |
| §2.17 "Marionette already listed" does not exist | Spec gap | Unknown candidate | Hao Guo (PLAN-011 §6 item 5) |
| Ceiling traversal for the Weaver | Technical | New navigation | Prototype spike before committing |
| Voice lines for the Stare | Assets | Missing content | Placeholder recording allowed; real voice later |
| Many active hunters with no cap | Performance, audio legibility | Frame time and mix | PLAN-021 priority; profiling |

## 7. Deferred follow-ups

Further roster entries beyond §2.15; bosses; co-op-only threats.

## 8. Definition of done

- [ ] Every remaining §2.15 hunter ships with an approved brief, module, cues, curses, gate and sweep pass, or has a recorded owner decision to drop it.
- [ ] Each approved §2.17 candidate ships the same way, or is recorded as declined.
- [ ] Live HorrorRun evidence with mixed and duplicate rosters.
- [ ] Owner playtest notes per hunter.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
