# Hunter roster selection — PLAN-021 MOSS review, 2026-10-01

Path-only manifest consumed by `Worsen/Audio/Assign Hunter Roster Selection` after `Build Horror Soundscape`. No audio is copied, embedded, downloaded or redistributed. Paths resolve in the licensed main checkout; public clones fail explicitly until the packs are installed. Existing asset GUIDs/import settings are untouched.

The previous selection did NOT use MOSS: it used filenames and PCM measurements. This revision ran the installed MOSS-Audio 4B-Instruct locally on whole files, with names hidden, preserving raw descriptions and the pig/orc/goblin/human/size/material questionnaire. Human listening is **pending owner for EVERY clip**, including retained clips. These are provisional selections, not a claim of audible acceptance or deployed asset wiring.

Evidence: `Logs/AgentValidation/Horror/audio-analysis/roster-001` (45 structured questionnaires), `roster-describe-001` (45 open descriptions), `roster-followup-001` (13 plain-language follow-ups, four new candidates). All completed; provenance records input SHA-256, processed PCM SHA-256, prompts, model revision `6907a499dc0e87cc77c8ae0fe23fd0eb5476a02d`, source commit `66326e6e0db34f036c86a76ba005efa4830c69dd`, package versions, GPU and timing. Source/model hashes were checked offline. No downloads, audio copies, cuts, or vendor edits. The 24 s whisper candidate was excluded, not cropped.

MOSS is highly prompt-sensitive on this installed model: metallic clicks become creatures in the questionnaire, and the snap becomes a goblin squeal in the follow-up. Source/material/size claims are hypotheses, not facts. All conflicting output is preserved; no majority-vote approval is implied. Reject the consistent large-monster breaths/roars outside Ram, and withhold unverified human-breath and spoken-call slots. Never claim that a filename proves the model wrong. Owner audition is a blocking quality gate.

Duration is whole-file seconds; peak/RMS are unweighted dBFS, not LUFS. All binding gains are provisional linear source gains multiplied by the effects bus/settings. Source catalogue below preserves the earlier naming rationale as an audit trail; the MOSS review and revised binding table, not that rationale, govern these picks.

## Candidate source catalogue (original naming rationale, not listening evidence)

Each clip key resolves to exactly this case-preserved vendor path. MonstersSFX and RegularImpactsSFX are installed vendor packs; Audio subfolder names identify the other installed packs. These references do not grant redistribution rights.

