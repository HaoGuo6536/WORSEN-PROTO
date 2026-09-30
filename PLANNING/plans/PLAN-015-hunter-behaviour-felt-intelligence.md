---
id: PLAN-015
type: plan
title: Hunter behaviour and felt intelligence
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Hunter worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-015 — Hunter behaviour and felt intelligence

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.2](../specs/SPEC-004-horror-direction-content-proposals.md#22-making-the-hunters-scary): the approved behaviour rows, "Felt intelligence" items 1–6, the Director hint change, environment habits, Final IK and the animation note. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Give the kept hunter machinery (goal-oriented action planning (GOAP), fact model, factory, registry, motor and attack configs, light hooks, animation driver) the behaviours every new archetype will use. The player should be able to watch a hunter approach slowly, stalk, deliberate, predict, remember, react to sound and sometimes retreat. Also supply the hooks that archetypes configure: habits, environment habits, the four tunables and rule mutations with tells. Specific hunters are PLAN-016/017.

## 2. Starting point

- [`HunterDefinitions.cs`](../../Assets/Scripts/Domain/Hunter/Definitions/HunterDefinitions.cs) line 22: actions are Patrol, InvestigateHint, Chase, Lunge, SearchLastKnown, CutOff, InvestigateLight, AvoidLight and FlankLight. Stalk, Retreat, Reposition and BreakLoop are absent.
- [`HunterController.cs`](../../Assets/Scripts/Domain/Hunter/Controller/HunterController.cs) lines 145–146: only Patrol uses `PatrolSpeed`; every other action runs at sprint × `ChaseSpeedMultiplier`. Lunge recovery holds the hunter still.
- [`GoapPlannerUtility`](../../Assets/Scripts/Domain/Hunter/Controller/GoapPlannerUtility.cs) (94 lines) plans for one live goal at a time (SPEC-004 survey).
- [`DirectorController`](../../Assets/Scripts/Domain/Director/Controller/DirectorController.cs) delivers delayed historical positions with an uncertainty radius. Relief withholds hints only; there is no retreat request.
- [`HunterAnimationDriver`](../../Assets/Scripts/Domain/Hunter/Driver/HunterAnimationDriver.cs) references RootMotion but only disables root motion and foot IK. Final IK is imported under `Assets/External/Plugins/RootMotion/`.
- Room connections are openings, not doors (PLAN-011 §2), so door habits need PLAN-026 interactables.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Walk while investigating and searching; chase speed only in confirmed pursuit | `HunterController`, `HunterProfile` | — |
| 2 | Stalk: close distance only while unseen; break into chase on sight | GOAP actions, facts, controller | 1 |
| 3 | Retreat on a Director request after long pursuit; silence sometimes means it left and sometimes not | Hunter, Director | 2 |
| 4 | Missed lunge: short stagger with a forward stumble | Controller, attack driver/presenter | — |
| 5 | Four tunables explicit per profile (inertia, commitment, speed ratio, loss rule) | `HunterProfile`, Chase loss rule per archetype | H1 hunter brief |
| 6 | Purposeful patrol: rooms with remaining cakes, the open exit, the last pickup spot | Controller reads Floor view | Floor view via coordinator |
| 7 | Three or four competing goals with changing utility (locate prey, deny route to nearest cake, protect exit, break loop) | `GoapPlannerUtility`, goal definitions | 6 |
| 8 | Visible deliberation: stop, turn head to candidate, vocalise, then move | Controller phase; animation driver; detection cue | Final IK (11) |
| 9 | Prediction: intercept where the player will be; sometimes take the parallel corridor | Existing intercept-room logic | — |
| 10 | Memory: expanding search from last-known, check exit doorway, room beyond, return once; same pattern each loss | Controller search plan | — |
| 11 | Sound reactions: turn first, then decide by loudness and current goal | Hearing path on the shared occlusion model | H1 shared hearing |
| 12 | Director hints as regions and noise events; widen the hint radius with occlusion | `DirectorController`, hint payload | H1 |
| 13 | Habit framework: threshold pause, turn-to-face on belief drop, audible reaction to a nearby cake pickup; per-archetype data | Profile data, controller hooks | 5 |
| 14 | Environment habits as small driver actions: close a door, snuff a torch, BreakLoop, vault a player window, knock a prop, hold a doorway, mark a threshold, react to collapsing rooms | Driver actions on PLAN-026 interactables | H1 world interactables |
| 15 | Final IK: head and spine toward last-known, look-at held through the catch, foot placement on stairs and split levels | Animation driver | — |
| 16 | Stop-motion option: per-archetype animation sample rate | Animation config | — |
| 17 | Rule mutation hook: an archetype rule changed for the rest of the run, with a tell, never announced | Controller/profile override, mutation fact | H1 progression events |
| 18 | First-contact rule: spawn out of line of sight and beyond a minimum path distance from the exit room; spawn-moving effects pass the same check | Rule and validator contract; enforced in generation by PLAN-026 | — |
| 19 | Emergence bias: prefer approaches out of occlusion (doorways, corners, stair heads) | Route presenter bias toward the last occluded waypoint | PLAN-014 done |

**Requests.** Floor view for patrol targets and cake-pickup habits (coordinator and PLAN-019). Interactables (PLAN-026). The catch look-at timing (PLAN-022). The detection vocal slot (PLAN-021).

## 4. Sequence

1. Run GitNexus upstream impact on `HunterController`, `GoapPlannerUtility`, `HunterAction`, `DirectorController` and the hint payload. `HunterController` is central, so expect HIGH risk and warn before editing.
2. **Wave 1:** 1 and 2 (SPEC-004 priority 1).
3. **Wave 2:** 3–13, 15 and 16, each with tests. Items 7–10 are the felt-intelligence set; demonstrate each in the arena before moving on.
4. **Wave 2, after PLAN-014:** 19. **With PLAN-026 interactables:** 14. **With PLAN-023 events:** 17. Item 18 is handed to PLAN-026 as soon as the rule is fixed.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

- `HunterControllerTests`: walk speed in investigate and search; Stalk does not advance while seen and switches to chase on sight; Retreat on request; stagger duration after a miss; the search order repeats identically; sound reaction turns before moving; habit hooks fire on thresholds, belief drop and nearby pickup; mutation override with tell.
- `GoapPlannerUtilityTests`: several live goals; a utility change mid-route produces a visible re-plan; unreachable goals are rejected.
- `DirectorControllerTests`: region hints, noise-event hints, and a radius that grows with occlusion.
- Live arena and HorrorRun: record a hunter deliberating, predicting through a parallel corridor, and repeating its search pattern. Owner playtest judges whether intelligence is felt.
- A fallback symptom to watch: a hunter that sprints to a hint means walk speed is not applied.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Walk speed and Stalk reduce catch rate | Balance | Measured chase statistics shift | Re-measure; owner judges feel over the old target |
| Multi-goal planning cost with many active hunters (no cap) | Performance | Frame time | Budget replans per tick; profile at many hunters |
| Retreat cadence rules | Open value | Pressure | Director config; Hao Guo |
| Minimum first-contact path distance | Open value | Opening pacing | Config; Hao Guo |
| Final IK licence or package status in builds | Vendor | Build failure | Coordinator verifies |

## 7. Deferred follow-ups

Specific archetype behaviours (PLAN-016/017); the progression event cadence (PLAN-023); the catch presentation (PLAN-022).

## 8. Definition of done

- [ ] Items 1–19 pass their tests, and live evidence exists for 1–4, 7–11, 14 and 15.
- [ ] Every profile declares the four tunables; the Chase loss rule reads per-archetype values.
- [ ] Director emits region and noise hints and can request a retreat.
- [ ] Owner playtest notes confirm the hunter reads as deciding, predicting and remembering.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
