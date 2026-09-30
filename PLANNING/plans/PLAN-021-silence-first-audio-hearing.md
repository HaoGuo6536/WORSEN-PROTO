---
id: PLAN-021
type: plan
title: Silence-first audio and shared hearing
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Audio worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001, SPEC-005]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-021 — Silence-first audio and shared hearing

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004 §2.6](../specs/SPEC-004-horror-direction-content-proposals.md#26-presentation) audio rows, "Minimal audio, by decision" and "Shared hearing rule"; the §2.3 rows for the clean all-clear, occluded player noise (mix side) and false positives; the §2.5 combo-sting row; and the §2.2 per-hunter sound-identity row (slots and mix). Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Make silence the default and sound the main presence channel:

- A small, trusted cue budget with variation, direction and distance readable from the mix alone through a head-related transfer function (HRTF) spatializer and occlusion.
- Music that enters only when a hunter holds a belief, and never resolves to a clean all-clear.
- Hunters and the player hear the same world through the same occlusion model, so "if you heard it, assume they did" is literally true.

## 2. Starting point

- [`CueId`](../../Assets/Scripts/Core/Definitions/CueId.cs) has 67 identifiers, including surface footsteps, posture rustle, sprint exertion, the `CakeChain` combo sting and separate windup, swing, miss, hit and recovery cues. SPEC-004 lists `PlayerCritical`, `GrabHit`, `Consumed`, `WindLoop`, `DoorOpen`, `Restart` and `Chase` as never played.
- [`AudioChaseMusicPresenter`](../../Assets/Scripts/Presentation/Audio/Driver/AudioChaseMusicPresenter.cs) mixes an escalation layer and a danger layer from threat facts. SPEC-004 reports every stem at zero when no threat is near, plus a distinct chase gain.
- Sources are plain Unity 3D with linear rolloff. `ProjectSettings/AudioManager.asset` has no spatializer plugin. Occlusion exists as `SetEmitterOcclusion` in [`AudioSoundscapeDriver`](../../Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapeDriver.cs) and is used only for curse noises.
- An interior ambience bed and proximity tension play during the sweep. Breathing plays only below a quarter health. Mixer groups are unassigned.

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Music: a low tension floor that never resolves; the danger layer fades out on a randomised delay after loss; occasionally it fades while the hunter is still close; music enters only when a hunter holds a belief | `AudioChaseMusicPresenter`, driver state, config | Belief fact via coordinator |
| 2 | Silence default: drop the ambience bed to near silence during the sweep | Soundscape and horror ambience config | — |
| 3 | HRTF spatializer plugin selected and installed | `Packages/**`, `AudioManager.asset` (coordinator, under lease) | Licence check |
| 4 | Mix occlusion from the shared model for hunter presence, footsteps and every world sound | Audio world presenter; Core occlusion utility | H1 shared hearing |
| 5 | Cue budget: player 3 (footsteps, breathing, traversal contact); per hunter 5 (presence, detection, chase layer, attack timing, death sting); a small world set (pickup, exit door, room telegraph, torch gutter, a door a hunter closed); interface cues outside the run only. Everything else is removed from the mix, not turned down | Cue catalogue (Core contract), definitions, feedback presenter | H1 cue budget |
| 6 | One voice per category at a time; hunter presence and attack timing never ducked | Mix presenter | 5 |
| 7 | Variation: small random pitch, volume and timing jitter, and two or three alternates; timing fixed where timing is a tell | Sound definition, driver | 5 |
| 8 | Per-archetype presence loops audible through walls with occlusion | World presenter; per-archetype assets from PLAN-016/017 | 4, 5 |
| 9 | Remove the `CakeChain` combo sting (or replace with a distant hunter reaction) | Feedback presenter | — |
| 10 | False positives: a rare distant footstep or door with no hunter, rate-limited | New presenter state, injected randomness | 4 |
| 11 | Unplayed cues: wire `DoorOpen` to the physical exit door first; audit the rest against the budget | Floor exit fact routing | 5 |
| 12 | Embodiment: the player's breathing under pursuit; a heartbeat that scales with proximity and slightly masks the mix; a heartbeat spike on the grace window | Presenter, mix | PLAN-013 grace fact |
| 13 | Mix hooks: deafening (Herald), Muffled Dark (Blinder), Silent Presence curse, Keen Ears upgrade, collapse pulse | Mix presenter reads the active-effects view | H1; PLAN-017/023/024 |
| 14 | Mixer groups: master, music and effects, for pause-menu volumes | Mixer asset, driver | — |
| 15 | Parity check: every world cue maps to a noise source kind, so nothing environmental plays without a hunter-audible event | Test fixture over the cue catalogue | H1 shared hearing |

Emitting noise events is each gameplay owner's job (PLAN-013 player, PLAN-019 pickups, PLAN-015/016/017 hunters, PLAN-018 grabs). This plan owns the mix and the parity check.

Amended 2026-09-30: §1's “if you heard it” promise, C15's all-world parity and the pickup/grab emitter obligation above are superseded. Shared acoustic attenuation remains; ordinary hunter hearing accepts player movement, Firecracker and player-triggered cake traps only, never Pacification, world or false-positive audio. Explicit hunter-rule broadcasts use typed gameplay routing, not playback-derived sensing. Test positive allowed origins and negative ingress through Session, Hunter and Director; cue metadata alone is not proof.

Amended 2026-09-30: [SPEC-005](../specs/SPEC-005-hunter-briefs.md) supplies the approved roster sound rules. Mannequin catch uses a short snap/crunch rather than the loud shared sting. PLAN-012 verifies detection/presence, held catch and telegraph; no distinct clean Lose/all-clear cue. Coordinator chooses installed-pack/in-house clips with provenance. C3's installation prerequisite is superseded by Steam Audio 4.8.1 imported (`947395e`); spatializer/mixer routing and live headphone acceptance remain, not another download request.

## 4. Sequence

1. Run GitNexus upstream impact on `CueId` consumers (expect HIGH), `AudioChaseMusicPresenter`, `AudioFeedbackPresenter`, `AudioWorldPresenter` and `AudioOrchestrator`.
2. **Wave 1:** 1 (priority 3), and per-archetype presence through occlusion using the existing mix (priority 1).
3. **Wave 2:** 3, then 4–9, 11, 12 and 14. The cue catalogue shrink is coordinated at H1 because Core consumers change together.
4. **Wave 3:** 10, 13 and 15.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

Amended 2026-09-30: the parity test below and §8 E3 are superseded by the origin-admission/negative-ingress tests in amended C15. §5/§8 evidence names exact integrated hash, setup version, fresh result counts, build seed and owner date. Headless tests/old logs cannot establish rendering, physics, input focus, audible tells, spatialization or owner feel.

- Presenter tests: the music floor never reaches zero; the fade delay is randomised within bounds using injected randomness; entry happens only with a belief; one voice per category; presence never ducked; jitter disabled for tell cues.
- Parity test: each world cue has a noise source kind; interface and music have none.
- Live HorrorRun recordings: silence during the sweep; presence heard through a wall and locatable with eyes closed (owner check with headphones); a loss that does not resolve.
- MOSS-Audio (see `CLAUDE.md`) may describe captured clips, but model output does not establish in-game mix quality; owner listening does.
- A fallback symptom to watch: a cue that still plays from a removed catalogue entry means an old path is live.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Spatializer choice and licence | Vendor | Package change | Coordinator evaluates under lease |
| Does a false-positive sound also reach hunters? | Spec gap (shared hearing rule) | Trust and AI | Decided 2026-09-30 (owner): no; world noises without a player source do not reach hunters either |
| Cue removal breaks boilerplate tests and PLAN-007 evidence | Regression | Test churn | Migrate tests; PLAN-011 §3.4 note |
| Legibility with many active hunters | Mix | Noise | Priority rules; owner playtest |

Amended 2026-09-30: the false-positive question is closed (no), as is Pacification (not heard); Firecracker and player-triggered cake traps are heard. World sounds never become hunter stimuli merely because they play. Steam Audio import is complete, but configuration and listening evidence are pending; no licence/download decision is reopened here.

## 7. Deferred follow-ups

Surface-specific footsteps, only once a surface matters to a hunter's hearing; adaptive music beyond the danger and tension layers.

## 8. Definition of done

- [ ] The music floor never resolves and enters only with a belief, in tests and live.
- [ ] The HRTF spatializer and shared occlusion are live in HorrorRun.
- [ ] The cue catalogue matches the budget; variation and priority rules hold; the parity test passes.
- [ ] Breathing, heartbeat, false positives and effect hooks work live.
- [ ] Owner headphone review accepts direction, distance and silence.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | Music | Music floor kept, release randomised, sweep silenced. Live proximity/belief facts and run-end reset remain | Commit 87ebdd9 |
