# PLAN-021 audio pass 3 — partial content completion; offline gates green

## Result and limits

Nine distinct whole-file source paths are newly selected through setup manifests (not hand-written Unity assets). The goose, conflicted tick01/02, old snap and plastic-described crunch are no longer selected. Herald's four specified SFX remain unchanged. Shared legacy growl/scream banks are retired, not reused as fallback. Ram retains its previously reviewed own bellow; no pig/orc growl is newly approved.

Seven collapse cues now map from committed hand facts and room phase edges, with per-room ownership, zero timing jitter, bounded one-shot playback and fresh-fact rearticulation. Duplicate hand facts and repeated room samples do not restart them. Health initialization/healing/death stay silent; a nonfatal decrease emits PlayerHit. Existing confirmed catch admission remains the only death trigger.

This is NOT full content acceptance. Heartbeat, cake/golden pickup, human Echo/Herald breaths, Stare's whisper/wordless vocal, wet grabbing and a convincing flesh hit/bite remain unresolved. I did not bind a model-agreeable answer over an incompatible open description. Cake/golden legacy UI/coin banks and Mimic's plastic-described bite become explicitly missing/silent-with-warning rather than retaining known mismatches. Dry cloth/snap cues cover physical hand contacts provisionally, NOT the requested wet texture. PlayerHit is a human gasp, not a fabricated mixed impact+gasp. A hand hit can separately produce dry contact and a health gasp through the two committed routes.

All selected clips: `human_listened: false`. No human audition, Unity execution, asset import, mixer save, scene edit, vendor copy, download or main-checkout write occurred. Existing serialized assets are intentionally unchanged until the coordinator runs setup.

## Evidence and reproducibility

Local roots:

- `Logs/AgentValidation/Horror/audio-analysis/`
- `Logs/HorrorExpansion/audio/pass3-provenance.json` — hash-verified join of all nine newly selected paths to exact model prompts/responses and cue ids.

| Run | Completed clips | Purpose |
|---|---:|---|
| pass3-describe-001 | 16 | Breath/voice, hand/contact, stings, original four disputed labels |
| pass3-describe-002 | 23 | Low drums/thumps, reward tones, air/structure candidates, gasps, replacement ticks |
| pass3-describe-003 | 9 | More physical/wet/structure/whisper candidates |
| pass3-fit-001 | 39 | Role-specific fit questions, preserved separately from descriptions |
| pass3-fit-002 | 9 | Competing-role assessment with explicit none option |
| pass3-fit-003 | 4 | Structural/catch assessment and adversarial reward-tone checks |

All six statuses say completed/GPU released; audit checked status counts against candidate/result sets and rehashed selected source WAVs. Pinned model revision: `6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d`; upstream source commit: `66326e6e0db34f036c86a76ba005efa4830c69dd`. Raw logs also record processed mono PCM hashes, source/model/package identities, GPU admission, memory and timing. One clip per inference, 11 GiB guard unchanged. No inference refusal occurred in these runs.

The first prepared candidate manifest is preserved in `pass3-describe-001/candidate-manifest.json`; subsequent path manifests and fit prompts are in `tools/audio/pass3-*.json`. Long ghost loops (37.452 s and 26.558 s) and the 16 s ghost manifestation candidate were excluded by the whole-file 12 s bound, not cropped or evaluated. Their absence is NOT a negative fit verdict.

`analyze_pass3.py --coordinator-admitted --question ... [--questions <json>]` supplies fit questions to the same bounded local runner; use a NEW prepared result directory via MOSS_AUDIO_RESULTS_DIR. `audit_pass3.py --output <new checkout Logs JSON>` rechecks completed evidence without inference or Unity. The tracked manifests contain paths only, not audio.

## MOSS evidence table for every newly selected source

D prompt for every row is exactly: `Describe this audio.` Fit prompts below are verbatim. Descriptions are abbreviated model output, not agent listening. No filename was supplied to MOSS. Provisional selection means usable for that stated role subject to owner audition, not a global approval of the source.

