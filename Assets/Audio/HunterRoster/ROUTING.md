# PLAN-021 hunter audio investigation — 2026-10-01

Status: code/selection review, not deployed or owner-auditioned. No Unity, lease, asset assignment, vendor audio edits, downloads or commits were performed by this worker. The coordinator must assign the reviewed selection and run native checks after integration. `SELECTION.md` contains the executable 95-cue table, exact vendor paths, MOSS descriptions and conflicts. Every chosen clip is pending owner listening.

## What produced the wrong noises

The old selection was explicitly filename/PCM based, not MOSS based. Directly selected low monster roars were used by Echo chase/death, Skip chase, Blinder death and Stare death/quickened gaze. Generic monster breaths were used by Echo/Blinder/Herald/Stare. Monster Bite was used by Skip/Mimic. These are not merely missing-binding fallbacks: an exact binding can itself be the wrong content.

The serialized soundscape also contains shared banks:

| Bank | Resolved current clip paths |
|---|---|
| Presence, Detection, Chase, EnemyWindup | `Assets/Audio/Horror/Expansion/WORSEN_growl_01.wav`, `WORSEN_growl_02.wav` in the same folder |
| EnemyScream | `Assets/Audio/Horror/Expansion/WORSEN_scream_01.wav`, `_02.wav`, `_03.wav` in the same folder |
| Death | `Assets/Audio/Horror/Expansion/WORSEN_hit_01.wav`, `WORSEN_hit_02.wav` in the same folder |
| Legacy AudioDriverConfig Presence/Detection/Chase/Death | Corresponding `Assets/Resources/Audio/Presentation/Audio/{Presence,Detection,Chase,Death}.wav` |

`roster-audit-002.json` resolves serialized GUIDs to paths; this is asset-state evidence, NOT a captured runtime waveform. The original bindings are preserved in `roster-001/selection-before.md` and the audit's serialized snapshot. The untouched serialized config still has the old picks until the coordinator runs setup.

MOSS questionnaire labels WORSEN_growl_01/02 and WORSEN_scream_01 as large orc-like creature vocals. Open descriptions call growl_01 a large dog, scream_01 a large feline, but growl_02 a pure electronic tone: the latter conflict remains unresolved. The low-roar/generic-breath/Monster Bite results provide further plausible creature-noise sources. The installed pig control `Assets/External/Audio/Soundbits_freeSFX_2025/Sounds/ca_pig_grunt_13.wav` is described as wet guttural/snorting sounds. No GUID-resolved direct roster path points to that pig control. We cannot identify the owner's exact heard pig recording without a runtime capture, nor prove the original source of preprocessed WORSEN clips from their names. Do not claim an exact pig-source identification.

## Reachability and closure

| Entry / previous route | New behavior |
|---|---|
| Generic HunterFeedback → `AudioRosterPresenter.Feedback` → `{archetype}.{presence,detection,chase,attack}` | Exact own binding required. Blank feedback cannot erase a previously known identity. Dedicated/silent hunters retain their suppression rules. |
| Missing `{hunter}.{slot}` → `ResolveBinding` → `hunter.{slot}` | Compatibility lookup exists ONLY for explicit rusher, hexer, lurker, thorncaller and watcher. The ten new hunters, unknown and future ids never inherit it. |
| Exact binding with null Clip → `PlayRoster` → Bank's Clips | A new-hunter binding may not borrow Bank audio even when Placeholder is false. It warns once per id/run and spends no voice. Bank remains budgeting metadata. |
| TurnToFace / cake reaction / deliberation → `hunter.turn` or `hunter.cake-reaction` → Presence growls | New hunters emit own ids; explicit zero-gain clip-free bindings suppress the generic grunt. Mannequin/Mimic/Skip habits remain suppressed. Unknown identity is a warned missing own cue. |
| Named dedicated facts: Echo/Weaver/Ticking/Ram/Mimic/Blinder/Herald/Stare | `OwnsCommand` verifies the identified hunter owns the prefix or exact Herald alias before binding lookup. A misconfigured Herald SoundId cannot smuggle `hunter.attack` or `ram-bellow`. |
| Progression hidden mutation → anonymous pending TellId | Only roster-owned ids are admitted for entity-free tells, never shared/legacy ids. Unknown/missing tells warn. Mannequin long-step is authored silence until hold/motion can be proved. |
| Direct `AudioDriver.PlayCue[At]` → `AudioSoundscapeDriver.Play[Local]` → shared enemy bank | Raw enemy/death cues require an explicitly identified legacy emitter. New/unknown/default emitter 0 cannot bypass the roster. Player/environment/UI sounds remain unchanged. |
| HunterHit → LastAttacker → catch admission → `PlayDeath(false)` | Own `{archetype}.death` only. Unknown/missing attacker or missing clip warns and stays silent; no generic death fallback. Duplicate/death admission stays with the existing catch presenter. |
| `PlayDeath(true)` for the spectral hand | Explicit hand-only path may use its Death bank; this is not a new hunter and cannot be reached through the generic local-cue loophole. |
| Mannequin confirmed catch | Own short snap at gain 0.30; no loud shared sting. No presence/detection/chase/attack/mutation sound while frozen. Movement creak is not implemented without motion facts. |
| Existing serialized stale roster rows | Setup resolves the entire manifest before mutation, then replaces ALL owned roster ids while preserving legacy/unrelated ids. Removed speech/old aliases cannot survive setup as stale monster bindings. |

