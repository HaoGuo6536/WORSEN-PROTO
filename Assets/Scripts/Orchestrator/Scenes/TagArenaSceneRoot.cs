// ============================================================================
// TagArenaSceneRoot.cs
// ============================================================================
// PURPOSE:
//   Assembles the movement and hunter tag arena through wired services and factories.
//   Publishes readiness only after initialization succeeds. The Session drives
//   gameplay and movement after every dependency is ready.
// ARCHITECTURAL ROLE:
//   SceneRoot (§6b) · Orchestrator · TagArena scene assembly.
// KEY RESPONSIBILITIES:
//   - Initialize canonical persistent services and hand off the ready scene.
//   - Initialize Level, spawn the Player and preserve the shared seeded source.
// DEPENDENCIES:
//   - Session.Run/SceneFlow; Domain.Player/Level; Presentation Input, DebugOverlay,
//     Camera, PostFX and Telemetry. No per-frame gameplay work lives here.
// USAGE NOTES:
//   - Scene-owned. Setup wires all fields before the scene is played.
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
    public sealed class TagArenaSceneRoot : MonoBehaviour
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

        public static event Action<SceneKey> SceneReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneReady = null;

        private void Start()
        {
            _run = _run != null ? _run.Initialize(_seed) : RunSessionManager.Instance;
            _sceneFlow = _sceneFlow != null ? _sceneFlow.Initialize() : SceneFlowManager.Instance;
            _input = _input != null ? _input.Initialize() : InputManager.Instance;
            _overlay = _overlay != null ? _overlay.Initialize() : DebugOverlayManager.Instance;
            if (_run == null || _sceneFlow == null || _input == null || _overlay == null)
            {
                Debug.LogError("TagArena bootstrap is unwired. Run Worsen/Scenes/1 — Build TagArena.", this);
                return;
            }
            if (_level == null || _playerFactory == null || _playerProfile == null ||
                _camera == null || _postFX == null || _telemetry == null)
            {
                Debug.LogError("TagArena movement wiring is missing. Rebuild TagArena.", this);
                return;
            }
            _telemetry = _telemetry.Initialize();
            if (_hunterFactory == null || _hunterProfile == null || _chase == null || _chaseConfig == null ||
                _audio == null || _hud == null || _results == null)
                throw new InvalidOperationException("TagArena chase/feedback wiring is missing. Rebuild TagArena.");
            _audio = _audio.Initialize();
            _hud.Initialize();
            _results.Initialize();
            _run.ConfigureCapture(_sourceRevision, _configSnapshotHash);
            _run.PrepareScene(SceneKey.TagArena);
            _level.Initialize();
            _camera.Initialize();
            _postFX.Initialize();
            if (!_camera.IsReady || !_postFX.IsReady) throw new InvalidOperationException("Camera/PostFX initialization failed; rebuild TagArena.");
            _playerFactory.Configure(_playerProfile, _run.RandomSource);
            var playerId = _playerFactory.Spawn(new SpawnRequest(_playerProfile.ArchetypeKey, _spawnPosition, Quaternion.Euler(0f, 90f, 0f)));
            if (!PlayerRegistry.TryGet(playerId, out var player)) throw new InvalidOperationException("Player registration failed.");
            _hunterFactory.Configure(_hunterProfile, _run.RandomSource, player.ReadOnlyState, _level.ReadOnlyState);
            _hunterFactory.Spawn(new SpawnRequest(_hunterProfile.ArchetypeKey, _hunterSpawnPosition, Quaternion.Euler(0f, 270f, 0f)));
            _chase.Initialize(_chaseConfig, player.ReadOnlyState);
            _run.BindGameplay(_chase, null, null);
            _assembled = true;
            SceneReady?.Invoke(SceneKey.TagArena);
        }
        private void OnDestroy() { if (_assembled && _run != null) _run.SuspendForSceneLoad(); }
    }
}
