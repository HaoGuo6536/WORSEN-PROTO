# WP-A hand-off

## Delivery boundary

Audio-owned implementation and path selections are ready for integration. No complete plan or live-audio exit criterion is declared closed. No Unity process, asset import, lease, scene/prefab edit, `.meta` authoring, vendor copy, network request, model call, commit or staging operation was performed. The main checkout was read only. No Audio code publishes AI stimuli.

`SELECTION.md` is the setup input, not just prose: 75 named bindings reference 30 verified installed WAV paths. It covers all ten five-slot inventories, dedicated aliases, the Mannequin snap and the six currently authored mutation tell ids. Seven intentionally silent inventory slots remain dormant (Mannequin first four; Mimic first three). Clip character is filename-inferred; duration/peak/RMS are decoded measurements, not listening approval. Stare's required spoken words remain an explicit content gap.

## Backlog disposition

The following are the audited WP-A rows, not claims that other owners' work or Unity evidence is complete. References below are relative to `Assets/`.

| Backlog row | WP-A evidence / regression test | Remaining reason the entire row is not closed |
|---|---|---|
| PLAN-011 §3.3 hunter brief | `Editor/Audio/HunterRosterAudioSetup.Parse/Configure`, `Audio/HunterRoster/SELECTION.md`; `HunterRosterAudioSetupTests.RealSelectionCoversFiveSlotsForEveryHunterAndAllAuthoredMutationTells` passed | Session wiring, profile/art and live acceptance outside this package. |
| PLAN-012 C6 four cues | Existing no-Lose admission retained; `AudioSoundscapePresenterTests` and `AudioFeedbackPresenterTests` passed | Legacy all-clear criterion needs owner documentation amendment; live capture not allowed. |
| PLAN-015 C17 mutation + tell | Selected all six authored ids; `Presentation/Audio/Driver/AudioRosterPresenter.Mutation`; six `MutationTellQueuesOnceAndSurvivesOnlyFloorReset` cases passed | Actual integrated event/profile delivery and audiovisual acceptance need WP-I/V. |
| PLAN-016 C5 Echo; E3 complete content/live | `SELECTION.md` Echo five slots and replay alias, Weaver skitter/wet-click, Ticking aliases; real-manifest coverage test passed | Setup has not been invoked in Unity; art/arena acceptance remains. |
| PLAN-017 Hunter Echo | Five slots/replay clip alternatives selected; existing replay implementation retained | Integrated source matching/listening and admission/sweep evidence require coordinator. |
| PLAN-017 Hunter Weaver | Skitter and fixed web-warning paths selected; manifests assert fixed attack clip and alternates | Native web warning timing/ceiling presentation not exercised. |
| PLAN-017 Hunter Ticking | Tick/winding/key/wake paths selected; exact authoritative cadence implementation retained | Coordinator must run setup and cadence playback tests; no replacement Audio clock added. |
| PLAN-017 Hunter Ram | `AudioRosterPresenter.Ram`; five `RamTellsAndImpactsDoNotAddBudgetSlots` cases passed | WP-I must forward `HunterManager.OnRamFact`; geometry impact handling remains outside Audio. |
| PLAN-017 Hunter Blinder | `AudioRosterPresenter.Blinder`, `AudioWorldMixPresenter.BlinderHit`, receiver chain; five mapping cases plus `RawSensoryFactsReadUpgradesOnceAndIgnoreDuplicates` passed | WP-I must route raw accepted hit and sound facts; camera blindness remains WP-C. |
| PLAN-017 Hunter Herald | `AudioRosterPresenter.Herald/HeraldBreath`, `AudioWorldMixPresenter.HeraldDeafen`; fixed clip aliases; four `HeraldUsesEmissionTickAndAcousticOriginNotOldPlayerClue` cases, breath and effect tests passed | WP-I must forward scream/breath/deafen facts. Audio reads emission tick, not stale player-clue tick, and never forwards the fact's AI hint. |
| PLAN-017 Hunter Mannequin (owner catch request) | Identity receiver, generic/habit silence, no generic death fallback, 1.200 s snap selected at gain .30; pure identity/silence and manifest snap tests passed | Native `AudioRosterPlaybackTests.MannequinCatchPlaysQuietSnapAndNeverGenericFallback` compiled, not run; WP-I must seed identity before habits/hits. |
| PLAN-017 Hunter Mimic | `AudioRosterPresenter.Mimic` maps only committed bite; no pose/population/early-win audio; bite/catch-gating pure tests passed | World/arrow/accepted-hit wiring belongs to WP-I/H. |
| PLAN-017 Hunter Skip | Five-slot inventory selected; no relocation consumer added, retaining silent teleport and ordinary hit decision | Gameplay traversal/interception remains outside Audio. |
| PLAN-017 Hunter Stare | `AudioRosterPresenter.Stare` preserves sound id/position/curse volume, ignores unconfirmed catch; `MimicBiteAndStareCallRetainIdentityAndCurseVolume` passed | `stare.i-see-you` and `stare.find-me` have no verified exact speech clips. They remain unbound and warn; nonverbal sounds are not misrepresented as speech. WP-I forwarding also needed. |
| PLAN-017 Common cues/curses/gates/sweeps; E1 each hunter | Same manifest, mappings, effect readers and regression coverage above | Gates, sweeps, art, exact Stare speech and live routing are not all owned by Audio. |
| PLAN-021 C7 variation/alternates | Content/source gap closed: `AudioRosterBinding.Alternates/Gain/OverrideGain`, `AudioSoundscapeDriver.PlayRoster`, selection table; manifest alternate test and `AudioSoundscapePresenterTests.VariantsNeverRepeatImmediatelyAndPitchIsBounded` passed | Installation and actual source alternate playback still need Unity; native roster-specific test compiled. |
| PLAN-021 C8 unique presence through walls | Distinct selected presence material and tick/skitter paths, existing shared acoustic attenuation untouched | Stare speech remains missing; no listening proof. Bounded fact-driven one-shots retained rather than inventing continuous beds for silent hunters. |
| PLAN-021 C13 timed/effect hooks | Audio reader gap closed: raw Herald/Blinder receivers apply Ear Plugs/Mirror Skin once, dedup per hunter/player/tick, retain protected masks; pure upgrade/invalid-duration tests passed | Session/Orchestrator subscriptions outside owned scope remain missing. |
| PLAN-021 E3 budget/variation/parity tests | Audio-side tests added, existing budget/variation suite passed; no noise publisher exists in Audio | Negative gameplay ingress/parity tests belong to WP-I/V, native saturation still needed. |
| PLAN-021 E4 embodiment/hooks live | Receiver implementations and tests above | No live recording or integrated subscriptions yet. |
| PLAN-021 C3, C14, E1, E2, E5 | No changes: spatializer/mixer/live-audio rows are not closed | HRTF permission/install, serialized mixer binding, listening/owner acceptance are explicitly outside this worker's execution scope. |

