# Architecture findings report

ARCH_RESULT findings=100 waived=88 expired=0

One row per finding; symbols distinguish classes sharing a file. This is evidence, not an approved waiver ledger.

| Path | Alarm | Measured value | Symbol | Status |
|---|---|---|---|---|
| Assets/Editor/Scenes/HorrorRunSceneSetup.cs | responsibilities | 9 |  | waived |
| Assets/Editor/Tests/Architecture/ArchitectureConformanceTests.cs | responsibilities | 8 |  | waived |
| Assets/Editor/Tests/Camera/FeedbackRoutingIntegrationTests.cs | responsibilities | 7 |  | waived |
| Assets/Editor/Tests/Chase/HunterRouteIntegrationTests.cs | responsibilities | 6 |  | waived |
| Assets/Editor/Tests/Director/DirectorHintIntegrationTests.cs | responsibilities | 6 |  | waived |
| Assets/Editor/Tests/Expedition/HorrorRunIntegrationTests.cs | responsibilities | 6 |  | waived |
| Assets/Editor/Tests/Floor/FloorControllerTests.cs | responsibilities | 9 |  | waived |
| Assets/Editor/Tests/Floor/FloorLifecycleTests.cs | responsibilities | 7 |  | waived |
| Assets/Editor/Tests/Hunter/HunterChaseIntegrationTests.cs | responsibilities | 6 |  | waived |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | responsibilities | 6 |  | waived |
| Assets/Editor/Tests/Player/PlayerControllerTests.cs | responsibilities | 11 |  | waived |
| Assets/Editor/Tests/Player/PlayerDriverTests.cs | responsibilities | 8 |  | waived |
| Assets/Editor/Tests/Player/PlayerMoverPresenterTests.cs | responsibilities | 7 |  | waived |
| Assets/Editor/Tests/Procedural/ProceduralCastlePresenterTests.cs | responsibilities | 7 |  | waived |
| Assets/Editor/Tests/Progression/ProgressionSessionControllerTests.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Core/Definitions/PlayerMovementDefinitions.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Core/Worsen.Core.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Domain/Director/Manager/DirectorManager.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Floor/Config/FloorConfig.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Domain/Floor/Config/FloorDriverConfig.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Domain/Floor/Controller/FloorController.cs | responsibilities | 13 |  | waived |
| Assets/Scripts/Domain/Floor/Driver/FloorDriver.cs | responsibilities | 14 |  | waived |
| Assets/Scripts/Domain/Floor/Driver/FloorExitDoor.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Floor/Driver/RoomCollapseVolume.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Floor/Manager/FloorManager.cs | relay-surface | {"handlers": 4, "publicEvents": 17, "relayHandlers": 0} | FloorManager | error |
| Assets/Scripts/Domain/Floor/Manager/FloorManager.cs | responsibilities | 12 |  | waived |
| Assets/Scripts/Domain/Floor/State/FloorBehaviorState.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Domain/Hunter/Config/HunterProfile.cs | responsibilities | 10 |  | waived |
| Assets/Scripts/Domain/Hunter/Controller/HunterController.cs | responsibilities | 14 |  | waived |
| Assets/Scripts/Domain/Hunter/Driver/HunterDriver.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Domain/Hunter/Driver/HunterSteeringPresenter.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | relay-surface | {"handlers": 5, "publicEvents": 20, "relayHandlers": 2} | HunterManager | error |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | responsibilities | 11 |  | waived |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | type-switch | 8 | Initialize | error |
| Assets/Scripts/Domain/Hunter/State/HunterBehaviorState.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Level/Manager/LevelManager.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Player/Config/PlayerProfile.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Player/Controller/PlayerController.cs | responsibilities | 22 |  | waived |
| Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs | responsibilities | 14 |  | waived |
| Assets/Scripts/Domain/Player/Driver/PlayerMoverPresenter.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Domain/Player/Manager/PlayerManager.cs | responsibilities | 13 |  | waived |
| Assets/Scripts/Domain/Player/State/PlayerBehaviorState.cs | responsibilities | 14 |  | waived |
| Assets/Scripts/Domain/Procedural/Config/ProceduralConfig.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Domain/Procedural/Controller/ProceduralController.cs | responsibilities | 11 |  | waived |
| Assets/Scripts/Domain/Procedural/Definitions/ProceduralDefinitions.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs | responsibilities | 11 |  | waived |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralDriverState.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralGeometryPresenter.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Domain/Procedural/Manager/ProceduralManager.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Domain/Worsen.Domain.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Orchestrator/AudioOrchestrator.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Orchestrator/EnvironmentOrchestrator.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Orchestrator/HUDOrchestrator.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Orchestrator/HorrorOrchestrator.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Orchestrator/MenuOrchestrator.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Orchestrator/ProgressionUIOrchestrator.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs | responsibilities | 10 |  | waived |
| Assets/Scripts/Orchestrator/Worsen.Orchestrator.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Presentation/Audio/Config/AudioSoundscapeDriverConfig.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Audio/Driver/AudioDriver.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Presentation/Audio/Driver/AudioFeedbackPresenter.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapeDriver.cs | responsibilities | 11 |  | waived |
| Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapePresenter.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Audio/Manager/AudioManager.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/Camera/Driver/CameraDriver.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/Camera/Driver/CameraDriverState.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Camera/Driver/CameraFeedbackPresenter.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/Camera/Manager/CameraManager.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Environment/Driver/EnvironmentDriver.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Presentation/Environment/Driver/EnvironmentPresenter.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/Environment/Manager/EnvironmentManager.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/HUD/Driver/HUDPresenter.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Presentation/HUD/Manager/HUDManager.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/Input/Driver/PlayerInputDriver.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Presentation/Input/Manager/InputManager.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXDriver.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXDriverState.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXPresenter.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/PostFX/Manager/PostFXManager.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIDriver.cs | responsibilities | 8 |  | waived |
| Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIPresenter.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Presentation/ProgressionUI/Manager/ProgressionUIManager.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Presentation/Worsen.Presentation.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Session/Expedition/Controller/ExpeditionSessionController.cs | responsibilities | 10 |  | waived |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | fan-out | 11 | ExpeditionSessionManager | error |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | responsibilities | 18 |  | waived |
| Assets/Scripts/Session/Expedition/State/ExpeditionSessionBehaviorState.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Session/HorrorEffects/Manager/HorrorEffectsManager.cs | fan-out | 6 | HorrorEffectsManager | error |
| Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs | responsibilities | 13 |  | waived |
| Assets/Scripts/Session/Progression/Manager/ProgressionSessionManager.cs | responsibilities | 12 |  | waived |
| Assets/Scripts/Session/Progression/Shop/Controller/ShopController.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Session/Progression/State/ProgressionSessionBehaviorState.cs | responsibilities | 7 |  | waived |
| Assets/Scripts/Session/Run/Controller/RunSessionController.cs | responsibilities | 9 |  | waived |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | fan-out | 7 | RunSessionManager | error |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | relay-surface | {"handlers": 37, "publicEvents": 44, "relayHandlers": 19} | RunSessionManager | error |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | responsibilities | 16 |  | waived |
| Assets/Scripts/Session/Run/State/RunSessionBehaviorState.cs | responsibilities | 6 |  | waived |
| Assets/Scripts/Session/Worsen.Session.asmdef | asmdef | autoReferenced=true |  | warning |

## Session edges

| Source | Dependency | Evidence paths |
|---|---|---|
| Expedition | HorrorEffects | Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs |
| Expedition | Progression | Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs |
| Expedition | Run | Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs |
| HorrorEffects | Progression | Assets/Scripts/Session/HorrorEffects/Manager/HorrorEffectsManager.cs |
| Run | Progression | Assets/Scripts/Session/Run/Manager/RunSessionManager.cs |

Proposed dependency-first order: ["Progression", "SceneFlow", "Settings", "HorrorEffects", "Run", "Expedition"]