| Source project path | Fit prompt | Open description and fit response | Verdict / use |
|---|---|---|---|
| Assets/External/RegularImpactsSFX/ImpactMechanical01/SFX_impactmechanical03.wav | Describe the actual sound. Is this a short dry mechanical tick suitable for a clockwork creature's exact cadence, or a voice/squeal/growl? Give yes/no/uncertain for mechanical tick, and explicitly flag pig/orc resemblance if present. | D: brief metallic click, fast attack/immediate decay, no speech/music. F: yes, short dry mechanical tick. | Select tick replacement; 0.125 s, supplied cadence unchanged. human_listened: false. |
| Assets/External/RegularImpactsSFX/ImpactMechanical01/SFX_impactmechanical04.wav | Describe the actual sound. Is this a short dry mechanical tick suitable for a clockwork creature's exact cadence, or a voice/squeal/growl? Give yes/no/uncertain for mechanical tick, and explicitly flag pig/orc resemblance if present. | D: sharp metallic mechanical-switch click, dry immediate decay. F: yes, short dry mechanical tick. | Select alternate/attack/trap tick. human_listened: false. |
| Assets/External/Audio/Horror Elements/Misc/Misc_Shh.wav | Describe the sound. Would it fit a restrained non-animal hiss for an aimed threat and creeping fog, or is it a growl, voice or sharp electronic effect? Give yes/no/uncertain for hiss fit, and explain. | D: continuous steam-like hiss, steady intensity, no voice. F: restrained non-animal sustained breathy hiss, yes. | Select Blinder hiss and MistAdvance; NOT human breath or Stare voice. human_listened: false. |
| Assets/External/MonstersSFX/MonstersUpdateOne/Combat/SFX_Punch_Designed_Gore_01.wav | Describe the audible sound and choose any fitting game role: wet hand grab, flesh hit, dry physical snap, structure tearing, soft eerie pickup, warm rare chime, human breath, wordless whisper, or none. Reject pig/orc growls. Explain why any other role does not fit; do not assume any role must fit. | D: dry sharp crack followed by low muffled thud, rigid material breaking. F: dry brittle physical snap; specifically lacks wet/flesh resonance. | Select dry Mannequin/Stare catch snap and GrabHit. Reject as wet gore despite filename. human_listened: false. |
| Assets/External/Audio/Horror Elements/Hits/Hit_upstair_boom.wav | Describe the audible sound. Assess it as a brief non-cartoon frightening catch sting; reject comic punching or animal vocals. Is it suitable, unsuitable, or uncertain and why? | D: single forceful bass-drum strike, deep resonant thud, natural decay. F: brief non-cartoon frightening catch sting. | Select one-second hand/legacy catch. Reject heartbeat: no paired lub-dub. Named hunter deaths remain independent. human_listened: false. |
| Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ss2-h_female_shout-of-pain_014.wav | Describe the voice. Is it a short human gasp suitable for an injury cue, human breathing, a sustained scream, or a creature/pig/orc vocalization? Give yes/no/uncertain for short human gasp and for human breath, with reasons. | D: isolated sharp gasp, sudden intake conveying pain/shock. F: short human injury gasp, not sustained scream/nonhuman voice. | Select PlayerHit at low gain; NOT a loop or Echo/Herald breath. Flesh impact still missing. human_listened: false. |
| Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/jw3_whoosh_cloth_leather_fight-007.wav | Describe the sound. Does it fit a short physical hand grabbing or releasing cloth/body, rather than a cartoon punch or weapon effect? Give yes/no/uncertain for grab/release fit and explain. | D: fast whoosh then low dry soft-surface/fabric thud; conjectures whip-like implement. F: yes, natural short hand grabbing/releasing cloth/body, not cartoon impact. | Provisional quiet cloth contact for GrabWarning/Start/Escape; uncertainty about source preserved; NOT wet flesh. human_listened: false. |
| Assets/External/Vefects/Stylized AoE VFX/Audio/WAV/Sergi/SFX_Vefects_Stylized_AoE_Air_Burst_01.wav | Describe the material, scale, attack and tail you hear. Assess whether this conveys a structure tearing under stress in a horror game. Give reasons for rejection as well as fit; do not assume the proposed role is correct. | D: massive metallic groan, abrasive friction and strained screech, large reverberant space. F: deep resonant impact and decaying structural-failure tail. | Select RoomTear, not fog despite initial leading fit answer calling it breathy air. human_listened: false. |
| Assets/External/Audio/Free Pack/Magic Spell_Short Reverse_1.wav | Describe the sound. Does it fit a soft eerie satisfying pickup, a warmer rare chime reward, or a deep consuming swell? Reject coin rattles, bright UI blips and animal growls. Rate each role yes/no/uncertain with a short reason. | D: sustained smooth low-frequency synthetic drone. F: deep consuming swell yes; both reward roles no. | Select RoomConsumed, NOT pickup. Drone/swell distinction still needs in-game audition. human_listened: false. |

