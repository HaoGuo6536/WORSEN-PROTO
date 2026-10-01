# PLAN-021 audio pass 2 — partial, not ready for promotion

## Implemented and checked offline

- Removed the landing-impact heartbeat fallback. An absent dedicated clip stays silent and warns once on the first heartbeat per driver lifetime, including across run resets.
- Preserved the pure heartbeat envelope and exposed it through AudioSoundscapeDriver, AudioDriver and AudioManager; public presentation output is zero when paused, disabled, dead or outside a run. No PostFX routing was changed.
- Set both the serialized soundscape asset and HorrorAudioSetup to ambience gain 0.01.
- AudioMixerSetup creates Effects/Ambience and exposes AmbienceVolume. Keeping Ambience under Effects preserves the existing user Effects slider/mute; sources do not multiply that bus gain twice. The regenerated mixer itself requires Unity.
- Restored logarithmic source rolloff. Spatial voices use only the graph's portal/door attenuation in the presenter, preventing duplicate distance loss. Nonspatial voices still receive the original graph distance calculation.
- Conditional spatialization uses AudioSettings.GetSpatializerPluginName(), without a SteamAudio assembly dependency. Spatialization and blend are reset when a pooled source is reused for a 2D cue.
- Exposed AudioManager.EffectsGroup (and AudioDriver.EffectsGroup) for coordinator-routed cake-trap mixer wiring. Floor still must not depend on Presentation.
- Herald bindings, all other clip bindings, jump silence, Core, Domain, Orchestrators and ProjectSettings are unchanged.

## Audit reconciliation

1. Heartbeat null and Land fallback confirmed. The fallback is fixed; the dedicated clip remains unbound. A filename scan found no heartbeat-named WAV/MP3/OGG/FLAC/AIFF in the installed Assets tree; this does not prove no unlabeled candidate exists.
2. Pickup provenance confirms Vefects item_pick_up; golden pickup alternates Vefects pick_up and Magic Spell_Coins_2, rather than only the latter. Hit/catch legacy clips are Bloody punch/Indiana Jones Punch. They have NOT been reapproved by this pass.
3. Serialized ambience was 0.25 while the code default was 0.01. Both generated and checked-in config now use 0.01; perceived balance against Dither Fog still requires listening in Unity.
4. Read the main checkout's generated mixer: Master/Music/Effects, no Ambience or AmbienceVolume. The worktree mixer is an LFS pointer, so it was not hand-edited. Read FloorCakeTrap.Configure: its AudioSource has no outputAudioMixerGroup.
5. The driver used a flat custom curve. Conditional logarithmic attenuation is now implemented. Steam Audio source and Windows phonon binaries are present in the main checkout; this pass neither selected nor exercised the plugin.
6. Echo/Herald breath and Stare speech remain unbound. No animal breath or growl was added.
7. Tick/hiss/snap/crunch label conflicts remain unresolved until a fresh MOSS run; filename labels are not treated as evidence.

## MOSS evidence and blocking gate

Newly bound clips: **none**. There is no pass-2 MOSS evidence table to certify yet: GPU-window admission was requested, but not received. The audio skill requires coordination before loading the model. No inference, fabricated rating, vendor copy, download or main-checkout write was substituted.

CPU-only preparation DID run successfully. Worktree-local evidence:

- `Logs/AgentValidation/Horror/audio-analysis/pass2-001/candidates.json`
- `Logs/AgentValidation/Horror/audio-analysis/pass2-001/model-files.json`
- `tools/audio/pass2-candidates.json` records all exact existing source paths.
- 16 candidates frozen with source and excerpt hashes, duration, sample rate, peak and RMS; 0 excluded. Model manifest includes 14 files. These are integrity/level measurements, NOT MOSS listening or fit scores.

| Candidate IDs | Pending evaluation, not approval |
|---|---|
| human-breath, human-female | Human rather than animal breath for Echo/Herald |
| ghost-quiet, ghost-wind, ghost-no1, shh | Whisper/wordless Stare vocal, reject monster/animal identity |
| cloth-leather | Optional subtle jump/contact; jump remains silent pending fit |
| body-wet, dark-impact, stress-impact | Hit, catch and hand/body texture; reject comic punch identity |
| short-reverse, suspense | Pickup, mist/tear/consuming swell; reject bright UI/magic identity |
| tick1, hiss, snap, crunch | Fresh blinded pass on all four prior label conflicts |

No exact CC0 item URL/licence could be verified from the available local records. Network lookup/download was prohibited; no invented CC0 listing is supplied. Coordinator should supply a licensed heartbeat candidate (or approve external discovery) if no installed candidate passes.

After explicit GPU admission, use the existing `analyze_roster.py` against the frozen candidate manifest and worktree output directory, with `--gpu-window-admitted`, and inspect every per-clip Q/D/F response. Do not bind from this pending table. Any derived deliverable then needs its provenance record and final hash-matched MOSS review. Feedback/hand/room admission is intentionally not enabled against the still-mismatched legacy banks.

## Integration requests and setup

