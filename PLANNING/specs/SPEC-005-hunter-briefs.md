---
id: SPEC-005
type: spec
title: Hunter briefs
status: LIVE
created: 2026-09-30
updated: 2026-09-30
owner: Hao Guo
supersedes: none
superseded_by: none
source: none
archived: none
---

# SPEC-005 — Hunter briefs

> Status: LIVE since 2026-09-30 (approved by Hao Guo). See [../index.md](../index.md).

## 1. Subject and scope

Owner-review briefs for [PLAN-016][p16] and [PLAN-017][p17], grounded in the checkout containing `ddd2072`. They elaborate [SPEC-004 §2.2, §2.15–§2.17][s4], not an approval to model, tune or integrate. Working archetype labels are identifiers, not lore names. Identity proposals describe silhouette, gait and distant approach; no face or backstory is required.

Amended 2026-09-30: the pre-approval wording above is superseded by the owner's LIVE approval in §5. Source/build observations remain dated evidence, not proof of current integration or arena/art acceptance. Earlier per-brief questions about gates, Skip damage, Ticking's slot, silhouettes and Mannequin catch are resolved by §5; tuning and runtime acceptance remain separate.

“Built” below means source implementation exists, not that it passed a live Unity test. “Stubbed” means a contract, emitted fact or setup recipe exists without a complete consumer/content path. “Missing” means the inspected checkout lacks the named content or integration. No Unity operation was performed. Each brief is a compact standalone page of review content; references and shared evidence are outside its page budget.

## 2. Intended behaviour

### 2.1 Roster overview and reading key

| Hunter / id | Kind and distinguishing rule | Provisional novelty data | Current build status |
|---|---|---|---|
| Echo / `echo` | Pursuer; delayed recorded route | Depth 1 schema default | Built rules and assets; selection integration missing |
| Weaver / `weaver` | Pursuer; ceiling body, warned web | Depth 1 schema default | Built rules and assets; distinctive sounds missing |
| Ticking / `ticking` | Annoyance; moving maintenance clock | Depth 1 schema default | Built body/keys; not a bodyless Director threat |
| Ram / `ram` | Pursuer; committed straight charge | Depth 4 setup default | Built rules; profile asset and cross-system facts unwired |
| Skip / `skip` | Annoyance; reused-route intercept | Depth 6 setup default | Built rules; traversal/mark integration stubbed |
| Mimic / `mimic` | Annoyance; false cake | Depth 5 setup default | Built rules; disguise/arrow integration stubbed |
| Blinder / `blinder` | Pursuer; traps and blindness throw | Depth 5 setup default | Built rules; trap/effect/audio consumers incomplete |
| Herald / `herald` | Pursuer; broadcasts and deafens | Depth 6 setup default | Built rules; scream routing/content missing |
| Mannequin / `mannequin` | Pursuer; darkness and observation gate | Depth 4 setup default | Built rules; world observation/light integration incomplete |
| Stare / `stare` | Annoyance; attention window, then pursuit | Depth 6 setup default | Built rules; placement/audio integration incomplete |
| Watcher / `watcher` | Legacy pursuer; light memory | No novelty gate; depth 1 default | Built legacy profile, still in selection |
| Rusher / `rusher` | Legacy pursuer; close lunge | No novelty gate; depth 1 default | Built legacy profile, still in selection |
| Lurker / `lurker` | Legacy pursuer; beam dodge | No novelty gate; depth 1 default | Built legacy profile, still in selection |
| Hexer / `hexer` | Legacy ranged hunter; bolts | No novelty gate; depth 1 default | Built legacy profile, still in selection |
| Thorncaller / `thorncaller` | Legacy ranged hunter; ground eruption | No novelty gate; depth 1 default | Built legacy profile, still in selection |

All numerical tuning in this document is **provisional**, including values already serialized. Each brief cites its profile/config source. Settled owner rules in §3 are not provisional. Inertia means acceleration (m/s²) and turn rate (degrees/s); commitment separates planner hold from attack timing; ratio normally means chase speed/player sprint speed. Echo instead scales recorded playback time. Ordinary loss requires **both** no sight for the stated time and distance **greater than** the stated separation ([ChaseController][chase]); it does not despawn the hunter.

**Evidence boundaries.** [HunterProfile][profile] supplies absent serialized fields. Echo, Weaver and Ticking have checked-in profile/rule assets. The other seven new hunters have config classes and deterministic setup recipes, but no corresponding profile assets were found under the mirrored Hunter asset tree. Recipe values are not claimed as live asset values. `MinimumDepth` has no production reader outside its declaration in this checkout; no lifetime-run gate was found. All ten threat catalogue rows have availability 1, while [ProgressionConfig][progression] and [its asset][progression-asset] still select the five legacy ids. [ProgressionSessionController][selection] chooses from that list, not automatically from catalogue threat rows. Thus the overview's later depths are authored intent, not working first appearances.

**Sound evidence key.** Slots are `Presence`, `Detection`, `ChaseLayer`, `AttackTiming`, `DeathSting`. “Generic assigned” means a fallback bank has non-null serialized clip references in [AudioSoundscapeDriverConfig.asset][audio-asset] (also [AudioDriverConfig.asset][audio-old]); it does not mean a distinctive clip or live playback was verified. [Binding defaults][audio-config] have null direct clips; [AudioRosterPresenter][audio-roster] falls back only for standard dotted suffixes, and [AudioSoundscapeDriver][audio-driver] deliberately leaves `Placeholder` bindings silent. The asset has no serialized `_rosterBindings` override. Dedicated Ram/Mimic/Blinder/Herald/Stare facts are emitted by [HunterManager][manager] but no production subscribers to those audio facts were found. Each brief distinguishes that gap from intentional silence.

**Habits.** The shared set is threshold pause, turn toward last-known position on loss, and nearby cake reaction; [HunterRuleData][habits] provisionally sets pause 0.4 s and radius 15 m. [HunterController][shared] gates habits by state: this is not a promise to pause during every committed attack. Where a module replaces shared pursuit, the three listed habits are observable module rules rather than invented profile entries. Mutation pools are recorded separately from selectable curses.

### 2.2 Echo

