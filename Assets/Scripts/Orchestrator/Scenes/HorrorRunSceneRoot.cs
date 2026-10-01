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
//   - Initialize canonical services and bind generated-floor readiness before play.
//   - Bind acoustics, effects, procedural dressing, approved rim and micro-event routes.
//   - Bind camera catches, held-item lifetime and outcome telemetry; release scene references.
//   - Compose preferences and results with fresh or explicitly fixed seed selection.
//   - Publish preferences and show the title before accepting a user start.
// DEPENDENCIES:
//   - SharedSceneRoot initializes common services and scopes audio catches.
//   - Session Expedition/Progression/Run/SceneFlow/Settings, Domain factory/service APIs,
//     and Presentation manager APIs. No game rules are implemented here.
// USAGE NOTES:
//   Scene-owned, explicitly initialized in Start. No per-frame work. Generated
//   readiness is relayed from Expedition to SceneReady exactly once per floor.
//   OnDestroy releases Expedition's scene references before their next use.
//   ProgressionUI owns choices/shops; Results owns terminal outcomes and fixed-seed restarts.
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
using Worsen.Presentation.HeldItem;
using Worsen.Presentation.Horror;
using Worsen.Presentation.Fog;
using Worsen.Presentation.ProgressionUI;
using Worsen.Presentation.Telemetry;
using Worsen.Session.Run;
using Worsen.Session.SceneFlow;
using Worsen.Session.Expedition;
using Worsen.Session.Progression;
using Worsen.Session.HorrorEffects;
using Worsen.Presentation.Environment;
using Worsen.Presentation.Menu;
using Worsen.Presentation.Results;
using Worsen.Session.Settings;
namespace Worsen.Orchestrator
{
    public sealed class HorrorRunSceneRoot : MonoBehaviour
    {
        [SerializeField] private RunSessionManager _run;
        [SerializeField] private MenuManager _menu;
        [SerializeField] private SettingsManager _settings;
        [SerializeField] private ResultsManager _results;
        [SerializeField] private SettingsOrchestrator _settingsRoute;
        [SerializeField] private SceneFlowManager _sceneFlow;
        [SerializeField] private InputManager _input;
        [SerializeField] private InputOrchestrator _inputRoute;
        [SerializeField] private DebugOverlayManager _overlay;
        [SerializeField] private AudioManager _audio;
        [SerializeField] private HUDManager _hud;
        [SerializeField] private CameraManager _camera;
        [SerializeField] private HeldItemManager _heldItem = null;
        [SerializeField] private HeldItemDriverConfig _heldItemConfig = null;
        [SerializeField] private HeldItemOrchestrator _heldItemRoute = null;
        [SerializeField] private PostFXManager _postFX;
        [SerializeField] private TelemetryManager _telemetry;
        [SerializeField] private HorrorManager _horror;
        [SerializeField] private HorrorDriverConfig _horrorConfig;
        [SerializeField] private HorrorOrchestrator _horrorRoute;
        [SerializeField] private HeraldOrchestrator _heraldRoute = null;
        [SerializeField] private FogManager _fog = null;
        [SerializeField] private FogDriverConfig _fogConfig = null;
        [SerializeField] private FogOrchestrator _fogRoute = null;
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
        [SerializeField] private FloorDriverConfig _floorVisuals = null;
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
            _seed = runSeed;
            SharedSceneRoot.InitializeGeneratedServices(runSeed,
                ref _run, ref _sceneFlow, ref _input, ref _inputRoute, ref _overlay, ref _audio, ref _telemetry,
                _hud, _camera, _postFX);
            _heldItem.Initialize(_camera.OutputCamera, _heldItemConfig);
            _horror.Initialize(_horrorConfig); _progressionUI.Initialize();
            _progression = _progression.Initialize(_progressionConfig, runSeed);

