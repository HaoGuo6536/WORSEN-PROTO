---
id: PLAN-020
type: plan
title: In-run interface, title, pause and results
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Interface worker (assignee UNKNOWN — owner input needed)
specs: [SPEC-004, SPEC-001]
direction_ids: n/a
supersedes: none
superseded_by: none
source: none
evidence: none
archived: none
---

# PLAN-020 — In-run interface, title, pause and results

> Status: LIVE since 2026-09-30 (approved by Hao Guo). Implements [SPEC-004](../specs/SPEC-004-horror-direction-content-proposals.md) §2.7; the §2.3 rows for the HUNTED label, hidden health and hidden count; §2.18 arrow presentation; the §2.5 golden-count and results rows; the §2.6 headphones and pause rows; and the §2.8 hint-line and empty-slot items. Coordinated by [PLAN-011](PLAN-011-horror-direction-coordination.md). See the [registry](../index.md). Direction documentation is not present.

## 1. Objective

Reduce the in-run screen to one white arrow, a quiet cake count and the consumable slots (once consumables exist), with no text that reports chase state or health. Add a title screen that asks for headphones, a pause menu that exposes the settings playtesters need, and a HorrorRun results screen that says how the run ended and lets a seed be replayed.

## 2. Starting point

- [`HUDVisualDriver.cs`](../../Assets/Scripts/Presentation/HUD/Driver/HUDVisualDriver.cs) builds:
  - a titled top-right panel with cake count and gauge, exit LOCKED or OPEN, and a `HUNTED` warning (line 78);
  - an extra layer with a 3D compass ([`HUDCompassPresenter`](../../Assets/Scripts/Presentation/HUD/Driver/HUDCompassPresenter.cs)) and an inventory panel of empty slots (line 97);
  - a hard-coded controls hint (line 107).
- The Player inventory is always two empty strings ([`PlayerController`](../../Assets/Scripts/Domain/Player/Controller/PlayerController.cs) line 81).
- HorrorRun shows health as a number and bar ([`ProgressionUIPresenter`](../../Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIPresenter.cs) lines 64–65). The shelter lists every retained threat.
- There is no title screen and no pause or quit. Sensitivity, invert Y, field of view, tilt, punch and reacquire blur are locked in designer configs. Mixer groups are unassigned (PLAN-021 assigns them). No persistence exists.
- HorrorRun ends on the shelter screen with round and wallet only; [`ResultsDriver`](../../Assets/Scripts/Presentation/Results/Driver/ResultsDriver.cs) serves the boilerplate scenes.
- LIVE [PLAN-007](../archive/plans/PLAN-007-audio-hud-results.md) defines the current HUD (PLAN-011 §3.4).

## 3. Changes

| # | Change | Files / symbols | Depends on |
|---|---|---|---|
| 1 | Remove the `HUNTED` label; hide HUD chrome during a chase, with the arrow exempt pending PLAN-011 §6 item 1 | `HUDVisualDriver`, `HUDPresenter` | — |
| 2 | Hide health during the run; show the number only in the shelter | `ProgressionUIPresenter`, visual driver | — |
| 3 | One white arrow replaces the compass needle, always drawn, including in chases | `HUDCompassPresenter` (rename via GitNexus), HUD config | PLAN-019 guidance |
| 4 | Declutter: no titled panel, chrome or hint line; a quiet cake count; golden count per decision; slots hidden until consumables exist | HUD driver, config | PLAN-011 §6 item 6; PLAN-024 |
| 5 | Threat-arrow state and second arrows (Ticking keys, collector threats, Golden Sense, Exit Sense) drawn in the same language | HUD | PLAN-019 targets |
| 6 | Hidden Count curse: the shelter hides which retained hunters are active | Shelter presenter | PLAN-023 |
| 7 | Title screen: "wear headphones", then the run; becomes build index 0 | New Presentation menu system, scene or overlay; build settings (coordinator) | PLAN-012 item 4 |
| 8 | Pause menu: sensitivity, invert Y, field of view, tilt, punch, reacquire blur, master/music/effects volume, resume and quit; settings saved | New menu system; Input pause binding; config overrides at runtime | PLAN-021 mixer groups; H1 persistence |
| 9 | HorrorRun results: cause of death and killer, chases escaped, grabs escaped, time from exit open to escape, the seed with a fixed-seed field for the next run, and persisted best depth | Results presenter/driver; `RunSummary` fields (coordinator) | H1 persistence; PLAN-022 hard cut |
| 10 | Remove the Interact hint text | HUD | PLAN-023/025 decide Interact |

