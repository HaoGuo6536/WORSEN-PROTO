# Architecture findings report

ARCH_RESULT findings=242 waived=0 expired=0

One row per finding; symbols distinguish classes sharing a file. This is evidence, not an approved waiver ledger.

| Path | Alarm | Measured value | Symbol | Status |
|---|---|---|---|---|
| Assets/Editor/Scenes/FloorLoopSceneSetup.cs | fan-out | 22 | FloorLoopSceneSetup | error |
| Assets/Editor/Scenes/HorrorRunSceneSetup.cs | fan-out | 31 | HorrorRunSceneSetup | error |
| Assets/Editor/Scenes/HorrorRunSceneSetup.cs | responsibilities | 9 |  | error |
| Assets/Editor/Scenes/TagArenaSceneSetup.cs | fan-out | 19 | TagArenaSceneSetup | error |
| Assets/Editor/Tests/Architecture/ArchitectureConformanceTests.cs | responsibilities | 8 |  | error |
| Assets/Editor/Tests/Audio/AudioCatchRoutingTests.cs | fan-out | 7 | AudioCatchRoutingTests | error |
| Assets/Editor/Tests/Audio/AudioCatchRoutingTests.cs | fan-out | 7 | HunterFixture | error |
| Assets/Editor/Tests/Camera/CriticalHealthFeedbackIntegrationTests.cs | fan-out | 9 | CriticalHealthFeedbackIntegrationTests | error |
| Assets/Editor/Tests/Camera/CriticalHealthFeedbackIntegrationTests.cs | fan-out | 9 | CriticalHealthFrameObserver | error |
| Assets/Editor/Tests/Camera/CriticalHealthFeedbackIntegrationTests.cs | fan-out | 9 | Life | error |
| Assets/Editor/Tests/Camera/CriticalHealthFeedbackIntegrationTests.cs | fan-out | 9 | Trial | error |
| Assets/Editor/Tests/Camera/FeedbackRoutingIntegrationTests.cs | fan-out | 10 | FeedbackFrameObserver | error |
| Assets/Editor/Tests/Camera/FeedbackRoutingIntegrationTests.cs | fan-out | 10 | FeedbackRoutingIntegrationTests | error |
| Assets/Editor/Tests/Camera/FeedbackRoutingIntegrationTests.cs | fan-out | 10 | Trial | error |
| Assets/Editor/Tests/Camera/FeedbackRoutingIntegrationTests.cs | responsibilities | 7 |  | error |
| Assets/Editor/Tests/CastleEnvironment/EnvironmentOrchestratorRoutingTests.cs | fan-out | 7 | EnvironmentOrchestratorRoutingTests | error |
| Assets/Editor/Tests/Chase/ChaseLossIntegrationTests.cs | fan-out | 7 | ChaseLossFeedbackObserver | error |
| Assets/Editor/Tests/Chase/ChaseLossIntegrationTests.cs | fan-out | 7 | ChaseLossIntegrationTests | error |
| Assets/Editor/Tests/Chase/ChaseLossIntegrationTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Chase/HunterRouteIntegrationTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Director/DirectorHintIntegrationTests.cs | fan-out | 7 | DirectorHintIntegrationTests | error |
| Assets/Editor/Tests/Director/DirectorHintIntegrationTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Director/DirectorHintIntegrationTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Director/DirectorIntegrationTests.cs | fan-out | 7 | DirectorIntegrationTests | error |
| Assets/Editor/Tests/Director/DirectorIntegrationTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | ChaseRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | DirectorPacingIntegrationTests | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | FileRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | GapRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | HintRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | PoseRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | PressureRow | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | Trial | error |
| Assets/Editor/Tests/Director/DirectorPacingIntegrationTests.cs | fan-out | 8 | TrialReport | error |
| Assets/Editor/Tests/Expedition/ExpeditionFollowupTests.cs | fan-out | 7 | ExpeditionFollowupTests | error |
| Assets/Editor/Tests/Expedition/ExpeditionProfileRosterTests.cs | fan-out | 6 | ExpeditionProfileRosterTests | error |
| Assets/Editor/Tests/Expedition/ExpeditionProfileRosterTests.cs | fan-out | 6 | World | error |
| Assets/Editor/Tests/Expedition/ExpeditionWorldWiringTests.cs | fan-out | 10 | ExpeditionWorldWiringTests | error |
| Assets/Editor/Tests/Expedition/HorrorConsumptionOrderTests.cs | fan-out | 11 | Fixture | error |
| Assets/Editor/Tests/Expedition/HorrorConsumptionOrderTests.cs | fan-out | 11 | HorrorConsumptionOrderTests | error |
| Assets/Editor/Tests/Expedition/HorrorRunIntegrationTests.cs | fan-out | 11 | HorrorRunIntegrationTests | error |
| Assets/Editor/Tests/Expedition/HorrorRunIntegrationTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Expedition/ShrineWorldRouteTests.cs | fan-out | 9 | ShrineWorldRouteTests | error |
| Assets/Editor/Tests/Floor/FloorControllerTests.cs | responsibilities | 9 |  | error |
| Assets/Editor/Tests/Floor/FloorLifecycleTests.cs | responsibilities | 7 |  | error |
| Assets/Editor/Tests/Floor/FloorLoopIntegrationTests.cs | fan-out | 11 | FloorLoopIntegrationTests | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | Attempt | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | AttemptIndex | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | AttemptReport | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | ChasePhaseRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | ChaseRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | Cohort | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | FileStamp | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | FloorOpposedCaptureTests | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | HealthRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | HintRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | HitRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | IntrusionRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | KeyValue | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | PickupRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | PressureRow | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | Route | error |
| Assets/Editor/Tests/Floor/FloorOpposedCaptureTests.cs | fan-out | 13 | TickRow | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | CueSample | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | FileStamp | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | FloorTraversalIntegrationTests | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | PickupSample | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | Route | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | RouteReport | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | RouteSample | error |
| Assets/Editor/Tests/Floor/FloorTraversalIntegrationTests.cs | fan-out | 6 | Trial | error |
| Assets/Editor/Tests/Horror/PresentationWiringTests.cs | fan-out | 9 | PresentationWiringTests | error |
| Assets/Editor/Tests/Hunter/HunterChaseIntegrationTests.cs | fan-out | 7 | HunterChaseIntegrationTests | error |
| Assets/Editor/Tests/Hunter/HunterChaseIntegrationTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Hunter/HunterChaseIntegrationTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredCaptureEnd | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredCutWindow | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredFileStamp | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredHunterReport | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredHunterRow | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredHunterTrial | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredPlayerProbe | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredRoomEntry | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | AuthoredRouteTurn | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | FixedHorizonComparisonReport | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | fan-out | 8 | HunterRouteComparisonTests | error |
| Assets/Editor/Tests/Hunter/HunterRouteComparisonTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | CohortReport | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | CueBinding | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | CueBindingReport | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | CueObservation | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | Leg | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | LegStats | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderBatchDeferral | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderFrameObservation | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderImageAdmission | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderImageRequest | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderLabelRequest | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderObserver | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RenderObserverReport | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | Route | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | RouteReport | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | Sample | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | Stationary | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | TagArenaDesignSpeedAcceptanceTests | error |
| Assets/Editor/Tests/Level/TagArenaDesignSpeedAcceptanceTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Player/LookBackTraversalIntegrationTests.cs | fan-out | 6 | LookBackTraversalIntegrationTests | error |
| Assets/Editor/Tests/Player/LookBackTraversalIntegrationTests.cs | fan-out | 6 | Trial | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | CohortReport | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | LockRow | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | PhaseRow | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | PlayerContinuousChainAcceptanceTests | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | RunReport | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | Sample | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | StationaryRow | error |
| Assets/Editor/Tests/Player/PlayerContinuousChainAcceptanceTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/Player/PlayerControllerTests.cs | responsibilities | 11 |  | error |
| Assets/Editor/Tests/Player/PlayerDriverTests.cs | responsibilities | 8 |  | error |
| Assets/Editor/Tests/Player/PlayerMoverPresenterTests.cs | responsibilities | 7 |  | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | CameraPass | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | CaptureGateTrace | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | CaptureObservation | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | LimbObservation | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | LimbRenderEvidence | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | LimbTarget | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | PlayerTraversalIntegrationTests | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | RenderObservation | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | RenderReport | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | Sample | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | TickObservation | error |
| Assets/Editor/Tests/Player/PlayerTraversalIntegrationTests.cs | fan-out | 7 | Trial | error |
| Assets/Editor/Tests/PostFX/PostFXOrchestratorRoutingTests.cs | fan-out | 6 | CountingRandom | error |
| Assets/Editor/Tests/PostFX/PostFXOrchestratorRoutingTests.cs | fan-out | 6 | PostFXOrchestratorRoutingTests | error |
| Assets/Editor/Tests/Procedural/ProceduralCastlePresenterTests.cs | responsibilities | 7 |  | error |
| Assets/Editor/Tests/Progression/ProgressionSessionControllerTests.cs | responsibilities | 6 |  | error |
| Assets/Editor/Tests/Results/RunInterfaceRoutingTests.cs | fan-out | 7 | RunInterfaceRoutingTests | error |
| Assets/Editor/Tests/Run/RunFloorEffectWiringTests.cs | fan-out | 7 | LevelView | error |
| Assets/Editor/Tests/Run/RunFloorEffectWiringTests.cs | fan-out | 7 | RunFloorEffectWiringTests | error |
| Assets/Editor/Tests/Run/RunHunterRosterWiringTests.cs | fan-out | 8 | RunHunterRosterWiringTests | error |
| Assets/Editor/Tests/Run/RunShrineWiringTests.cs | fan-out | 7 | RunShrineWiringTests | error |
| Assets/Editor/Tests/Scenes/TagArenaIntegrationTests.cs | fan-out | 8 | TagArenaIntegrationTests | error |
| Assets/Editor/Tests/Settings/RuntimeSettingsTests.cs | fan-out | 6 | RuntimeSettingsTests | error |
| Assets/Scripts/Core/Definitions/PlayerMovementDefinitions.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Core/Definitions/ProgressionDefinitions.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Core/Worsen.Core.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Domain/Director/Manager/DirectorManager.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Floor/Config/FloorConfig.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Domain/Floor/Config/FloorDriverConfig.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Domain/Floor/Controller/FloorController.cs | responsibilities | 13 |  | error |
| Assets/Scripts/Domain/Floor/Driver/FloorDriver.cs | responsibilities | 14 |  | error |
| Assets/Scripts/Domain/Floor/Driver/FloorExitDoor.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Floor/Driver/RoomCollapseVolume.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Floor/Manager/FloorManager.cs | relay-surface | {"handlers": 4, "publicEvents": 17, "relayHandlers": 0} | FloorManager | error |
| Assets/Scripts/Domain/Floor/Manager/FloorManager.cs | responsibilities | 12 |  | error |
| Assets/Scripts/Domain/Floor/State/FloorBehaviorState.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Domain/Hunter/Config/HunterProfile.cs | responsibilities | 10 |  | error |
| Assets/Scripts/Domain/Hunter/Controller/HunterController.cs | responsibilities | 14 |  | error |
| Assets/Scripts/Domain/Hunter/Driver/HunterDriver.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Domain/Hunter/Driver/HunterSteeringPresenter.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | relay-surface | {"handlers": 5, "publicEvents": 20, "relayHandlers": 2} | HunterManager | error |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | responsibilities | 11 |  | error |
| Assets/Scripts/Domain/Hunter/Manager/HunterManager.cs | type-switch | 8 | Initialize | error |
| Assets/Scripts/Domain/Hunter/State/HunterBehaviorState.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Level/Manager/LevelManager.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Player/Config/PlayerProfile.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Player/Controller/PlayerController.cs | responsibilities | 22 |  | error |
| Assets/Scripts/Domain/Player/Driver/PlayerDriver.cs | responsibilities | 14 |  | error |
| Assets/Scripts/Domain/Player/Driver/PlayerMoverPresenter.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Domain/Player/Manager/PlayerManager.cs | responsibilities | 13 |  | error |
| Assets/Scripts/Domain/Player/State/PlayerBehaviorState.cs | responsibilities | 14 |  | error |
| Assets/Scripts/Domain/Procedural/Config/ProceduralConfig.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Domain/Procedural/Controller/ProceduralController.cs | responsibilities | 11 |  | error |
| Assets/Scripts/Domain/Procedural/Definitions/ProceduralDefinitions.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralDriver.cs | responsibilities | 11 |  | error |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralDriverState.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Procedural/Driver/ProceduralGeometryPresenter.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Domain/Procedural/Manager/ProceduralManager.cs | relay-surface | {"handlers": 2, "publicEvents": 8, "relayHandlers": 2} | ProceduralManager | error |
| Assets/Scripts/Domain/Procedural/Manager/ProceduralManager.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Domain/Worsen.Domain.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Orchestrator/AudioOrchestrator.cs | fan-out | 10 | AudioOrchestrator | error |
| Assets/Scripts/Orchestrator/AudioOrchestrator.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Orchestrator/EnvironmentOrchestrator.cs | fan-out | 7 | EnvironmentOrchestrator | error |
| Assets/Scripts/Orchestrator/EnvironmentOrchestrator.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Orchestrator/HUDOrchestrator.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Orchestrator/HorrorOrchestrator.cs | fan-out | 9 | HorrorOrchestrator | error |
| Assets/Scripts/Orchestrator/HorrorOrchestrator.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Orchestrator/MenuOrchestrator.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Orchestrator/PostFXOrchestrator.cs | fan-out | 7 | PostFXOrchestrator | error |
| Assets/Scripts/Orchestrator/ProgressionUIOrchestrator.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Orchestrator/ResultsOrchestrator.cs | fan-out | 6 | ResultsOrchestrator | error |
| Assets/Scripts/Orchestrator/Scenes/FloorLoopSceneRoot.cs | fan-out | 16 | FloorLoopSceneRoot | error |
| Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs | fan-out | 26 | HorrorRunSceneRoot | error |
| Assets/Scripts/Orchestrator/Scenes/HorrorRunSceneRoot.cs | responsibilities | 10 |  | error |
| Assets/Scripts/Orchestrator/Scenes/TagArenaSceneRoot.cs | fan-out | 14 | TagArenaSceneRoot | error |
| Assets/Scripts/Orchestrator/Worsen.Orchestrator.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Presentation/Audio/Config/AudioSoundscapeDriverConfig.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Audio/Driver/AudioDriver.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Presentation/Audio/Driver/AudioFeedbackPresenter.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapeDriver.cs | responsibilities | 11 |  | error |
| Assets/Scripts/Presentation/Audio/Driver/AudioSoundscapePresenter.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Audio/Manager/AudioManager.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/Camera/Driver/CameraDriver.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/Camera/Driver/CameraDriverState.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Camera/Driver/CameraFeedbackPresenter.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/Camera/Manager/CameraManager.cs | relay-surface | {"handlers": 2, "publicEvents": 2, "relayHandlers": 2} | CameraManager | error |
| Assets/Scripts/Presentation/Camera/Manager/CameraManager.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Environment/Driver/EnvironmentDriver.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Presentation/Environment/Driver/EnvironmentPresenter.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/Environment/Manager/EnvironmentManager.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/HUD/Driver/HUDPresenter.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Presentation/HUD/Manager/HUDManager.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Horror/Manager/HorrorManager.cs | relay-surface | {"handlers": 2, "publicEvents": 3, "relayHandlers": 2} | HorrorManager | error |
| Assets/Scripts/Presentation/Input/Driver/PlayerInputDriver.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Presentation/Input/Manager/InputManager.cs | relay-surface | {"handlers": 2, "publicEvents": 2, "relayHandlers": 2} | InputManager | error |
| Assets/Scripts/Presentation/Input/Manager/InputManager.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/Menu/Manager/MenuManager.cs | relay-surface | {"handlers": 4, "publicEvents": 4, "relayHandlers": 4} | MenuManager | error |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXDriver.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXDriverState.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Presentation/PostFX/Driver/PostFXPresenter.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/PostFX/Manager/PostFXManager.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIDriver.cs | responsibilities | 8 |  | error |
| Assets/Scripts/Presentation/ProgressionUI/Driver/ProgressionUIPresenter.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Presentation/ProgressionUI/Manager/ProgressionUIManager.cs | relay-surface | {"handlers": 10, "publicEvents": 10, "relayHandlers": 10} | ProgressionUIManager | error |
| Assets/Scripts/Presentation/ProgressionUI/Manager/ProgressionUIManager.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Presentation/Results/Manager/ResultsManager.cs | relay-surface | {"handlers": 2, "publicEvents": 2, "relayHandlers": 2} | ResultsManager | error |
| Assets/Scripts/Presentation/Worsen.Presentation.asmdef | asmdef | autoReferenced=true |  | warning |
| Assets/Scripts/Session/Expedition/Controller/ExpeditionSessionController.cs | responsibilities | 10 |  | error |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | fan-out | 11 | ExpeditionSessionManager | error |
| Assets/Scripts/Session/Expedition/Manager/ExpeditionSessionManager.cs | responsibilities | 18 |  | error |
| Assets/Scripts/Session/Expedition/State/ExpeditionSessionBehaviorState.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Session/HorrorEffects/Manager/HorrorEffectsManager.cs | fan-out | 6 | HorrorEffectsManager | error |
| Assets/Scripts/Session/Progression/Config/ProgressionConfig.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Session/Progression/Controller/ProgressionSessionController.cs | responsibilities | 13 |  | error |
| Assets/Scripts/Session/Progression/Manager/ProgressionSessionManager.cs | responsibilities | 12 |  | error |
| Assets/Scripts/Session/Progression/Shop/Controller/ShopController.cs | responsibilities | 6 |  | error |
| Assets/Scripts/Session/Progression/State/ProgressionSessionBehaviorState.cs | responsibilities | 7 |  | error |
| Assets/Scripts/Session/Run/Controller/RunSessionController.cs | responsibilities | 9 |  | error |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | fan-out | 7 | RunSessionManager | error |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | relay-surface | {"handlers": 37, "publicEvents": 44, "relayHandlers": 19} | RunSessionManager | error |
| Assets/Scripts/Session/Run/Manager/RunSessionManager.cs | responsibilities | 16 |  | error |
| Assets/Scripts/Session/Run/State/RunSessionBehaviorState.cs | responsibilities | 6 |  | error |
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