| Clip key | Vendor path | Seconds | Peak dBFS | RMS dBFS | Character / selection rationale |
|---|---|---|---|---|---|
| clip:step1 | Assets/External/MonstersSFX/PackGhostsAndZombies/Zombie_Footstep_v1/Zombie_FootStep_v1_wav.wav | 0.344 | -3.3 | -28.2 | Short dry foot contact, suitable for discrete replay rather than a long walking recording. |
| clip:step2 | Assets/External/MonstersSFX/PackGhostsAndZombies/Zombie_Footstep_v1/Zombie_FootStep_v1_variation_01_wav.wav | 0.409 | -3.3 | -28.0 | Comparable contact alternate without changing authoritative cadence. |
| clip:skitter1 | Assets/External/MonstersSFX/PackEnemies/Spider_Running/Spiders_Running_v1_wav.wav | 1.125 | -6.2 | -26.0 | Low-level clustered insect movement distinguishes overhead Weaver presence. |
| clip:skitter2 | Assets/External/MonstersSFX/PackEnemies/Spider_Running/Spiders_Running_v1_variation_01_wav.wav | 1.722 | -4.2 | -24.4 | Longer skitter alternate, still a fact-triggered one-shot. |
| clip:wet1 | Assets/External/MonstersSFX/PackEnemies/Spider_Attack/Spider_Attack_v1_wav.wav | 0.594 | -3.0 | -18.0 | Compact creature articulation selected for the readable web-release click. |
| clip:wet2 | Assets/External/MonstersSFX/PackEnemies/Spider_Attack/Spider_Attack_v2_wav.wav | 0.469 | -3.0 | -14.8 | Sharper short spider attack for detection/death, not randomized into the fixed tell. |
| clip:tick1 | Assets/External/RegularImpactsSFX/ImpactMechanical01/SFX_impactmechanical01.wav | 0.125 | -15.4 | -46.9 | Brief dry mechanical impulse, no music tail across the Ticking cadence. |
| clip:tick2 | Assets/External/RegularImpactsSFX/ImpactMechanical01/SFX_impactmechanical02.wav | 0.125 | -19.6 | -46.7 | Equal-length clockwork alternate for non-timing winding texture. |
| clip:bell | Assets/External/RegularImpactsSFX/ImpactBell01/SFX_impactbell01.wav | 1.890 | -8.3 | -27.2 | Metallic ring distinguishes key appearance/wake from the ordinary tick. |
| clip:heavy1 | Assets/External/RegularImpactsSFX/ImpactBigHeavy01/SFX_impactbigheavy01.wav | 5.812 | -6.3 | -28.6 | Heavy resonant impact for Ram collisions; long tail stays inside one voice. |
| clip:heavy2 | Assets/External/RegularImpactsSFX/ImpactBigHeavy01/SFX_impactbigheavy02.wav | 5.667 | -6.7 | -29.6 | Comparable collision alternate avoids identical wall impacts. |
| clip:roar1 | Assets/External/MonstersSFX/PackMonsterLowVoices/Roar_Scream_01/SFX-Roar-Scream-01_wav.wav | 2.606 | -3.3 | -15.6 | Strong low creature roar reserved for attack/chase rather than a continuous bed. |
| clip:roar2 | Assets/External/MonstersSFX/PackMonsterLowVoices/Roar_Scream_01/SFX-Roar-Scream-02_wav.wav | 1.693 | -3.4 | -18.3 | Shorter low roar makes Ram discovery distinct from the charge bellow. |
| clip:roar3 | Assets/External/MonstersSFX/PackMonsterLowVoices/Roar_Scream_01/SFX-Roar-Scream-03_wav.wav | 1.255 | -7.0 | -23.9 | Quieter terse growl for repeatable chase punctuation. |
| clip:plastic1 | Assets/External/RegularImpactsSFX/ImpactPlasticRaw01/SFX_impactplasticraw01.wav | 0.761 | -14.1 | -41.9 | Thin dry clack for Skip's physical body; never a teleport effect. |
| clip:plastic2 | Assets/External/RegularImpactsSFX/ImpactPlasticRaw01/SFX_impactplasticraw02.wav | 0.759 | -10.4 | -39.5 | Same-material alternate for ordinary physical movement. |
| clip:bite | Assets/External/Audio/Free Pack/Monster Bite.wav | 1.339 | 0.0 | -12.9 | Dense bite transient, deliberately attenuated because its raw peak reaches full scale. |
| clip:hiss | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/tt2_goose_hissing_01.wav | 1.502 | -8.6 | -30.1 | Breath/noise onset distinguishes Blinder's aimed throw from a scream. |
| clip:breath1 | Assets/External/MonstersSFX/PackMonsterLowVoices/Breath_Generic_01/SFX-Breath-Generic-01_wav.wav | 3.251 | -5.2 | -18.8 | Low bodily breath for presence; Herald warning playback is cut at its supplied duration. |
| clip:breath2 | Assets/External/MonstersSFX/PackMonsterLowVoices/Breath_Generic_01/SFX-Breath-Generic-02_wav.wav | 2.376 | -7.6 | -20.4 | Shorter breath alternate, lower gain avoids an ambient drone. |
| clip:herald-discovery | Assets/External/Audio/Mangled_Screams_free/Sounds/ms_mangled_scream_03.wav | 1.755 | -3.1 | -18.0 | Exact Herald discovery identity specified by HeraldController. |
| clip:herald-chase1 | Assets/External/Audio/Mangled_Screams_free/Sounds/sb_mangled_scream_01.wav | 2.086 | -3.0 | -17.6 | First authoritative Herald chase scream; Audio preserves supplied pitch. |
| clip:herald-attack | Assets/External/Audio/Mangled_Screams_free/Sounds/sb_mangled_scream_02.wav | 2.329 | -3.4 | -14.9 | Fixed Herald attack identity, no random pitch, gain jitter or delay. |
| clip:herald-chase2 | Assets/External/Audio/Mangled_Screams_free/Sounds/sb_mangled_scream_03.wav | 2.794 | -3.0 | -16.4 | Second authoritative Herald chase scream, not a local timer/alternate decision. |
| clip:snap | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/jmg_bone-break_snap_027.wav | 1.200 | -4.1 | -29.6 | Short bone snap replaces Mannequin's loud sting; low gain and no scream tail. |
| clip:crunch | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/jmg_crunch_rip_014.wav | 1.482 | -4.3 | -32.9 | Quiet crunch alternative reserved for mutation tell, not a second catch layer. |
| clip:ghost1 | Assets/External/MonstersSFX/PackCinematicMonsters/Primary(DistantAbsctractScreamShort01).wav | 6.348 | -3.5 | -21.0 | Distant abstract voice for Stare's nonverbal chase layer; not claimed to speak words. |
| clip:ghost2 | Assets/External/MonstersSFX/PackCinematicMonsters/Primary(DistantAbsctractScreamShort02).wav | 8.736 | -4.1 | -21.5 | Longer voice alternative stays bounded by the existing chase slot. |
| clip:wood1 | Assets/External/RegularImpactsSFX/ImpactWoodRaw01/SFX_impactwoodraw01.wav | 0.425 | -7.8 | -25.1 | Dry solid stamp gives Ram readable attack onset without a long lead-in. |
| clip:wood2 | Assets/External/RegularImpactsSFX/ImpactWoodRaw01/SFX_impactwoodraw02.wav | 0.731 | -7.2 | -28.7 | Lower-average solid contact for Ram presence/stride variation. |
| clip:wind-up | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/antiques_camera_super-8_bauer-88b_wind-up_10.wav | 2.644 | -5.0 | -37.2 | New whole-file mechanical winding candidate. |
| clip:wood-creak | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ucns_woodenbed_creaks_04.wav | 6.620 | -4.5 | -34.4 | Audition candidate only: no movement-gated consumer exists. |

