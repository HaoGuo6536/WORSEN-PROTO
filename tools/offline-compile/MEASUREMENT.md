# PLAN-011 headless pure-test measurement — L2-08 / L5-01

## Scope and acceptance

Measured in `C:/Users/Hao Guo/Documents/UnityProjects/WORSEN-wt/puretest-runner`, branch `wt/puretest-runner`, with required ancestor `7e7c4bd` present. No Unity process, bridge, test lease, import or native runtime verification was used; shared `Library` was read-only.

Compile evidence: `puretest-002`. Whole-suite evidence: `puretest-full-005`, under `Logs/AgentValidation/PLAN-002/offline-compile/` in this worktree. Harness sources and dependencies are hash-recorded inside the run.

- `PURE_RESULT passed=733 failed=0 environment=1531 skipped=67`
- Discovered cases: 2,331 (the whole current suite, not only the 1,218 new cases mentioned in the audit context).
- Execution wall time: 3.4248206 seconds. Total script wall time including dependency preparation and harness compilation: 4.7934073 seconds.
- Pure fraction: 733 / 2,331 = 31.445731445731447%.
- Under-three-minute target: met. At-least-half-pure target: **not met**; at this unchanged denominator, at least 433 additional genuinely pure cases are needed.
- Environment causes: 1,175 ECall results; 356 conservative UnityEditor API dependencies. These are unavailable cases, not passes. No configuration/engine stubs or constructor bypasses were used to improve coverage.
- Summary SHA-256: `9331b4e3eeac9be176896bfb962a6f63470f7c68b6a7a8f2b980c2fa5a84b6ec`.
- Compiled test assembly SHA-256: `4F3B111AFEB93A310E68F4E80D3C73BA993DE7B1A59ABEF3ED4555D277975BF5`.

## Totals by namespace

Grouping uses the first system namespace below `Worsen.Tests`. Every row has the `Worsen.Tests.` prefix. Totals were checked against all enumerated cases, not inferred from NUnit aggregate pass counts.

| Namespace suffix | Passed | Failed | Environment | Skipped |
|---|---:|---:|---:|---:|
| Architecture | 0 | 0 | 13 | 0 |
| Audio | 58 | 0 | 51 | 7 |
| Camera | 0 | 0 | 35 | 2 |
| CastleEnvironment | 23 | 0 | 6 | 0 |
| Chase | 0 | 0 | 39 | 1 |
| Core | 43 | 0 | 0 | 0 |
| DebugOverlay | 17 | 0 | 1 | 0 |
| Director | 0 | 0 | 50 | 4 |
| Expedition | 32 | 0 | 35 | 3 |
| Floor | 93 | 0 | 127 | 4 |
| Fog | 0 | 0 | 12 | 0 |
| Horror | 15 | 0 | 59 | 0 |
| HorrorEffects | 0 | 0 | 66 | 0 |
| HUD | 44 | 0 | 7 | 0 |
| Hunter | 100 | 0 | 312 | 3 |
| Input | 28 | 0 | 3 | 9 |
| Level | 25 | 0 | 8 | 18 |
| Menu | 0 | 0 | 21 | 0 |
| Player | 35 | 0 | 247 | 7 |
| PostFX | 0 | 0 | 56 | 0 |
| Procedural | 19 | 0 | 180 | 0 |
| Progression | 7 | 0 | 122 | 2 |
| ProgressionUI | 45 | 0 | 10 | 0 |
| Results | 33 | 0 | 6 | 1 |
| Run | 44 | 0 | 30 | 3 |
| SceneFlow | 9 | 0 | 0 | 0 |
| Scenes | 0 | 0 | 0 | 3 |
| Settings | 3 | 0 | 11 | 0 |
| Shrine | 0 | 0 | 19 | 0 |
| Telemetry | 60 | 0 | 5 | 0 |
| **Total** | **733** | **0** | **1531** | **67** |

## Environment-dominated fixtures

140 fixture classes have environment results for strictly more than half of their discovered cases (including skipped cases in the denominator). Below, concatenate the namespace and each class name for the exact fully qualified identifier. Per-fixture counts and every environment message are in `summary.json`.