Settings changes must not write to shared designer assets at runtime. They apply as runtime overrides from the persistence store (SPEC-001 configuration separated from state).

Amended 2026-09-30: C4 uses quiet normal and golden collected/total counters fixed at generation; collapse never shrinks totals, and Passage/puzzle reservations are accounted for exactly once. C6's shelter redaction is superseded: Hidden Count hides in-floor cake counters, while the shelter roster remains readable and all retained hunters stay active (`8d59c61`, `58ac976`). C5 collector content is declined. C7 means a title-capable HorrorRun first in the deterministic build list, not a separately required title scene. Shrine Interact is owned by PLAN-025.

## 4. Sequence

1. Run GitNexus upstream impact on `HUDVisualDriver`, `HUDPresenter`, `HUDCompassPresenter`, `ProgressionUIPresenter`, `ResultsDriver`, `RunSummary` and `CameraDriverConfig` consumers.
2. **Wave 1:** 1 and 2 (SPEC-004 priority 3), then 3 once PLAN-019's arrow fix lands.
3. **Wave 2:** 4, 7, 8 and 10. Pause and settings is "the most useful single addition for playtesting", so schedule it early in the wave.
4. **Wave 3:** 5, 6 and 9.

## 5. Verification

Apply the common gates in [PLAN-011 §5](PLAN-011-horror-direction-coordination.md#5-verification).

Amended 2026-09-30: §5/§8 evidence names exact integrated hash, setup version, fresh result counts, build seed and owner date. Headless tests/old logs do not establish rendering, physics, input focus, audio or owner feel; verify Hidden Count in-floor and a readable shelter roster.

- HUD presenter tests: no chase text; chrome hidden in a chase while the arrow stays; no health in the run; slots hidden with zero consumables.
- Settings tests: overrides persist, reload and apply without modifying config assets (compare asset hashes before and after).
- Results tests: every field is present for death by hunter, death by hand and escape; a fixed seed reproduces the PLAN-026 layout manifest.
- Live HorrorRun screenshots at sweep, chase, shelter and results; title-to-run flow in a build; pause during a chase.
- Owner review of clutter.

## 6. Risks and open questions

| Item | Type | Impact | Mitigation / owner |
|---|---|---|---|
| Arrow during chases (PLAN-011 §6 item 1) | Spec conflict | Chase HUD | Hao Guo |
| Golden count display (item 6) | Spec conflict | Layout | Hao Guo |
| Shelter and shop screen redesign | Scope | SPEC-004 calls it a separate pass | Shop screen in PLAN-024; shelter redesign deferred |
| Pause during a run with simulation ticking | Technical | Determinism of replays | Pause through the Session tick owner, not time scale alone |

## 7. Deferred follow-ups

Full shelter redesign; accessibility options beyond the listed settings; key rebinding.

Amended 2026-09-30: the blanket key-rebinding deferral above has one conditional exception: if run-scoped option A in SPEC-006 is approved, provide its dedicated rebindable action and exact preview, never overload shrine Interact. SPEC-006 remains DRAFT/not approved. No speculative persistent-hell/P1 shelter interface is authorised; persistent hell is dropped.

## 8. Definition of done

- [ ] The in-run screen shows only the arrow, the count and populated slots; no chase text and no health.
- [ ] The title screen asks for headphones and boots from a build.
- [ ] The pause menu exposes every listed setting, persists them and never edits config assets.
- [ ] HorrorRun results show every listed field and a fixed-seed replay works.
- [ ] Owner review accepts the declutter.

## 9. Execution log

| Date | Step | Result | Evidence |
|---|---|---|---|
| 2026-09-30 | HUD and menus | Chase text removed; HUD chrome hidden in chases and health hidden in play. Title, pause, settings and results presentation accepted on a worker branch; wiring in progress | Commit 893bd82; branch wt/plan020-menus 8a31dcc |
