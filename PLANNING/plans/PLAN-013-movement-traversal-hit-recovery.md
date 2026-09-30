---
id: PLAN-013
type: plan
title: Movement, traversal and hit recovery
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Player worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-013 — Movement, traversal and hit recovery

> Status: DRAFT since 2026-09-30. Implements [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.9 rows 1–6 and its on-hit boost and regeneration rule, the §2.4 grace window, §2.8 items 1–2, §2.6 fail forward and hands, and the §2.3 look-back and crouch decisions. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Not approved for execution while SPEC-004 is DRAFT. Direction documentation is not present.

## 1. Objective

Make the base movement kit stop fighting the player, and make a hit a chance to break away rather than the start of a chain. Movement outcomes: a mantle that keeps mouse look, a slide that bends, ramps that do not jitter, ledges that catch a missed jump, a timed vault boost, air control from a standing jump, and a mistake that stays part of the chase. Hit outcomes: a grace window with pass-through, a boost scaled by severity, and health that is full at each floor start and slowly regenerates. Also a look-back that snaps rather than scans.

## 2. Starting point

- [`PlayerController.cs`](../../Assets/Scripts/Domain/Player/Controller/PlayerController.cs) (485 lines). Look-back freezes body heading and passes free head look (around lines 326–330); `PlayerProfile._lookBackSteerAuthority` is documented as unused. Vault and mantle are scripted moves with `VaultDuration`/`MantleDuration` and `InputLockSeconds` (around lines 150–225). Air steering is clamped to current horizontal speed (around line 359), so a standing jump has none.
- [`PlayerProfile.cs`](../../Assets/Scripts/Domain/Player/Config/PlayerProfile.cs): `_slideLateralAcceleration = 5`, mantle 0.35 s. SPEC-004 reports slide speed lost to a collision is never recovered.
- [`PlayerDriver.cs`](../../Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs) resolves `ITraversalSurface` on the hit collider (around lines 72–80). Untagged edges never vault, mantle or rebound.
- Health: lunges deal half of full health and hands a quarter, with no grace. Health carries between rounds through [`ProgressionSessionController.RecordHealth`](../../Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs). Hunter capsules collide with the player.
- The Stumble state exists with no effect; a failed vault only emits a fact. [`PlayerLimbStandIn`](../../Assets/Scripts/Domain/Player/Driver/PlayerLimbStandIn.cs) hides hands and feet in normal motion (SPEC-004 §2.6).
- LIVE [PLAN-003](PLAN-003-player-movement-health.md) holds measured evidence (free-speed 90th percentile, input lock ≤0.35 s, replay determinism). This plan changes rules that evidence covers (PLAN-011 §3.4).

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Hit grace: on any hit, no further damage and hunter pass-through for a configured window; publish grace start/end | Player Controller/State/Driver; physics layer swap | H1 hit and grace contract |
| 2 | On-hit boost scaled by severity: light (scream, hand) small and brief, heavy (lunge) larger and longer | Player Controller, Profile | 1 |
| 3 | Full health at floor start, slow regeneration within the floor; carry-over removed | Player Controller; Session request | H1; PLAN-023 removes carry-over |
| 4 | Curse and upgrade hooks: Short Grace, Thick Skin, Short Burst, Heavy Legs, Long Boost, Slow Mend, No Regen, Field Kit, Rough Start, Thin Skin, Speed Boost, Quick Start, Air Control, Fast Hands, Longer Slide, Higher Jump, Soft Landing, Stored Momentum, Quiet Slide, Low Profile | Player reads the active-effects view | H1 effect identity; PLAN-023/024 catalogues |
| 5 | Look-back snap: a held snap state, steering continues on body heading, no head scanning; disabled by No Look-Back | Player Controller; Input unchanged | Camera side in PLAN-022 |
| 6 | Physics-driven mantle and vault: live mouse look, steering in the last third, cancel into a jump; publish progress for the camera curve; lock ceiling ≤1 s (GDD) | Controller, Driver, Presenter | PLAN-022 camera curve |
| 7 | Slide: raise turn authority, keep the speed cap, redirect along a wall instead of stopping | Controller, Profile | — |
| 8 | Stairs, player side: raise the step offset, widen landing collision | Driver, `PlayerMoverDriverConfig` | Ramp colliders in PLAN-026 |
| 9 | Ledge climb on any collider edge in reach while airborne; tagged surfaces still drive authored routes | Driver probe, Controller state | — |
| 10 | Vault and ledge boost: a timed jump press inside a short window grants a forward burst | Controller | 6, 9 |
| 11 | Air-control floor for a standing jump | Controller, Profile | — |
| 12 | Fail forward: a bad vault stumbles and briefly cuts speed; a missed gap drops the player to a lower route instead of sliding off | Controller, Driver | 6, 9; camera in PLAN-022 |
| 13 | Show hands in play | `PlayerLimbStandIn` | Art placeholder allowed |
| 14 | Crouch: confirm it has no speed or loudness modifier and remains a posture for slides and low gaps | Profile, Controller | — |
| 15 | Noise emitters per the shared hearing contract: footsteps, landing, slide, vault and rebound carry a source kind | Controller, State | H1 shared hearing |

**Requests to other owners.** To the coordinator: the grace physics layer and collision matrix, hit severity in the Session hit routing, and Session health carry-over removal. To PLAN-022: snap camera, vault curve, landing dip, stumble shake and grace desaturation. To PLAN-021: breathing and grace heartbeat spike. To PLAN-026: single ramp colliders under stepped visuals.

## 4. Sequence

1. Run GitNexus upstream impact on `PlayerController`, `PlayerDriver`, `PlayerProfile` and `PlayerLimbStandIn`, and on the Session health path. Record the risk level.
2. **Wave 1:** changes 1, 2 and 5 (grace, boost, look-back rules). Hand grace facts to PLAN-022/021.
3. **Wave 2:** 3, 11, 14 and 15, then 6, 7, 8, 9, 10, 12 and 13, one commit each with tests.
4. **Wave 3:** 4, as the PLAN-023/024 catalogues publish identifiers.
5. Re-run the PLAN-003 measurement set and report changes against its historical values.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- `PlayerControllerTests` and `PlayerMoverPresenterTests`: grace boundaries (a hit at window end), no damage inside grace, boost magnitude and duration per severity, regeneration rate and floor reset, snap state with heading steering, air-control floor at zero speed, boost window edges, stumble and speed cut, crouch with no loudness change, and noise source kinds.
- Replay determinism: recorded `InputFrame` and `MovementProbe` still reproduce identical trajectories with the new mantle.
- Live HorrorRun checks: after a lunge, the player passes through the hunter and escapes; stairs climb without jitter; the mouse looks during a vault; a slide bends around a corner; an untagged ledge catches a missed jump; a timed boost fires; a missed gap lands on a lower route.
- A fallback symptom to watch: the player blocked by a hunter during grace means the layer swap did not apply.
- Owner playtest judges vault and slide feel.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Grace, boost and regeneration numbers | Open value | Balance | Tune in arena; SPEC-004 §5; Hao Guo |
| Mantle change invalidates PLAN-003 measurements | Evidence | Reopened acceptance | Re-measure; keep history |
| Ledge climb on any edge exposes geometry exploits | Design | Sequence breaks | PLAN-026 validation, level blockers |
| Boost stacking with Speed Boost | Balance | Outrunning hunters | Cap below chase speed (SPEC-004 §2.13) |
| What "a missed gap changes elevation" requires of geometry | Design | Needs lower routes | PLAN-026 supplies them |

## 7. Deferred follow-ups

Camera presentation of these rules (PLAN-022); per-surface footsteps (removed by the PLAN-021 budget); extra movement upgrades beyond the §2.13 catalogue.

## 8. Definition of done

- [ ] Grace, pass-through, boost and regeneration work in tests and live against a real hunter and hand.
- [ ] Look-back snap rules hold and the No Look-Back hook removes the snap.
- [ ] Mantle, slide, step offset, ledge climb, boost, air control and fail forward pass tests and live checks.
- [ ] Hands are visible; crouch has no stealth effect.
- [ ] PLAN-003 measurements re-run, with changes reported and history kept.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
