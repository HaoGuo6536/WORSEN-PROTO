---
id: PLAN-019
type: plan
title: Cakes, the white arrow and cake traps
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Floor worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-019 — Cakes, the white arrow and cake traps

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.5 cake rows (density, creation horror, golden count data), §2.3 and §2.18 arrow rows (accuracy and guidance data), and the §2.11 cake-trap row. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Make each cake a destination rather than a pellet. The white arrow should point correctly and be the only way to tell a real cake from a trap or Mimic.

- Cakes: one to three per room on the unused anchor types, with the required count a subset so some cakes are optional risk. Cakes read as the one saturated, candle-lit object someone baked.
- Arrow: always accurate, holds the last good direction, and publishes typed guidance targets that later threats and upgrades reuse.
- Traps: from about floor three, some cake spawns become traps the arrow ignores.

## 2. Starting point

- [`ProceduralController.cs`](../../Assets/Scripts/Domain/Procedural/Controller/ProceduralController.cs) lines 183–194 emit two lines of `CakesPerLine = 5` anchors per room, all `CakeAnchorType.Flow`. [`FloorController.Weight`](../../Assets/Scripts/Domain/Floor/Controller/FloorController.cs) (lines 244–252) already weights Precision, Detour, Risk and Vertical from [`FloorConfig`](../../Assets/Scripts/Domain/Floor/Config/FloorConfig.cs), but those anchors never exist. Every cake is required.
- Arrow: [`FloorDriver`](../../Assets/Scripts/Domain/Floor/Driver/FloorDriver.cs) samples the NavMesh and computes a path (lines 121–125). [`FloorPresenter`](../../Assets/Scripts/Domain/Floor/Driver/FloorPresenter.cs) (line 33) takes the first corner that differs, measured from the raw position. The first corner is the sample point, so the arrow points at the projection when the player is airborne, on stairs or off-mesh (SPEC-004 §2.18). A failed sample or path yields no direction.
- [`HUDCompassPresenter`](../../Assets/Scripts/Presentation/HUD/Driver/HUDCompassPresenter.cs) draws a 3D needle that hides during chases; drawing moves to PLAN-020.
- Cakes use primitive fallback visuals; [`FloorLumenGlow`](../../Assets/Scripts/Domain/Floor/Driver/FloorLumenGlow.cs) exists. The golden count is in Floor state but never shown. Pickups are silent to hunters unless the Gilded Hunger curse is active.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Arrow fix: measure from the sample point, skip corners within about 1 m, fall back to straight-line direction when sampling or pathing fails (flagged as fallback), hold the last good direction across a failed refresh | `FloorPresenter`, `FloorDriver`, state | — |
| 2 | Typed guidance targets per the H1 contract; the white arrow is published during chases too | Floor state, Core guidance payload (coordinator) | H1 |
| 3 | Density: one to three cakes per room on typed anchors; required count is a configured subset; optional cakes are risk | `FloorController`, `FloorConfig`; typed anchors requested from PLAN-026 | PLAN-026 wave 1 |
| 4 | Pickup noise: every pickup emits a noise event (shared hearing); Gilded Hunger's special case retires with the placeholder curses | `CakePickup`, `FloorController` | H1; PLAN-023 |
| 5 | Creation-horror cakes: lit candle, the same name piped on every cake, saturated against a desaturated world; stacked Lumen glow and a small light pool | Cake prefab, `FloorLumenGlow` | PLAN-022 lighting grammar |
| 6 | Golden count published for display | Floor display payload | PLAN-020 draws |
| 7 | Cake traps from a configured floor: some cake spawns replaced by traps (blind, slow, or announce through shared hearing); never a white-arrow target; Sweet Tooth turns one per floor into a cake | Trap driver in Floor, config | 2, 4; PLAN-022 blind; PLAN-013 slow |
| 8 | Requests applied for other plans: exit room never collapses (018), early bail (023), cake snatch (018), Greedy Door (023), Blind Faith and Golden Sense (024), Mimic exclusion (017), Ticking and collector threat arrows (017), micro-event counter lie (022) | `FloorController` and guidance | Their plans |

## 4. Sequence

1. Run GitNexus upstream impact on `FloorPresenter`, `FloorDriver`, `FloorController`, `CakePickup` and the HUD compass consumers.
2. **Wave 1:** change 1 (playtest unblocker), with the regression test first. Then change 3 as soon as PLAN-026 provides typed anchors (SPEC-004 priority 2).
3. **Wave 2:** 2, 4, 5 and 6.
4. **Wave 3:** 7, and the change 8 requests as the other plans arrive.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- The SPEC-004 test: origin 1 m above the mesh, and the direction points along the path, not down at the sample. Also: airborne, on stairs, off-mesh, failed sample (fallback flagged), and a failed refresh (last good direction held).
- Density tests: one to three anchors per room; typed anchors used; required subset reachable; optional cakes excluded from exit gating.
- Guidance tests: white-arrow targets never include traps or Mimics unless a named curse is active; the arrow is present during a chase.
- Live HorrorRun: the arrow stays correct on stairs and mid-jump; a floor shows few, placed cakes; a trap springs and the arrow ignored it. Owner playtest judges the arrow-as-trust read.
- A fallback symptom to watch: frequent fallback flags in telemetry mean the path fix is not working.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Name piped on the cakes | Open design | Lore | Hao Guo |
| Required-subset size and trap floor | Open value | Pacing | Config; Hao Guo |
| Golden count on the HUD (PLAN-011 §6 item 6) | Spec conflict | Display | Hao Guo |
| Many requesters edit `FloorController` | Ownership | Merge conflicts | This plan applies all requests, one at a time |

## 7. Deferred follow-ups

Theme-specific cake looks (PLAN-026); the HUD arrow and count drawing (PLAN-020).

## 8. Definition of done

- [ ] Arrow regression tests pass and live evidence shows a correct arrow on stairs, mid-jump and in chases.
- [ ] Floors generate one to three cakes per room on typed anchors with a required subset.
- [ ] Pickups are noise events; cakes read as creation horror in a screenshot review.
- [ ] Traps spawn from the configured floor and are never white-arrow targets.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