Legacy compatibility is intentional, not disabled globally. Chase music, player breathing and world ambience remain separate from hunter vocal bindings. Fixed attack facts retain gain/pitch/position/timing; no new cadence timer, random delay or audio slicing was added.

## Per-hunter audible selection summary

Keys below resolve to exact paths and all model output/conflicts in `SELECTION.md`. This table describes requested assignment, not current live Unity state.

| Hunter | Cue → selected clip keys → MOSS description |
|---|---|
| Echo | replay/presence → step1/2 → short percussive transients / metallic clinks; attack/catch → wood1 → wooden strike (other pass says metallic). Detection/chase human breath withheld with warnings. Wrong rhythm remains gameplay's replay timing. |
| Weaver | presence/skitter/chase → skitter1/2 → brittle irregular crackles; web/attack/catch → wet1/2 → airy transient / metallic click. Model does not establish wet material. |
| Ticking | tick → tick1/2 → mechanical click in D, creature/orc in Q/F (unresolved); winding/chase → wind-up → small hard mechanical clinks; key/wake/catch → bell → resonant metallic chime. |
| Ram | stride/stamp → wood1/2 → short wooden contact (D differs); bellow/detection/chase/catch → roar1 → large resonant animal growl; collision → heavy2 → deep hollow thud / explosion. ONLY Ram intentionally receives a large creature bellow. |
| Skip | approach/relocation/detection/chase/attack → silence; confirmed catch → wood1 → brief physical strike. No Monster Bite or low growl. |
| Mimic | disguise/detection/chase → silence; wrong-bite/confirmed catch → crunch → dry material snap/crinkle in D, creature squeal in Q/F (unresolved). |
| Blinder | presence/detection/chase/throw/catch → hiss → hiss/thump in Q, electronic tone in D, creature squeal in F (unresolved); trap → tick2 → same mechanical/creature conflict. |
| Herald | exact discovery/chase/attack ids → herald-discovery/chase1/chase2/attack → piercing screams / electronic alarms; warning human breath withheld. No generic monster breath. |
| Mannequin | frozen/generic/mutation → silence; confirmed catch → snap → brittle dry-material crack in D, creature squeal in Q/F (unresolved). wood-creak remains unbound audition candidate. |
| Stare | exact spoken calls/chase/mutation → missing, warns; attack/catch → snap → same dry-crack/creature conflict. No abstract engine/monster scream substituted for speech. |

No intentional pig/orc/goblin assignment is approved. MOSS is an advisory listener, not reliable proof of material or creature absence. In particular the conflicted tick/hiss/snap/crunch selections MUST be heard by the owner before audio acceptance. Repeated model calls cannot manufacture that approval.

## Evidence and native checks

Read-only main-checkout vendor/model hashes were verified. `Logs/AgentValidation/Horror/audio-analysis/` contains 45 structured responses, 45 open descriptions and 13 follow-up responses across 49 distinct analyzed paths; one overlong whisper was excluded, not cropped. The audit verifies every selected whole-file SHA-256 still matches and every chosen path has model evidence. Raw descriptions include unsupported model narratives and conflicts; they are retained verbatim.

Pure coverage: ten identities × all five slots plus habits, exact/null binding policy, unknown/default emitters, legacy compatibility, alias ownership, parser safety, authored silence vs missing clips, and real-manifest identities.

