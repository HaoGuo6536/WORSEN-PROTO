# Hunter roster selection — WP-A

Path-only manifest consumed by `Worsen/Audio/Assign Hunter Roster Selection` after `Build Horror Soundscape`. No audio is copied, embedded, downloaded or redistributed. Paths resolve in the licensed main checkout; public clones fail explicitly until the packs are installed. Existing asset GUIDs/import settings are untouched.

Selection basis: filename/pack taxonomy plus decoded PCM measurements made read-only with soundfile 0.14.0. Character descriptions below are selection rationale inferred from vendor naming, NOT listening evidence or model output. No MOSS/model calls were made. Duration is whole-file seconds, peak and RMS are unweighted dBFS over all channels (not LUFS or perceived loudness). Coordinator headphone review remains required. All gains are provisional linear source gains, further multiplied by player settings and the effects bus. No normalization or audio edits were performed.

## Source catalogue

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

## Binding table

The primary/alternate keys below resolve to paths and measured character/loudness/length above. This is the executable selection table. Fixed timing tells have no alternates. Chase layers here are bounded, fact-driven one-shots, not new continuous sources. Silence exceptions remain authoritative: Mannequin's first four slots and Mimic's first three slots have selected dormant assets for inventory completeness, but gameplay presentation does not play them. Skip relocation is always silent. Dedicated Stare spoken calls (`stare.i-see-you`, `stare.find-me`) remain unbound and warn: no installed clip has been verified to speak those exact lines; unrelated whispers are not substituted.