## MOSS description for every selected clip

Descriptions below are abbreviated model output, not agent listening. Q = structured questionnaire; D = open description; F = plain follow-up. Every row is pending owner listening. Paths resolve through the exact case-preserved keys above. Pack licence remains the owner's installed vendor licence; these references grant no redistribution rights.

| Selected key(s) | MOSS description | Pig/orc/goblin, human/creature, size/material assessment and conflicts | Human listening |
|---|---|---|---|
| step1, step2 | Q: sharp high-pitched percussive sound, rapid onset and short decay. D: metallic keys clinking. | Q: none; nonvocal, small, air. D does NOT verify a footstep; retained only as provisional short replay contacts. | pending owner |
| skitter1 | D: sharp crack followed by dense brittle crackling, popping and snapping. | Q says human gasp/grunt, small/air, none of the named creatures; D says large dry fibrous fracture. Texture supports clustered skitter, source uncertain. | pending owner |
| skitter2 | D: dense cascade of brittle granular impacts, rapid irregular clicks/clatters. | Q says forceful exhalation/snort, nonvocal/small/air, none. D's glass/fracture narrative is not verified. | pending owner |
| wet1, wet2 | Q: sharp forceful exhalation with airy texture. D: brief metallic click/clink. | Q: none, nonvocal, small/air. Wet material is NOT established; fixed short articulation retained for web timing. | pending owner |
| tick1, tick2 | D: brief sharp metallic mechanical-switch click with fast attack/decay. | Q calls both medium orc grunts; F calls both small creature squeals. Serious conflict: mechanical candidate, NOT certified nonvocal. | pending owner |
| bell | Q: single high-pitched bell-like tone, clear attack, long decay. D: resonant metallic chime. | Q: none, nonvocal, small/metallic; useful key/wake contrast. | pending owner |
| wind-up | Q: metallic click followed by thud. D: rapid metallic clinks. F: brittle small hard objects being manipulated. | Q/F: no voice/animal; small hard metallic texture, not verified clockwork. | pending owner |
| heavy2 | Q: deep resonant thud with long smooth decay, large hollow object struck. D: powerful explosion and decaying echo. | Q: none, nonvocal, large/hollow. Selected for Ram collision only; long tail requires mix audition. | pending owner |
| roar1 | Q: deep guttural growl, wet throaty texture, large creature. D: resonant large-animal growl, sustained low rumble. | Q: none of pig/orc/goblin. Deliberate large creature bellow ONLY for Ram, never human breath. | pending owner |
| wood1 | Q: brief bright wooden strike or hand clap. D: metallic key clink. | Q: none, nonvocal, small/wood. Material conflicts, onset is short. | pending owner |
| wood2 | Q: low-pitched resonant wooden thud, heavy object hitting solid surface. D: metallic key clink. | Q: none, nonvocal, large/wood. Provisional heavier Ram step, material uncertain. | pending owner |
| hiss | Q: sharp high hiss followed by low thump. D: metallic click then electronic tone. F: goblin-like squeal. | Q: none, nonvocal/small/air; F contradicts this. Provisional Blinder warning, not a verified human voice. | pending owner |
| herald-discovery | Q: piercing shriek, sharp onset/decay. D: high-pitched strained female scream. | Q: none; source/size uncertain. Exact authored Herald identity retained. | pending owner |
| herald-chase1 | Q: sustained harsh distorted human/creature scream. D: piercing continuous electronic alarm. | Q: none, human voice/medium/air; D conflicts on source. Exact authored identity retained. | pending owner |
| herald-attack | Q: sudden piercing non-human shriek. D: high metallic electronic alarm tone. | Q: none, uncertain small/nonvocal/air. Exact fixed attack identity retained. | pending owner |
| herald-chase2 | Q: loud sustained strained scream/shriek. D: continuous piercing metallic alarm. | Q: none, large/nonvocal/air, high uncertainty. Exact authored identity retained. | pending owner |
| snap | D: sharp crackle, initial snap and brittle micro-tears of thin dry material. | Q: small squeaky creature-like/nonvocal; F: goblin-like squeal. Required Mannequin snap is provisional, NOT owner-approved audio. | pending owner |
| crunch | D: crisp snap and dry crinkling/rustling of thin flexible material. | Q: small squeaky creature-like/nonvocal; F: goblin squeal. Replaces Monster Bite's large orc grunt for Mimic; unresolved model conflict. | pending owner |

