// ============================================================================
// FloorLoopSceneRoot.cs
// ============================================================================
// PURPOSE:
//   Assembles the movement and hunter complete floor through wired services and factories.
//   Publishes readiness only after initialization succeeds. The Session drives
//   gameplay and movement after every dependency is ready.
// ARCHITECTURAL ROLE:
//   SceneRoot (§6b) · Orchestrator · FloorLoop scene assembly.
// KEY RESPONSIBILITIES:
//   - Bind camera catches to Results and canonical audio; release both on teardown.
//   - Initialize canonical persistent services and hand off the ready scene.
//   - Initialize Level, spawn the Player and preserve the shared seeded source.
//   - Supply the initialized Director with Level topology for region and noise inference.
// DEPENDENCIES:
//   - SharedSceneRoot centralizes common service, actor and catch wiring.
//   - Session.Run/SceneFlow; Domain.Player/Level; Presentation Input, DebugOverlay,
//     Camera, PostFX, Audio, HUD, Results and Telemetry. No per-frame gameplay work lives here.
// USAGE NOTES:
//   - Scene-owned. Setup wires all fields before the scene is played.
//   - Compatibility layer (§6b): no Expedition is bound, so the Orchestrators'
//     _expedition == null routes remain active for FloorLoop.
//   - Start is this root's assembly entry point; it explicitly initializes its
//     dependencies instead of depending on any other component's Start.
//   - Static readiness announces a scene to persistent subscribers and resets
//     at SubsystemRegistration for Enter Play Mode without domain reload.
//   - Initialize surviving scene-local references so duplicates retire; fall
//     back to canonical services when a duplicate was destroyed before Start.
// ============================================================================

using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Hunter;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using Worsen.Domain.Director;
using Worsen.Presentation.Audio;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Results;
using Worsen.Presentation.Camera;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Telemetry;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.Input;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;

namespace Worsen.Orchestrator
{
    public sealed class FloorLoopSceneRoot : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private InputManager _input;
        [SerializeField] private DebugOverlayManager _overlay;
        [SerializeField] private int _seed = 1;
        [SerializeField] private LevelManager _level;
        [SerializeField] private PlayerFactory _playerFactory;
        [SerializeField] private PlayerProfile _playerProfile;
        [SerializeField] private CameraManager _camera;
        [SerializeField] private PostFXManager _postFX;
        [SerializeField] private TelemetryManager _telemetry;
        [SerializeField] private Vector3 _spawnPosition;
        [SerializeField] private string _sourceRevision;
        [SerializeField] private string _configSnapshotHash;
        [SerializeField] private HunterFactory _hunterFactory;
        [SerializeField] private HunterProfile _hunterProfile;
        [SerializeField] private ChaseManager _chase;
        [SerializeField] private ChaseConfig _chaseConfig;
        [SerializeField] private AudioManager _audio;
        [SerializeField] private HUDManager _hud;
        [SerializeField] private ResultsManager _results;
        [SerializeField] private Vector3 _hunterSpawnPosition = new Vector3(8f, 0f, 5f);
        private bool _assembled;
        [SerializeField] private FloorManager _floor;
        [SerializeField] private FloorConfig _floorConfig;
        [SerializeField] private DirectorManager _director;
        [SerializeField] private DirectorConfig _directorConfig;

        public static event Action<SceneKey> SceneReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneReady = null;

        private void Start()
        {
            if (!SharedSceneRoot.TryAssembleCompatibility(SceneKey.FloorLoop, "Worsen/Scenes/2 — Build FloorLoop", this,
                _seed, ref _run, ref _sceneFlow, ref _input, ref _overlay, ref _telemetry, ref _audio,
                _level, _playerFactory, _playerProfile, _spawnPosition, _camera, _postFX,
                _hunterFactory, _hunterProfile, _hunterSpawnPosition, _chase, _chaseConfig, _hud,
                _results, _sourceRevision, _configSnapshotHash, out var player)) return;
            if (_floor == null || _floorConfig == null || _director == null || _directorConfig == null)
                throw new InvalidOperationException("Floor progression or Director wiring is missing. Rebuild FloorLoop.");
            _run.BindGameplay(_chase, _floor, _director);
            _floor.Initialize(_floorConfig, _level.ReadOnlyState, new[] { player.ReadOnlyState }, _run.RandomSource);
            _director.Initialize(_directorConfig, _run.RandomSource, _chase.ReadOnlyState, _floor.ReadOnlyState);
            _director.SetLevelView(_level.ReadOnlyState);
            _assembled = true;
            SceneReady?.Invoke(SceneKey.FloorLoop);
        }
        private void OnDestroy()
        {
            SharedSceneRoot.ClearCatch(_results, _audio, clearExpansion: false);
            if (_assembled && _run != null) _run.SuspendForSceneLoad();
        }
    }
}