| Cue id | Existing budget bank | Linear gain | Primary key | Alternate keys | One-line slot justification |
|---|---|---|---|---|---|
| cue:echo.presence | Presence | 0.50 | step1 | step2 | Dry sub-half-second contact, low RMS, distinguishes replayed locomotion. |
| cue:echo.detection | Detection | 0.35 | breath2 | breath1 | Restrained 2–3 s breath rather than a second generic roar. |
| cue:echo.chase | Chase | 0.35 | roar3 | roar2 | Short low-level growl punctuates pursuit within the existing slot. |
| cue:echo.attack | EnemyWindup | 0.50 | wood1 | - | Fixed 0.425 s dry onset remains a timing tell. |
| cue:echo.death | Death | 0.50 | roar2 | - | 1.693 s low roar marks confirmed hunter death, not hand death. |
| cue:weaver.presence | Presence | 0.50 | skitter1 | skitter2 | Low-RMS overhead insect movement, 1.125–1.722 s. |
| cue:weaver.detection | Detection | 0.40 | wet2 | wet1 | Brief louder articulation differentiates discovery from skitter. |
| cue:weaver.chase | Chase | 0.55 | skitter2 | skitter1 | Denser movement punctuation without adding an ambience source. |
| cue:weaver.attack | EnemyWindup | 0.50 | wet1 | - | Single 0.594 s web-release articulation at fixed pitch. |
| cue:weaver.death | Death | 0.45 | wet2 | - | Compact spider attack, avoids a long generic scream. |
| cue:ticking.presence | Presence | 0.90 | tick1 | tick2 | Very quiet 0.125 s mechanical impulse needs higher source gain. |
| cue:ticking.detection | Detection | 0.55 | bell | - | 1.890 s metallic wake contrasts with the dry ticking. |
| cue:ticking.chase | Chase | 0.65 | plastic1 | plastic2 | Short mechanical body rattle, not a second tick clock. |
| cue:ticking.attack | EnemyWindup | 0.65 | wood1 | - | Fixed dry strike reads at attack onset. |
| cue:ticking.death | Death | 0.55 | heavy1 | - | Resonant heavy body impact reserved for confirmed catch. |
| cue:ram.presence | Presence | 0.55 | wood2 | wood1 | Solid sub-second footfall rather than ambient rumble. |
| cue:ram.detection | Detection | 0.45 | roar2 | roar3 | Short low roar at restrained gain distinguishes discovery. |
| cue:ram.chase | Chase | 0.45 | roar3 | roar2 | Repeated terse bellow bounded to one chase slot. |
| cue:ram.attack | EnemyWindup | 0.55 | wood1 | - | 0.425 s locked stamp preserves charge timing. |
| cue:ram.death | Death | 0.50 | roar1 | - | Strong 2.606 s bellow only for the confirmed fatal catch. |
| cue:skip.presence | Presence | 0.70 | plastic1 | plastic2 | Quiet short clacks identify ordinary body movement only. |
| cue:skip.detection | Detection | 0.45 | wet2 | wet1 | Abrupt short articulation without a teleport announcement. |
| cue:skip.chase | Chase | 0.40 | roar3 | roar2 | Low-level pursuit punctuation, no arrival sting. |
| cue:skip.attack | EnemyWindup | 0.65 | plastic2 | - | Fixed physical attack clack; interception remains normal hit. |
| cue:skip.death | Death | 0.35 | bite | - | Attenuated 1.339 s body bite only on confirmed death. |
| cue:mimic.presence | Presence | 0.00 | plastic1 | plastic2 | Dormant selection: quiet disguise must not announce itself. |
| cue:mimic.detection | Detection | 0.00 | wet1 | - | Dormant selection: bait discovery is silent. |
| cue:mimic.chase | Chase | 0.00 | roar3 | - | Dormant selection: stationary bait does not acquire a chase bed. |
| cue:mimic.attack | EnemyWindup | 0.30 | bite | - | Full-scale bite attenuated to 0.30; fixed committed bite onset. |
| cue:mimic.death | Death | 0.35 | bite | - | Short wrong-bite identity retained only for confirmed catch. |
| cue:blinder.presence | Presence | 0.30 | breath2 | breath1 | Restrained bodily breath below the throw warning. |
| cue:blinder.detection | Detection | 0.50 | hiss | - | Distinct 1.502 s noise gesture, not a broad scream. |
| cue:blinder.chase | Chase | 0.40 | breath1 | breath2 | Breath punctuation occupies only the chase slot. |
| cue:blinder.attack | EnemyWindup | 0.60 | hiss | - | Fixed hiss onset retains projectile timing. |
| cue:blinder.death | Death | 0.45 | roar3 | - | Short subdued creature catch sound. |
| cue:herald.presence | Presence | 0.25 | breath2 | breath1 | Quiet inhalation contrasts with the required high-energy screams. |
| cue:herald.detection | Detection | 0.50 | herald-discovery | - | Exact 1.755 s discovery scream, -18.0 dBFS RMS before gain. |
| cue:herald.chase | Chase | 0.50 | herald-chase1 | herald-chase2 | 2–3 s specified chase voices; dedicated facts select each identity. |
| cue:herald.attack | EnemyWindup | 0.50 | herald-attack | - | Exact 2.329 s attack scream, pitch 1 and no scheduling jitter. |
| cue:herald.death | Death | 0.50 | herald-attack | - | Same vocal identity reserved for confirmed catch, no new layer. |
| cue:mannequin.presence | Presence | 0.00 | plastic1 | - | Dormant inventory choice: Mannequin movement remains silent. |
| cue:mannequin.detection | Detection | 0.00 | plastic2 | - | Dormant inventory choice: no audible discovery. |
| cue:mannequin.chase | Chase | 0.00 | crunch | - | Dormant inventory choice: no chase layer breaks its silence. |
| cue:mannequin.attack | EnemyWindup | 0.00 | snap | - | Dormant inventory choice: no pre-catch sting. |
| cue:mannequin.death | Death | 0.30 | snap | - | Approved 1.200 s bone snap at 0.30 replaces the loud sting. |
| cue:stare.presence | Presence | 0.25 | breath2 | breath1 | Nonverbal slot inventory only; cannot replace the unverified spoken call. |
| cue:stare.detection | Detection | 0.30 | ghost1 | - | Distant voice inventory, not claimed to say 'I see you'. |
| cue:stare.chase | Chase | 0.35 | ghost1 | ghost2 | Distant 6–9 s voice punctuation bounded by the existing slot. |
| cue:stare.attack | EnemyWindup | 0.45 | wet2 | - | Short fixed articulation avoids moving the authoritative attack onset. |
| cue:stare.death | Death | 0.40 | roar2 | - | Short fatal-catch punctuation rather than extra continuous ambience. |
| cue:echo.footstep | Footstep | 0.50 | step1 | step2 | Sub-half-second replay contacts retain supplied gain/pitch/cadence. |
| cue:weaver.skitter | Presence | 0.50 | skitter1 | skitter2 | Actual Weaver presence alias uses the overhead movement bank. |
| cue:weaver.wet-click | EnemyWindup | 0.50 | wet1 | - | Actual web warning alias uses the fixed 0.594 s clip. |
| cue:ticking.tick | Presence | 0.90 | tick1 | - | Fixed 0.125 s impulse, playback capped by the supplied interval. |
| cue:ticking.winding | Presence | 0.80 | tick2 | - | Short quiet mechanism differentiates winding from the louder wake. |
| cue:ticking.key-appeared | Presence | 0.45 | bell | - | Restrained metallic ring advertises the physical key. |
| cue:ticking.wake | Detection | 0.55 | bell | - | Metallic wake, not another timer-generated tick. |
| cue:ram-stamp | EnemyWindup | 0.55 | wood1 | - | Fixed dry charge stamp. |
| cue:ram-bellow | EnemyWindup | 0.45 | roar1 | - | Fixed charge bellow, admitted in the same attack slot. |
| cue:ram-stride | Presence | 0.55 | wood2 | wood1 | Short body contacts alternate within the hunter presence slot. |
| cue:ram-impact | Presence | 0.50 | heavy1 | heavy2 | Long heavy tail stays within a single impact/presence voice. |
| cue:mimic-wrong-bite | EnemyWindup | 0.30 | bite | - | Attenuated fixed bite; no sound on pose/population facts. |
| cue:blinder.throw-hiss | EnemyWindup | 0.60 | hiss | - | Fixed projectile warning, never random delay. |
| cue:blinder.trap-tick | EnemyWindup | 0.85 | tick2 | - | Short weak tick only when gameplay publishes an audible trap fact. |
| cue:ms_mangled_scream_03 | Detection | 0.50 | herald-discovery | - | Exact discovery sound id from HeraldController, spatial at Herald. |
| cue:sb_mangled_scream_01 | Chase | 0.50 | herald-chase1 | - | Exact first chase id; preserve gameplay-supplied pitch. |
| cue:sb_mangled_scream_03 | Chase | 0.50 | herald-chase2 | - | Exact second chase id; no extra Audio alternation. |
| cue:sb_mangled_scream_02 | EnemyWindup | 0.50 | herald-attack | - | Exact fixed-pitch attack scream. |
| cue:herald.breath | EnemyWindup | 0.30 | breath2 | - | Warning inhalation, cut at authoritative warning duration. |
| cue:echo.quickened-recording | Presence | 0.55 | step2 | - | Short displaced contact flags recording mutation without text. |
| cue:weaver-quickened-skitter | Presence | 0.55 | skitter2 | - | Longer denser skitter flags changed Weaver behavior. |
| cue:blinder-quickened-approach | Presence | 0.45 | hiss | - | Recognizable 1.502 s hiss identifies Blinder mutation. |
| cue:herald-quickened-approach | Presence | 0.35 | herald-chase2 | - | Restrained second scream identifies the Herald mutation, not a gameplay attack. |
| cue:mannequin.long-step | Presence | 0.20 | crunch | - | Quiet 1.482 s local mutation tell, not ordinary Mannequin movement. |
| cue:stare.quickened-gaze | Presence | 0.25 | ghost1 | - | Distant abstract voice flags changed gaze, not an invented spoken line. |

Mutation ids are the six currently authored by EchoProfileSetup, WeaverProfileSetup, BlinderHeraldProfileSetup and ObservedHunterProfileSetup. Other profiles have no authored mutation tell to bind; unknown future ids retain visible missing-clip warnings. Stop and Won/Catch gameplay facts do not synthesize death audio: only confirmed catch admission plays a death slot.
