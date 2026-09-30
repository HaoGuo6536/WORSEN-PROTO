---
id: SPEC-004
type: spec
title: Horror direction and content proposals
status: DRAFT
created: 2026-09-30
updated: 2026-09-30
owner: Hao Guo (project owner)
supersedes: none
superseded_by: none
source: Claude Doc "WORSEN Horror Direction Proposals" (https://claude.ai/code/artifact/056949d8-b09f-4ba8-a223-889ef2d7591e), sources/2026-09-30-horror-direction-chat-1-stakes-and-ladder.txt, sources/2026-09-30-horror-direction-chat-2-music-lore-art.txt, sources/WORSEN_GDD_Rev3.docx
archived: none
---

# SPEC-004 — Horror direction and content proposals

> Status: DRAFT since 2026-09-30. See the [registry](../index.md).
> Source: the reviewed Claude Doc [WORSEN Horror Direction Proposals](https://claude.ai/code/artifact/056949d8-b09f-4ba8-a223-889ef2d7591e), captured on 2026-09-30 after the owner's comment pass; the two design conversations retained as [chat 1 (stakes, hidden state, the Make it Worse ladder)](sources/2026-09-30-horror-direction-chat-1-stakes-and-ladder.txt) and [chat 2 (chase music, behavioural lore, art target)](sources/2026-09-30-horror-direction-chat-2-music-lore-art.txt); and the unchanged [Game Design Document (GDD), Revision 3](sources/WORSEN_GDD_Rev3.docx) summarised by [SPEC-002](SPEC-002-worsen-game-design.md).

## 1. Subject and scope

This spec records the 2026-09-30 direction shift for WORSEN: from an arcade collect-and-run toward sustained horror, with the hunters themselves frightening and stakes layered on top, without losing the competitive-tag pace. It governs the intended behaviour of hunters and non-hunter threats, hidden state and the arrow, stakes and worsenings, collection and collapse, movement and traversal, level and room design, the shop with its upgrades, consumables, general curses and shrines, presentation and sound, the in-run interface, and the specific hunter roster and curse catalogue requested by the owner. Rows and sections that the owner approved in the source page carry an inline "(approved 2026-09-30)" mark; items marked deferred to co-op or deferred for the prototype are recorded as such and are not requirements now.

It overrides the following lines of the GDD as summarised in SPEC-002 where they conflict: the visual direction should feel "relatively non-serious at rest"; the cake as an absurd joke; the exact cake counter and directional cue; the assumption of a chase track; and the five placeholder hunters. SPEC-002 remains the broader design source for everything this spec does not touch. It does not cover networking or co-op resolution, survivor classes beyond the flashlight as a class-style item, procedural generation internals (see [SPEC-003](SPEC-003-procedural-maps-level-progression.md)), or engineering rules (see [SPEC-001](SPEC-001-project-architecture-guidelines.md)). It contains no implementation steps; plans implementing it are listed in section 6.

Each subsection of section 2 keeps the source page's three-column shape where it had one: the proposal, what the build does today as observed on 2026-09-30, and the change. The "today" column is evidence from the code survey at that date, not a description to be kept current; `DOCUMENTATION/` owns current implementation once it exists.

## 2. Intended behaviour

### 2.1 What the direction shift settles

The hunters must still be scary. Stakes make a run tense on the hundredth attempt, but they do not replace a hunter that is frightening to be near; they are the reason a frightening hunter keeps working after the player has memorised it. The two layers stack. This page treats them as separate work: the next section is about the hunters themselves, and the stakes section comes after it.

The conclusions from your two conversations that this page applies:

| Conclusion | What it means for the build |
| --- | --- |
| Hunters stay scary through gait, approach, sound signature and what they do when they win, not through their face | Spend on animation, audio identity, stalking behaviour and the catch; keep silhouettes legible at distance |
| Hide state, never rules | No markers, no exact cue, no text telling the player the chase state; sound with occlusion is the primary channel; look-back is the only hard confirmation |
| Silence is the default; a clean all-clear cue drains dread | No resolving off-state for the music; layers that never fully settle; music that occasionally lies |
| The catch ends the run and is ugly | A held, loud, close catch replaces the current camera snap and fade |
| Make it Worse is a ladder of fear axes the player climbs | Worsenings remove information, tools, time and safety, stated plainly; stakes climb faster than lethality |
| Anxiety is the base state, terror is punctuation | Design the freeze at doorways; budget startles to one or two per run plus the catch |
| Lore is inferred from behaviour and imagery | Hunter superstitions, authored wrongness in the generated rooms, cakes as creation horror, rare unexplained events |
| Art target is plausible realism in darkness with a degradation layer | Not lo-fi; flashlight cone, fog, grain, non-human hunters that dodge the animation tax |

Where the GDD now conflicts with this: its line that the world should feel relatively non-serious at rest, the absurd-cake framing as a joke, the exact counter plus directional cue, and the assumption of a chase track. Those are called out again in the last section.

### 2.2 Making the hunters scary

The five current hunters are placeholders and the roster is rebuilt from zero. What stays is the machinery around them: the GOAP planner and fact model, the factory and registry, the per-archetype motor, attack and animation configs, the trait flags, the light-response hooks, the animation driver and Final IK. The rows below are therefore requirements for the new roster and its systems, not patches to the Watcher, Rusher, Lurker, Hexer or Thorncaller. The fear of a hunter lives in the approach, its habits, its sound, and the catch.

| Proposal | What exists today | Change |
| --- | --- | --- |
| Slow the approach (approved 2026-09-30) | Every non-patrol action, including hint investigation and last-known search, runs at chase speed | Walk when investigating and searching; reserve chase speed for confirmed pursuit |
| Add a Stalk action (approved 2026-09-30) | GOAP library has Patrol, InvestigateHint, SearchLastKnown, Chase, CutOff, Lunge and light responses; no Stalk, Retreat or Retarget | Stalk closes distance only while unseen and breaks into a chase on sight, so the first reveal is a shape at the edge of the beam, not a sprint |
| Reveals, not spawns (approved 2026-09-30) | Hunters spawn somewhere on the map at floor start while the player spawns in the central exit room; that rule stays | Add a first-contact guarantee on top of that rule: spawn out of line of sight and beyond a minimum path distance from the exit room, and make any future curse that moves spawns pass the same check, so the player never meets a hunter in the opening seconds. Once moving, prefer approaches that emerge from occlusion: doorways, corners, stair heads; the route presenter can bias to the last occluded waypoint |
| Superstitions designed in from day one (approved 2026-09-30) | No threshold pauses, no turn-to-face on loss, no reaction to cakes | Three or four consistent unnatural habits per hunter, written into its brief before its model exists: pause on every doorway threshold, turn to face the last-known position when the belief drops, react audibly when a cake is taken nearby. Players write the folklore |
| Per-hunter sound identity (approved 2026-09-30) | Presence, detection and loss cues are generic; the Chase cue is defined but never played; no death stings | Each archetype owns a distant presence loop, a detection vocal, a chase layer and a death sting. Presence should be audible through walls with occlusion so the player hears which hunter is on the floor before seeing it |
| One partition-ignoring hunter (approved 2026-09-30) | None; every archetype respects walls and the NavMesh | Design one in from the start rather than retrofitting. The Hexer, already hovering, was the obvious retrofit; in a rebuilt roster it is a purpose-built hunter that paths through optional doors, vault windows and thin partitions, so the room is open space |
| A real Retreat (approved 2026-09-30) | Relief only withholds Director hints; hunters re-acquire on their own | A Retreat action the Director can request after long pursuit, so silence sometimes means it left and sometimes does not |
| Make a missed lunge cost (approved 2026-09-30) | The hunter stands still through recovery; that is the only penalty | Add a short stagger with a forward stumble so a dodged lunge visibly overshoots; this is the tag reach the GDD describes |
| Hidden mutations as infrequent progression events (approved 2026-09-30) | Traits exist but are all chosen by the player | Not every floor and not at run start. Roughly every eight floors a progression event fires, in the Nullscape manner: an environmental hazard, an upgrade to an existing hunter, an extra hunter, or a random one. A hidden rule mutation drawn from a learnable pool is one entry in that event pool. When it fires, tell the player something is different, never say what, and give each mutation a tell |
| Novelty on a schedule (approved 2026-09-30) | All five current hunters are available from round one | Ship fewer hunters and gate later ones by depth or lifetime runs, so a new hunter arrives every so often and returns real unknown for a few runs |
| The catch (approved 2026-09-30) | Camera snaps to the killer, a Death cue, a fade; hand deaths get a short Consumed fade | Close-up on the killer's animation held for a beat, a single hard sting, a hard cut to the run summary. This should be the loudest and ugliest moment in the game |
| Four tunables as the brief (approved 2026-09-30) | The current five drift from 0.72 to 1.12 speed ratio with identical senses | Each new hunter states its inertia, commitment, speed ratio and loss rule explicitly, and those four numbers are tuned in the arena before any art, as GDD section 5 asks |

**Build order for the new roster.** Follow the GDD's own sequence: one physical pursuer first, alone in the arena, until its four tunables feel like tag and its approach frightens without art. Then the partition-ignoring hunter. Then one non-physical systemic threat expressed by the Director rather than a body. Further hunters only behind depth gates, so each arrival is a return to the unknown.

**One-page brief per hunter, written before modelling.** Approach signature: how it closes and what the player hears first. The reveal: how it enters view. Three habits. Its sound set: presence, detection, chase layer, attack timing, death sting. Its four tunables. What it does when it wins. Its mutation pool, each entry with a tell. Silhouette readable against fog at look-back speed. Non-human, masked or cloth-covered where possible, to dodge the mocap tax.

**Felt intelligence.** Today the planner is real but invisible: with one live goal at a time the hunter either sees you and chases, or does not and wanders, so GOAP reads as a raycast plus a patrol. Intelligence is felt when the player can watch a hunter decide, predict, and remember. Six changes make the existing planner visible:

1. Purposeful patrol. Patrol targets should be where the player must go: rooms with remaining cakes, the exit once it opens, the spot where the last cake was taken. Aimless waypoints are what reads as dumb.
2. Competing goals at once. Keep three or four live goals with changing utility, such as locate prey, deny the route to the nearest remaining cake, protect the exit corridor, and break a detected loop, so the planner is choosing between real alternatives and the choice is visible as a change of direction mid-route.
3. Visible deliberation. When a belief drops or a hint arrives, the hunter stops, turns its head toward the candidate, and vocalises before it moves. A beat of thinking is what humans read as a mind. Final IK head tracking covers this.
4. Prediction over pursuit. Chase should aim at where you will be, not where you are, using the existing intercept-room logic more often than the direct path, and the hunter should sometimes take the parallel corridor rather than follow you through the door.
5. Memory used in the open. After losing you, search in an expanding pattern from the last-known position, check the doorway you went through, then the room beyond, then return once to the loss point. Reuse the same search on the next loss so the player learns it and can exploit it.
6. Readable reactions to sound. On a noise event, turn toward it first, then decide whether to go, with the decision depending on loudness and on what the hunter was doing. A hunter that ignores a quiet sound while protecting the exit reads as a judgment, not a bug.

The Director should also feed hints as regions and noise events rather than only a stale position, so the hunter's arrival at a room and its sweep of it look like an inference rather than teleporting knowledge.

**Environment interaction as a habit axis.** Habits that touch the level are the most legible kind, because the player sees their evidence after the hunter has gone. Candidates, each a small driver action on geometry that already exists: closing the door it just came through; snuffing a torch as it passes so darkness follows it; breaking or blocking a repeated loop object, which is the GDD's BreakLoop action; vaulting a window only the player was meant to use; knocking a prop into a corridor as a noise and an obstacle; standing in a doorway to hold it; leaving a mark on a threshold it has crossed; and treating collapsing rooms differently from the player, such as refusing to enter a telegraphed room or ignoring it entirely for the partition-ignoring hunter. Each new hunter's brief should name at least one environment habit so the roster differs in what it does to the level, not only in speed.

Animation budget note from the second conversation: low frame-rate, stop-motion style movement is cheaper than smooth and more unsettling. The animation driver already exists per archetype, so this is a clip and sample-rate choice, not new code.

Final IK is already imported under `Assets/External/Plugins/RootMotion/FinalIK` but nothing in the hunter animation driver uses it yet; the driver only disables root motion and foot IK. It fits three rows above directly: head and spine tracking toward the player's last-known position for the turn-to-face habit, a look-at that holds on the player through the catch close-up, and foot placement on the generated stairs and split levels so a walking stalk reads as a body in the room rather than a sliding capsule. It also lets a few base clips cover many habits, which keeps the animation budget on gait and approach.

### 2.3 Hide state, not rules

The player should know everything about how hunters behave and almost nothing about where they are right now. Several current defaults hand that information over for free.

| Proposal | What exists today | Change |
| --- | --- | --- |
| Remove the HUNTED label | The HUD panel prints HUNTED and turns warning-coloured in a chase; only the extra layer hides | Hide the whole HUD in a chase. The detection beat is the camera punch and the hunter's own sound, never text |
| Kill the clean all-clear | Music zeroes every stem when no threat is near and switches to a distinct chase gain on chase start | Keep a low tension floor that never resolves; fade the danger layer out on a randomised delay after loss; occasionally let it fade while the hunter is still close |
| Cake arrow: accurate, always on, white | Cue follows the NavMesh shortest path to the nearest cake, refreshed every half second | Decision on 2026-09-30: the arrow stays exact and always active, drawn as a single white arrow. Its horror job is not vagueness but trust: enemies and traps that imitate cakes are told apart only by where the arrow does not point, and collectable-based threats can borrow the same arrow language. The current arrow also misfires; see the lighting and arrow section for the cause and fix |
| Look-back as a snap | Q freezes body heading and the mouse scans freely at full speed; steering authority is documented as unused | Decision on 2026-09-30: a Mario Kart style snap back. Press turns the view fully behind in one fixed frame, release snaps forward, no scanning. Steering continues on the body heading. A curse in the selection pool removes the look-back entirely as a player choice. The camera config already has unused yaw and duration fields. |
| Occlude player noise | Curse noises get wall occlusion; player footsteps do not, transmission is fixed at one | Route player noise through the same occlusion path and widen the Director's hint radius with occlusion, so walls and distance buy real uncertainty. Decided 2026-09-30: crouch stays purely a posture for sliding and low gaps, with no stealth speed or loudness of its own |
| Hide health | HorrorRun shows a number and a bar during play | Show the number only in the shelter; the vignette, breathing and speed penalty already carry the state in the run |
| Hidden hunter count | The shelter screen lists every retained threat | Fine at low rungs; a worsening can hide which of the retained hunters is active on this floor |
| Occasional false positives | None | A rare distant footstep or door sound with no hunter behind it, rate-limited so trust erodes but does not break |

### 2.4 Stakes and the Make it Worse ladder

The wallet already dies with the player and persists across floors, which is the right foundation. What is missing is a way to put something on the table before the first hunter appears, an honourable way out of the freeze, and worsenings that climb fear rather than lethality.

| Proposal | What exists today | Change |
| --- | --- | --- |
| Early bail with a penalty | The exit opens only after the last required cake; the only way out of a bad sweep is death | Let the door open early at a cost, such as losing most of the wallet or taking a permanent curse. This turns "I can't push forward" into a decision the player made and will want to beat |
| Bring-in stakes (deferred to co-op) | Nothing is carried between runs | Deferred to co-op on 2026-09-30: carrying a purchased item into the next run, lost on death, only makes sense once a death is not the end of the run |
| Wager the wallet | Wallet is at risk only through death | A rung where the floor's haul is doubled if you escape and the whole wallet is lost if you die on that floor |
| Worsenings as removals | The curse list is almost entirely "the hunter gets a bit better": longer lunge, faster recovery, wider cone | Add rungs that remove what the player leans on, stated plainly in the card copy: no look-back, no cake counter, no hunter presence cues, hidden hunter count, faster collapse, a clock on the first sweep |
| In-run worsen verb | None | An optional mid-floor action that raises reward and pressure together, in the spirit of dimming a torch. The unused Interact button is free for it |
| Persistent hell | Run state resets fully on restart | A curse that stays until the player climbs out of it, or a debt that compounds on death, gives the ladder memory and makes the player the author |
| Stakes climb faster than lethality | Lunges do half of full health and hands a quarter, with no post-hit grace | Keep the top rungs survivable by a skilled player and catastrophic to lose. Add a short grace window after a hit so a lunge cannot chain into a grab |

The five axes from your conversation, as a checklist for any new worsening: information, unpredictability, stakes, agency, time. A worsening that only raises hunter numbers should be the exception.

### 2.5 Collection, collapse and the run loop

The generated floor places two rows of five cakes in every room, all on Flow anchors, all required. With seven to fifteen rooms that is between seventy and one hundred fifty pellets gating the exit, and it is the single strongest arcade signal in the build. Fewer, placed cakes that are each a destination, plus cake imagery that unsettles, changes the whole read of a floor.

| Proposal | What exists today | Change |
| --- | --- | --- |
| Cake density | Ten per room, straight lines, Flow only; the generator ignores the anchor weights in `FloorConfig` | One to three per room on Precision, Detour, Risk and Vertical anchors; make the required count a subset so some cakes are optional risk |
| Cakes as creation horror | Primitive fallback visuals | A lit candle, the same name piped on every cake, the one bright saturated object in a desaturated world. Someone baked them |
| Remove the combo sting | Every third pickup within a window plays a chain sound | Cut it, or replace it with a distant reaction from the hunter on the floor |
| Golden count on the HUD | Never shown | The GDD lists a shared cake and golden count; show it as a quiet second figure |
| Exit soft-lock | The exit room can reach Closed and then refuse the player, with no death and no exit | Decided 2026-09-30: the exit room never collapses. Everything else can, and the fog and hands can reach its doorways, but the room itself stays open so no rule can trap the player |
| Hands as a real cost | Grabs halve speed, deal a quarter of health, never kill directly | Let a second grab within a short window be fatal, or let the hold last until escaped so the room can kill |
| Deep dark in collapse | Fog is black at eight to twenty-four metres through the whole floor | Keep the sweep readable and tie fog near-distance and torch budget to the collapse phase, so the level visibly dies |
| Decline a hunter | The threat choice is mandatory with no skip | Deferred on 2026-09-30: the choice stays mandatory during prototyping. See the shop and selection section |
| Generation retry | One failed assembly ends the run | Bounded retries with an incremented seed, then a reported fallback, as SPEC-003 asks |
| Results for HorrorRun | Ends on the shelter screen with round and wallet only | Cause of death and killer, chases escaped, grabs escaped, time from exit open to escape, the seed with a fixed-seed field, and a persisted best depth |
| Unverifiable micro-events | None | A door left open is now closed; a silhouette stands where it cannot path; the counter shows a cake that is no longer there. One or two rare ones with a very low per-run chance become folklore |

### 2.6 Presentation

Audio is the one presence channel a flat screen keeps almost in full, and the art target is plausible realism in darkness rather than lo-fi. Most of the pieces exist; they need defaults changed and a few gaps filled.

| Proposal | What exists today | Change |
| --- | --- | --- |
| Silence as the default | Interior ambience bed plus tension scaled by proximity | Drop the ambience bed to near-silence during the sweep so a footstep can land; music enters only when a hunter holds a belief about you |
| Binaural and occlusion | Plain Unity 3D sources with linear rolloff; occlusion only on curse noises | Add an HRTF spatializer plugin and route hunter presence and footsteps through occlusion, so direction and distance are readable from the mix alone |
| Ask for headphones | No title screen | A title screen that says to wear headphones, then the run |
| Embodiment | Hands and feet are hidden by the limb stand-in; no head bob or landing dip; breathing only below a quarter health | Show hands; add a small landing dip; add the player's own breathing under pursuit and a heartbeat that scales with proximity and slightly masks the mix |
| Degradation layer | Chromatic aberration, distortion and vignette react to events; grain fires only during the intrusion | A constant low grain and slight aberration with a diegetic frame, so store assets and your own read as one plausible image |
| Startle budget | Intrusion fires whenever the player is slow; no other startles | One or two earned startles per run from a broken expectation, plus the catch |
| Fail forward | A failed vault emits a fact and nothing else; the stumble state has no effect | A bad vault stumbles the camera and briefly cuts speed; a missed gap changes elevation. The mistake stays part of the chase |
| Pause and settings | Sensitivity, invert Y, FOV, tilt, punch and reacquire blur are locked in designer assets; mixer groups are unassigned; no pause or quit | One pause menu exposing those plus master, music and effects volume. This is the most useful single addition for playtesting |
| Never-emitted cues | PlayerCritical, GrabHit, Consumed, WindLoop, DoorOpen, Restart and Chase are defined but never played | Wire DoorOpen to the physical exit door first; it is the most noticeable silence |

**Minimal audio, by decision.** The cue catalogue has grown to roughly seventy cue ids, covering surface-specific footsteps, posture rustles, sprint exertion, a pickup chain, UI moves and confirms, and separate windup, swing, miss, hit and recovery cues per attack. That density fights the silence-first rule and buries the cues that carry information. Proposed budget, applied when the roster is rebuilt:

- The player owns three sounds: footsteps with occlusion, breathing under pursuit or injury, and traversal contact. No exertion layer, no posture rustle, no surface variants until a surface matters to a hunter's hearing.
- Each hunter owns five: presence, detection, chase layer, attack timing, death sting. Windup, swing and recovery collapse into the one attack-timing cue.
- The world owns a handful of diegetic sounds that mean something: the cake pickup itself, the exit door, a room telegraph, a torch guttering, a door a hunter closed. No pickup chain, no UI cues in the run.
- One voice per category at a time, with hunter presence and attack timing never ducked by anything else. Everything outside the budget is removed from the mix rather than turned down, so the remaining cues are trusted. Because the budget is small, every repeated sound varies on each play: a small random pitch range, a little volume and timing jitter, and two or three alternate takes where they exist, so nothing in the mix becomes monotonous. Cues whose exact timing is a tell, such as attack windups, vary pitch but not timing.

**Shared hearing rule.** Anything the player can hear in the world, the hunters can hear too, with the same occlusion and falloff. Every environmental sound the mix plays is also a noise event: footsteps, landings, slides, vaults, a door, a knocked prop, and the cake pickup. Interface sounds and the music are excluded. Today only footsteps and traversal emit noise, and cake pickups are silent to hunters unless the Gilded Hunger curse is active; under this rule the pickup sound stays and is always heard, which makes every cake a small wager and turns the pickup into the distraction and subversion it is meant to be. The rule also gives the player an honest model: if you heard it, assume they did.

### 2.7 User interface

Noted on 2026-09-30: the in-run UI is too cluttered. Today the screen carries a titled top-right panel with a cake count and gauge, the exit's LOCKED or OPEN state and a HUNTED label; an extra layer with a 3D compass needle, three empty item-slot outlines and a hard-coded controls hint line; and, in HorrorRun, a status panel with a health number and bar. Much of this duplicates what the world and the sound already say, and several rows on this page already remove pieces of it: the HUNTED label, the health readout, the compass needle in favour of one white arrow, and the empty slot outlines until consumables exist.

The target, to be designed rather than fixed here: one white arrow, a quiet cake count, and the three consumable slots, with everything else off screen. No title, no hint line, no panel chrome. The shelter and shop screens are a separate pass.

### 2.8 Smaller fixes independent of direction

These came out of the code survey and hold whichever direction wins. Each is a contained change.

- [ ] A standing jump has no air control because air steering is capped at current horizontal speed; give it a small floor.
- [ ] Two lunges kill from full health with no post-hit grace or knockback, so a lunge into a hand grab chains; add a short grace window.
- [ ] Sealed Sills is hard-coded to 0.4 in the expedition controller while the configurable value on the effects manager is never read.
- [ ] The UseItem press is consumed by both the Horror orchestrator and the effects controller; verify the flashlight cannot desync.
- [ ] Interact is bound and never read, and the HUD controls hint is hard-coded text; use it or remove it.
- [ ] Inventory is always two empty strings; either populate it or drop the empty slot outlines until items exist.
- [ ] Telemetry records nothing about progression: round, wallet, choices and per-floor seed.
- [ ] TagArena is build index zero, so a normal build boots the test arena rather than HorrorRun.
- [ ] Stale comments: the scene flow manager says FloorLoop is unavailable, and RunPhase says later milestones will supply exit and collapse facts.
- [ ] Chase-start, lose, death and room-telegraph cues are gated off when an expedition is active; confirm the expansion audio path covers all four in HorrorRun.

### 2.9 Playtest notes 2026-09-30: movement and traversal

These come from hands-on testing rather than the code survey. The theme is that the base kit exists but several verbs fight the player: the vault locks the camera, the slide cannot be steered, stairs jitter, and a hit leaves you body-blocked by the thing that hit you.

| Issue | What the build does today | Change |
| --- | --- | --- |
| Body-blocked after a hit (approved 2026-09-30) | Hunters carry a capsule collider and rigidbody that collide with the player; there is no grace after damage | On any hit, start a grace window during which hunter colliders are ignored for the player (layer swap or per-pair ignore) and no further damage lands. The player can pass through the hunter to escape. Short desaturation or a heartbeat spike marks the window so it reads as a rule |
| Vault feels glitchy and locked (approved 2026-09-30) | Vault and mantle are a scripted position lerp of 0.25 or 0.35 s with input and camera locked for the whole motion | Replace with a physics-driven mantle: keep mouse look live, blend the camera on an authored curve with a small dip and rise, allow steering during the last third, and cancel into a jump. Test against natural feel rather than the fixed timings; the GDD's one-second lock ceiling still applies |
| Slide is not controllable (approved 2026-09-30) | Slide steering is capped at a low turn rate with limited lateral acceleration, and speed lost to a collision is never recovered | Raise slide turn authority so the mouse can bend the slide, keep the speed cap, and let a wall contact redirect rather than kill the slide. Slide-jump cancel already exists and stays |
| Stairs jitter and catch on edges (approved 2026-09-30) | Rooms with stairs generate step colliders; climbing rolls over each step and the player clips on edges when jumping onto them at an angle | Give every stair a single ramp collider under a stepped visual, widen landing collision at the top and bottom, and raise the character step offset. Ramps also fix the hunter's stair traversal |
| Ledge climb (approved 2026-09-30) | Vault, mantle and rebound only trigger on colliders tagged as traversal surfaces | Apex Legends style ledge climb on any collider edge within reach while airborne: grab, pull up, continue. Tagged surfaces remain for authored routes, but untagged ledges must work so a missed jump fails forward instead of sliding off |
| Ledge and vault boost (approved 2026-09-30) | No timed input during traversal | A timed jump press inside a short window during a mantle or vault grants a forward burst, in the spirit of a superglide. Miss the window and the vault is ordinary; the boost is a skill, not a default |
| Hunter AI breaks on corners (approved 2026-09-30; two prior attempts failed) | Hunters stall or jitter against corner obstacles despite the corner clearance tests passing | Reproduce in generated rooms rather than the test scene: the likely cause is agent radius versus the capsule against generated door frames. Add a corner margin to the route presenter and a stuck detector that replans after a short stall. This has been debugged twice without a fix, so treat it as an investigation with evidence, not a tweak: record every stall to telemetry with position, room, nearest obstacle and the agent's path corners; capture input replays of the failing chases; run an automated sweep that drives each hunter through every door frame and stair edge of a set of generated floors and asserts no stall. Candidate causes to rule out one at a time are the NavMesh agent radius versus the capsule, the steering presenter overriding the agent's own avoidance at corners, missing off-mesh links at door thresholds, and the bake settings for the generated geometry. Acceptance is zero stalls across a declared number of generated floors with the replays retained |

**On-hit speed boost and health regeneration (decided 2026-09-30).** Being hit gives the player a short speed boost in the Dead by Daylight manner, scaled by severity: a light hit such as a Herald scream or a hand grab gives a small, brief burst; a heavy hit such as a lunge gives a larger, longer one. The boost runs alongside the hit grace window so a hit is a chance to break away rather than the start of a chain. Health is full at the start of every floor and regenerates slowly during the floor, so a careful sweep recovers from an early mistake and the between-floor shop heal becomes unnecessary; the current carry-over of health between rounds is dropped. Both rules are curse targets: curses can shorten or remove the boost, slow or stop the regeneration, or start the floor below full health.

### 2.10 Collapse, fog and grabs

The collapse order is right and the presentation is wrong. Rooms die farthest-from-exit first, but each room's mist is its own volume that fills on its own timer, so the fog reads as cells switching on in a three-dimensional array rather than as something advancing through the building. The grab system that should make the fog dangerous does not function in play.

| Issue | What the build does today | Change |
| --- | --- | --- |
| Fog reads as a grid (approved 2026-09-30) | One collapse volume per room with cracks, mist and hands driven by that room's progress; mist appears at the room's far corners | Keep fog on a room-by-room basis, but blend it. A collapsed room's fog leaks through its doorways and openings into the neighbouring room and thickens there as that room's own collapse progresses, so the leak is part of how a room visibly begins to die and there are no hard cell edges. Implementation can still be one low-resolution 3D density texture that the collapse controller writes per room and blurs across boundaries, sampled by a volumetric or screen-space fog pass, in the League of Legends fog-of-war manner but in three dimensions. Dark, not white. Tendrils extend outward from the fog's own body along floors and door frames, never hanging from ceilings. The doorway of a fully collapsed room reads as a rounded, pitch-black foggy mouth filling the frame, with hands reaching in and out of it; that silhouette is the collapse's signature image. A faint glow only where the field is thin. Room progress still drives it, but the player sees a front, not tiles |
| Direction of collapse (approved 2026-09-30) | Mist grows from the corners farthest from the beacon inside each room | Distance from the exit decides which room collapses next, outer to inner, as it does today; it is a selection rule, not a shape for the fog. Within that order the blended, leaking fog shows where the front is: the side of a room where fog is seeping in is the side that dies next, and the clear side is the way home, which agrees with the exit arrow |
| Grab, slow and damage do not work (approved 2026-09-30) | Hands warn, grab, halve speed and deal damage in the controller, and the unit tests pass, but in play the chain does not fire | Rebuild the runtime chain simply and verify it live: the hand volume is a trigger at the fog boundary; enter it and a grab starts; escape by leaving the radius within the grace; otherwise take damage and release with a cooldown. Show the hand state on the debug overlay so a failure is visible. Wax Ward and the shared hearing rule hook into the same events. Visual reference for the hands: the Gate of Truth scene from Fullmetal Alchemist, many thin black arms pouring out of a dark doorway, grasping and dragging in a mass rather than as single limbs (reference clip: https://www.youtube.com/watch?v=HfL68GT0P8Q). The collapsed-room mouth in the row above is where that image lives |
| Fog as a threat, not scenery (rules set 2026-09-30) | Rooms are never lethal by themselves | Rules as decided on 2026-09-30. A grab deals a set amount of damage and then throws the player away from the fog; damage that would kill, kills as usual, with no special fatal-grab rule. A room about to collapse needs an indicator: its fog pulses in the darkness faster and faster, like a ticking bomb, until it goes. A fully collapsed room cannot be re-entered: its fog acts as a rubber-band wall that stretches under the player and springs them back out, and touching it starts the grab sequence as normal, so testing the wall costs health and position. As a room's collapse progresses, hands reach directly toward any cakes left in it; when the collapse completes they snatch the cakes and retreat into the fog, or the cakes are drawn into the fog, so the loss of that room's reward is shown rather than implied. The boundary is where the hands live, and depth of fog is the danger gradient |

### 2.11 Level and room design

Rooms are fixed 12 metre cells on one storey, chosen from five families. Later floors should be larger, taller and more demanding, and vertical movement should never depend on finding the staircase.

| Proposal | What the build does today | Change |
| --- | --- | --- |
| Larger rooms (approved 2026-09-30) | Every room is a 12 m cell; growth adds cells, never size | Allow room modules of two and three cells and irregular footprints, so a floor mixes compressed connectors with genuinely wide halls. This is also what makes a long sightline and a wall-ignoring hunter matter |
| Multi-floor levels (approved 2026-09-30) | Single storey; stair halls and split-level libraries give height inside a room only | For later rounds, generate floors as a building with lower and higher sections. Crucially, every level change must be reachable by more than the staircase: drops through broken floors, holes and shafts, ledges climbable with the new ledge climb, collapsed ramps, balconies you vault down from. Going up should be possible by climbing and rebounding, not only by walking to stairs |
| Stairs become ramps (approved 2026-09-30) | Stepped stairs | See the movement section: ramp colliders under stepped visuals everywhere, for both player and hunters |
| Execution puzzles in later floors (approved 2026-09-30) | None | From a configurable round onward, introduce rooms with simple, readable execution puzzles in the ULTRAKILL manner: hit three switches in order within a window, cross a room on a lit path that dims, a door that opens only while you keep moving, a timed sequence of vaults. Never a wait state, never a read-a-note puzzle. They add execution stress while a hunter is on the floor |
| Cake traps (approved 2026-09-30) | Cakes are only ever cakes | From about floor three, some cake spawns are replaced by a trap in the spirit of Nullscape's tripmines. The white arrow never points at a trap, so the arrow is the tell; greed for a cake the arrow ignores is what springs it. Traps can blind, slow, or announce you to hunters through the shared hearing rule |
| Threshold freeze rooms (approved 2026-09-30) | Not designed | Author a few doorway situations deliberately: the next required cake is through a door, the hunter is audible beyond it, the collapse is behind you. These are the freeze moments the first conversation describes, and the early bail is their honourable exit |

**Level diversity, not only a dungeon.** The generator currently produces one castle vocabulary: torch galleries, stair halls, libraries, cloisters. The GDD already describes biome bands, a hospital sequence, a forest sequence and later uncanny spaces, each returning at a higher tier. Build the module catalogue so a theme is a swappable set: the same connection rules, anchors, height classes and collapse behaviour, but different room families, materials, props, light sources and sound zones per theme. A first slice needs two contrasting themes, for example the castle and a hospital, to prove the swap works; later themes can be added as content. Theme also changes what the fog and the hands look like, what the torches become, and which of the wrongness tells belong, so the level-as-authored idea from the second conversation reads differently in each. Lighting follows the theme: the Lumen grammar in the lighting section is the rule, and each theme supplies its own light sources to apply it to.

**Gaps in the floor plan (decided 2026-09-30).** The map does not need to be fully filled. The generator may leave voids between rooms: missing cells, open shafts, a collapsed wing, a chasm the interior falls into. Gaps give the long sightlines and interior falls the GDD asks for, break the grid feel, and create places the player can see but not reach. The Shrine of Passage is the tool that fills them: it stands at a gap's edge and, once activated, bridges it into a pocket of rooms that was not otherwise connected, holding Golden Cakes and collapsing behind the player. Generation keeps the required cakes and the exit on the connected part of the floor, so a gap never blocks the sweep; the pockets beyond gaps are optional reward space.

### 2.12 Relics, consumables, shop and selection cadence

Upgrades are underwhelming because they are stat nudges: a narrower beam, quieter steps, a shorter cooldown. Treat them as relics in the Risk of Rain 2 sense, each a gimmick that changes what the player can do, and make the shop expensive enough that buying one is a decision.

| Proposal | What the build does today | Change |
| --- | --- | --- |
| Relics, not stat buffs (approved 2026-09-30) | Six offers, four unique: narrower cone, quieter footsteps, shorter rebound cooldown, door marks; two repeatable: a heal and a one-charge grab break | Rebuild the catalogue as relics with unique rules. Examples to seed it: a vault that stores momentum and releases it on the next jump; a rebound that chains once off a hunter; a slide that passes under a grab; a look-back that also reveals the hunter's outline for a beat; a cake that, when collected, closes the door behind you; a decoy that drops a fake footstep trail; a relic that makes the exit arrow show through walls; a relic that turns one trap per floor into a cake. No relic is a percentage |
| Flashlight as a recharging class item with a stun (approved 2026-09-30) | A toggle flashlight with a beam that hunters react to, and a curse for the afterimage | Keep the flashlight as a light: it still brightens dark rooms while it is on. Make its stun the consumable, in the Dead by Daylight manner: aim and hold on a hunter's face to stun it briefly, which spends a charge that recharges slowly over time rather than being bought. The flashlight is therefore a class-style item, the first of what a future survivor-class system would give each character, and not a shop consumable. The shop still stocks genuine consumables with limited uses. Lighting a room remains free; interrupting a hunter costs the recharge |
| Expensive shop (approved 2026-09-30) | Prices of 2 to 4 with one golden cake per pickup | Price relics at a meaningful fraction of a floor's whole golden haul, so a purchase means a second sweep survived. Consumables cheaper but still felt. Prices scale with round, and so does availability: certain relics only enter the shop from later floors, in the Nullscape manner, because later floors are larger and carry more golden cakes, so the expensive relics arrive when a haul can actually pay for them |
| Selection every two rounds (approved 2026-09-30) | Threat and curse are offered on every combat round; a shop floor appears every second combat floor | Every two rounds the player enters a selection round for a hunter and a curse together; the shop keeps its own cadence. Rounds between are pure floors |
| Duplicate hunters, no cap of five, all active (approved 2026-09-30) | A hunter id can be active once and at most five threats are active; after that the threat choice is skipped | Remove both limits in the Nullscape manner: the player can pick the same hunter again and keep adding, and every chosen hunter is active on every floor. There is no body budget. The Director's job stays pressure management, deciding who is on the scent and when to feed hints, never which hunters exist on the floor. Audio priority and hunter sound identity are what keep a large active roster legible |
| Decline a hunter (deferred 2026-09-30) | The choice is mandatory | Deferred: not for the prototype. Picking a hunter stays mandatory to avoid confusion during prototyping; the decline-then-assign rule from the GDD is kept for later game design |

**Reference points for the relic catalogue.** Draw from [Nullscape's upgrades](https://nullscape.miraheze.org/wiki/Upgrades) and [curses](https://nullscape.miraheze.org/wiki/Curses), [Risk of Rain 2's items](https://riskofrain2.wiki.gg/wiki/Items), and other roguelites with the same shape. What those catalogues do that WORSEN's should copy:

- Nullscape's upgrade shops appear only on certain levels, offer three pedestals with a reroll, gate availability on which enemies, curses or upgrades you already hold, allow stacking to a cap, and scale prices by player count. Gating relics on your chosen hunters is the most useful of these: a relic that only exists because the Skip is in your run is a relic that means something.
- Risk of Rain 2 sorts items into tiers with distinct rarity, gives most of them a rule rather than a number, lets copies stack into stronger versions of the same rule, and keeps Lunar items as power with a drawback. WORSEN's equivalents: common relics that change one verb, rare relics that change a hunter's rule against you, and cursed relics that make it worse for a bigger payout.
- Both catalogues are readable in one line each. A relic the player cannot explain to a friend in a sentence is a stat buff wearing a costume.

### 2.13 Shop catalogue: relics, consumables and general curses

A starting catalogue in the same language as the hunter curses: short Title Case names, one line each, rules rather than numbers. Relic is the design language, not a category: these are the shop's upgrades, permanent for the run, each a rule rather than a number. There is no rarity. Each upgrade has a floor from which it can appear, and some appear only while a given hunter is in the run, as Nullscape gates its upgrades on enemies, curses and level. Consumables are bought with limited uses and spent in motion. General curses are picked at the selection round and climb the five fear axes from the stakes section. Prices and availability floors are left to the shop configuration; the expensive-shop and later-floor rules apply.

**Relics**

| Upgrade | From floor | Requires | Effect |
| --- | --- | --- | --- |
| Stored Momentum | 1 | none | A vault stores your speed and the next jump releases it |
| Soft Landing | 1 | none | Hard landings no longer stumble you |
| Quiet Slide | 1 | none | Slides make no noise at all |
| Thick Skin | 1 | none, stacks | The grace window after a hit lasts longer |
| Wax Heart | 2 | none | The first grab of every floor breaks automatically |
| Low Profile | 2 | none | Sliding passes under collapse hands; a slide cannot be grabbed |
| Steady Hand | 2 | none | The flashlight stun recharges faster |
| Second Bounce | 3 | any pursuer | Once per chase, a wall rebound can also bounce off a hunter's body |
| Sweet Tooth | 3 | none | One cake trap per floor is a real cake instead |
| Glimpse | 4 | any pursuer | The look-back snap also outlines the hunter behind you for a beat, through fog |
| Latch | 4 | the Skip or the Echo | The first door you sprint through in each room closes behind you |
| Echo Boots | 5 | the Herald or the Ram | Your footsteps reach hunters from where you were three seconds ago |
| Exit Sense | 5 | none | Once the exit opens, its arrow shows through walls |
| Blind Faith | 6 | the Mimic | Every cake counts double toward the exit, but the arrow is gone |
| Loud Heart | 6 | the Herald | You sprint faster while chased, but hunters hear your heartbeat during a chase |
| Gilded Greed | 8 | none | Golden Cakes are worth double, but the exit only opens after your first Golden Cake |
| Longer Slide | 1 | none | Slides last longer and keep more speed |
| Higher Jump | 1 | none | You jump higher; more ledges become reachable |
| Sticky Fingers | 2 | none | Cakes are collected from slightly farther away |
| Bigger Pockets | 2 | none, stacks | One more consumable slot above the base of three |
| Cat Eyes | 2 | none | You see slightly farther into the dark; fog starts farther out |
| Field Kit | 2 | none, stacks | Health regenerates faster during a floor |
| Lucky Reroll | 2 | none, stacks | One extra reroll at hunter and curse selection |
| Bargain Hunter | 3 | none | Prices are lower at the next shop |
| Keen Ears | 3 | any pursuer | Hunter presence cues are audible from farther away |
| Trail Reader | 3 | the Echo | After a look-back, the Echo's remaining path is faintly visible for a moment |
| Sure Footing | 3 | the Ram | A glancing Ram charge knocks you aside without damage |
| Golden Sense | 4 | none | During collapse, a second arrow points to the nearest Golden Cake |
| Stone Nerves | 4 | the Stare | The Stare's window is longer |
| Web Cutter | 4 | the Weaver | Webs slow you half as much |
| Marked Doors | 4 | the Skip | Doorways the Skip is watching show a faint mark |
| Afterglow | 4 | the Mannequin | A room whose light breaks stays safe from the Mannequin for a few seconds more |
| Spare Key | 4 | the Ticking | Keys spawn closer to you |
| Mirror Skin | 5 | the Blinder | Blindness lasts half as long |
| Ear Plugs | 5 | the Herald | The Herald's scream deafens you for half as long |
| Bail Bond | 5 | none | The early bail penalty is halved |
| Extra Life | 6 | none | Once per run, dying returns you to the floor's start at half health while the collapse continues |
| Golden Touch | 2 | none | Each Golden Cake is worth one more |
| Shop Reroll | 2 | none, stacks | One free reroll of the shop's offers per visit |
| Loyalty Card | 3 | none, stacks | All shop prices are lower |
| Interest | 3 | none | The wallet grows by a small share between floors, up to a cap |
| Refund | 4 | none | A consumable replaced at the shop refunds half its price |
| Extra Pedestal | 4 | none | The shop shows one more offer per visit |
| Business License | 5 | none, stacks twice | Golden Cake yield is multiplied by 1.25; stacks twice to 1.5, in the manner of Nullscape's Business License |
| Speed Boost | 1 | none, stacks to a cap | Flat increase to sprint speed, capped below the hunters' chase speed |
| Quick Start | 1 | none, stacks to a cap | Flat increase to acceleration from a standstill |
| Air Control | 2 | none, stacks to a cap | Flat increase to steering while airborne |
| Fast Hands | 2 | none, stacks to a cap | Vaults, mantles and ledge climbs complete faster |
| Long Boost | 3 | none, stacks to a cap | The on-hit speed boost lasts longer |

**Consumables**

Shop presentation and inventory, decided 2026-09-30. The shop never displays the whole catalogue: each visit draws a small subset of upgrades and consumables from what the current floor and hunters allow, in Nullscape's pedestal manner, with rerolls as a scarce resource. The player holds three consumables by default; the Bigger Pockets upgrade can add slots. Buying one while every slot is full forces a choice of which held consumable to replace, and the replaced one is gone. Upgrades have no slot limit.

| Consumable | Uses | Effect |
| --- | --- | --- |
| Firecracker | 2 | Throw it; the bang is a loud noise event that pulls nearby hunters to the impact point |
| Gauze | 1 | Restores a moderate amount of health over a few seconds while you keep moving |
| Smelling Salts | 1 | Instantly ends deafness, blindness or a web slow |
| Wax Ward | 1 | Breaks the next collapse-hand grab automatically |
| Doorstop | 2 | Jams the door behind you for a short time; hunters must break it and the break is loud |
| Oil Flask | 1 | A slick patch that makes hunters slip and lose their momentum for a moment |
| Glass Vial | 1 | Shatter it at a hunter's face for a short flinch, weaker than the flashlight stun but instant |
| Adrenaline | 1 | A brief speed burst, usable only while critical |

**General curses**

| Curse | Axis | Effect |
| --- | --- | --- |
| No Look-Back | Information | The look-back snap is removed |
| Silent Presence | Information | Hunters' distant presence loops are removed; only detection and attack cues remain |
| Hidden Count | Information | The shelter no longer shows which hunters are active |
| Darker Floors | Information | Fewer lit rooms and shorter fog distance on every floor |
| Random Spawn | Unpredictability | You start the floor away from the exit room |
| Shuffled Collapse | Unpredictability | Collapse order is no longer farthest-first; the escape route is still guaranteed |
| Nothing??? | Unpredictability | Shown description reads only "Nothing???". Hidden effect: one hunter rule is mutated for the rest of the run, with a tell but no announcement, in the pattern of Nullscape's Nothing? curse |
| Wagered Haul (deferred to co-op) | Stakes | Escaping earns 25% more Golden Cakes from the floor. Deferred to co-op: in solo a death already ends the run, so a wallet wager means nothing; the wager version waits until a death is not the end |
| Thin Skin | Stakes | Maximum health is reduced |
| Spent Pockets | Stakes | Consumables do not carry between floors; anything unused is lost at the exit. (Bring-in items across runs are a co-op feature, and there are no relics, only upgrades) |
| Greedy Door | Agency | When the last cake is taken, the Golden Cakes appear but the exit stays locked until 40% of them are collected, with the required share scaled by how many cakes remain against how many were collected. If none are collected and the collapse completes, the exit opens anyway. The exit room never collapses, so this can never trap the player |
| Short Grace | Agency | The grace window after a hit is shorter |
| Faster Collapse | Time | Rooms collapse sooner after the exit opens |
| No Regen | Stakes | Health no longer regenerates during a floor. Offered only once Slow Mend is already carried |
| Slow Mend | Stakes | Health regenerates at half speed |
| Rough Start | Stakes | Each floor starts at half health instead of full |
| Heavy Legs | Agency | The on-hit speed boost is removed. Offered only once Short Burst is already carried |
| Short Burst | Agency | The on-hit speed boost is shorter |

### 2.14 Shrines

Shrines are single-use structures placed on certain rooms when a floor is generated, in the manner of [Nullscape's altars](https://nullscape.wiki/wiki/Altar): each type has one effect, the count grows with floor depth, and they are found rather than pointed at. The GDD names these altars as an in-motion level interaction; here they are called shrines. A shrine is activated by touching it or by the context Interact, which is bound but unused today, and never holds the player in place; its effect resolves while they keep moving. Counts follow Nullscape's curve as a starting point: one on floors three to seven, two on eight to eleven, three on twelve to fifteen, and so on to a cap of six, with an upgrade that adds more.

| Shrine | From floor | Effect |
| --- | --- | --- |
| Shrine of Chance | 3 | Applies a random positive or negative effect to the current floor, drawn from the general curse and upgrade pools |
| Shrine of Bargain | 3 | A devil's deal, presented after the floor. Touching the shrine on the floor marks it; once the player escapes, the deal appears in the shelter: three curses, the player takes one and is paid Golden Cakes at once, the sum scaled to that curse's value and to the current floor, so a heavy curse taken deep pays the most. Walking away is free |
| Shrine of Passage | 8 | Stands at the edge of one of the floor's gaps. Activating it spans the gap with a bridge of tiles into an otherwise unreachable area, lined with Golden Cakes; the tiles begin collapsing at once, so the crossing pays and closes. Gaps are a deliberate part of the floor plan, see the level section |
| Shrine of Protection | 8 | Costs Golden Cakes; grants a shield with a set amount of hit points that is spent before health, does not regenerate, and lasts until it is gone |
| Shrine of Echo | 8 | Reactivates the last shrine used on the run with a changed outcome |
| Shrine of Purgatory | 8 | Golden Cake yield for the rest of the floor is raised by up to 100%. The bonus scales downward with how much of the floor was already collected when the shrine was used, so activating it early pays double and activating it late pays little. The downsides never scale: on activation one extra hunter from the run's roster spawns on the floor at once, and there is a chance that one hunter rule is mutated for the rest of the run, with a tell but no announcement. The revive form is co-op only |
| Shrine of Pacification | 5 | Every hunter loses your position and drops its belief; a breath bought at the cost of the shrine's loud activation, which the shared hearing rule broadcasts a moment later |
| Shrine of Wick | 6 | Lights every lamp on the floor for a while, which makes the Mannequin harmless and every other hunter's sight better |

Shrines answer the same ladder as curses: Chance, Bargain and Purgatory make it worse for a payout, Passage and Purgatory feed greed, Protection and Pacification buy a breath. A More Shrines upgrade belongs in the upgrades table when the system is built.

### 2.15 Requested hunter designs

These are the hunters you asked for on 2026-09-30, written against the one-page brief format from the hunters section. Working names only. Each needs its four tunables, sound set and habits filled in before modelling; the columns here are the rule, the counterplay, and the tell.

| Working name | Rule | Counterplay | Tell and sound |
| --- | --- | --- | --- |
| The Echo (approved 2026-09-30) | Replays your exact path with a fixed delay, never backtracks, and never takes a route you did not take | Never loop back into your own trail; lead it through a door you close, into collapse, or across a drop it cannot follow. Backtracking into it is death | Your own footsteps, slightly wrong, arriving late; silhouette moves like a recording |
| The Mannequin (approved 2026-09-30) | A weeping angel in the Little Nightmares 2 manner: it moves only in darkness and freezes while lit or observed. Some rooms are lit and therefore safe from it. Rare subversion: a lit room's light fails the moment the player relaxes. If the intended rule is the reverse, moving only in light, swap the two lines and the subversion becomes a light switching on | Stay in lit rooms; look at it to hold it; never turn your back in a dark connector | Silent. Its horror is that it never makes a sound; the only tell is that it is closer than it was |
| The Stare (approved 2026-09-30) | An annoyance type, not a pursuer. It spawns directly near the player with an audible cue and keeps repositioning so that it is always somewhere inside the player's view, at the edge of it, never behind. The player must pinpoint it and hold their look on it to scare it away; once scared away it despawns and later respawns near the player again on its usual cadence. Failing to do so within the window starts a chase with a harder loss rule | Find it in your own field of view while still moving, hold the look, then carry on. The cost is attention and steering under the collapse clock, since it sits where you are trying to look | Its normal cue is a spoken "I see you", spatialised, with a second call when the window is half gone. Rare subversion: the cue is instead a similar but different line, "Find me", and it is not in view at all; the player must actually search, and the familiar rule has been quietly broken |
| The Weaver (approved 2026-09-30) | Fires a slowing web projectile along a warned line and traverses the ceiling, so it arrives from above doorways. Not necessarily a literal spider. Lesson from the Hexer, whose bolts clipped walls: the Weaver's planner must include a Reposition action that moves it to a spot with a verified clear line to the target before it is allowed to shoot, checked by a sweep along the intended path, and the projectile's collider is kept small so door frames and props do not eat shots | Break line of sight before the shot, or vault through the web line; slowed players should still be able to slide | Skittering above, a wet click before the shot, a web that glows faintly |
| The Ram (approved 2026-09-30) | Charges in a straight line after a wind-up, cannot turn during the charge, and stops hard on contact with a wall | Sidestep at the last moment or bait it into a wall; corners are safety, corridors are death | A stamp, then a bellow; the floor thumps with each stride |
| The Mimic (annoyance type, approved 2026-09-30) | An annoyance type, not a pursuer. Sits in the world as an uncollected cake. The white arrow never points to it, which is the only way to tell | Trust the arrow over your eyes; greed for a cake the arrow ignores is what springs it | Identical to a cake until touched; then a wrong sound |
| The Skip (annoyance type, approved 2026-09-30) | An annoyance type, not a pursuer. Slow on foot, but teleports silently and invisibly on a cooldown to intercept at doorways or vault windows the player has used often this floor | Vary your routes; do not reuse the same doorway three times; the cooldown makes it avoidable | No sound on the teleport; a habit tell such as a mark left where it appears, learnable over runs |
| The Blinder (approved 2026-09-30) | Obscures the player's vision temporarily, by traps laid on the floor or by a thrown projectile | Watch the floor for traps; break line of sight before the throw; blindness is survivable if you were already moving on a known route | A hiss before the throw; traps make a faint tick |
| The Ticking (approved 2026-09-30) | An annoyance type. Nothing stationary: a clockwork thing keeps pace with the player at a distance, always somewhere behind, and it runs down. While it ticks it is dormant. When the ticking stops it wakes and hunts until wound again. It is wound by touching a key, and keys spawn on a timer a short way from wherever the player currently is, marked by this threat's own custom arrow rather than the white cake arrow, so the detour is always short but always interrupts routing and scales to any floor size. Nullscape's Cadence, with its timed instrument spawns, is the model, without the fixed room | Learn the tick's rhythm; take the key when it lands near your route rather than when the tick is almost gone. The cost is a small detour under the collapse clock and a hunter listening for it | A quiet tick behind you that slows as it runs down, never a tune; the tell is the gap between ticks lengthening, and silence means it has stopped |
| The Herald (added and approved 2026-09-30) | A pursuer whose weapon is its voice. Every scream is a noise event that hands the player's current position to every other hunter on the floor through the shared hearing rule and the Director's hint channel, so the Herald is dangerous in proportion to who else is in the run. Its attack is a scream with a semi-large radius: low damage, but a hit temporarily deafens the player, muffling the mix and the other hunters' cues. Audio is fixed to the imported Mangled Screams set: ms\_mangled\_scream\_03 on discovery, sb\_mangled\_scream\_01 and sb\_mangled\_scream\_03 alternating during the chase, sb\_mangled\_scream\_02 for the attack. | Break line of sight fast, because every second in its view is a broadcast. Outrange the attack scream rather than dodge it; when deafened, route by memory and the arrow until hearing returns. Prioritise losing the Herald over any other hunter when several are active. | The four screams are the whole sound set: discovery, two chase calls, attack. A drawn breath before the attack scream is the timing tell. Its screams are the loudest sounds on the floor and are never ducked. Every play is pitch-shifted within a small random range and the two chase calls alternate with randomised spacing, so the same four files never sound monotonous; the attack scream keeps a fixed pitch so its tell stays learnable. |

Two roster slots from the earlier sections still apply and can be filled by these: the first physical pursuer should be the one whose four tunables are tuned in the arena before any art, and the Weaver or the Skip can be the partition-ignoring hunter if it moves through vault windows and optional doors.

### 2.16 Curses per hunter

Each hunter carries at least three curses, most four, in Nullscape's design language: a one-to-three-word Title Case name that is a comparative adjective or a plain verb phrase, and a single line stating the mechanical change. Nullscape offers hunter curses only once that hunter is in the run, lets them stack to a cap, and never dresses them up; the [curse list](https://nullscape.wiki/wiki/Curses) is the reference. These replace the current fifteen hunter upgrades, which are all multipliers on the placeholder roster. All curses in this table were accepted on 2026-09-30, with Cake Thief removed and Faulty Bulbs and Wrong Words replaced; the Herald's were added afterwards.

| Hunter | Curse | Effect |
| --- | --- | --- |
| The Echo | Shorter Delay | The Echo replays your path with less delay behind you |
| The Echo | Faster Playback | The Echo replays your path faster than you ran it, gaining on straight sections |
| The Echo | Silent Steps | The Echo's footsteps are quieter and harder to place |
| The Mannequin | Fewer Lamps | Fewer rooms on the floor are lit, so there are fewer places the Mannequin cannot follow |
| The Mannequin | Peripheral Creep | Only a direct look freezes the Mannequin; the edge of your view no longer counts |
| The Mannequin | Longer Strides | The Mannequin covers more ground each time it moves unseen |
| The Mannequin | Broken Lights | A lit room goes dark for the rest of the floor once the Mannequin enters it |
| The Stare | Shorter Window | Less time to find and hold the Stare before it starts a chase |
| The Stare | Quieter Call | The Stare's call is quieter and harder to place |
| The Stare | Sooner Return | The Stare respawns sooner after being scared away |
| The Stare | Wider Wander | The Stare repositions across a wider arc of your view, so it is harder to keep the look held on it |
| The Weaver | Stickier Webs | Web slows last longer |
| The Weaver | Wider Webs | Web projectiles are larger and harder to sidestep |
| The Weaver | Doorway Nests | Some doorways start the floor already webbed |
| The Weaver | Quick Spin | The Weaver's warning before a shot is shorter |
| The Ram | Longer Charge | The Ram charges farther before stopping |
| The Ram | Shorter Windup | The Ram's stamp-to-charge warning is shorter |
| The Ram | Partition Breaker | The Ram's charge breaks thin partitions and optional props |
| The Ram | Second Charge | The Ram turns and charges again once before recovering |
| The Mimic | More Mimics | Extra Mimics per floor |
| The Mimic | Golden Mimic | Mimics can also pose as Golden Cakes |
| The Mimic | Faithless Arrow | The white arrow briefly points at a Mimic now and then |
| The Mimic | Longer Bite | A sprung Mimic holds you for longer |
| The Skip | Shorter Cooldown | The Skip teleports more often |
| The Skip | Quicker Learner | The Skip starts intercepting a doorway after fewer uses |
| The Skip | Wider Reach | The Skip also intercepts at stair heads and drops |
| The Skip | No Tell | The Skip leaves no mark where it appears |
| The Blinder | More Traps | Extra Blinder traps per floor |
| The Blinder | Longer Dark | Blindness lasts longer |
| The Blinder | Muffled Dark | Blindness also muffles your hearing |
| The Blinder | Silent Traps | Blinder traps no longer tick |
| The Ticking | Runs Faster | The Ticking winds down faster |
| The Ticking | Farther Keys | Keys spawn farther from you |
| The Ticking | Loud Keys | Taking a key makes a noise every hunter hears |
| The Ticking | Double Spring | Two keys are needed to fully wind the Ticking |
| The Herald | Longer Deafness | Deafness from the attack scream lasts longer |
| The Herald | Wider Scream | The attack scream reaches farther |
| The Herald | Sharper Ears | Hunters that hear a Herald scream get your exact position instead of a stale one |
| The Herald | Restless Throat | The Herald screams during the chase more often |
| The Herald | Deaf Landing | While deafened by the attack scream, the player cannot run |

The general curses keep the same language when they are rewritten: Lower Gravity, Faster Collapse, No Look-Back, Hidden Count, Wagered Haul, in the pattern of the removals listed in the stakes section.

### 2.17 Non-hunter threats

A threat does not need a body that chases. Nullscape and Grace both keep a large share of their rosters as annoyances: a maintenance task with a clock, a rule about a room, a thing you must answer before it escalates. These raise stress without adding another pursuer to the mix, which is exactly what the Director needs at depth when more bodies would become noise.

What the two wikis describe, as reference:

- Nullscape's [Cadence](https://nullscape.miraheze.org/wiki/Nullscape_Wiki/News) sits in one room and every 25 seconds spawns an instrument somewhere on the map, announced by a global sound. Players must collect the instruments; more than five on the map enrages it and kills. The threat is a collection chore under a clock, which is why the arrow-guided design fits it.
- Grace's [entities](https://grace.miraheze.org/wiki/Entities) include Sin, which in one mode punishes lingering in a room by cracking it and spawning a fruit at the door; the Doombringer, a rat whose scream you must silence in time or it explodes; and one-hit oddities like DUK. See also the [entity counter guide](https://www.gamezebo.com/walkthroughs/grace-entities/).

Principles for WORSEN's version:

- Each non-hunter threat is a rule the player can state in one sentence, with a clock or a count, and a fail state that either kills, summons a hunter, or costs the wallet.
- Collectable-based threats use the white arrow language: a second arrow, or the same arrow in a different state, points at the thing that must be dealt with, so the arrow becomes a stress channel of its own.
- They never require standing still. Winding, collecting, silencing and looking are all done in motion or in under a second.
- They are selectable at the hunter and curse round like any other threat and, like hunters, are always active once chosen; the Director expresses them but never benches them.

Candidates beyond the Marionette already listed: a Cadence-like collector that spawns offerings on a timer and enrages past a count; a lingering rule where staying in any room too long cracks it early; a screamer that must be silenced by touch before it calls every hunter to you; and a counterfeit arrow that occasionally points somewhere wrong for a few seconds, as a curse rather than a body.

### 2.18 Lighting, Lumen and the cake arrow

The arrow misfires for a specific reason, the flashlight is not earning its place, and Lumen 2 is installed but used only as a fake-light flare substitute. The reference image you attached, Lumen's layer-based FX shot, shows what is unused: fanned volumetric rays, stacked glow discs and orbs, warm lantern halos with soft falloff, and flat toon light pools on the ground, all against a near-black backdrop.

| Proposal | What the build does today | Change |
| --- | --- | --- |
| Arrow points the wrong way (approved 2026-09-30) | The direction is the first NavMesh path corner that differs from the player. The first corner of a path is the player's own position projected onto the mesh, so when you are airborne, on stairs, or slightly off the mesh, the arrow points at that projection point, which looks random | Confirmed in the driver: the path is computed from the NavMesh sample of the player's position, but the direction is measured from the raw, unsampled position to the first corner, and that first corner is the sample point itself. Fix: measure from the sample point to the second corner, or skip any corner within about a metre of the origin; when the sample or path fails, fall back to the straight-line direction instead of zero; hold the last good direction across a failed refresh. A unit test that places the origin one metre above the mesh and asserts the direction points along the path, not downward at the sample, will show the fix working before it ships |
| Arrow presentation (approved 2026-09-30) | A 3D compass needle in the HUD extra layer that hides during a chase | One white arrow, always active, including during chases. Threats and traps use the same arrow language: mimics are what the arrow does not point at; collector threats get a second arrow state |
| Flashlight (approved 2026-09-30) | A toggle beam that hunters react to; you report it is not useful | Keeps its light and gains a charged stun, as described in the shop section; the beam still brightens dark rooms |
| Lighting and mood (approved 2026-09-30) | Ambient grey-green, black fog from eight metres, Lumen fake-light flares budgeted to twelve nearest, no per-object treatment | Use Lumen's layered FX as the visual grammar for everything that matters: the cake gets stacked glow layers and a small light pool so it is the one saturated object; the exit door gets fanned rays through its frame that intensify as the exit opens; torches get lantern halos with soft ground pools; the fog boundary gets a faint cold glow only where it is thin. Everything else stays dark. Objects around the map can carry a low toon rim where a silhouette must read at look-back speed. The same grammar applies per theme with different sources: torches in the castle, flickering fluorescents in a hospital, lanterns or moonlight in a forest |
| Toon and horror together (approved 2026-09-30) | Lumen's stylised look is unused | Keep the plausible-realism darkness from the art target and let Lumen supply only the light itself: stylised light in a real-material dark room reads as uncanny, which is the tone. The reference image is a catalogue of Lumen's options, not a target to copy: use whichever of its features fits each object, and do not reproduce the orb merely because it is in the shot |

## 3. Constraints and non-negotiables

- Engineering follows [SPEC-001](SPEC-001-project-architecture-guidelines.md): injected time and randomness, layer dependencies, designer configuration separated from runtime state, paired lifecycle and events, deterministic asset wiring, and the verification gates. Nothing here relaxes those.
- The five current hunter profiles (watcher, rusher, lurker, hexer, thorncaller) are placeholders. The GOAP planner and fact model, factory and registry, per-archetype motor, attack and animation configs, trait flags, light-response hooks, animation driver and Final IK are kept; the roster is rebuilt from the briefs in the requested hunter designs and curses subsections.
- Every chosen hunter is active on every floor. There is no body budget; the Director manages pressure and hints, never presence.
- Hide state, never rules: no markers, no text that reports chase state, and one exact, always-on white arrow. Enemies and traps that imitate cakes are told apart only by where the arrow does not point.
- Audio is minimal by decision, and every environmental sound the player hears is a noise event hunters hear with the same occlusion; interface sounds and music are excluded. Repeated sounds vary in pitch and timing except where timing is a tell.
- The exit room never collapses. Fog is room by room with blended leaks; distance from the exit decides only which room collapses next.
- Crouch is a posture for sliding and low gaps only; it has no stealth speed or loudness.
- The player holds three consumables by default; upgrades may add slots. Purchases with full slots force a replacement. There are no relics as a category; "relic" names the design language of upgrades, which have no rarity, only a floor of availability and optional hunter requirements.
- Health is full at the start of every floor and regenerates slowly within it; health does not carry between rounds. A hit grants a grace window through hunters and a severity-scaled speed boost.
- Deferred, not requirements now: declining a hunter at selection; bring-in items across runs; the wallet wager; the Shrine of Purgatory revive form. All are co-op or later-design features.

## 4. Rationale

The owner's two design conversations settled the reasoning this spec applies: hunters stay frightening through gait, approach, sound signature and the catch rather than their face; stake-fear survives repetition where horror-fear decays, so the run itself must have something on the table; a clean all-clear cue drains dread, so music never fully resolves; the catch ends the run and is ugly; "Make it Worse" is a ladder of fear axes (information, unpredictability, stakes, agency, time) the player climbs by choice, with stakes climbing faster than lethality; lore is inferred from hunter behaviour and level imagery, never read; and the art target is plausible realism in darkness with a degradation layer, not lo-fi. The code survey found the prototype's defaults pulling the other way (pellet-dense required cakes, exact GPS-style guidance, a visible health bar, a labelled chase state, a combo sting, hunters that run to every hint at chase speed), and each proposal here is the smallest change that moves a default toward those conclusions.

The priority order from the source page:

If only a handful of items land this month, these six move the feel the most per hour of work:

1. Slow the approach and add Stalk, with per-hunter presence loops through occlusion.
2. Cut cake density and move cakes onto the unused anchor types.
3. Remove the HUNTED label, hide health in the run, and take the clean off-state out of the music.
4. Make the look-back a costed snap.
5. Replace the death snap with a held, loud catch.
6. Add the early bail with a penalty so the freeze has an honourable exit.

From the 2026-09-30 playtest notes, the three that unblock testing everything else: the hit grace through hunters, the arrow fix, and stairs to ramps. The fog-of-war field and the relic catalogue are the largest pieces of new work on this page and belong in their own plans.

## 5. Open questions

| Question | Why it matters | Owner | Needed by |
|----------|----------------|-------|-----------|
| Does the Mannequin move only in darkness (lit rooms safe, rare light failure as the subversion) or only in light? The brief records the darkness reading with the alternative noted. | Decides which rooms are refuges and what the subversion is. | Hao Guo | Mannequin brief before modelling |
| Does the Mimic curse "Faithless Arrow" stay? It breaks the rule that the arrow never points at a Mimic, as a deliberate information-axis worsening. | Conflicts with the arrow-as-trust decision unless accepted as a curse exception. | Hao Guo | Curse catalogue approval |
| Concrete numbers: shop prices and availability floors, grace window and speed-boost durations, regeneration rate, shrine counts per floor, hunter tunables per new hunter. | The spec fixes rules, not values; values are tuned in the arena and shop per the GDD. | Hao Guo | Implementing plans |
| Which two themes form the first level-diversity slice (castle plus hospital is proposed). | Determines the first module catalogue and light-source set. | Hao Guo | Level plan |
| Cadence of shrines versus the eight-floor progression events, and whether both fire on the same floor. | Avoids stacking two worsening sources on one floor by accident. | Hao Guo | Progression plan |

## 6. Plans implementing this spec

None yet. Candidate plans, in the order the source page's priority list suggests: hunter roster rebuild (briefs, felt intelligence, Stalk and Retreat, sound identity); movement and traversal fixes (hit grace and on-hit boost, vault and slide rework, ramps, ledge climb and boost, corner-stall investigation); collapse, fog and grabs; cakes, arrow and interface; shop, upgrades, consumables, curses and shrines; level diversity, gaps and multi-floor generation (with SPEC-003). Register each with `$docs-plans new plan` when execution is approved.

## 7. History

| Date | Change | By |
|------|--------|----|
| 2026-09-30 | Created from the reviewed Claude Doc after the owner's comment pass; approved and deferred marks carried inline. Registered DRAFT. | $docs-plans new spec |