            _expedition = _expedition.Initialize();
            _effects = _effects.Initialize(_effectsConfig);
            _heldItemRoute.Configure(_progression, _run, _expedition, _heldItem, _camera);
            _environment.Initialize(_environmentConfig);
            _postFX.GetComponent<PostFXOrchestrator>().Configure(_run, _postFX, _camera, _horror,
                progression: _progression, effects: _effects, environment: _environment, heartbeatEnvelope: () => _audio.HeartbeatEnvelope);
            _fog.Initialize(_fogConfig);
            _fogRoute.Configure(_expedition, _level, _floor, _fog);
            _run.ConfigureCapture(_sourceRevision, _configSnapshotHash);
            _expedition.ConfigureScene(_run, _progression, _procedural, _proceduralConfig, _proceduralDriverConfig,
                _level, _playerFactory, _playerProfile, _hunterFactory, _hunterProfile, _chase, _chaseConfig,
                _floor, _floorConfig, _director, _directorConfig, SceneKey.HorrorRun, _hunterRoster, _effects);
            _progressionRoute.Configure(_progression, _progressionUI, _run, _camera,
                _useFixedSeed ? null : (Func<int>)CreateRunSeed, terminalResults: true, hud: _hud, modalVisible: () => _progressionUI.IsModalVisible);
            _horrorRoute.Configure(_run, _progression, _input, _horror, _effects, _camera, _expedition, _level, _hud);
            _horrorRoute.ConfigureProjectilePresentation(_horror.LaunchProjectile, _horror.HitProjectile, _horror.TickProjectiles, _horror.ClearProjectiles);
            _heraldRoute.Configure(_run, _director.HearHeraldBroadcast);
            _audio.GetComponent<AudioOrchestrator>().ConfigureExpansion(_progression, _effects, _expedition, _progressionUI, _environment, _level, _floor);
            SharedSceneRoot.ConfigureCatch(_audio, _camera, null, requireAudioRoute: true);
            // Exit rays need the level and the exit door visuals the FloorDriver uses.
            _environmentRoute.Configure(_run, _expedition, _effects, _environment, _level, _floorVisuals, _horror, _procedural);
            _inputRoute.ConfigureProgression(_progression);
            _telemetry.GetComponent<TelemetryOrchestrator>().ConfigureProgression(_progression);
            _telemetry.GetComponent<TelemetryOrchestrator>().ConfigureHorror(_horror);
            _assembled = true;
            OnEnable();
            if (SettingsManager.Instance != null && SettingsManager.Instance != _settings)
            {
                if (_settings != null) Destroy(_settings.gameObject);
                _settings = SettingsManager.Instance;
            }
            if (_settings == null || !_settings.Initialize())
                throw new InvalidOperationException("HorrorRun requires a configured Settings service.");
            _menu = _menu != null ? _menu : GetComponentInChildren<MenuManager>(true);
            if (_menu == null) throw new InvalidOperationException("HorrorRun requires a configured Menu service.");
            _menu.Initialize();
            _menu.GetComponent<MenuOrchestrator>().Configure(_menu, _settings, StartFromTitle, _progression, _input, _run);
            _settingsRoute.Configure(_settings, _input, _camera, _postFX, _audio);
            _results.Initialize();
            _results.GetComponent<ResultsOrchestrator>().ConfigureHorrorRun(_run, _results, _camera,
                _progression, _settings, RestartFromResults);
            _settings.PublishCurrent();
            _input.SetInputEnabled(false);
            _progressionUI.Hide();
            _menu.ShowTitle();
        }
        private void StartFromTitle() => _progression.StartRun(_seed);
        private void RestartFromResults(bool fixedSeed, int seed)
        {
            if (!_progression.Snapshot.CanRestart) return;
            _useFixedSeed = fixedSeed;
            _seed = fixedSeed ? seed : CreateRunSeed();
            _progression.RestartRun(_progression.Snapshot.Revision, _seed);
        }
        private static int CreateRunSeed() => Guid.NewGuid().GetHashCode() & int.MaxValue;
        private void OnDestroy()
        {
            if (_heraldRoute != null) _heraldRoute.Configure(null, null);
            if (_horrorRoute != null) _horrorRoute.ConfigureProjectilePresentation(null, null, null, null);
            if (_heldItemRoute != null) _heldItemRoute.Configure(null, null, null, null, null);
            if (_heldItem != null) _heldItem.Teardown();
            if (_telemetry != null) _telemetry.GetComponent<TelemetryOrchestrator>()?.ConfigureHorror(null);
            SharedSceneRoot.ClearCatch(null, _audio, clearExpansion: true);
            if (_assembled && _expedition != null) _expedition.ClearScene();
        }
    }
}