- **Identity:** proposal—narrow, covered upright silhouette; gait repeats the player's rhythm like a bad recording. It arrives along a route the player already crossed, never by a new shortcut. Current profile reuses a legacy body, not this art.
- **Rule, tell and counter:** replay sampled positions in order after a provisional 4 s delay; delayed, slightly wrong footsteps disclose its route. Never reverse into the trail. Close a door or take a hunter-forbidden drop; it holds rather than rerouting. “Never backtracks” means its recording cursor never rewinds, not that it cannot follow a loop the player recorded.
- **Four tunables:** [EchoProfile][echo-profile]: inertia 20 / 240; planner commitment 0.5 s, shared lunge windup 0.25 s / active 0.3 s / recovery 0.8 s; playback ratio 1.0. [EchoConfig][echo-config] sets `NeverLoses=true`: profile loss 2.5 s / 14 m is overridden, not counterplay.
- **Habits, built:** repeats recorded footfalls; waits at closed doors without seeking a detour; after an inaccessible leg or off-trail displacement waits for a later trail intersection. Profile `_habits` is empty. Door-passage facts exist for the environment hand-off.
- **Sound identity:** Presence `echo.footstep` → Footstep, generic assigned (gain 1, pitch 0.94 in [rule asset][echo-rules]); Detection `echo.detection`, generic assigned; ChaseLayer `echo.chase`, generic assigned fallback, no distinct layer; AttackTiming `echo.attack`, generic assigned; DeathSting `echo.death`, generic assigned. Shared feedback and the catch supply the latter slots; not five unique authored recordings.
- **Curses:** catalogue-backed `echo-shorter-delay` multiplies delay by 0.75; `echo-faster-playback` multiplies playback by 1.25; `echo-silent-steps` multiplies footstep gain by 0.5 per stack. Cap 3 each, from [rule asset][echo-rules] and [catalogue][catalogue].
- **Novelty gate:** depth 1 schema default, no lifetime condition; current selection gap in §2.1. Mutation: ratio 1.1, tell `echo.quickened-recording` in profile; cue is a no-clip placeholder.
- **Build status:** built [EchoController][echo-controller], profile and rules; silhouette stubbed, distinctive audio missing. Off-trail spawn waits indefinitely unless a later recorded segment intersects it; not proven first-contact delivery.
- **Open questions:** can the spawn/trail rule reliably produce the first encounter? Approve the silhouette and arena tuning before modelling; test door closure and drop counterplay live.

### 2.3 Weaver

- **Identity:** proposal—wide, jointed overhead outline, not necessarily a spider; deliberate ceiling skitter that drops before firing. The reveal belongs above a doorway, not at face height.
- **Rule, tell and counter:** finds a radius-swept clear firing line, repositions when blocked, drops and warns before launching a faintly glowing slowing web. Break that line or cross it before release; slow must not disable sliding. Physical implementation follows floor navigation with an elevated body, not a free three-dimensional ceiling pathfinder.
- **Four tunables:** [WeaverProfile][weaver-profile]: inertia 16 / 200; planner commitment 0.6 s; chase ratio 1.05; loss 2.5 s and >14 m. [WeaverConfig asset][weaver-rules]: shot warning 0.7 s, cooldown 3 s, projectile radius 0.06 m, slow multiplier 0.55 for 2.5 s. Shared close lunge remains separate.
- **Habits, built:** threshold pause; turn toward last-known position; nearby cake reaction (profile set); leaves glowing webs at passed doorways ([WeaverController][weaver-controller]).
- **Sound identity:** Presence `weaver.skitter`, no clip, explicit placeholder; Detection `weaver.detection`, generic assigned; ChaseLayer `weaver.chase`, generic assigned fallback, distinct layer missing; AttackTiming `weaver.wet-click`, no clip, explicit placeholder (shared close attack can use `weaver.attack`); DeathSting `weaver.death`, generic assigned. Skitter interval 1.2 s is provisional in the rule asset.
- **Curses:** catalogue-backed `weaver-stickier-webs` duration ×1.5; `weaver-wider-webs` radius ×1.25, hard-clamped to 0.1 m; `weaver-doorway-nests` seeds doors at chance 0.25 per stack; `weaver-quick-spin` warning ×0.75. Cap 3 each ([config][weaver-config], [catalogue][catalogue]); radius clamp is in the controller.
- **Novelty gate:** depth 1 schema default, no lifetime condition; not scheduled novelty yet. Profile mutation: ratio 1.15, tell `weaver-quickened-skitter`, no clip.
- **Build status:** built rules, sweep/web driver and assets; [motor asset][weaver-motor] navigation mask 9 admits special areas, but thin-partition/door routes require authored geometry and live verification. Model reused; defining sounds missing.
- **Open questions:** does elevated floor navigation read as ceiling movement? Do special routes actually cross optional partitions without violating required walls? Approve readable web/shot art only after this counterplay works.

### 2.4 Ticking

- **Identity:** proposal—compact asymmetric clockwork silhouette that keeps a rear pocket, never a stationary instrument. Quiet ticks arrive before a body reveal; no tune.
- **Rule, tell and counter:** provisional spring 45 s; a key becomes due after 30 s, at 6–10 m from the player, with its own threat arrow. Tick intervals stretch from 0.35 to 2 s; silence precedes the wake. Take keys in motion, before winding becomes urgent ([TickingConfig asset][ticking-rules]).
- **Four tunables:** [TickingProfile][ticking-profile]: inertia 20 / 240; commitment 0.5 s, shared lunge 0.25 s windup / 0.3 s active / 0.8 s recovery; hunting ratio 1.12; ordinary chase loss 2.5 s and >14 m. Dormant follow uses 1.2 sprint ratio outside its 12–18 m rear band ([config][ticking-config]). Sight loss does not rewind the spring; only a key restores dormancy.
- **Habits, built:** holds behind rather than orbiting a stationary player; slows its ticks as charge drains; offers only one live key and rejects unavailable placements; profile threshold pause. These are maintenance rules, not a Director decision to bench it.
- **Sound identity:** Presence `ticking.tick`, `ticking.winding`, `ticking.key-appeared` share the slot—all no-clip placeholders; Detection `ticking.wake` → generic assigned; ChaseLayer `ticking.chase`, generic assigned fallback; AttackTiming `ticking.attack`, generic assigned; DeathSting `ticking.death`, generic assigned. `Stop` stops voices, not an extra cue.
- **Curses:** `ticking-runs-faster` spring ×0.8; `ticking-farther-keys` distance ×1.25 (cap 3 each); `ticking-loud-keys` emits collection noise; `ticking-double-spring` needs a half-wind then a full-wind key (cap 1 each). [Catalogue][catalogue] and [controller][ticking-controller]. Loud Keys routing must respect the owner noise distinction in §3.
- **Novelty gate:** depth 1 schema default; no lifetime condition. Profile mutation pool empty.
- **Build status:** built [controller][ticking-controller], key/contact/manager stack and assets; tick identity missing. This is a moving body that can hunt, not the originally requested bodyless systemic slot.
- **Open questions:** approve this interpretation of PLAN-016's third threat or commission a separate systemic rule? Are key detours fair during collapse? Audio is required to judge the rule.