## Rejections and conflicts

- Heartbeat: eight low drum/muffled/soft/punchbag/upstairs candidates evaluated. Bare fit yes for drum1/drum2/punchbag/upstairs conflicts with descriptions of single strikes, clicks or hum; others no/uncertain. No low paired cycle established. Coordinator should supply a CC0 heartbeat; I could not verify a CC0 URL without permitted network access.
- Cake/golden: bell initially received yes, but final critique calls it bright/clear/UI-like and potentially immersion-breaking. Sleeping spell initially received yes but is 11.5 s with dramatic attack/decay, final verdict uncertain. Piano/short-reverse do not fit pickup. Crystal describes a sharp metal/coin-like clink; reject. No consistently fitting soft pickup/warmer rare chime selected.
- Echo/Herald breaths: Misc_breath is motor-whirr in D and human breath in F; reject the unresolved conflict. Human female scsh is sustained scream in D/gasp in F. Gasp candidates are injury reactions, not breathing cycles. No animal/growl substitution; human breaths remain missing.
- Stare: ghost-no is a forceful low processed spoken “Now” in D and creature-like growl in F. ghost-die is large-animal growl/roar and F none. ghost-kill is metallic screech in D, breath in F. None proves a whispered/wordless human vocal. Long quiet/wind loops were not evaluated. The two vocal ids remain unresolved; no unsupported spoken line is claimed.
- Tick/hiss/snap/crunch reanalysis is preserved in describe-001/fit-001. Old tick01/02 now read mechanical, but previous orc/squeal conflicts remain; fresh mechanical03/04 replace them. Goose removed regardless of fit yes. Old snap remains thin-material crackle, and crunch remains plastic crinkling in D despite wet-bite F; new consistent dry snap replaces snap, wet Mimic bite is missing.
- Wet candidates body-wet/slime/blood-impact/gore2/squeeze/stab/stone-crash have incompatible D/F material readings. No wet-flesh claim made. Wood/metal/tape creaks read keys or small toy squeaks, not structural failure. Air-burst is the structural selection.

## Setup and native coverage

Coordinator only, after integration/import and its own Unity admission:

1. `Worsen.Editor.Audio.HorrorAudioSetup.BuildMenu` — Build Horror Soundscape; consumes PASS3-BANKS.md, clears unverified heartbeat, assigns reviewed bank overlay and mixer.
2. `Worsen.Editor.Audio.HunterRosterAudioSetup.BuildMenu` — Assign Hunter Roster Selection; consumes SELECTION.md, replaces every owned named binding including Stare catch alias and removes old goose/crunch bindings.
3. `Worsen.Editor.Audio.AudioMixerSetup.Build` — Setup Runtime Volume Mixer; repairs groups and prunes only unadmitted banks.

Run the following full fixtures; environment/skipped cases have no headless pass claim:

- Worsen.Tests.Audio.AudioPass3SetupTests
- Worsen.Tests.Audio.AudioCatchRoutingTests
- Worsen.Tests.Audio.AudioChaseMusicPlaybackTests
- Worsen.Tests.Audio.AudioChaseMusicPresenterTests
- Worsen.Tests.Audio.AudioDriverTests
- Worsen.Tests.Audio.AudioExpansionRoutingTests
- Worsen.Tests.Audio.AudioMixerPlayModeTests
- Worsen.Tests.Audio.AudioMixerSetupTests
- Worsen.Tests.Audio.AudioRosterPlaybackTests
- Worsen.Tests.Audio.AudioRosterTests
- Worsen.Tests.Audio.AudioSoundscapeDriverTests
- Worsen.Tests.Audio.AudioWorldMixPresenterTests
- Worsen.Tests.Audio.AudioWorldPresenterTests

