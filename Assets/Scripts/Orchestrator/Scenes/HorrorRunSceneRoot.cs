// ============================================================================
// HorrorRunSceneRoot.cs
// ============================================================================
// PURPOSE:
//   Assembles a complete procedural horror expedition from explicitly wired
//   services. The Expedition Session owns repeated floor generation and the Run
//   Session owns gameplay ticks after this one-time bootstrap has completed.
// ARCHITECTURAL ROLE:
//   SceneRoot (§6b) · Orchestrator · HorrorRun scene assembly.
// KEY RESPONSIBILITIES:
//   - Initialize canonical persistent services and scene presentation.
//   - Bind the generated-floor flow and publish its first player choice.
//   - Choose fresh expedition seeds at the composition boundary unless fixed replay is selected.
// DEPENDENCIES:
//   - Session Expedition/Progression/Run/SceneFlow, Domain factory/service APIs,
//     and Presentation manager APIs. No game rules are implemented here.
// USAGE NOTES:
//   Scene-owned, explicitly initialized in Start. No per-frame work. Generated
//   readiness is relayed from Expedition to SceneReady exactly once per floor.
//   OnDestroy releases Expedition's scene references before their next use.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Player;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Procedural;
using Worsen.Domain.Chase;
using Worsen.Domain.Floor;
using Worsen.Domain.Director;
using Worsen.Presentation.Audio;
using Worsen.Presentation.Camera;
using Worsen.Presentation.PostFX;
using Worsen.Presentation.Input;
using Worsen.Presentation.DebugOverlay;
using Worsen.Presentation.HUD;
using Worsen.Presentation.Horror;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
namespace Worsen.Orchestrator
{
    public sealed class HorrorRunSceneRoot : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private InputManager _input;
        [SerializeField] private InputOrchestrator _inputRoute;
        [SerializeField] private DebugOverlayManager _overlay;
        [SerializeField] private AudioManager _audio;
        [SerializeField] private HUDManager _hud;
        [SerializeField] private CameraManager _camera;
        [SerializeField] private PostFXManager _postFX;
        [SerializeField] private TelemetryManager _telemetry;
        [SerializeField] private HorrorManager _horror;
        [SerializeField] private HorrorDriverConfig _horrorConfig;
        [SerializeField] private HorrorOrchestrator _horrorRoute;
        [SerializeField] private ProgressionUIManager _progressionUI;
        [SerializeField] private ProgressionUIOrchestrator _progressionRoute;
        [SerializeField] private ProgressionSessionManager _progression;
        [SerializeField] private ProgressionConfig _progressionConfig;
        [SerializeField] private ExpeditionSessionManager _expedition;
        [SerializeField] private ProceduralManager _procedural;
        [SerializeField] private ProceduralConfig _proceduralConfig;
        [SerializeField] private ProceduralDriverConfig _proceduralDriverConfig;
        [SerializeField] private LevelManager _level;
        [SerializeField] private PlayerFactory _playerFactory;
        [SerializeField] private PlayerProfile _playerProfile;
        [SerializeField] private HunterFactory _hunterFactory;
        [SerializeField] private HunterProfile _hunterProfile;
        [SerializeField] private HunterProfile[] _hunterRoster;
        [SerializeField] private HorrorEffectsManager _effects;
        [SerializeField] private HorrorEffectsConfig _effectsConfig;
        [SerializeField] private EnvironmentManager _environment;
        [SerializeField] private EnvironmentDriverConfig _environmentConfig;
        [SerializeField] private EnvironmentOrchestrator _environmentRoute;
        [SerializeField] private ChaseManager _chase;
        [SerializeField] private ChaseConfig _chaseConfig;
        [SerializeField] private FloorManager _floor;
        [SerializeField] private FloorConfig _floorConfig;
        [SerializeField] private DirectorManager _director;
        [SerializeField] private DirectorConfig _directorConfig;
        [SerializeField] private int _seed = 1701;
        [SerializeField] private bool _useFixedSeed;
        [SerializeField] private string _sourceRevision;
        [SerializeField] private string _configSnapshotHash;
        private bool _assembled;
        public static event Action<SceneKey> SceneReady;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneReady = null;
        private void OnEnable() { if (_assembled && _expedition != null) _expedition.AssemblyReady += OnAssemblyReady; }
        private void OnDisable() { if (_expedition != null) _expedition.AssemblyReady -= OnAssemblyReady; }
        private void OnAssemblyReady(ProgressionGenerationRequest request, Vector3 position, Quaternion rotation)
            => SceneReady?.Invoke(SceneKey.HorrorRun);
        private void Start()
        {
            int runSeed = _useFixedSeed ? _seed : CreateRunSeed();
            _run = _run.Initialize(runSeed);
            _sceneFlow = _sceneFlow.Initialize();
            _input = _input.Initialize();
            _inputRoute = _input.GetComponent<InputOrchestrator>();
            _overlay = _overlay.Initialize();
            _audio = _audio.Initialize();
            _telemetry = _telemetry.Initialize();
            _hud.Initialize(); _camera.Initialize(); _postFX.Initialize();
            _camera.GetComponent<CameraOrchestrator>().Configure(_run, _camera);
            _postFX.GetComponent<PostFXOrchestrator>().Configure(_run, _postFX, _camera);
            _horror.Initialize(_horrorConfig); _progressionUI.Initialize();
            _progression = _progression.Initialize(_progressionConfig, runSeed);
            _expedition = _expedition.Initialize();
            _effects = _effects.Initialize(_effectsConfig);
            _environment.Initialize(_environmentConfig);
            _run.ConfigureCapture(_sourceRevision, _configSnapshotHash);
            _expedition.ConfigureScene(_run, _progression, _procedural, _proceduralConfig, _proceduralDriverConfig,
                _level, _playerFactory, _playerProfile, _hunterFactory, _hunterProfile, _chase, _chaseConfig,
                _floor, _floorConfig, _director, _directorConfig, SceneKey.HorrorRun, _hunterRoster, _effects);
            _progressionRoute.Configure(_progression, _progressionUI, _run, _camera,
                _useFixedSeed ? null : (Func<int>)CreateRunSeed);
            _horrorRoute.Configure(_run, _progression, _input, _horror, _effects, _camera);
            _audio.GetComponent<AudioOrchestrator>().ConfigureExpansion(_progression, _effects, _expedition, _progressionUI, _environment);
            _environmentRoute.Configure(_run, _expedition, _effects, _environment);
            _inputRoute.ConfigureProgression(_progression);
            _assembled = true;
            OnEnable();
            _progression.StartRun(runSeed);
        }
        private static int CreateRunSeed() => Guid.NewGuid().GetHashCode() & int.MaxValue;
        private void OnDestroy()
        {
            if (!_assembled) return;
            if (_audio != null) _audio.GetComponent<AudioOrchestrator>()?.ClearExpansion();
            if (_expedition != null) _expedition.ClearScene();
        }
    }
}