- `Worsen.Tests.Architecture`: ArchitectureConformanceTests.
- `Worsen.Tests.Audio`: AudioCatchRoutingTests, AudioChaseMusicPresenterTests, AudioMixerSetupTests, AudioSoundscapeDriverTests, AudioWorldMixPresenterTests, AudioWorldPresenterTests.
- `Worsen.Tests.Camera`: CameraFeedbackPresenterTests, CameraTraversalPresenterTests, CatchPresentationRoutingTests.
- `Worsen.Tests.CastleEnvironment`: EnvironmentOrchestratorRoutingTests.
- `Worsen.Tests.Chase`: ChaseControllerTests, HunterRouteIntegrationTests.
- `Worsen.Tests.Director`: DirectorControllerTests, DirectorManagerTests.
- `Worsen.Tests.Expedition`: ExpeditionActiveEffectsTests, ExpeditionFloorHookTests, ExpeditionFollowupTests, ExpeditionProfileRosterTests, ExpeditionWorldWiringTests, ShrineWorldRouteTests.
- `Worsen.Tests.Floor`: FloorCakeIntegrationTests, FloorCakeRulesTests, FloorCollapseIntegrationTests, FloorControllerTests, FloorExitDoorIntegrationTests, FloorFreestandingExitTests, FloorGoldenCakeVisualTests, FloorLifecycleTests, FloorLumenIntegrationTests, FloorSafeShuffleTests, FloorThemeConsumerTests.
- `Worsen.Tests.Fog`: FogDensityPresenterTests, FogOrchestratorTests, FogThemeConsumerTests.
- `Worsen.Tests.Horror`: HorrorAtmosphereDriverTests, HorrorEffectHookTests, HorrorMicroEventPresenterTests, HorrorMicroEventRoutingTests, HorrorPresenterTests, PresentationWiringTests.
- `Worsen.Tests.HorrorEffects`: ConsumableControllerTests, ConsumableRoutingTests, HorrorEffectsControllerTests, HorrorTrapAndThrowTests.
- `Worsen.Tests.HUD`: HUDCompassVisualTests, HUDOrchestratorTests.
- `Worsen.Tests.Hunter`: BlinderControllerTests, BlinderHeraldProfileSetupTests, DefaultHunterControllerTests, EchoControllerTests, HeraldControllerTests, HorrorHunterContentIntegrationTests, HunterAttackPresenterTests, HunterAttackVocalTests, HunterBeliefClearTests, HunterClearTurnTests, HunterControllerTests, HunterCornerClearanceTests, HunterCurseScopeTests, HunterDriverTests, HunterEchoIntegrationTests, HunterExpansionControllerTests, HunterFactoryRosterTests, HunterHabitMutationTests, HunterIntelligenceTests, HunterLightDriverTests, HunterPrefabGeneratorTests, HunterRangedDriverTests, HunterStairTraversalTests, MimicControllerTests, RamControllerTests, RosterBIntegrationTests, SkipControllerTests, TickingControllerTests, TickingIntegrationTests, WeaverControllerTests, WeaverIntegrationTests.
- `Worsen.Tests.Input`: InputPauseActionTests.
- `Worsen.Tests.Level`: FloorLoopNavigationTests, LevelInteractableControllerTests, LevelTraversalEndpointAuthoringTests.
- `Worsen.Tests.Menu`: MenuOrchestratorTests, MenuPauseOwnershipTests, MenuPresenterTests.
- `Worsen.Tests.Player`: BlockyCharacterSetupTests, PlayerActiveEffectsTests, PlayerConsumableTests, PlayerControllerTests, PlayerDriverTests, PlayerEffectUtilityTests, PlayerHitRecoveryIntegrationTests, PlayerShieldTests, PlayerTrapSpeedTests, PlayerWebSlowTests.
- `Worsen.Tests.PostFX`: PostFXConsumableTests, PostFXGraceTests, PostFXOrchestratorRoutingTests, PostFXPresenterTests.
- `Worsen.Tests.Procedural`: ProceduralCastlePresenterTests, ProceduralControllerTests, ProceduralDriverTests, ProceduralFootprintUtilityTests, ProceduralFreezeUtilityTests, ProceduralGeometryPresenterTests, ProceduralInteractablePresenterTests, ProceduralNavigationPresenterTests, ProceduralPassageNavigationTests, ProceduralPassagePresenterTests, ProceduralPuzzleLayoutPresenterTests, ProceduralRoutePresenterTests, ProceduralShrineSitePresenterTests, ProceduralSpawnUtilityTests, ProceduralStoreyNavigationTests, ProceduralStoreyPresenterTests, ProceduralStoreyUtilityTests, ProceduralThemeUtilityTests, ProceduralWorldObjectTests.
- `Worsen.Tests.Progression`: ConsumableInventoryTests, EffectCatalogueConfigTests, ProgressionActiveEffectsTests, ProgressionConfiguredThreatTests, ProgressionEventControllerTests, ProgressionSessionControllerTests, ProgressionShrineTests, ShopControllerTests, ShrineProgressionControllerTests.
- `Worsen.Tests.ProgressionUI`: ProgressionEventRoutingTests, ProgressionUIVisualTests.
- `Worsen.Tests.Results`: RunInterfaceRoutingTests.
- `Worsen.Tests.Run`: RunFloorEffectWiringTests, RunHitRecoveryTests, RunHunterRosterWiringTests, RunPauseSummaryTests, RunShrineWiringTests.
- `Worsen.Tests.Settings`: RuntimeSettingsTests, SettingsDriverTests.
- `Worsen.Tests.Shrine`: ShrineControllerTests, ShrineManagerTests.
- `Worsen.Tests.Telemetry`: TelemetryDriverTests.