Rejected: `roar2`, `roar3`, `breath1`, `breath2`, `bite`, `ghost1`, `ghost2`, humanoid/giant/hate/undead breaths, whisper-fear, and the pig/orc/goblin controls. In particular Q labels roar2/3, breath1, Monster Bite and ghost1 orc-like; D describes low animal growls or engine roars, not Echo/Stare. Pig control is described as wet guttural snorts; no selected path names the pig/orc/goblin controls. No intentional pig/orc/goblin assignment is approved. Ram alone retains a large-creature bellow (`roar1`). Conflicted snap/hiss/tick candidates remain flagged rather than being represented as proven safe.

Withheld: Echo/Herald human breath (all evaluated options were growls, screams or otherwise inconsistent); Stare's exact spoken lines and voice identity. `wood-creak` Q says dry woody percussive impacts, D/F says metallic clinks, all nonvocal. It is reserved for audition, NOT bound until movement/hold facts can prevent playback while frozen. No time slicing or pretend human listening resolves these gaps.

## Binding table

This executable table is the per-hunter cue → clip table; join each selected key to the MOSS description and path above. Fixed attack tells have no alternates. `silence` means deliberate zero-gain, clip-free silence without a voice or warning. `missing` means an unresolved positive-gain placeholder: silent with a missing-clip warning, NEVER a fallback. Stare's spoken ids remain absent and warn; unrelated whispers are not substituted. All new-hunter habits use explicit own silence. No shared `hunter.*` bindings are selected here. Coordinator must run the assignment tool after integration: the existing serialized asset still contains the OLD picks until then.

