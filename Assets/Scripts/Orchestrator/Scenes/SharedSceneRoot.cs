// ============================================================================
// SharedSceneRoot.cs
// ============================================================================
// PURPOSE:
//   Centralizes the service and actor wiring shared by the three supported scenes.
//   Each scene retains its serialized references and readiness hand-off, while this
//   helper preserves canonical service selection and the original assembly order.
// ARCHITECTURAL ROLE:
//   SceneRoot (§6b) assembly helper · Orchestrator · Scenes.
// KEY RESPONSIBILITIES:
//   - Initialize canonical services with explicit fallback and input-route policies.
//   - Assemble the TagArena/FloorLoop compatibility layer's actors and feedback.
//   - Initialize the generated scene's common presentation services in order.
//   - Pair camera-catch bindings with scene teardown.
// DEPENDENCIES:
//   - Session Run/SceneFlow; Domain Level/Player/Hunter/Chase assembly APIs.
//   - Presentation Input/DebugOverlay/Audio/Telemetry/HUD/Results/Camera/PostFX
//     manager APIs and their Orchestrator routes; Core scene and spawn payloads.
// USAGE NOTES:
//   Scene-owned assembly only: static and stateless, with no lifecycle callbacks.
//   Callers retain returned canonical references and own SceneReady publication.
//   Compatibility (§6b): TagArena/FloorLoop never bind Expedition, preserving the
//   AudioOrchestrator _expedition == null routes. HorrorRun requires authored
//   services and resolves the canonical Input route before initializing the overlay.
//   The SceneRoot suffix identifies assembly machinery, not another scene component.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Input;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Results;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Orchestrator
{
    public static class SharedSceneRoot
    {
        public static void InitializeServices(int seed, bool allowCanonicalFallback,
            bool resolveInputRoute, ref RunSessionManager run, ref SceneFlowManager sceneFlow,
            ref InputManager input, ref InputOrchestrator inputRoute, ref DebugOverlayManager overlay)
        {
            run = allowCanonicalFallback && run == null ? RunSessionManager.Instance : run.Initialize(seed);
            sceneFlow = allowCanonicalFallback && sceneFlow == null ? SceneFlowManager.Instance : sceneFlow.Initialize();
            input = allowCanonicalFallback && input == null ? InputManager.Instance : input.Initialize();
            if (resolveInputRoute) inputRoute = input.GetComponent<InputOrchestrator>();
            overlay = allowCanonicalFallback && overlay == null ? DebugOverlayManager.Instance : overlay.Initialize();
        }

        public static void InitializeGeneratedServices(int seed,
            ref RunSessionManager run, ref SceneFlowManager sceneFlow, ref InputManager input,
            ref InputOrchestrator inputRoute, ref DebugOverlayManager overlay, ref AudioManager audio, ref TelemetryManager telemetry,
            HUDManager hud, CameraManager camera, PostFXManager postFX)
        {
            InitializeServices(seed, allowCanonicalFallback: false, resolveInputRoute: true,
                ref run, ref sceneFlow, ref input, ref inputRoute, ref overlay);
            audio = audio.Initialize();
            telemetry = telemetry.Initialize();
            hud.Initialize();
            InitializeViews(camera, postFX);
            camera.GetComponent<CameraOrchestrator>().Configure(run, camera);
        }

        public static bool TryAssembleCompatibility(SceneKey scene, string buildCommand, UnityEngine.Object context,
            int seed, ref RunSessionManager run, ref SceneFlowManager sceneFlow, ref InputManager input,
            ref DebugOverlayManager overlay, ref TelemetryManager telemetry, ref AudioManager audio,
            LevelManager level, PlayerFactory playerFactory, PlayerProfile playerProfile, Vector3 spawnPosition,
            CameraManager camera, PostFXManager postFX, HunterFactory hunterFactory, HunterProfile hunterProfile,
            Vector3 hunterSpawnPosition, ChaseManager chase, ChaseConfig chaseConfig, HUDManager hud,
            ResultsManager results, string sourceRevision, string configSnapshotHash, out PlayerManager player)
        {
            player = null;
            InputOrchestrator unusedInputRoute = null;
            InitializeServices(seed, allowCanonicalFallback: true, resolveInputRoute: false,
                ref run, ref sceneFlow, ref input, ref unusedInputRoute, ref overlay);
            if (run == null || sceneFlow == null || input == null || overlay == null)
            {
                Debug.LogError($"{scene} bootstrap is unwired. Run {buildCommand}.", context);
                return false;
            }
            if (level == null || playerFactory == null || playerProfile == null ||
                camera == null || postFX == null || telemetry == null)
            {
                Debug.LogError($"{scene} movement wiring is missing. Rebuild {scene}.", context);
                return false;
            }
            telemetry = telemetry.Initialize();
            if (hunterFactory == null || hunterProfile == null || chase == null || chaseConfig == null ||
                audio == null || hud == null || results == null)
                throw new InvalidOperationException($"{scene} chase/feedback wiring is missing. Rebuild {scene}.");
            audio = audio.Initialize();
            hud.Initialize();
            results.Initialize();
            run.ConfigureCapture(sourceRevision, configSnapshotHash);
            run.PrepareScene(scene);
            level.Initialize();
            InitializeViews(camera, postFX);
            if (!camera.IsReady || !postFX.IsReady)
                throw new InvalidOperationException($"Camera/PostFX initialization failed; rebuild {scene}.");
            ConfigureCatch(audio, camera, results, requireAudioRoute: false);
            playerFactory.Configure(playerProfile, run.RandomSource);
            var playerId = playerFactory.Spawn(new SpawnRequest(playerProfile.ArchetypeKey, spawnPosition, Quaternion.Euler(0f, 90f, 0f)));
            if (!PlayerRegistry.TryGet(playerId, out player)) throw new InvalidOperationException("Player registration failed.");
            hunterFactory.Configure(hunterProfile, run.RandomSource, player.ReadOnlyState, level.ReadOnlyState);
            hunterFactory.Spawn(new SpawnRequest(hunterProfile.ArchetypeKey, hunterSpawnPosition, Quaternion.Euler(0f, 270f, 0f)));
            chase.Initialize(chaseConfig, player.ReadOnlyState);
            return true;
        }

        private static void InitializeViews(CameraManager camera, PostFXManager postFX)
        {
            camera.Initialize();
            postFX.Initialize();
        }

        public static void ConfigureCatch(AudioManager audio, CameraManager camera, ResultsManager results,
            bool requireAudioRoute)
        {
            if (results != null) results.GetComponent<ResultsOrchestrator>()?.ConfigureCatch(camera);
            if (requireAudioRoute) audio.GetComponent<AudioOrchestrator>().ConfigureCatch(camera);
            else audio.GetComponent<AudioOrchestrator>()?.ConfigureCatch(camera);
        }

        public static void ClearCatch(ResultsManager results, AudioManager audio, bool clearExpansion)
        {
            if (results != null) results.GetComponent<ResultsOrchestrator>()?.ConfigureCatch(null);
            if (audio == null) return;
            audio.GetComponent<AudioOrchestrator>()?.ClearCatch();
            if (clearExpansion) audio.GetComponent<AudioOrchestrator>()?.ClearExpansion();
        }
    }
}