### 2.5 Ram

- **Identity:** proposal—broad, low-front silhouette, heavy approach and planted stamp; a sudden straight rush makes corridor geometry the threat. No facial reveal required.
- **Rule, tell and counter:** stamps, locks heading, bellows, then charges without turning. Sidestep or bait a wall; wall contact stops and staggers it even with Partition Breaker.
- **Four tunables:** [RosterBProfileSetup][setup-b] authors [HunterProfile][profile] fields: inertia 8 / 90; planner commitment 1 s; chase ratio 1.05; loss 2.5 s and >14 m. [RamConfig][ram-config]: warning 1 s; charge 18 m at 18 m/s; stagger 1.2 s. These are recipe/class defaults, not a checked-in Ram profile.
- **Habits, built:** threshold pause; turn on loss; nearby cake reaction (recipe's shared set); stamp and hold a locked line before each charge. The wall stagger is its environment signature ([RamController][ram-controller]).
- **Sound identity:** Presence `ram-stride` every 3 m of charge; Detection `ram.detection` generic assigned; ChaseLayer `ram-bellow`; AttackTiming `ram-stamp` and `ram-impact`; DeathSting `ram-win`. Hyphenated module ids have no assigned clips/bindings or production audio consumer; standard detection fallback has a generic bank only. Slot grouping is proposed for module facts, not implemented mapping.
- **Curses:** hook-only, absent from catalogue: `ram-longer-charge` distance ×1.25; `ram-shorter-windup` warning ×0.8 (controller caps numeric stacks at 3); `ram-partition-breaker` emits an optional breakable impact; `ram-second-charge` adds one freshly warned charge when sight remains. Boolean hooks test presence, not repeated extra charges.
- **Novelty gate:** provisional depth 4 in setup, not enforced; no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built charge controller and swept motor path; setup stubbed, profile/art/audio missing. Partition-impact facts still need Level routing; emission alone does not break a wall.
- **Open questions:** does the stamp give enough lateral escape time? Can a second warning be read beside other hunters? Integration owner must bind impact and sound facts before acceptance.

### 2.6 Skip

- **Identity:** proposal—small, crooked, slowly shifting upright shape found at a familiar threshold; silent relocation, with no teleport flash. The changed position is the reveal.
- **Rule, tell and counter:** learns completed doorway/window uses this floor; after a provisional 3 uses and 12 s cooldown, proposes an intercept at a heavily reused anchor. Vary routes and learn the mark; unavailable destinations are rejected ([SkipConfig][skip-config], [SkipController][skip-controller]).
- **Four tunables:** [RosterBProfileSetup][setup-b]: inertia 4 / 90; commitment 0.6 s; stored chase ratio 0.2, but actual module walking is patrol speed 1.5 m/s; loss fields 0 s / 0 m are unused for this always-dormant shared-chase interface. It forgets learned routes at floor reset, not via a chase-loss timer.
- **Habits, built:** walks toward the learned anchor, not the player's live position; prefers most-used eligible routes with seeded tie-breaking; marks only a successfully placed arrival. Threshold pause is the recipe's only shared habit. The mark is an emitted environment fact, not verified visible art.
- **Sound identity:** Presence, Detection, ChaseLayer and AttackTiming deliberately silent in the module; no assigned dedicated clips. Teleport and mark never emit audio. DeathSting has no module cue; generic `skip.death` fallback would have a bank if a catch were routed, but a Skip catch is not built. Resolve that gap rather than invent a sting.
- **Curses:** hook-only, absent from catalogue: `skip-shorter-cooldown` interval ×0.8; `skip-quicker-learner` subtracts uses down to minimum 1 (numeric hooks capped at 3); `skip-wider-reach` admits stair heads/drops; `skip-no-tell` suppresses mark facts.
- **Novelty gate:** provisional depth 6 recipe, not enforced; no lifetime condition. Mutation pool empty.
- **Build status:** built learning, placement acknowledgement and slow-movement rules; profile missing. Traversal feed, mark rendering and a damaging interception outcome are not established by the module. Shared pursuit/lunges are suppressed.
- **Open questions:** what exactly does reaching its intercept cost the player? Approve that outcome before calling this a finished threat. Can No Tell remain learnable from route repetition alone?

### 2.7 Mimic

- **Identity:** cake silhouette, stationary and visually indistinguishable before touch; the reveal is the bite, not a face. Golden disguise is curse-controlled.
- **Rule, tell and counter:** poses at its spawn point, springs one bite, then stays spent. Ordinarily trust the white arrow rather than greed. **Faithless Arrow is an ordinary selectable Mimic curse**, a named exception to that trust rule—not an unresolved owner decision.
- **Four tunables:** [RosterBProfileSetup][setup-b]: inertia 0 / 0; stored commitment 1.2 s; speed ratio 0; loss 0 / 0, inapplicable to stationary non-pursuit. [MimicConfig][mimic-config]: bite hold 1.2 s, damage 25, touch radius 0.65 m; these replace any meaningful chase/lunge tuning.
- **Habits, built:** never moves to tempt the player; keeps a seeded disguise roll for its life; bites only once and removes its pose. Shared habits are empty. The environmental habit is occupying a false collection site, not generating a real cake.
- **Sound identity:** Presence, Detection and ChaseLayer intentionally silent; no clips assigned. AttackTiming `mimic-wrong-bite`; DeathSting `mimic-win`; both unbound/no clips. These slot assignments are proposed mappings of existing facts, not verified playback.
- **Curses:** hook-only, absent from catalogue: `mimic-more-mimics` publishes extra population count, capped at 3; `mimic-golden-mimic` enables provisional 0.25 golden chance; `mimic-faithless-arrow` emits a 2 s window every 20 s; `mimic-longer-bite` hold ×1.25, cap 3 ([controller][mimic-controller], config). Binary hooks do not intensify with extra copies.
- **Novelty gate:** provisional depth 5 recipe, not enforced; no lifetime condition. Mutation pool empty.
- **Build status:** built pose/bite/population facts and normal hit relay; disguise rendering, population and arrow consumers stubbed. Config `_allowFaithlessArrow=false` still blocks its ordinary-curse rule: implementation debt, not policy. Profile/art/audio missing.
- **Open questions:** how will the brief betrayal remain recognisable without adding an unapproved extra warning? Check accepted-hit/grace handling before applying bite holds. Remove the obsolete approval switch through the owning implementation task.

### 2.8 Blinder

- **Identity:** proposal—narrow, stooped throwing silhouette with a slow setup and abrupt arm action; danger is floor evidence and a hiss through fog, not its face.
- **Rule, tell and counter:** lays blinding traps through Floor policy and throws along a freshly swept clear line after repositioning. Watch the floor and break line of sight during warning; preserve momentum along a known route when blinded.
- **Four tunables:** [BlinderHeraldProfileSetup][setup-bh]: inertia 16 / 200; commitment 0.6 s; ratio 1.05; loss 2.5 s and >14 m. [BlinderConfig][blinder-config]: throw warning 0.7 s, cooldown 4 s, range 16 m, radius 0.06 m, speed 12 m/s, blindness 3 s. No profile asset yet.
- **Habits, built:** threshold pause; turn on loss; nearby cake reaction; holds still for its warned throw ([controller][blinder-controller]). Traps are the intended environment evidence, but a policy fact is not a placed trap.
- **Sound identity:** Presence `BlinderSound.Presence` (6 s cadence); Detection `.Detection`; ChaseLayer `.Chase` (2 s cadence); AttackTiming `.ThrowHiss` plus `.TrapTick`; DeathSting `.Catch`. These are enum facts, **not clip ids**: no dedicated mapping/clip assignment found. Standard `blinder.*` feedback may borrow generic banks, but cannot supply the missing trap/throw identity. Intervals come from config.
- **Curses:** `blinder-more-traps` and `blinder-silent-traps` have catalogue rows (cap 1 there). `blinder-longer-dark` and `blinder-muffled-dark` are hook-only. Controller publishes extra-trap stacks, duration ×1.5 per Longer Dark, muffling and silent-tick flags; numeric hook cap 3 in config. Catalogue/runtime cap disagreement is unresolved, not silently harmonised.
- **Novelty gate:** provisional depth 5 recipe, not enforced; no lifetime gate. Mutation ratio 1.15 with `blinder-quickened-approach`, no assigned tell clip.
- **Build status:** built independent sweep/throw logic and trap-policy facts; Floor and sensory/audio delivery not proven and no production subscribers to the dedicated facts found. Profile/model missing; ordinary ground navigation intentionally does not borrow Weaver partition access.
- **Open questions:** reconcile trap stack caps and wire effects before tuning darkness. Can trap ticks and the throw hiss be separated by ear without exceeding its cue budget?

### 2.9 Herald

- **Identity:** proposal—tall, open-chested silhouette, walking approach interrupted by a full-body breath; sound reaches the player before the shape. No facial detail needed.
- **Rule, tell and counter:** screams broadcast player clues to other hunters/Director; warned radius scream damages lightly and deafens. Break sight early to stop later calls, but a committed attack continues after sight breaks: outrange it. This specific gameplay broadcast is not ambient world-noise forwarding.
- **Four tunables:** [setup][setup-bh]: inertia 14 / 200; commitment 0.6 s; ratio 1.02; loss 2.5 s and >14 m. [HeraldConfig][herald-config]: warning 0.8 s, radius 7 m, damage 10, deafness 4 s, attack cooldown 5 s; chase spacing 2.5–4 s, non-attack pitch variation ±0.06; attack pitch fixed at 1 in [controller][herald-controller].
- **Habits, built:** shared threshold pause, loss turn and cake reaction; alternates two chase calls and plants itself for the attack breath. Threshold occupation is its current environment habit, not a new destructive power.
- **Sound identity:** Presence has no dedicated loop; generic `herald.presence` bank only. Detection `ms_mangled_scream_03`; ChaseLayer alternating `sb_mangled_scream_01` / `sb_mangled_scream_03`; AttackTiming `HeraldBreathFact` followed by `sb_mangled_scream_02`; DeathSting intended reuse of attack file (setup comment), no implemented dedicated catch mapping. Named files are constants, not assigned AudioClips. The SPEC-004 imported directory and matching files were not found in this checkout; all dedicated assignments are missing.
- **Curses:** hook-only, absent from catalogue: `herald-longer-deafness` duration ×1.5; `herald-wider-scream` radius ×1.2; `herald-sharper-ears` uses exact rather than last-known clue; `herald-restless-throat` cadence ×0.75; `herald-deaf-landing` requests sprint denial while deafened. Numeric cap 3; binary flags test presence.
- **Novelty gate:** provisional depth 6 recipe, not enforced; no lifetime gate. Mutation ratio 1.15, tell `herald-quickened-approach`, unbound.
- **Build status:** built screams/breath/hit facts, not their floor-wide, deafening or audio delivery. Profile and dedicated assets missing.
- **Open questions:** restore the fixed sound source and route broadcasts before judging fairness. Verify the loudest-scream/no-duck rule with several retained hunters; do not infer it from emitted facts.

### 2.10 Mannequin

- **Identity:** proposal—rigid, covered upright silhouette; no idle fidget. Distance closes in unseen steps, with pose changes rather than a facial scare.
- **Rule, tell and counter:** moves **only in darkness while unobserved**; lit rooms are refuges, direct illumination freezes it, and Wick always freezes it. Unknown camera/room data safely holds. Look to pin it, then cross between lights. Rare ordinary light failure is not permission to override Wick.
- **Four tunables:** [ObservedHunterProfileSetup][setup-observed]: inertia 32 / 540; commitment 0.35 s; ratio 1.05; loss 2.5 s and >14 m. [MannequinConfig][mannequin-config]: darkness default true; rare failure check 15 s, chance 0.02, duration 1.25 s. These are class/recipe defaults, not live profile values.
- **Habits, built:** freezes under observation; waits at safe light boundaries; stays silent even while winning. The recipe also authors threshold pause. Broken Lights adds permanent room-darkening facts as a curse, not a fourth unconditional habit ([controller][mannequin-controller]).
- **Sound identity:** Presence, Detection, ChaseLayer, AttackTiming and DeathSting are intentionally silent in its module; no assigned dedicated clips. `SilentSoundSet` is a fact, not a clip. Shared death audio must be checked so fallback does not accidentally violate this silence; see §5's catch question.
- **Curses:** hook-only, absent from catalogue: `mannequin-fewer-lamps` lamp budget ×0.8, cap 3; `mannequin-peripheral-creep` direct-look half-angle 12°; `mannequin-longer-strides` speed ×1.2, cap 3; `mannequin-broken-lights` permanently darkens entered lit rooms. Binary observation/break hooks cap at 1. Wick is evaluated before light-breaking.
- **Novelty gate:** provisional depth 4 recipe, not enforced; no lifetime gate. Mutation ratio 1.15, tell `mannequin.long-step`; its visual delivery is not built by a string id.
- **Build status:** built hold/light/observation logic; setup and facts exist, production observation/light consumers incomplete. Profile, intended model and visible mutation tell missing. Reverse-light config remains in code, but the owner has settled darkness-only.
- **Open questions:** can light refuges and rare failure be read without false safe signals? Verify Wick priority and missing-data hold in the real level. Darkness direction itself is closed, not an open design choice.

### 2.11 Stare

- **Identity:** proposal—thin, asymmetrical covered shape at the view edge, shifting only until pinned by attention. It must read while the player keeps moving; no eye or face detail carries the rule.
- **Rule, tell and counter:** normal cue “I see you,” then a half-window repeat; locate and hold the look for provisional 1 s within an 8 s window, dismissing it for 25 s. Rare “Find me” (chance 0.03) places it outside view and holds it there. Failing the window starts harder-to-lose pursuit ([StareConfig][stare-config]).
- **Four tunables:** [setup][setup-observed]: inertia 20 / 240; commitment 0.5 s; ratio 1.12; base loss 2.5 s and >14 m, both multiplied by 2 when hunting (config). Attention placements use provisional distance 6 m, view-edge fraction 0.82, look half-angle 8°; they are not chase motion.
- **Habits, built:** occupies the forward view edge but stops repositioning while looked at; repeats the current call halfway; disappears and returns on cadence; pauses at thresholds (recipe's shared environment habit). It rejects placements in closed/unavailable rooms ([controller][stare-controller]).
- **Sound identity:** Presence initial `stare.i-see-you` or `stare.find-me`; Detection the same line halfway; ChaseLayer `stare.chase`; AttackTiming `stare.attack`; DeathSting `stare.catch`. No dedicated clips or production Stare-fact audio consumer found. The dotted chase/attack names could borrow generic banks if routed; the voice/catch names cannot. Do not count that possibility as assigned voice content.
- **Curses:** hook-only, absent from catalogue: `stare-shorter-window` window ×0.8; `stare-quieter-call` gain ×0.7; `stare-sooner-return` interval ×0.8; `stare-wider-wander` edge fraction spread +0.04 per stack, reseeded every 2 s. Controller cap 3; the effective window never falls below the required hold.
- **Novelty gate:** provisional depth 6 recipe, not enforced; no lifetime gate. Mutation ratio 1.2, tell `stare.quickened-gaze`, unbound.
- **Build status:** built attention/placement acknowledgement and harder loss rules, plus driver; profile, voice content and complete camera-to-world integration missing.
- **Open questions:** is an uninterrupted look feasible on stairs/collapse routes? Does the rare line make searching fair? Approve the non-facial silhouette only after placement and voice are legible.

### 2.12 Watcher — legacy evidence, not retained design approval

- **Identity:** existing imported horned upright silhouette with walk/run clips; deliberate light-investigation and cut-off approach. Retire rather than invest in facial detail or backstory ([HorrorHunterSetup][legacy-setup]).
- **Rule, tell and counter:** follows remembered illumination, predicts routes and lunges. Its turn/approach and warned reach are the tell; break sight, change route rather than repeat a loop, and sidestep the lunge.
- **Four tunables:** [watcher.asset][watcher]: inertia 20 / 240; windup 0.55 s, active 0.3 s, recovery 1.3 s; ratio 1.02. Planner commitment 0.5 s and loss 2.5 s / >14 m come from absent-field defaults in [HunterProfile][profile], not serialized values.
- **Habits, built shared defaults:** threshold pause, last-known loss turn, nearby cake reaction ([habits][habits], [shared controller][shared]); no distinct authored legacy habit set. Threshold behaviour supplies the environment interaction.
- **Sound identity:** Presence `watcher.presence`, Detection `watcher.detection`, ChaseLayer `watcher.chase`, AttackTiming `watcher.attack`, DeathSting `watcher.death`: generic assigned banks for all, no per-Watcher clip binding. Chase scream is probabilistic attack feedback, not a continuous unique chase layer.
- **Curses:** [ProgressionConfig][progression] legacy entries, not the new catalogue: `watcher-long-memory` extends sight/light memory; `watcher-cutting-corners` extends prediction horizon; `watcher-unquiet-gaze` increases sight and permits attack screams. Effects still implemented by trait checks in [HunterController][shared].
- **Novelty gate:** no functioning gate; depth 1 profile default, no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built profile, prefab/animation references and shared rules through [DefaultHunterController][default]; still selectable. This is retirement debt under PLAN-016, not a new roster slot.
- **Open questions:** coordinator to remove selection reachability and migrate legacy-dependent tests without damaging the shared machinery; no new model approval requested.

### 2.13 Rusher — legacy evidence, not retained design approval

- **Identity:** existing large hunched creature with run and ready/attack clips; fast closing and a committed reach, not face-based fear ([setup][legacy-setup]). Its prefab is also reused by new profile recipes.
- **Rule, tell and counter:** close-range lunge after a visible windup. Sidestep the committed line and exploit recovery rather than racing down a straight corridor.
- **Four tunables:** [rusher.asset][rusher]: inertia 20 / 240; windup 0.3 s, active 0.3 s, recovery 0.9 s; ratio 1.12. Planner commitment 0.5 s; loss 2.5 s and >14 m from [profile schema][profile].
- **Habits, built shared defaults:** threshold pause, last-known loss turn, nearby cake reaction; environmental threshold pause is state-gated, not a mid-lunge interruption ([habits][habits], [controller][shared]).
- **Sound identity:** Presence `rusher.presence`, Detection `rusher.detection`, ChaseLayer `rusher.chase`, AttackTiming `rusher.attack`, DeathSting `rusher.death`: generic assigned banks, no unique roster clip binding. Attack screams are enabled in the profile; this is not proof of a dedicated chase loop.
- **Curses:** legacy [ProgressionConfig][progression]: `rusher-long-stride` extends reach; `rusher-second-wind` shortens recovery; `rusher-blood-scent` extends hearing range/noise memory. Trait implementation remains in [HunterController][shared], not the new catalogue.
- **Novelty gate:** no gate; depth 1 schema default, no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built legacy content and default-module rules; still selectable. New Echo/Weaver and setup recipes borrow its prefab, so retirement cannot blindly delete the asset.
- **Open questions:** separate “remove legacy selection” from “remove a reused placeholder prefab.” Replace borrowed art only after new briefs and tag tuning are approved.

### 2.14 Lurker — legacy evidence, not retained design approval

- **Identity:** existing short, low creature, walk/run and attack clips; sideways beam response makes its approach discontinuous ([setup][legacy-setup]). No face detail is needed to explain the rule.
- **Rule, tell and counter:** dodges aside when illuminated, then uses a warned lunge. Track the lateral displacement, preserve an escape lane, and bait a miss rather than assuming light is a stun.
- **Four tunables:** [lurker.asset][lurker]: inertia 20 / 240; windup 0.45 s, active 0.3 s, recovery 1.1 s; ratio 1.05. Planner commitment 0.5 s and loss 2.5 s / >14 m from [HunterProfile][profile].
- **Habits, built shared defaults:** threshold pause, loss turn, nearby cake reaction; its beam dodge is an additional repeatable rule, not an authored new-roster habit ([habits][habits], [controller][shared]).
- **Sound identity:** Presence `lurker.presence`, Detection `lurker.detection`, ChaseLayer `lurker.chase`, AttackTiming `lurker.attack`, DeathSting `lurker.death`: generic assigned banks, no unique clips. Provisional attack scream chance 0.15 in the profile; absence of a scream is not all-clear.
- **Curses:** legacy [ProgressionConfig][progression]: `lurker-dark-adaptation` widens sight cone; `lurker-crooked-step` strengthens/lengthens beam dodge; `lurker-stolen-silence` shortens attack warning. Still trait-based in [HunterController][shared], absent from the new catalogue.
- **Novelty gate:** no gate; depth 1 schema default, no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built legacy profile and imported rig through the default module; still selectable, scheduled for retirement.
- **Open questions:** preserve beam-response coverage in fixture tests after retirement; do not reuse this as proof of Mannequin light-refuge behaviour.

### 2.15 Hexer — legacy evidence, not retained design approval

- **Identity:** existing small winged hovering silhouette with fly clips, approaching more slowly than a sprinter ([setup][legacy-setup]). Hovering art does not grant partition access.
- **Rule, tell and counter:** casts a warned travelling bolt toward the route. Break line of sight or step off the firing line. Existing projectile-clearance limitations are why Weaver requires fresh swept evidence.
- **Four tunables:** [hexer.asset][hexer]: inertia 20 / 240; casting windup 0.95 s, active 0.15 s, recovery 1.5 s; ratio 0.83. Planner commitment 0.5 s and loss 2.5 s / >14 m from [profile schema][profile]. Profile projectile speed 11 m/s, radius 0.22 m, range 15 m are provisional.
- **Habits, built shared defaults:** threshold pause, last-known loss turn, nearby cake reaction ([habits][habits], [controller][shared]). No dedicated wall-passing or ceiling habit is built for it.
- **Sound identity:** Presence `hexer.presence`, Detection `hexer.detection`, ChaseLayer `hexer.chase`, AttackTiming `hexer.attack`, DeathSting `hexer.death`: generic assigned banks. No distinctive roster clips; chase scream is disabled in this profile, so its fallback bank is not evidence of an emitted chase layer.
- **Curses:** legacy [ProgressionConfig][progression]: `hexer-split-bolt` fans three bolts; `hexer-hasty-script` shortens warning; `hexer-lingering-hex` makes bolts slower and wider. [HunterController][shared] still applies these trait hooks; no new-catalogue rows.
- **Novelty gate:** no gate; depth 1 schema default, no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built imported content, shared ranged attack and default module; still selectable. It is not the approved partition-ignoring replacement.
- **Open questions:** migrate projectile fixture coverage before retiring selection; no further Hexer model or design work proposed.

### 2.16 Thorncaller — legacy evidence, not retained design approval

- **Identity:** existing broad plant/root silhouette with walk/run and attack clips; slow approach followed by a planted cast ([setup][legacy-setup]). Despite the old “rooted” description it has movement tuning, not a stationary-only rule.
- **Rule, tell and counter:** warns a ground area then raises damaging spikes. Leave the marked region before eruption; keep moving rather than racing the body.
- **Four tunables:** [thorncaller.asset][thorncaller]: inertia 20 / 240; warning 1.15 s, active 0.15 s, recovery 1.7 s; ratio 0.72. Planner commitment 0.5 s and loss 2.5 s / >14 m from [HunterProfile][profile]. Provisional attack range 12 m, spike radius 1.5 m in the asset.
- **Habits, built shared defaults:** threshold pause, loss turn, nearby cake reaction ([habits][habits], [controller][shared]); threshold occupation is the environment habit. Ground warning is attack behaviour, not persistent level destruction.
- **Sound identity:** Presence `thorncaller.presence`, Detection `thorncaller.detection`, ChaseLayer `thorncaller.chase`, AttackTiming `thorncaller.attack` (including SpikeWarning feedback), DeathSting `thorncaller.death`: generic assigned banks, no unique bindings. Profile chase scream disabled; no distinctive chase layer established.
- **Curses:** legacy [ProgressionConfig][progression]: `thorncaller-thorn-ring` adds a ring; `thorncaller-quick-roots` shortens ground warning; `thorncaller-reaching-roots` enlarges eruption. Trait-based [controller][shared] remains; none is a new-catalogue entry.
- **Novelty gate:** no gate; depth 1 schema default, no lifetime condition. Mutation pool empty by schema default.
- **Build status:** built legacy content and ground attack through default module; still selectable, not retained by this draft.
- **Open questions:** keep ground-warning/collider tests as fixtures after selection retirement; no new art requested.

### 2.17 Non-hunter scope boundary

The collector, lingering-room rule, touch-to-silence screamer and counterfeit-arrow candidate in SPEC-004 §2.17 are not additional approved hunter briefs. No new ids, gates or numbers are invented for them here. Ticking covers moving key maintenance, not proof of a bodyless systemic implementation. Counterfeit guidance overlaps the settled Mimic curse; it must not silently become a second independent exception. Generic `HunterProfile`/`FloorLoopHunterProfile` and `DefaultHunterController` are fixtures/shared machinery, not additional authored roster identities.

Amended 2026-09-30: optional collector, lingering-room and touch-screamer are declined. The built moving-body Ticking satisfies the systemic slot; the old suggestion that a separate bodyless implementation needs approval is superseded. Faithless Arrow remains the ordinary Mimic curse, not a second candidate.

## 3. Constraints and non-negotiables

- Owner decisions of 2026-09-30 override older alternatives: Mannequin is darkness-only, lit rooms are refuges, Wick always freezes it; Faithless Arrow is an ordinary Mimic curse. Neither is reopened by stale config/comments.
- Neither false-positive sounds nor world noises reach hunters. Player-origin gameplay noise and explicit mechanics such as Herald broadcasts require deliberate routing; an audible presentation event must not automatically become a hearing stimulus. Older SPEC-004/PLAN-011 environmental-forwarding language is superseded by this owner instruction.
- Every retained hunter is active on every floor, including duplicates. Dormancy/attention/maintenance are that hunter's active rules, not permission for Director benching. Novelty gates govern first admission only.
- The catch ends the run except for once-per-run Extra Life: revive **in place**, with collision grace and temporary immunity. No early bail. Current floor-start revival call and bail/catalogue copy are implementation debt, not alternatives authorised here.
- No lore names/backstory in implementation. Fear must read through silhouette, gait, approach, habits, sound and catch. No face-based dependency. Approve briefs and arena four-tunable evidence before modelling.
- Preserve [SPEC-001][architecture] layers, injected time/randomness, lifecycle and engineering gates. This writing task changes no source, assets, scenes, registry or plan status.

## 4. Rationale

The new modules already encode useful rules, but a class, emitted fact, setup recipe, serialized asset and playable threat are different evidence levels. Reporting their gaps prevents art from masking incomplete counterplay. Legacy entries remain visible here because retirement has not happened in this checkout; documenting them is not permission to keep them. Distinct cues matter most where sound itself teaches the rule: silent tick/skitter placeholders cannot pass that test.

## 5. Open questions

Amended 2026-09-30: the first six historical questions below are superseded as decision requests by the approval list immediately following the table: provisional silhouette/gait/tunables accepted for play tuning; Skip normal hit/grace; built Ticking accepted; round gates fixed; Mannequin snap/crunch; coordinator sound sourcing authorised. Missing content/consumers and play acceptance remain implementation work, not renewed design approval. Hearing is explicitly player movement, Firecracker and player-triggered cake traps; never Pacification, world or false-positive audio. Dedicated hunter-rule broadcasts are not playback-derived stimuli.

| Question | Why it matters | Owner | Needed by |
|---|---|---|---|
| Approve each proposed non-facial silhouette and four-tunable baseline? | Values and art direction are provisional; rules need arena acceptance first. | Hao Guo | Before modelling |
| What is Skip's actual interception penalty? | Current module suppresses shared attacks and supplies no replacement hit. | Hao Guo / PLAN-017 | Before threat acceptance |
| Does Ticking satisfy the third-threat slot, or is a bodyless rule still required? | Moving clock implementation differs from PLAN-016 build-order wording. | Hao Guo / PLAN-016 | Roster approval |
| Which actual first-appearance schedule replaces the unused depth field and all-depth-1 catalogue rows? | Recipe gates and persistence counters do not currently schedule novelty. | Hao Guo / PLAN-016 / PLAN-023 | Before selection integration |
| Can Mannequin's silent catch coexist with the shared hard death sting? | Module says silent even on winning; global catch playback may still sound. | Hao Guo / PLAN-021 / PLAN-022 | Audio acceptance |
| Who supplies missing voice/tick/skitter/Herald assets and routes module facts? | No-clip placeholders and unconsumed facts cannot teach counterplay. | PLAN-017 / PLAN-021 / coordinator | Before mixed-roster playtest |
| Resolve source/asset/catalogue gaps, caps and owner-decision drift? | Faithless switch, old revival/bail, legacy selection and incomplete consumers contradict intended play. | Coordinator and respective owners | Before claiming implementation complete |

### Owner decisions on approval (2026-09-30)

- Run selection uses the ten new hunters only; the five legacy archetypes are retired from selection and remain only for the TagArena/FloorLoop compatibility scenes.
- First appearance uses round gates: Echo, Weaver and Ticking from round 1; Ram and Mannequin from round 4; Mimic and Blinder from round 5; Skip, Herald and Stare from round 6.
- A Skip interception is a normal hit (same damage and grace as other hunters).
- The built Ticking (a moving clock with a body) fills the systemic threat slot.
- The Mannequin's catch plays a short snap or crunch instead of the loud shared death sting.
- Silhouettes, gaits and the four tunables are a provisional baseline, tuned in play.
- Hunter-type curses apply to every instance of that type.
- Voice and sound assets are chosen by the coordinator from installed packs or made in-house, with provenance recorded.

## 6. Plans implementing this spec

Amended 2026-09-30: supersedes the stale DRAFT/registration request; these briefs are LIVE. Implementing plans (their exits are not declared met):

- PLAN-016 — LIVE — [roster foundation][p16]
- PLAN-017 — LIVE — [roster expansion][p17]
- PLAN-023 — LIVE — [admission and curse catalogue][p23]
- PLAN-024 — LIVE — [hunter-gated upgrades](../plans/PLAN-024-shop-upgrades-consumables.md)
- PLAN-021 — LIVE — [audio][p21]

Related catch presentation remains [PLAN-022][p22]; shared routing remains coordinator-owned under [PLAN-011][p11].

### Requests

Amended 2026-09-30: the prior proposed DRAFT registry row is superseded by the single LIVE row in [PLANNING/index.md](../index.md). Registration is no longer outstanding; no implementation exit is implied.

Implementation hand-offs, not changes authorised by this draft: [ProgressionConfig][progression] `_threats` and [selection][selection] admission must replace legacy reachability and consume approved novelty data; [EffectCatalogueConfig][catalogue] `_entries` must reconcile the explicitly labelled hook-only curses/caps; [MimicConfig][mimic-config] `AllowFaithlessArrow` / [MimicController][mimic-controller] `FaithlessEnabled` must reflect the settled ordinary-curse rule. [HunterManager][manager] fact publishers need coordinator-owned consumers rather than edits from this writing task. Preserve borrowed prefabs during retirement.

Amended 2026-09-30: “this draft” above is superseded by LIVE approval. These implementation hand-offs belong to the approved plans listed in §6, not to this documentation-only amendment.

### Verification — config provenance

Every brief's numbers were read from the following sources, not inferred from a live editor. P = [HunterProfile.cs][profile]; H = [HunterRuleData.cs][habits]. The linked setup recipes assign fields on P; their absence as serialized profiles is recorded, not papered over. Common audio assignment evidence is the two audio assets and binding schema linked in §2.1; curse registration evidence is [catalogue source][catalogue] and [serialized catalogue][catalogue-asset].

| Hunter | Config/profile values read |
|---|---|
| Echo | [EchoProfile.asset][echo-profile], [EchoConfig.asset][echo-rules], [EchoConfig.cs][echo-config], P |
| Weaver | [WeaverProfile.asset][weaver-profile], [WeaverConfig.asset][weaver-rules], [WeaverConfig.cs][weaver-config], [WeaverDriverConfig.cs][weaver-driver-config], [WeaverMotorDriverConfig.asset][weaver-motor], P, H |
| Ticking | [TickingProfile.asset][ticking-profile], [TickingConfig.asset][ticking-rules], [TickingConfig.cs][ticking-config], P, H |
| Ram | [RamConfig.cs][ram-config], P/H as assigned by [RosterBProfileSetup.cs][setup-b] |
| Skip | [SkipConfig.cs][skip-config], P/H as assigned by [RosterBProfileSetup.cs][setup-b] |
| Mimic | [MimicConfig.cs][mimic-config], P as assigned by [RosterBProfileSetup.cs][setup-b] |
| Blinder | [BlinderConfig.cs][blinder-config], [WeaverDriverConfig.cs][weaver-driver-config], P/H as assigned by [BlinderHeraldProfileSetup.cs][setup-bh] |
| Herald | [HeraldConfig.cs][herald-config], P/H as assigned by [BlinderHeraldProfileSetup.cs][setup-bh] |
| Mannequin | [MannequinConfig.cs][mannequin-config], P/H as assigned by [ObservedHunterProfileSetup.cs][setup-observed] |
| Stare | [StareConfig.cs][stare-config], P/H as assigned by [ObservedHunterProfileSetup.cs][setup-observed] |
| Watcher | [watcher.asset][watcher], P, H; [ProgressionConfig.cs][progression] curse ids/copy |
| Rusher | [rusher.asset][rusher], P, H; [ProgressionConfig.cs][progression] curse ids/copy |
| Lurker | [lurker.asset][lurker], P, H; [ProgressionConfig.cs][progression] curse ids/copy |
| Hexer | [hexer.asset][hexer], P, H; [ProgressionConfig.cs][progression] curse ids/copy |
| Thorncaller | [thorncaller.asset][thorncaller], P, H; [ProgressionConfig.cs][progression] curse ids/copy |

## 7. History

| Date | Change | By |
|---|---|---|
| 2026-09-30 | Drafted source-grounded briefs, legacy retirement evidence and explicit integration gaps for owner review; recorded settled owner decisions without implementing them. | Hermes, for Hao Guo |
| 2026-09-30 | Approved by the owner (LIVE); owner decisions recorded under §5 | Hao Guo / coordinator |
| 2026-09-30 | Amended 2026-09-30: registered LIVE; corrected §6 implementing plans and closed stale approval questions by reference to §5; recorded declined candidates and hearing origins without claiming runtime acceptance | WP-D, owner-requested $docs-plans |

[s4]: SPEC-004-horror-direction-content-proposals.md
[architecture]: SPEC-001-project-architecture-guidelines.md
[p11]: ../plans/PLAN-011-horror-direction-coordination.md
[p16]: ../plans/PLAN-016-hunter-roster-foundation.md
[p17]: ../plans/PLAN-017-hunter-roster-expansion.md
[p21]: ../plans/PLAN-021-silence-first-audio-hearing.md
[p22]: ../plans/PLAN-022-camera-catch-degradation-lighting.md
[p23]: ../plans/PLAN-023-selection-curses-stakes.md
[profile]: ../../Assets/Scripts/Domain/Hunter/Config/HunterProfile.cs
[habits]: ../../Assets/Scripts/Domain/Hunter/Config/HunterRuleData.cs
[shared]: ../../Assets/Scripts/Domain/Hunter/Controller/HunterController.cs
[chase]: ../../Assets/Scripts/Domain/Chase/Controller/ChaseController.cs
[manager]: ../../Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs
[default]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Default/Controller/DefaultHunterController.cs
[progression]: ../../Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs
[progression-asset]: ../../Assets/Resources/ScriptableObjects/Session/Progression/ProgressionConfig.asset
[selection]: ../../Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs
[catalogue]: ../../Assets/Scripts/Session/Progression/Config/EffectCatalogueConfig.cs
[catalogue-asset]: ../../Assets/Resources/ScriptableObjects/Session/Progression/EffectCatalogueConfig.asset
[audio-config]: ../../Assets/Scripts/Presentation/Audio/Config/AudioSoundscapeDriverConfig.cs
[audio-asset]: ../../Assets/Resources/ScriptableObjects/Presentation/Audio/AudioSoundscapeDriverConfig.asset
[audio-old]: ../../Assets/Resources/ScriptableObjects/Presentation/Audio/AudioDriverConfig.asset
[audio-roster]: ../../Assets/Scripts/Presentation/Audio/Driver/AudioRosterPresenter.cs
[audio-driver]: ../../Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapeDriver.cs
[setup-b]: ../../Assets/Editor/Hunter/RosterBProfileSetup.cs
[setup-bh]: ../../Assets/Editor/Hunter/BlinderHeraldProfileSetup.cs
[setup-observed]: ../../Assets/Editor/Hunter/ObservedHunterProfileSetup.cs
[legacy-setup]: ../../Assets/Editor/Hunter/HorrorHunterSetup.cs
[echo-profile]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Echo/EchoProfile.asset
[echo-rules]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Echo/EchoConfig.asset
[echo-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Echo/Config/EchoConfig.cs
[echo-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Echo/Controller/EchoController.cs
[weaver-profile]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Weaver/WeaverProfile.asset
[weaver-rules]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Weaver/WeaverConfig.asset
[weaver-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Weaver/Config/WeaverConfig.cs
[weaver-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Weaver/Controller/WeaverController.cs
[weaver-driver-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Weaver/Config/WeaverDriverConfig.cs
[weaver-motor]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Weaver/WeaverMotorDriverConfig.asset
[ticking-profile]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Ticking/TickingProfile.asset
[ticking-rules]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Archetypes/Ticking/TickingConfig.asset
[ticking-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Ticking/Config/TickingConfig.cs
[ticking-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Ticking/Controller/TickingController.cs
[ram-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Ram/Config/RamConfig.cs
[ram-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Ram/Controller/RamController.cs
[skip-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Skip/Config/SkipConfig.cs
[skip-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Skip/Controller/SkipController.cs
[mimic-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Mimic/Config/MimicConfig.cs
[mimic-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Mimic/Controller/MimicController.cs
[blinder-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Blinder/Config/BlinderConfig.cs
[blinder-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Blinder/Controller/BlinderController.cs
[herald-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Herald/Config/HeraldConfig.cs
[herald-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Herald/Controller/HeraldController.cs
[mannequin-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Mannequin/Config/MannequinConfig.cs
[mannequin-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Mannequin/Controller/MannequinController.cs
[stare-config]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Stare/Config/StareConfig.cs
[stare-controller]: ../../Assets/Scripts/Domain/Hunter/Archetypes/Stare/Controller/StareController.cs
[watcher]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/watcher.asset
[rusher]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/rusher.asset
[lurker]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/lurker.asset
[hexer]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/hexer.asset
[thorncaller]: ../../Assets/Resources/ScriptableObjects/Domain/Hunter/Expansion/thorncaller.asset