| Cue id | Existing budget bank | Linear gain | Primary key | Alternate keys | One-line slot justification |
|---|---|---|---|---|---|
| cue:echo.presence | Presence | 0.50 | step1 | step2 | Dry sub-half-second contact, low RMS, distinguishes replayed locomotion. |
| cue:echo.detection | Detection | 0.35 | missing | - | Human breath not verified; warn instead of using a monster breath. |
| cue:echo.chase | Chase | 0.35 | missing | - | Distorted human breath pending; no low roar. |
| cue:echo.attack | EnemyWindup | 0.50 | wood1 | - | Fixed 0.425 s dry onset remains a timing tell. |
| cue:echo.death | Death | 0.40 | wood1 | - | Short physical catch contact replaces the unrelated low roar. |
| cue:weaver.presence | Presence | 0.50 | skitter1 | skitter2 | Low-RMS overhead insect movement, 1.125–1.722 s. |
| cue:weaver.detection | Detection | 0.40 | wet2 | wet1 | Brief louder articulation differentiates discovery from skitter. |
| cue:weaver.chase | Chase | 0.55 | skitter2 | skitter1 | Denser movement punctuation without adding an ambience source. |
| cue:weaver.attack | EnemyWindup | 0.50 | wet1 | - | Single 0.594 s web-release articulation at fixed pitch. |
| cue:weaver.death | Death | 0.45 | wet2 | - | Compact spider attack, avoids a long generic scream. |
| cue:ticking.presence | Presence | 0.90 | tick1 | tick2 | Very quiet 0.125 s mechanical impulse needs higher source gain. |
| cue:ticking.detection | Detection | 0.55 | bell | - | 1.890 s metallic wake contrasts with the dry ticking. |
| cue:ticking.chase | Chase | 0.45 | wind-up | - | Manipulated hard mechanism texture; no creature or second tick timer. |
| cue:ticking.attack | EnemyWindup | 0.65 | tick2 | - | Fixed mechanical contact at attack onset. |
| cue:ticking.death | Death | 0.45 | bell | - | Metallic catch rather than a huge monster/explosion. |
| cue:ram.presence | Presence | 0.55 | wood2 | wood1 | Solid sub-second footfall rather than ambient rumble. |
| cue:ram.detection | Detection | 0.35 | roar1 | - | Large-creature bellow fits this hunter only. |
| cue:ram.chase | Chase | 0.35 | roar1 | - | Same deliberate large voice; no random orc alternates. |
| cue:ram.attack | EnemyWindup | 0.55 | wood1 | - | 0.425 s locked stamp preserves charge timing. |
| cue:ram.death | Death | 0.50 | roar1 | - | Strong 2.606 s bellow only for the confirmed fatal catch. |
| cue:skip.presence | Presence | 0 | silence | - | Silent approach/relocation identity; no ambient clacks. |
| cue:skip.detection | Detection | 0 | silence | - | Position is the reveal. |
| cue:skip.chase | Chase | 0 | silence | - | No legacy growl on dormant shared feedback. |
| cue:skip.attack | EnemyWindup | 0 | silence | - | No teleport/mark cue or borrowed attack voice. |
| cue:skip.death | Death | 0.30 | wood1 | - | Physical accepted catch contact only; not a creature bite. |
| cue:mimic.presence | Presence | 0 | silence | - | Stationary disguise remains silent. |
| cue:mimic.detection | Detection | 0 | silence | - | Bait discovery remains silent. |
| cue:mimic.chase | Chase | 0 | silence | - | No pursuit voice. |
| cue:mimic.attack | EnemyWindup | 0.30 | crunch | - | Dry rip/crunch replaces the large guttural Monster Bite. |
| cue:mimic.death | Death | 0.35 | crunch | - | Same wrong-bite texture for accepted catch only. |
| cue:blinder.presence | Presence | 0.20 | hiss | - | Restrained noisy hiss rather than monster breathing. |
| cue:blinder.detection | Detection | 0.50 | hiss | - | Distinct 1.502 s noise gesture, not a broad scream. |
| cue:blinder.chase | Chase | 0.25 | hiss | - | Own noisy warning texture instead of low monster breaths. |
| cue:blinder.attack | EnemyWindup | 0.60 | hiss | - | Fixed hiss onset retains projectile timing. |
| cue:blinder.death | Death | 0.40 | hiss | - | Own hiss identity, never the low shared roar. |
| cue:herald.presence | Presence | 0 | silence | - | No dedicated presence loop in the brief. |
| cue:herald.detection | Detection | 0.50 | herald-discovery | - | Exact 1.755 s discovery scream, -18.0 dBFS RMS before gain. |
| cue:herald.chase | Chase | 0.50 | herald-chase1 | herald-chase2 | 2–3 s specified chase voices; dedicated facts select each identity. |
| cue:herald.attack | EnemyWindup | 0.50 | herald-attack | - | Exact 2.329 s attack scream, pitch 1 and no scheduling jitter. |
| cue:herald.death | Death | 0.50 | herald-attack | - | Same vocal identity reserved for confirmed catch, no new layer. |
| cue:mannequin.presence | Presence | 0 | silence | - | No movement/hold fact to gate creaks; frozen must stay silent. |
| cue:mannequin.detection | Detection | 0 | silence | - | No audible discovery. |
| cue:mannequin.chase | Chase | 0 | silence | - | No chase bed while frozen. |
| cue:mannequin.attack | EnemyWindup | 0 | silence | - | No pre-catch sting. |
| cue:mannequin.death | Death | 0.30 | snap | - | Short snap catch, no loud shared sting; human approval pending. |
| cue:stare.presence | Presence | 0.25 | missing | - | Exact spoken identity required; no monster substitute. |
| cue:stare.detection | Detection | 0.30 | missing | - | No unrelated abstract roar in place of speech. |
| cue:stare.chase | Chase | 0.35 | missing | - | Previous abstract scream reads as engine/monster; withheld. |
| cue:stare.attack | EnemyWindup | 0.35 | snap | - | Brief physical catch articulation, not a spider/orc voice. |
| cue:stare.death | Death | 0.35 | snap | - | Physical snap instead of low roar on accepted catch. |
| cue:echo.footstep | Footstep | 0.50 | step1 | step2 | Sub-half-second replay contacts retain supplied gain/pitch/cadence. |
| cue:weaver.skitter | Presence | 0.50 | skitter1 | skitter2 | Actual Weaver presence alias uses the overhead movement bank. |
| cue:weaver.wet-click | EnemyWindup | 0.50 | wet1 | - | Actual web warning alias uses the fixed 0.594 s clip. |
| cue:ticking.tick | Presence | 0.90 | tick1 | - | Fixed 0.125 s impulse, playback capped by the supplied interval. |
| cue:ticking.winding | Presence | 0.45 | wind-up | - | Whole mechanical winding texture distinct from the single tick. |
| cue:ticking.key-appeared | Presence | 0.45 | bell | - | Restrained metallic ring advertises the physical key. |
| cue:ticking.wake | Detection | 0.55 | bell | - | Metallic wake, not another timer-generated tick. |
| cue:ram-stamp | EnemyWindup | 0.55 | wood1 | - | Fixed dry charge stamp. |
| cue:ram-bellow | EnemyWindup | 0.45 | roar1 | - | Fixed charge bellow, admitted in the same attack slot. |
| cue:ram-stride | Presence | 0.55 | wood2 | wood1 | Short body contacts alternate within the hunter presence slot. |
| cue:ram-impact | Presence | 0.50 | heavy2 | - | Large resonant impact, not heavy1's model-described firearm. |
| cue:mimic-wrong-bite | EnemyWindup | 0.30 | crunch | - | Dry material bite instead of large orc vocal; no pose sound. |
| cue:blinder.throw-hiss | EnemyWindup | 0.60 | hiss | - | Fixed projectile warning, never random delay. |
| cue:blinder.trap-tick | EnemyWindup | 0.85 | tick2 | - | Short weak tick only when gameplay publishes an audible trap fact. |
| cue:ms_mangled_scream_03 | Detection | 0.50 | herald-discovery | - | Exact discovery sound id from HeraldController, spatial at Herald. |
| cue:sb_mangled_scream_01 | Chase | 0.50 | herald-chase1 | - | Exact first chase id; preserve gameplay-supplied pitch. |
| cue:sb_mangled_scream_03 | Chase | 0.50 | herald-chase2 | - | Exact second chase id; no extra Audio alternation. |
| cue:sb_mangled_scream_02 | EnemyWindup | 0.50 | herald-attack | - | Exact fixed-pitch attack scream. |
| cue:herald.breath | EnemyWindup | 0.30 | missing | - | Human warning breath pending; generic monster breath rejected. |
| cue:echo.quickened-recording | Presence | 0.55 | step2 | - | Short displaced contact flags recording mutation without text. |
| cue:weaver-quickened-skitter | Presence | 0.55 | skitter2 | - | Longer denser skitter flags changed Weaver behavior. |
| cue:blinder-quickened-approach | Presence | 0.45 | hiss | - | Recognizable 1.502 s hiss identifies Blinder mutation. |
| cue:herald-quickened-approach | Presence | 0.35 | herald-chase2 | - | Restrained second scream identifies the Herald mutation, not a gameplay attack. |
| cue:mannequin.long-step | Presence | 0 | silence | - | Anonymous mutation lacks hold state: cannot safely emit creak while frozen. |
| cue:stare.quickened-gaze | Presence | 0.25 | missing | - | Do not recycle the rejected engine/monster roar. |
| cue:echo.turn | Presence | 0 | silence | - | No shared habit grunt. |
| cue:echo.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:weaver.turn | Presence | 0 | silence | - | Skitter facts own this identity. |
| cue:weaver.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:ticking.turn | Presence | 0 | silence | - | Clock facts own this identity. |
| cue:ticking.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:ram.turn | Presence | 0 | silence | - | No unrequested bellow on loss turn. |
| cue:ram.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:skip.turn | Presence | 0 | silence | - | Silent relocation identity. |
| cue:skip.cake-reaction | Presence | 0 | silence | - | Silent relocation identity. |
| cue:mimic.turn | Presence | 0 | silence | - | Silent disguise. |
| cue:mimic.cake-reaction | Presence | 0 | silence | - | Silent disguise. |
| cue:blinder.turn | Presence | 0 | silence | - | Dedicated throw/presence facts own sound. |
| cue:blinder.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:herald.turn | Presence | 0 | silence | - | Dedicated scream facts own sound. |
| cue:herald.cake-reaction | Presence | 0 | silence | - | No shared habit grunt. |
| cue:mannequin.turn | Presence | 0 | silence | - | Frozen silence. |
| cue:mannequin.cake-reaction | Presence | 0 | silence | - | Frozen silence. |
| cue:stare.turn | Presence | 0 | silence | - | No unrelated habit vocal. |
| cue:stare.cake-reaction | Presence | 0 | silence | - | No unrelated habit vocal. |

Mutation ids are the six currently authored by EchoProfileSetup, WeaverProfileSetup, BlinderHeraldProfileSetup and ObservedHunterProfileSetup. Other profiles have no authored mutation tell to bind; unknown future ids retain visible missing-clip warnings. Stop and Won/Catch gameplay facts do not synthesize death audio: only confirmed catch admission plays a death slot.