## Harness verification and retained earlier runs

`Test-PureRunner.ps1` against `puretest-002`, run `puretest-verified-003`, returned:

`SELF_TEST_OK cases=39 passed=9 failed=17 environment=5 skipped=8 timeouts=5`

The 17 failures are deliberate assertions, ordinary exceptions, invalid source and timeout cases. Actual GameObject allocation produced the captured CLR ECall exception; editor API detection recorded the exact member. Synthetic classifier inputs test additional exception boundaries but do not replace the actual engine-dependent fixture results. The verifier checks all 39 names/outcomes, inherited hooks, static source forms, ExpectedResult and Values, assertion precedence over engine cleanup, timeout continuation for all five execution/lifecycle paths, namespace/fixture totals, filters, Explicit exclusion, overwrite refusal and absence of surviving harness children. Its four filter probes have separate evidence directories and exit assertions.

The legacy wrapper, run `puretest-legacy-002`, returned `PURE_RESULT passed=82 failed=0 environment=10 skipped=0` for the old five-fixture regex. It now classifies their unavailable cases instead of silently excluding a hard-coded method.

Earlier run directories remain intact. In particular, `puretest-full-001` exposed false failures and false passes from omitted Unity coroutine lifecycle hooks; those fixtures are now explicitly skipped. `puretest-full-003` exposed CLR JIT-time ECall rejection without an engine frame; the exact runtime diagnostic is now recognized while ordinary SecurityException/MissingMethodException still fail. Earlier compiler/self-test failures are retained too. No failed-generation evidence was overwritten.

Project compile `puretest-002`: all seven assemblies returned exit 0, errors 0, with warnings matching baseline (Core 0, Domain 41, Session 0, Presentation 34, Orchestrator 70, Editor 0, Tests 0). Harness/self-test compile uses `-warnaserror+` and succeeded. `ast-grep scan` returned exit 0 with no findings; `git diff --check` returned exit 0.

No project test fixture or runtime assembly reference changed. Unity failures cannot be predicted from a green headless subset: focus, coroutine/lifecycle, serialization, native geometry and all 13 architecture cases still require coordinator execution. [REQUESTS.md](REQUESTS.md) records the exact worker and integration proposals, plus coverage migration/graph-index ownership requests.