Coordinator native checks (not run by worker):

- `AudioRosterPlaybackTests.MissingOrNullRosterSlotsNeverPlayPopulatedLegacyBanks` (ten cases; each exercises absent and exact-null bindings for five slots with populated legacy sentinels).
- `RawSharedAttackCannotBypassRosterBindingAndAuthoredSilenceAllocatesNothing`, `DedicatedFactCannotSmuggleSharedOrOtherHunterBinding`, `UnknownCatchWarnsOnceAndExplicitHandCatchStillUsesItsBank`.
- `MannequinCatchPlaysQuietSnapAndNeverGenericFallback`, `SetupReplacesStaleOwnedBindingsButPreservesLegacyAndUnrelatedIds`, `MissingSetupAssetFailsBeforeChangingAnyBinding`.
- `AudioCatchRoutingTests`, `AudioExpansionRoutingTests`, `AudioRosterTests`, `AudioSoundscapeDriverTests`, `AudioDriverTests`, `AudioChaseMusicPlaybackTests`, `AudioMixerPlayModeTests`.
- Dependent `CameraHandCatchRoutingTests`, `FeedbackRoutingIntegrationTests`, `ChaseLossIntegrationTests`, `RuntimeSettingsTests`, `RoutingExpansionSetupTests`, `SetupReferenceAuditTests`. Legacy compatibility fixtures now identify their rusher emitter or author exact Echo bindings rather than accidentally asserting forbidden fallback. No skip was added.
- In a mixed-ten-hunter owner playtest, record the actual active source clip on each cue, deliberately remove one binding and confirm one warning/no other bank, hold Mannequin frozen and catch, and separately verify the hand death still works. Check tick/hiss/snap/crunch for audible creature coloration and adjust only their owned selections if rejected.

## Provisional values

No new runtime timer, distance or random tunable. Existing selected linear gains remain provisional; complete values live in `SELECTION.md`. Changed gains: echo.death 0.40; ticking.chase/death/winding 0.45; ram.detection/chase 0.35; skip.presence/detection/chase/attack 0, skip.death 0.30; blinder.presence 0.20, chase 0.25, death 0.40; herald.presence 0; stare.attack/death 0.35; mannequin.long-step 0. New `{hunter}.turn` and `{hunter}.cake-reaction` gains are 0 for all ten hunters. Mannequin catch remains 0.30. Missing placeholders retain positive authored gains solely to distinguish a warning from intentional silence; they play nothing.

## Cross-owner requests and limits

- Coordinator: `Assets/Resources/ScriptableObjects/Presentation/Audio/AudioSoundscapeDriverConfig.asset`, `_rosterBindings`: run `HunterRosterAudioSetup.BuildMenu` (menu: Worsen/Audio/Assign Hunter Roster Selection) in licensed main checkout after integration; verify resolved clip refs and run native tests. Worker must not hand-edit this asset or run Unity.
- Core/Domain/Orchestrator owners: `Assets/Scripts/Core/Definitions/MannequinFactDefinitions.cs` (`MannequinFactKind`/`MannequinFact`), `Assets/Scripts/Domain/Hunter/Archetypes/Mannequin/Controller/MannequinController.cs` (movement/hold publication), `Assets/Scripts/Orchestrator/AudioOrchestrator.cs` (`OnMannequin` route): supply movement/hold facts with position so Audio can emit wooden creaks only during actual movement and stop them on freezing. Current `SilentSoundSet`/`RoomLightOverride`/`LampBudget` metadata is not proof of movement. These files were not edited.
- Content owner: provide/approve whole licensed human breaths for Echo and Herald, exact Stare spoken calls, and audition all selected clips (especially MOSS-conflicted ones). No verified replacement breath/speech was found in the evaluated installed candidates; no downloads/recording generation were authorized.
- GitNexus is partly stale/ambiguous (duplicate staging symbols). Exact Assets UIDs plus content searches were used. AudioRosterPresenter reports CRITICAL upstream risk; direct source caller is AudioSoundscapeDriver. AudioSoundscapeDriver's exact Assets UID reports LOW with an empty caller set, contradicted by the AudioDriver facade and dependent test fixtures. Setup also reports LOW with no indexed callers; BuildMenu and setup/playback fixtures explicitly reference it. Empty/index-stale results were not treated as proof of no dependency; edits stayed within owned Audio paths.
