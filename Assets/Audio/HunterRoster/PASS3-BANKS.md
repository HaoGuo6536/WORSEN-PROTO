# Pass-3 feedback/collapse bank manifest

Consumed by HorrorAudioSetup.BuildMenu before asset mutation, then AudioMixerSetup prunes only unadmitted banks. Run HunterRosterAudioSetup.BuildMenu afterward for named hunter bindings. Source WAVs stay in installed packs; no audio is copied. Every bound source has description/fit evidence in PASS3.md and the local inference logs. human_listened: false for every row.

Clips are whole files. Clips in one bank would be alternatives, NOT layered impact+gasp; no such layering is claimed. Missing means positive gain, empty bank and runtime warning. Silence means retired shared growl/scream banks. Herald named clips and Ram's reviewed bellow remain in SELECTION.md.

| Clip key | Vendor path | Seconds | Peak dBFS | RMS dBFS | Review |
|---|---|---|---|---|---|
| clip:catch | Assets/External/Audio/Horror Elements/Hits/Hit_upstair_boom.wav | 1.000 | -0.2 | -14.3 | Single low drum strike; suitable catch, NOT paired heartbeat. |
| clip:gasp | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ss2-h_female_shout-of-pain_014.wav | 2.053 | -3.9 | -27.0 | Open description and fit agree on isolated human injury gasp. |
| clip:cloth | Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/jw3_whoosh_cloth_leather_fight-007.wav | 2.244 | -4.0 | -28.8 | Whoosh and soft fabric thud; dry hand/cloth contact only, NOT wet flesh. |
| clip:snap | Assets/External/MonstersSFX/MonstersUpdateOne/Combat/SFX_Punch_Designed_Gore_01.wav | 0.640 | -4.9 | -22.4 | Both model passes describe dry physical snap/thud, not gore. |
| clip:tear | Assets/External/Vefects/Stylized AoE VFX/Audio/WAV/Sergi/SFX_Vefects_Stylized_AoE_Air_Burst_01.wav | 2.020 | -0.7 | -16.3 | Massive metallic groan/screech; structural stress fits, not airy fog. |
| clip:mist | Assets/External/Audio/Horror Elements/Misc/Misc_Shh.wav | 4.125 | -12.1 | -33.4 | Steam-like sustained non-animal hiss. |
| clip:consume | Assets/External/Audio/Free Pack/Magic Spell_Short Reverse_1.wav | 2.000 | -1.3 | -12.0 | Deep synthetic consuming tone; not a reward chime. |

| Cue id | Bank | Linear gain | Primary key | Alternates | Decision |
|---|---|---|---|---|---|
| cue:bank.death | Death | 0.18 | catch | - | Non-cartoon one-second catch strike for hand/legacy catch only; hunter-owned deaths remain their own. |
| cue:bank.player-hit | PlayerHit | 0.20 | gasp | - | Human gasp on accepted nonfatal health decrease; flesh component unresolved. |
| cue:bank.grab-warning | GrabWarning | 0.12 | cloth | - | Quiet approaching hand/cloth gesture; wet texture unresolved. |
| cue:bank.grab-start | GrabStart | 0.14 | cloth | - | Soft grabbing contact, not a growl. |
| cue:bank.grab-hit | GrabHit | 0.12 | snap | - | Short dry physical contact; health route separately emits gasp if health falls. |
| cue:bank.grab-escape | GrabEscape | 0.12 | cloth | - | Quiet cloth release, no extra loop. |
| cue:bank.room-tear | RoomTear | 0.10 | tear | - | Structural stress one-shot at Tearing edge. |
| cue:bank.mist-advance | MistAdvance | 0.12 | mist | - | Hiss one-shot at Encroaching edge, never unbounded per-room loop. |
| cue:bank.room-consumed | RoomConsumed | 0.08 | consume | - | Deep consuming tone at Closed edge. |
| cue:bank.cake | CakeCollect | 0.20 | missing | - | No consistently soft eerie candidate; remove bright legacy UI pickup. |
| cue:bank.golden-cake | GoldenCakeCollect | 0.20 | missing | - | Bell is UI-bright and sleeping spell too long; remove coins rather than certify either. |
| cue:bank.presence | Presence | 0 | silence | - | Retire unreviewed shared growls; named hunter clips remain separate. |
| cue:bank.detection | Detection | 0 | silence | - | No legacy growl fallback. |
| cue:bank.chase | Chase | 0 | silence | - | No legacy growl fallback. |
| cue:bank.windup | EnemyWindup | 0 | silence | - | No legacy growl fallback. |
| cue:bank.recovery | EnemyRecovery | 0 | silence | - | No legacy growl fallback. |
| cue:bank.lost | EnemyLost | 0 | silence | - | No legacy growl fallback. |
| cue:bank.scream | EnemyScream | 0 | silence | - | No unreviewed shared creature screams; Herald SFX stay named. |
| cue:bank.consumed | Consumed | 0 | silence | - | Confirmed catch owns Death; no old creature-scream layer. |

Provisional bank settings applied by HorrorAudioSetup.ReviewBanks: pitch 1, gain variation 0, cooldown 0, MaxConcurrent 1, one-shot/non-ambience, attenuation 1.8–16 m, priority 55 (Death 90). Existing Spatial flags are preserved: player feedback/catch is local, collapse is spatial. Runtime ownership is one hand slot and one structural slot per room within the existing finite voice pool, not a global MaxConcurrent limiter. New hand/phase facts rearticulate their room slot; duplicate facts are removed before admission. This is not a claim of in-game loudness acceptance.

Heartbeat stays explicitly null: no candidate established a paired lub-dub. No replacement fallback is permitted.
