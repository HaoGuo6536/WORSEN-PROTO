// ============================================================================
// ExpeditionSessionManager.cs
// ============================================================================
// PURPOSE:
//   Assembles each floor requested by progression and connects it to the existing
//   Run Session's sole gameplay tick. It replaces old actors and geometry before
//   admitting the new floor while progression retains health, choices and wallet.
// ARCHITECTURAL ROLE:
//   Manager (§1, §8b) · Session · Expedition (Session system).
// KEY RESPONSIBILITIES:
//   - Bind scene-owned services explicitly and release every binding on disable.
//   - Defer new assembly until old factory objects finish deferred destruction.
//   - Apply run-scoped modifiers and route completed floor facts to progression.
//   - Announce assembled floors for the SceneRoot readiness hand-off.
// DEPENDENCIES:
//   - Session Progression owns requests/rewards; Session Run owns gameplay/capture.
//   - Domain Procedural/Level assemble geometry; Player/Hunter factories own actors.
//   - Domain Chase/Floor/Director provide the generated floor's gameplay services.
// USAGE NOTES:
//   Persistent on its own root. ConfigureScene scopes all scene references; the
//   SceneRoot calls ClearScene before unloading. No FixedUpdate or Presentation
//   reference exists here. Events pair OnEnable/OnDisable; generation is explicit
//   BehaviorState during the coroutine yield, not hidden in coroutine locals.
//   Shop floors contain a player and geometry, with no pickups, collapse or hunters.
// ============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Chase;
using Worsen.Domain.Director;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using Worsen.Domain.Procedural;
using Worsen.Session.Progression;
using Worsen.Session.Run;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSessionManager : MonoBehaviour
    {
        private ExpeditionSessionBehaviorState _state;
        private ExpeditionSessionController _controller;
        private RunSessionManager _run;
        private ProgressionSessionManager _progression;
        private ProceduralManager _procedural;
        private ProceduralConfig _proceduralConfig;
        private ProceduralDriverConfig _proceduralDriverConfig;
        private LevelManager _level;
        private PlayerFactory _playerFactory;
        private PlayerProfile _playerProfile;
        private HunterFactory _hunterFactory;
        private HunterProfile _hunterProfile;
        private ChaseManager _chase;
        private ChaseConfig _chaseConfig;
        private FloorManager _floor;
        private FloorConfig _floorConfig;
        private DirectorManager _director;
        private DirectorConfig _directorConfig;
        private Coroutine _assembly;
        private bool _subscribed;

        public static ExpeditionSessionManager Instance { get; private set; }
        public ExpeditionAssemblyPhase AssemblyPhase => _state?.Phase ?? ExpeditionAssemblyPhase.Unbound;
        public int GenerationId => _state == null ? 0 : _state.Request.GenerationId;
        public EntityId ActivePlayerId => _state?.Player ?? EntityId.None;
        public int ActiveHunterCount => _state?.Hunters.Count ?? 0;
        public string LastError => _state?.Failure ?? string.Empty;
        public event Action<ProgressionGenerationRequest, Vector3, Quaternion> AssemblyReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public ExpeditionSessionManager Initialize()
        {
            if (Instance != null && Instance != this) { Destroy(this); return Instance; }
            Instance = this;
            if (_controller == null)
            {
                _state = new ExpeditionSessionBehaviorState();
                _controller = new ExpeditionSessionController(_state);
            }
            DontDestroyOnLoad(gameObject);
            return this;
        }

        public void ConfigureScene(RunSessionManager run, ProgressionSessionManager progression,
            ProceduralManager procedural, ProceduralConfig proceduralConfig, ProceduralDriverConfig proceduralDriverConfig,
            LevelManager level, PlayerFactory playerFactory, PlayerProfile playerProfile,
            HunterFactory hunterFactory, HunterProfile hunterProfile, ChaseManager chase, ChaseConfig chaseConfig,
            FloorManager floor, FloorConfig floorConfig, DirectorManager director, DirectorConfig directorConfig, SceneKey scene)
        {
            if (Instance != this || _controller == null)
                throw new InvalidOperationException("Initialize and use the canonical Expedition Session before binding a scene.");
            if (run == null || progression == null || procedural == null || proceduralConfig == null ||
                proceduralDriverConfig == null || level == null || playerFactory == null || playerProfile == null ||
                hunterFactory == null || hunterProfile == null || chase == null || chaseConfig == null ||
                floor == null || floorConfig == null || director == null || directorConfig == null || scene == SceneKey.None)
                throw new ArgumentException("Expedition scene assembly requires all authored services and configurations.");
            ClearScene();
            _run = run; _progression = progression; _procedural = procedural;
            _proceduralConfig = proceduralConfig; _proceduralDriverConfig = proceduralDriverConfig; _level = level;
            _playerFactory = playerFactory; _playerProfile = playerProfile; _hunterFactory = hunterFactory;
            _hunterProfile = hunterProfile; _chase = chase; _chaseConfig = chaseConfig;
            _floor = floor; _floorConfig = floorConfig; _director = director; _directorConfig = directorConfig;
            _controller.Bind(scene);
            if (isActiveAndEnabled) OnEnable();
        }

        private void OnEnable()
        {
            if (_subscribed || _run == null || _progression == null || _floor == null) return;
            _progression.GenerationRequested += HandleGeneration;
            _progression.SnapshotChanged += HandleSnapshot;
            _run.HealthChanged += HandleHealth;
            _run.RunEnded += HandleRunEnded;
            _floor.OnPickupCollected += HandlePickup;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_progression != null)
            {
                _progression.GenerationRequested -= HandleGeneration;
                _progression.SnapshotChanged -= HandleSnapshot;
            }
            if (_run != null) { _run.HealthChanged -= HandleHealth; _run.RunEnded -= HandleRunEnded; }
            if (_floor != null) _floor.OnPickupCollected -= HandlePickup;
            _subscribed = false;
        }

        private void OnDisable() => ClearScene();

        public void ClearScene()
        {
            Unsubscribe();
            if (_assembly != null) { StopCoroutine(_assembly); _assembly = null; }
            try
            {
                if (_run != null) _run.SuspendForSceneLoad();
                ReleaseFloor();
            }
            finally
            {
                _run = null; _progression = null; _procedural = null; _proceduralConfig = null;
                _proceduralDriverConfig = null; _level = null; _playerFactory = null; _playerProfile = null;
                _hunterFactory = null; _hunterProfile = null; _chase = null; _chaseConfig = null;
                _floor = null; _floorConfig = null; _director = null; _directorConfig = null;
                _controller?.ClearScene();
            }
        }

        private void HandleGeneration(ProgressionGenerationRequest request)
        {
            try
            {
                if (!_controller.Queue(request)) return;
                _run.SuspendForSceneLoad();
                ReleaseFloor();
                _assembly = StartCoroutine(AssembleAfterTeardown(request.GenerationId));
            }
            catch (Exception exception) { FailAssembly(request.GenerationId, exception); }
        }

        private IEnumerator AssembleAfterTeardown(int generationId)
        {
            // Factories use deferred Destroy. Finish the previous frame before baking.
            yield return null;
            _assembly = null;
            if (!isActiveAndEnabled || !_controller.Begin(generationId)) yield break;
            try { AssembleFloor(); }
            catch (Exception exception) { FailAssembly(generationId, exception); }
        }

        private void AssembleFloor()
        {
            var request = _state.Request;
            _procedural.Initialize(_proceduralConfig, _proceduralDriverConfig, request.Seed, request.Round);
            if (!_procedural.IsReady || _procedural.Graph == null)
                throw new InvalidOperationException("Procedural generation returned without a ready graph.");
            _level.InitializeGenerated(_procedural.Graph);
            _run.PrepareScene(_state.Scene, request.Seed);
            _playerFactory.Configure(_playerProfile, _run.RandomSource);
            _controller.RecordPlayer(_playerFactory.Spawn(_controller.PlayerSpawn(_playerProfile.ArchetypeKey,
                _procedural.PlayerSpawnPosition, _procedural.PlayerSpawnRotation)));
            if (!PlayerRegistry.TryGet(_state.Player, out var player) || player.ReadOnlyState == null)
                throw new InvalidOperationException("Generated player failed to register.");
            player.ApplyRunModifiers(request.Effects.Health, request.Effects.MaximumHealth, request.Effects.MovementSpeedMultiplier);

            var spawns = _controller.HunterSpawns(_hunterProfile.ArchetypeKey, _procedural.HunterSpawnPositions);
            _hunterFactory.Configure(_hunterProfile, _run.RandomSource, player.ReadOnlyState, _level.ReadOnlyState);
            foreach (var spawn in spawns)
            {
                EntityId id = _hunterFactory.Spawn(spawn);
                _controller.RecordHunter(id);
                if (!HunterRegistry.TryGet(id, out var hunter)) throw new InvalidOperationException("Generated hunter failed to register.");
                hunter.ApplyRunSpeedMultiplier(request.Effects.HunterSpeedMultiplier);
            }

            if (request.IsShop) _run.BindGameplay(null, null, null);
            else
            {
                _chase.Initialize(_chaseConfig, player.ReadOnlyState);
                _floor.Initialize(_floorConfig, _level.ReadOnlyState, new[] { player.ReadOnlyState },
                    _run.RandomSource, _procedural.Graph.Anchors.Count);
                _director.Initialize(_directorConfig, _run.RandomSource, _chase.ReadOnlyState, _floor.ReadOnlyState);
                _run.BindGameplay(_chase, _floor, _director);
            }
            _controller.Ready();
            AssemblyReady?.Invoke(request, _procedural.PlayerSpawnPosition, _procedural.PlayerSpawnRotation);
            if (!_progression.ConfirmFloorReady(request.GenerationId))
                throw new InvalidOperationException("Progression rejected readiness for the generated floor.");
        }

        private void HandleHealth(EntityId player, float health, float maximum)
        {
            if (_controller.AcceptsGameplay(player)) _progression.RecordHealth(GenerationId, health);
        }

        private void HandlePickup(PickupCollectedFact fact)
        {
            if (_controller.AcceptsGameplay(fact.PlayerId) && fact.Kind == PickupKind.GoldenCake)
                _progression.RecordGoldenCollected(GenerationId, fact.AnchorId);
        }

        private void HandleRunEnded(RunSummary summary)
        {
            if (summary.EndReason == RunEndReason.Unknown || !_controller.Resolve(summary.Scene)) return;
            int generationId = GenerationId;
            if (PlayerRegistry.TryGet(_state.Player, out var player) && player.ReadOnlyState != null)
                _progression.RecordHealth(generationId, player.ReadOnlyState.Health);
            if (summary.EndReason == RunEndReason.Escaped) _progression.CompleteFloor(generationId);
            else _progression.EndRun(generationId);
        }

        private void HandleSnapshot(ProgressionSnapshot snapshot)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Ready || !_state.Request.IsShop ||
                snapshot.GenerationId != GenerationId || snapshot.Phase != ProgressionPhase.Shop) return;
            if (PlayerRegistry.TryGet(_state.Player, out var player))
                player.ApplyRunModifiers(snapshot.Effects.Health, snapshot.Effects.MaximumHealth, snapshot.Effects.MovementSpeedMultiplier);
        }

        private void ReleaseFloor()
        {
            var failures = new List<Exception>();
            if (_director != null) Release(_director.Teardown, failures);
            if (_floor != null) Release(_floor.Teardown, failures);
            if (_chase != null) Release(_chase.Teardown, failures);
            if (_state != null)
            {
                if (_hunterFactory != null)
                    foreach (EntityId id in new List<EntityId>(_state.Hunters)) Release(() => _hunterFactory.Despawn(id), failures);
                if (_playerFactory != null && _state.Player.IsValid) Release(() => _playerFactory.Despawn(_state.Player), failures);
            }
            _controller?.ReleaseActors();
            if (_level != null) Release(_level.Teardown, failures);
            if (_procedural != null) Release(_procedural.Teardown, failures);
            if (failures.Count != 0) throw new AggregateException("Generated floor cleanup failed.", failures);
        }

        private static void Release(Action release, List<Exception> failures)
        { try { release(); } catch (Exception exception) { failures.Add(exception); } }

        private void FailAssembly(int generationId, Exception exception)
        {
            if (_run != null) _run.SuspendForSceneLoad();
            try { ReleaseFloor(); }
            catch (Exception cleanup) { exception = new AggregateException(exception, cleanup); }
            _controller.Fail("Floor " + _state.Request.Round + ", seed " + _state.Request.Seed + ": " + exception.Message);
            Debug.LogError(_state.Failure, this);
            if (_progression != null) _progression.FailGeneration(generationId, _state.Failure);
        }

        private void OnDestroy()
        {
            ClearScene();
            if (Instance == this) Instance = null;
            AssemblyReady = null;
        }
    }
}
