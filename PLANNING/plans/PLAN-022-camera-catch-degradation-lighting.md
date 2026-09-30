---
id: PLAN-022
type: plan
title: Camera feel, the catch, degradation and lighting
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Presentation worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-022 — Camera feel, the catch, degradation and lighting

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements the [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.2 catch row; §2.3 look-back (camera side); §2.6 embodiment (landing dip), degradation layer, startle budget and fail forward (camera side); the §2.5 micro-events row; and the §2.18 lighting rows. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

- **Camera:** a look-back that snaps, a vault that feels like a body, a landing that dips, a stumble that shows a mistake.
- **The catch:** the loudest and ugliest moment in the game.
- **Image:** a constant degradation layer that makes store and custom assets one plausible image.
- **Startles:** only one or two per run, and earned.
- **Lighting:** Lumen supplies only the light itself, on the few things that matter, in a dark real-material world.

## 2. Starting point

- [`CameraDriverConfig`](../../Assets/Scripts/Presentation/Camera/Config/CameraDriverConfig.cs) has `_lookBackYaw = 160` and `_lookBackSeconds = 0.12` (SPEC-004 calls them unused), driven by `CameraDriver.SetLookBack`.
- [`CameraFeedbackPresenter`](../../Assets/Scripts/Presentation/Camera/Driver/CameraFeedbackPresenter.cs) snaps to the killer and fades; consumption by hands takes precedence with a short fade. There is no landing dip.
- [`PostFXDriver`](../../Assets/Scripts/Presentation/PostFX/Driver/PostFXDriver.cs) applies distortion, vignette, desaturation and grain. Grain appears only during the intrusion (`PostFXDriverConfig._intrusionGrain`).
- The intrusion fires whenever the player is slow (Director intrusion episodes); there are no other startles.
- Lumen 2 is used as fake-light flares budgeted to the twelve nearest ([`HorrorLumenPresenter`](../../Assets/Scripts/Presentation/Horror/Driver/HorrorLumenPresenter.cs)). Ambient light is grey-green with black fog from 8 m.
- LIVE [PLAN-006](../archive/plans/PLAN-006-camera-postfx-feedback.md) defines the current death snap and look-back camera (PLAN-011 §3.4).

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Look-back snap: the press turns the view fully behind in one fixed frame, release snaps forward, no scanning | `CameraDriver`, `CameraDriverConfig` | PLAN-013 snap state |
| 2 | The catch: close-up on the killer's animation held for a beat, look-at held by Final IK, one hard sting, hard cut to the run summary | `CameraFeedbackPresenter`; requests to PLAN-015 (look-at), PLAN-021 (sting), PLAN-020 (cut) | H1 catch fact |
| 3 | Vault camera: authored curve with a small dip and rise, mouse look live | Camera presenter | PLAN-013 vault progress |
| 4 | Landing dip; fail-forward camera stumble | Camera presenter | PLAN-013 facts |
| 5 | Grace marker: short desaturation on the grace window | PostFX presenter | PLAN-013 grace fact |
| 6 | Degradation layer: constant low grain, slight chromatic aberration and a diegetic frame | PostFX config/driver; frame art | Art decision (§6) |
| 7 | Startle budget: one or two earned startles per run from a broken expectation, plus the catch; the intrusion becomes one budgeted source | Horror presentation state; Director intrusion request | PLAN-015 Director |
| 8 | Unverifiable micro-events: a door left open is now closed; a silhouette where it cannot path; the counter showing a cake that is gone; very low per-run chance | Horror presentation scheduler with injected randomness; requests to PLAN-019 and PLAN-026 | Interactables |
| 9 | Lighting grammar with Lumen: stacked glow and a small pool on the cake; fanned rays through the exit frame that intensify as it opens; lantern halos and soft ground pools on torches; a faint cold glow only where fog is thin; a low toon rim where a silhouette must read at look-back speed; everything else dark | `HorrorLumenPresenter`, environment lighting config; cake glow with PLAN-019; fog glow with PLAN-018 | — |
| 10 | Per-theme light sources on the same grammar (torches, fluorescents, lanterns or moonlight) | Lighting config per theme | PLAN-026 themes |
| 11 | Effect hooks: blindness (Blinder), Darker Floors, Cat Eyes, Wick shrine lamp state | PostFX and lighting read the active-effects view | H1; PLAN-017/023/024/025 |

## 4. Sequence

Amended 2026-09-30: C2's uniform loud-sting/hunter-only interpretation is superseded. Mannequin gets a short snap/crunch; hand death is also held: a fog-coloured hand emerges slowly, then quickly grabs the face, followed by the correct hand sting and hard cut (`15e32bd`). Extra Life intercepts terminal catch once, reviving in place with collision grace and damage immunity. Final inverse kinematics (IK) must work on shipped rigs, or an explicit owner-approved equivalent must be recorded; humanoid Animator IK and generic no-op are not Final IK acceptance. Vendor asmdef permission is approved; implementation remains in PLAN-027.

Amended 2026-09-30: C6's unspecified frame is superseded by old camcorder treatment: rounded vignette, soft edge blur, degradation-driven tape wobble, no text. Install the renderer feature and build-safe material, then measure its budget (`15e32bd`); source/shader presence alone does not prove installation.

1. Run GitNexus upstream impact on `CameraDriver`, `CameraFeedbackPresenter`, `PostFXDriver`, `PostFXPresenter`, `HorrorLumenPresenter` and the intrusion routing in `PostFXOrchestrator`.
2. **Wave 1:** 1 (priority 4) and 2 (priority 5).
3. **Wave 2:** 3, 4, 5, 6, 7 and 9.
4. **Wave 3:** 8, 10 and 11.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

Amended 2026-09-30: §8 E2 includes hand catch and the Mannequin/Extra Life exceptions in amended C2. §5/§8 evidence names exact integrated hash, setup version, fresh result counts, build seed and owner date. Headless tests/old logs cannot establish rendering, physics, input focus, audio or owner feel.

- Camera presenter tests: the snap completes in one fixed frame with no yaw scanning during hold; the catch sequence order is hold, then sting, then cut, with a declared hold duration; the vault curve is sampled by progress; landing dip scales with impact.
- Startle budget test: never more than the configured count per run from budgeted sources (injected randomness, seeded).
- Frame-time measurement for the degradation layer and lighting on the target machine against a declared budget.
- Live HorrorRun captures of each item; owner review of the catch and lighting. The reference image is a catalogue of options, not a target.
- A fallback symptom to watch: a fade before the cut means the old death path still runs.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| What the "diegetic frame" is | Art decision | Degradation look | Decided 2026-09-30 (owner): an old camcorder: rounded-corner vignette, soft edge blur, tape wobble when degraded, no text |
| Does a hand death use the held catch? | Spec gap | Death presentation | Decided 2026-09-30 (owner): yes, as a close-up: a hand reaches slowly out of the fog, then quickly grabs the face |
| The snap may cause discomfort | Comfort | Players | Owner comfort check; a setting if needed (PLAN-020) |
| Lumen cost with many light layers | Performance | Frame time | Budget and cull |

## 7. Deferred follow-ups

Theme art beyond light sources (PLAN-026); per-hunter catch animations (PLAN-016/017 art).

## 8. Definition of done

- [ ] Snap, vault curve, landing dip and stumble work live.
- [ ] The held catch replaces the snap and fade for every hunter death.
- [ ] Degradation layer and lighting grammar meet their frame budget and pass owner review.
- [ ] The startle budget and micro-events work within their configured chances.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Look-back and catch | Look-back snap, catch close-up hold, results gated on the hold, single catch sting and scene camera binding. Degradation layer, startle budget, Lumen grammar and their routing accepted on worker branches | Commits 5b90c96, 6d3bdf8, a2632db; branches wt/plan022-look 02730f6, wt/plan022-lookwire 521f4a3 |