- `Assets/Scripts/Domain/Floor/Driver/FloorDriver.cs`: accept an injected AudioMixerGroup through an owned configuration method; propagate it to existing and newly created traps in ConfigureTrap and SyncCakeTraps.
- `Assets/Scripts/Domain/Floor/Driver/FloorCakeTrap.cs`, Configure: accept that group and assign `_source.outputAudioMixerGroup`. The composing Orchestrator supplies `AudioManager.EffectsGroup` after Audio initialization. Do not add a Domain-to-Presentation reference.
- `ProjectSettings/AudioManager.asset`, `m_SpatializerPlugin`: coordinator selects the installed Steam Audio spatializer; verify both plugin-active and empty-plugin paths in Unity.
- `Assets/Scripts/Orchestrator/AudioOrchestrator.cs`: OnHealth, OnHand and OnDestruction already forward the current typed facts to Audio. No missing subscription was found in this base. The AudioFeedbackPresenter/catalogue still suppress hit/hand/new room cues under the old cue budget; implementing those mappings and bindings remains Audio work after selection, not a request to Floor to solve Presentation logic.
- Heartbeat/PostFX: coordinator routes `AudioManager.HeartbeatEnvelope` as the PostFX input, with subscriptions/lifecycle owned by Orchestrators. The inspected PostFX presentation has no heartbeat input yet, only injury-derived vignette behaviour; its owner must add the appropriate input rather than wiring Audio directly to PostFX.
- After the audio-selection blocker is resolved and before Unity assertions: `HorrorAudioSetup::BuildMenu`, `HunterRosterAudioSetup::BuildMenu`, `AudioMixerSetup::Build`. HorrorAudioSetup already invokes mixer Configure; finish with Build to repair the shared groups. Do not interpret running current setup as completion of the still-pending clip pass.

## Provisional settings

- Ambience source gain 0.01: AudioSoundscapeDriverConfig asset/default and HorrorAudioSetup. Existing intended value, now serialized consistently; not listening-approved.
- AmbienceVolume: new exposed mixer child gain, initial 0 dB, inherited Effects user gain.
- Spatial cue distances: bank MinimumDistance/MaximumDistance (existing defaults 1/20 m); hunter presence reference uses existing Hearing.ReferenceDistance (2 m) and KeenEarsRangeMultiplier (1.5); roster max uses existing RosterMaximumDistance (32 m). Minimum clamp 0.1 m and maximum at least minimum + 0.1 m are validity guards, not new designer fields.
- No new designer-tunable C# serialized fields or clip gains were introduced.

## Impact and tests

GitNexus AudioWorldMixPresenter is CRITICAL, partial/truncated (includes staging trees); indexed direct caller AudioSoundscapeDriver.Initialize, plus an unnamed edge. AudioMixerSetup and three indexed test classes are LOW with no indexed direct callers. AudioDriver, AudioManager, AudioSoundscapeDriver, config, HorrorAudioSetup and AudioSoundscapeDriverTests are ambiguous/UNKNOWN; do not treat their empty results as safe. Text-search tie-breaker confirms runtime caller chain Orchestrators/scene roots -> AudioManager -> AudioDriver -> AudioSoundscapeDriver -> AudioWorldMixPresenter. Mixer Configure is called by HorrorAudioSetup, menu Build and setup/play-mode tests. prepare_roster_candidates.main is UNKNOWN/unindexed; its only discovered invocations are its CLI entry point and documented command, not runtime code.

Added four headless cases in AudioWorldMixDistanceTests: distance owned once, door/portal retention, missing graph and unknown room, for both native-rolloff modes. Added native tests for the absent heartbeat/envelope/one-warning contract, spatial-to-2D pooled reuse, and graph gain without double distance. Existing mixer tests now resolve the exact Effects group (FindMatchingGroups also returns its new child), assert a separate Ambience group, and check AmbienceVolume exposure and inherited Effects mute. No failure was hidden by a skip or weakened assertion.

Coordinator Unity focus:

- AudioSoundscapeDriverTests.MissingHeartbeatNeverReusesLandAndRetainsEnvelopeAcrossResets
- AudioSoundscapeDriverTests.SpatialWorldVoiceUsesRolloffAndResetsSpatializationWhenReusedForTwoDimensionalCue
- AudioWorldMixPresenterTests.SpatialSourceOwnsDistanceWhilePortalAndDoorAttenuationRemain
- AudioMixerSetupTests.SetupIsIdempotentAndPreferencesUseAssignedMixerWithoutDoubleAttenuation
- AudioMixerPlayModeTests.PreferencesAreAcceptedReadBackAndUseUnitySourceGainInPlayMode
- AudioRosterPlaybackTests, AudioCatchRoutingTests, AudioChaseMusicPlaybackTests and RuntimeSettingsTests

The highest Unity risk is native mixer reflection/group regeneration and plugin-active output/falloff, not covered by offline pass counts. The new MonoBehaviour/ScriptableObject tests are environment-classified headlessly; Unity must execute them.

## Verification checkpoint

`audio-pass2-002`: all seven assemblies compile with 0 errors; warning counts match baseline (Core 0, Domain 41, Session 0, Presentation 34, Orchestrator 70, Editor 0, Tests 0).

`audio-pass2-pure-002`, filter `^Worsen[.]Tests[.]Audio[.]`:
`PURE_RESULT passed=200 failed=0 environment=88 skipped=8`

Four new pure cases passed. Environment/skipped cases are not passes. Their exact case names/reasons are in `Logs/AgentValidation/PLAN-002/offline-compile/audio-pass2-pure-002/summary.json`; run the full Audio namespace in Unity, including the eight coroutine-dependent cases in AudioChaseMusicPlaybackTests, AudioDriverTests and AudioMixerPlayModeTests. Candidate-manifest unit tests: 4 passed (including invalid-input subcases). CPU preparation: 16/16 candidates, no exclusions. ast-grep scan: 0 findings. git diff --check: clean.

Unity, playtest, model inference, clip selection/binding/provenance additions, CC0 licence verification, vignette routing and full pass-2 acceptance remain unverified or blocked. No commit, staging, push or Unity control was performed.