## Exact requests to other owners

1. `Assets/Scripts/Session/Run/Manager/RunSessionManager.cs`, `SubscribeHunter` / `UnsubscribeHunter` / teardown: pair and relay `HunterManager.OnRamFact`, `OnMimicFact`, `OnBlinderSound`, `OnBlinderHit`, `OnHeraldScream`, `OnHeraldBreath`, `OnHeraldDeafen`, `OnMannequinFact`, `OnStareFact`. Preserve pause/lifecycle guards. Only deliver accepted/current-player sensory hits; do not apply an upgrade multiplier before the Audio raw-fact receivers (or it would be applied twice).
2. `Assets/Scripts/Orchestrator/AudioOrchestrator.cs`, `OnEnable` / `OnDisable`: pair the relays with these new `AudioManager` commands: `ObserveRam`, `ObserveMimic`, `ObserveBlinder`, `ObserveBlinderHit`, `ObserveHerald`, `ObserveHeraldBreath`, `ObserveHeraldDeafen`, `ObserveMannequin`, `ObserveStare`. Continue `OnEffectsSnapshot` and `RefreshViews` before delivery. Use typed raw-fact sensory receivers, not both those receivers and `OnDeafening`/`OnMuffledDark`. Ensure initial Mannequin/Mimic identity reaches Audio before habits or a first hit. Generic Herald/Blinder/Stare feedback is suppressed to avoid duplicate playback after typed routing.
3. Coordinator setup: invoke `HunterRosterAudioSetup.BuildMenu` after `HorrorAudioSetup.Configure` and settled vendor imports. Target is existing `Assets/Resources/ScriptableObjects/Presentation/Audio/AudioSoundscapeDriverConfig.asset`; no asset/scene was changed here. Setup pre-resolves every path, preserves unrelated bindings and saves only that config. Verify two runs are idempotent, bindings resolve, sources use mixer groups, and missing-vendor failure leaves the config untouched.
4. Content owner: supply verified licensed recordings for exact Stare calls `stare.i-see-you` and `stare.find-me`. Add their explicit path rows to selection; retain missing-clip warnings until then. No request to reconsider any settled hunter/hearing decision.
5. WP-I owns `ProjectSettings/AudioManager.asset` spatializer (only after approval) and mixer asset wiring. Audio emits no AI stimuli; authorized movement/firecracker/player-triggered cake-trap ingress is gameplay work, never inferred from playback.

## Tests and validation

Clean final compile: `wp-a-004`, all seven assemblies exit 0, errors 0, warning counts Core/Domain/Session/Presentation/Orchestrator/Editor/Tests = 0/41/0/34/70/0/0, equal to baseline. Snapshot hashes match current source and all reference snapshots were stable. The earlier `wp-a-002` compile failure (ambiguous NUnit `Using` overload) is retained; explicit generic type fixed it.

