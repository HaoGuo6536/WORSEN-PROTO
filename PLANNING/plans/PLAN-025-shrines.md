---
id: PLAN-025
type: plan
title: Shrines
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Shrine worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-025 — Shrines

> Status: DRAFT since 2026-09-30. Implements [SPEC-004 §2.14](../specs/SPEC-004-horror-direction-content-proposals.md#214-shrines), and uses the Interact binding from §2.8 item 5. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Place single-use shrines on generated floors that the player finds rather than is pointed at. Each is activated by touch or Interact without holding the player in place, and each has one effect on the same ladder as curses: a worse floor for a payout, greed, or a bought breath. Counts grow with depth: one on floors 3–7, two on 8–11, three on 12–15, and so on to a cap of six.

## 2. Starting point

- No shrine or altar code exists. `InputButtons.Interact` is bound in [`PlayerInputDriver`](../../Assets/Scripts/Presentation/Input/Driver/PlayerInputDriver.cs) and never read.
- The generator does not produce gaps, so Passage has nowhere to stand until PLAN-026. The effect catalogue, mutation hook and shelter deal screen come from PLAN-023, PLAN-015 and PLAN-020.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Shrine system: world object, activation by touch or Interact while moving, single use, activation fact | New Domain system (placement, activation); Session resolves effects | H1; architecture review |
| 2 | Placement: generator places shrines per floor on the depth curve, with a More Shrines upgrade hook | Request to PLAN-026 | 1 |
| 3 | Chance (floor 3): a random positive or negative effect for this floor from the curse and upgrade pools | Effect catalogue | PLAN-023, PLAN-024 |
| 4 | Bargain (floor 3): marked on the floor; after escape, the shelter offers three curses; taking one pays Golden Cakes scaled to curse value and floor; walking away is free | Session deal state; shelter screen (PLAN-020) | PLAN-023 |
| 5 | Pacification (floor 5): every hunter drops its belief; the loud activation reaches hunters a moment later through shared hearing | Hunter belief request (PLAN-015); noise event | H1 shared hearing |
| 6 | Wick (floor 6): lights every lamp for a while; the Mannequin becomes harmless and other hunters see better | Lamp interactables (PLAN-026); lighting (PLAN-022) | PLAN-017 Mannequin |
| 7 | Passage (floor 8): at a gap's edge, bridges into an unreachable pocket lined with Golden Cakes; tiles collapse at once | Bridge geometry and collapse (PLAN-026, PLAN-018) | PLAN-026 gaps |
| 8 | Protection (floor 8): costs Golden Cakes; grants a shield that is spent before health, does not regenerate and lasts until gone | Player shield request (PLAN-013); wallet | — |
| 9 | Echo (floor 8): reactivates the last shrine used this run with a changed outcome | Shrine history in run state | 3–8 |
| 10 | Purgatory (floor 8): yield raised by up to 100%, scaled down by how much of the floor is already collected; one extra hunter from the roster spawns at once; a chance of a hidden mutation. The revive form is co-op only and deferred | Progression, spawn request, mutation hook | PLAN-016, PLAN-015 |

## 4. Sequence

1. Hao Guo decides the shrine and progression-event cadence (SPEC-004 §5) with PLAN-023.
2. Architecture review for the shrine system's layer and system placement. Run GitNexus impact on any shared symbol touched.
3. Changes 1 and 2, then Chance and Bargain (3, 4), Pacification (5) and Protection (8).
4. After PLAN-026 gaps and PLAN-017 Mannequin: Passage (7), Wick (6), Echo (9) and Purgatory (10).

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- Placement tests over a seed set: counts match the depth curve and cap; the floor from which each shrine appears is honoured; placement stays reachable, except Passage, which stands at a gap's edge.
- Effect tests per shrine: single use; resolves without holding the player; Bargain pays the scaled sum exactly once; Purgatory scaling at early and late activation; Echo changes the outcome.
- Live HorrorRun: activate each shrine type while moving. Pacification's delayed broadcast reaches hunters.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Shrine and event cadence on one floor | SPEC-004 §5 | Double worsening | Hao Guo |
| Echo's "changed outcome" for each shrine | Design | Undefined results | Hao Guo defines per shrine |
| Shield health and Protection cost | Open values | Balance | Config; Hao Guo |
| Interact also used by the worsen verb (PLAN-023) | Controls | Conflict | Context: nearest shrine wins; owner confirms |

## 7. Deferred follow-ups

Purgatory's revive form (co-op); further shrine types.

## 8. Definition of done

- [ ] All eight shrines work live with tests, placed on the depth curve.
- [ ] Passage bridges a real gap into a collapsing pocket.
- [ ] Shrines never hold the player in place.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