The new native saved-asset test is expected to fail until both setup manifests are applied. No setup menus were run here. AudioCatchRoutingTests plus CameraHandCatchRoutingTests and ChaseLossIntegrationTests should also be run after Floor/Chase integration (full outside-audio names: `Worsen.Tests.Camera.CameraHandCatchRoutingTests`, `Worsen.Tests.Chase.ChaseLossIntegrationTests`). Their inspected assertions required no edit. Human listening with concurrent rooms and the red vignette is still required.

## Ownership requests

- `Assets/Scripts/Domain/Hunter/Archetypes/Stare/Config/StareConfig.cs`, `_deathId`: coordinator/Hunter owner should change default `stare.catch` to `stare.death` and regenerate the Stare config via its setup tool. This pass supplies an in-scope identical clip/gain/bank alias; it does not edit Domain or add double playback.
- Content owner: provide/evaluate heartbeat, soft cake and warm rare reward chime, human breath cycles, whispered/wordless Stare voice, wet hand/flesh bite/impact candidates. No network/package/vendor writes were authorized.
- Floor owner: integrate the promised committed hand/destruction emissions. AudioOrchestrator already routes them; no cross-layer reference was added.

## Provisional values

All values are provisional pending mix audition, not additional runtime designer fields:

- PASS3-BANKS.md: Death .18; PlayerHit .20; GrabWarning .12; GrabStart .14; GrabHit .12; GrabEscape .12; RoomTear .10; MistAdvance .12; RoomConsumed .08. Missing Cake/Golden .20 each. Retired shared banks 0.
- HorrorAudioSetup.ReviewBanks: zero gain variation/cooldown, pitch 1, MaxConcurrent 1, one-shot/non-ambience, 1.8–16 m distance, priority 55 except Death 90. Existing spatial flags preserved. Runtime finite pool and per-room slots remain the actual polyphony bound.
- SELECTION.md: new tick gains retain .90 presence/tick, .65 attack, .85 Blinder trap; Mannequin snap .30, Stare attack/death/catch .35. Blinder hiss presence .20, detection .30, chase .25, attack/throw .35, death .30, mutation .25.
- No synthesized heartbeat rate, breath loop, mix layer, or time-cropped vendor asset introduced.

## Verification

Impact analysis: upstream exact-uid resolution marks AudioFeedbackPresenter and AudioCueCataloguePresenter CRITICAL; AudioSoundscapePresenter HIGH. Text-search confirms AudioDriver is the feedback caller, AudioSoundscapeDriver calls playback, and AudioMixerSetup.PruneBanks also consumes the catalogue. Index also contains staging-tree duplicates and unnamed edges; those are not treated as proof of isolation. HorrorAudioSetup and HunterRosterAudioSetupTests are LOW with no indexed direct callers; menu BuildMenu and the new bank-overlay fixture are the actual setup callers. AudioRosterPresenter is CRITICAL but was not changed. Python analyze_candidates.main was UNKNOWN/unindexed; wrapper calls in analyze_roster.py, analyze_ambience.py and analyze_pass3.py were inspected. No assembly/contract changes, no graph-policy relaxation.

`audio-pass3-003`: all seven assemblies exit 0, 0 errors; warning totals Core 0, Domain 41, Session 0, Presentation 34, Orchestrator 70, Editor 0, Tests 0, matching every baseline.

`audio-pass3-pure-003`, filter `^Worsen[.]Tests[.]Audio[.]`:
`PURE_RESULT passed=217 failed=0 environment=89 skipped=8`

Four new pure bank-overlay cases pass; new saved-asset case is environment-classified. Extended feedback/catalogue cases cover all hand/phase edges, duplicate suppression, injury initialization and concurrent rooms/rearticulation. Existing feedback and world-mix expectations changed only where the task restores formerly suppressed cues; roster snap expectation follows the new reviewed path. No assertions were weakened or skips added to hide failures.

Python candidate-manifest tests: 4 passed. ast-grep scan: exit 0, no findings. Provenance audit: all six complete runs matched, nine newly selected unique paths rehashed with both D/F evidence. No Unity/human quality pass is claimed.