The prescribed `tools/offline-compile/Run-PureTests.ps1` is absent in this worktree and main checkout. Existing `Run-ManagedPure.ps1` is restricted to unrelated fixtures. A bounded Audio-owned fallback `Editor/Tests/Audio/Run-RosterPure.ps1` therefore executes actual compiled managed NUnit methods by reflection, including setup and teardown; it is not Unity Test Framework execution and does not edit test-infrastructure attributes. A preliminary Windows PowerShell/.NET Framework load probe failed on netstandard 2.1; the verified fallback uses the installed .NET Core runtime instead.

Final fallback run: `wp-a-pure-002`, compile input `wp-a-004`.

`PURE_RESULT passed=105 failed=0 environment_failures=0 skipped=0`

Filter: `^Worsen[.]Tests[.]Audio[.](AudioRosterPresenterTests|HunterRosterAudioSetupTests|AudioFeedbackPresenterTests|AudioMixPresenterTests|AudioSoundscapePresenterTests)[.]`

Evidence: `Logs/AgentValidation/PLAN-002/offline-compile/wp-a-004/` and `wp-a-pure-002/results.log`. `ast-grep scan` exits 0 with no findings; `git diff --check` passes. Independent path/table check: `MANIFEST_VALID clips=30 bindings=75 missing_paths=0`.

Native tests compiled, not executed: all five new `AudioRosterPlaybackTests` methods (alternates/budgets, Herald source/pitch, Mannequin snap/warning, effect application/protection, setup failure atomicity). Existing dependent Audio/Camera/Chase/Floor/Settings fixtures were read; no existing expectations were changed. Coordinator should run `AudioRosterTests`, `AudioWorldMixPresenterTests`, `AudioSoundscapeDriverTests`, `AudioDriverTests`, `AudioCatchRoutingTests`, `AudioMixerSetupTests`, `AudioChaseMusicPlaybackTests`, `RuntimeSettingsTests` and the new native fixture.

Likely pre-existing Unity failures: `ChaseLossIntegrationTests.PhysicalOcclusionReacquiresWithinGraceThenEndsOnceAfterGraceExpires` still requires a Lose cue, intentionally removed by silence-first admission; `CriticalHealthFeedbackIntegrationTests.ArrangedPublicDamageRoutesCriticalBreathingAndFreshLifeClearsIt` reads removed private `_hunter`. Those fixtures are outside WP-A ownership and unchanged. Generic-feedback suppression also requires the typed WP-I wiring above before expansion-hunter audio can be heard. Native playback, direction/occlusion, timing suitability, mixer balance, speech intelligibility, scene wiring and headphones acceptance remain unverified.

## Provisional values

- `AudioSoundscapeDriverConfig._rosterPitchVariation = .03` (non-exact clips use .97–1.03).
- `AudioSoundscapeDriverConfig._rosterGainVariation = .02` linear; exact commands force zero jitter.
- `AudioSoundscapeDriverConfig._earPlugsDurationMultiplier = .5` and `_mirrorSkinDurationMultiplier = .5`, once regardless of upgrade stack count.
- `AudioRosterBinding.Gain = 1`, `OverrideGain = false`, `Alternates = null` are backward-compatible constructor defaults. Existing serialized bindings keep the old config gain until explicitly overridden.
- Each selected binding's explicit provisional gain is listed in the executable `SELECTION.md` table (including `.30` Mannequin catch, `.50` fixed Herald screams and `.90` Ticking tick). No hidden normalization, new timer, priority or voice-cap values were added.

## Impact evidence

Main-checkout GitNexus upstream analysis preceded edits. Exact UID disambiguation reports `AudioSoundscapeDriverConfig` CRITICAL (direct `AudioDriver.Initialize`, `AudioDriver.Update`, two staging copies plus an unresolved accessor), `AudioManager` CRITICAL (unresolved accessor and broad setup flows), `AudioSoundscapeDriver` LOW (direct plan-document import only), `AudioDriver` LOW (no indexed direct callers). These sparse class-level results are not complete call graphs.

`AudioRosterPresenter`, `AudioWorldMixPresenter`, `AudioRosterBinding`, `AudioWorldMixDriverState` are absent from the index: UNKNOWN, not an all-clear. Text references confirm runtime ownership: Manager → Driver → Soundscape → Roster/WorldMix presenters, Config → roster bindings, SoundscapeState → WorldMixState; `AudioFeedbackPresenter` also uses WorldMix room lookup. External AudioManager callers include Audio/Settings Orchestrators and TagArena/FloorLoop/HorrorRun scene roots. Changes stayed within owned Audio paths. Index refresh and graph-wide conformance were not run because they would write the shared main checkout; coordinator owns those integration gates.
