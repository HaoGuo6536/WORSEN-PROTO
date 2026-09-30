// ============================================================================
// ExpeditionSessionManager.cs
// ============================================================================
// PURPOSE:
//   Assembles each floor requested by progression and connects it to the existing
//   Run Session's sole gameplay tick. It replaces old actors and geometry before
//   admitting the new floor while progression retains maximum health, choices and wallet.
// ARCHITECTURAL ROLE:
//   Manager (§1, §8b) · Session · Expedition (Session system).
// KEY RESPONSIBILITIES:
//   - Open the identified Passage before pocket collapse and route its optional Golden Cakes.
//   - Bind every roster hunter's world/effects views and silently restore accepted run mutations.
//   - Route Core Echo door-passage facts to Level through paired Run subscriptions.
//   - Subscribe before generation to theme, optional-reward and threshold-freeze facts.
//   - Route committed puzzle ticks once, retain the run theme seed and release challenge state.
//   - Assemble floor shrines, preserve shields and route Wick, Passage and Purgatory outcomes.
//   - Snapshot catalogue cake/collapse hooks and the actual round into each non-shop Floor.
//   - Start each spawned Player at its effective maximum, never the previous floor's current health.
//   - Route immutable active effects before floor health and on current-floor revisions.
//   - Bind scene-owned services explicitly and release every binding on disable.
//   - Defer new assembly until old factory objects finish deferred destruction.
//   - Route authoritative light/curse effects, staged destruction and actual selected hunter identities.
//   - Apply run-scoped modifiers and route completed floor facts to progression.
//   - Forward resolved bail flags with the admitted generation for exactly-once penalties.
//   - Pass HorrorEffects' configured optional-window multiplier to procedural generation.
//   - Announce assembled floors for the SceneRoot readiness hand-off.
//   - Route Level interactables to geometry and door acoustics before hunter ticks.
//   - Retain fallback manifests and spawn shortfalls through the existing diagnostic path.
// DEPENDENCIES:
//   - Domain Shrine owns single-use world objects; own spawn Driver validates live late-spawn cover.
//   - Session HorrorEffects owns retained gameplay effects; its actor and hazard binding is floor-scoped.
//   - Session Progression owns requests/rewards; Session Run owns gameplay/capture.
//   - Domain Procedural/Level assemble geometry; Player/Hunter factories own actors.
//   - Domain Chase/Floor/Director provide the generated floor's gameplay services.
// USAGE NOTES:
//   Persistent on its own root. ConfigureScene scopes all scene references; the
//   SceneRoot calls ClearScene before unloading. No FixedUpdate or Presentation
//   reference exists here. Events pair OnEnable/OnDisable; generation is explicit
//   BehaviorState during the coroutine yield, not hidden in coroutine locals.
//   Shop floors contain a player and geometry, with no pickups, collapse or hunters.
//   BeginFloor updates HorrorEffects before assembly reads its optional-window multiplier.
//   Scenes without the effect service use neutral window density, not a second tuning source.
//   Floor bindings pair BindWorld/UnbindWorld; late hunters receive all world views on BeforeTick.
//   Fallback layouts never publish readiness. FloorReleased clears presentation even on failure.
//   Purgatory fraction means physical Golden Cakes collected / created, not exit credit.
//   Wick restores the first activation's lamp states; overlaps extend without replacing that snapshot.
//   Missing late-spawn/mutation/Passage admission publishes unresolved intent, never an unsafe fallback.
//   Vault outcomes retain the probe's surface identity; unidentified facts never solve puzzles.
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
using Worsen.Domain.Shrine;
using Worsen.Session.Progression;
using Worsen.Session.HorrorEffects;
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
        private HunterProfile[] _hunterRoster;
        private HorrorEffectsManager _effects;
        private ChaseManager _chase;
        private ChaseConfig _chaseConfig;
        private FloorManager _floor;
        private FloorConfig _floorConfig;
        private DirectorManager _director;
        private DirectorConfig _directorConfig;
        private Coroutine _assembly;
        private bool _subscribed;
        private bool _worldBound;
        [SerializeField] private ShrineConfig _shrineConfig = null;
        [SerializeField] private ShrineDriverConfig _shrineDriverConfig = null;
        [SerializeField] private ExpeditionSpawnDriverConfig _spawnConfig = null;
        private ShrineManager _shrines;
        private ExpeditionSpawnDriver _spawnDriver;

        public static ExpeditionSessionManager Instance { get; private set; }
        public ExpeditionAssemblyPhase AssemblyPhase => _state?.Phase ?? ExpeditionAssemblyPhase.Unbound;
        public int GenerationId => _state == null ? 0 : _state.Request.GenerationId;
        public EntityId ActivePlayerId => _state?.Player ?? EntityId.None;
        public int ActiveHunterCount => _state?.Hunters.Count ?? 0;
        public IReadOnlyList<GeneratedRoomSample> PresentationRooms => _state?.Rooms ?? Array.Empty<GeneratedRoomSample>();
        public string LastError => _state?.Failure ?? string.Empty;
        public bool UsedFallback => _state?.UsedFallback ?? false;
        public string LayoutManifest => _state?.LayoutManifest ?? string.Empty;
        public int HunterSpawnShortfall => _state?.HunterSpawnShortfall ?? 0;
        public event Action<ProgressionGenerationRequest, Vector3, Quaternion> AssemblyReady;
        public event Action<IReadOnlyList<GeneratedRoomSample>> RoomsReady;
        public event Action FloorReleased;
        public event Action<string, string, string, string, string> ThemePublished;
        public event Action<int, string, string> RoomThemePublished;
        public event Action<int, int, int, Vector3, Vector3> ThresholdFreezePublished;
        public bool WickActive => _controller?.WickActive ?? false;
        public event Action<bool> WickActiveChanged;
        public event Action<ShrineResolvedFact, string> ShrineWorldEffectUnresolved;

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
            FloorManager floor, FloorConfig floorConfig, DirectorManager director, DirectorConfig directorConfig, SceneKey scene,
            HunterProfile[] hunterRoster = null, HorrorEffectsManager effects = null)
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
            _hunterProfile = hunterProfile; _hunterRoster = hunterRoster; _effects = effects; _chase = chase; _chaseConfig = chaseConfig;
            _floor = floor; _floorConfig = floorConfig; _director = director; _directorConfig = directorConfig;
            _controller.Bind(scene);
            if (isActiveAndEnabled) OnEnable();
        }

        private void OnEnable()
        {
            if (_subscribed || _run == null || _progression == null || _floor == null) return;
            _progression.GenerationRequested += HandleGeneration;
            _progression.EffectsSnapshotChanged += HandleSnapshot;
            _progression.ShrineResolved += HandleShrineResolved;
            _progression.TransactionCommitted += HandleProgressionTransaction;
            _run.HealthChanged += HandleHealth;
            _run.RunEnded += HandleRunEnded;
            _floor.OnPickupCollected += HandlePickup;
            _floor.OnRoomDestruction += HandleDestruction;
            _floor.OnRoomPhaseChanged += HandleRoomPhase;
            _run.PlayerMovementPublished += HandleMovement;
            _run.PlayerTraversalPublished += HandleTraversal;
            _run.TickAdvanced += HandleTick;
            _run.HunterArchetypePublished += HandleArchetype;
            _run.HunterMutationPublished += HandleMutation;
            if (_procedural != null)
            {
                _procedural.ThemePublished += HandleTheme;
                _procedural.RoomThemePublished += HandleRoomTheme;
                _procedural.ThresholdFreezePublished += HandleFreeze;
                _procedural.OptionalPuzzleRewardPublished += HandlePuzzleReward;
                _procedural.PuzzleSolved += HandlePuzzleSolved;
                _procedural.PassageOpened += HandlePassageOpened;
            }
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (_procedural != null)
            {
                _procedural.ThemePublished -= HandleTheme;
                _procedural.RoomThemePublished -= HandleRoomTheme;
                _procedural.ThresholdFreezePublished -= HandleFreeze;
                _procedural.OptionalPuzzleRewardPublished -= HandlePuzzleReward;
                _procedural.PuzzleSolved -= HandlePuzzleSolved;
                _procedural.PassageOpened -= HandlePassageOpened;
            }
            if (_progression != null)
            {
                _progression.GenerationRequested -= HandleGeneration;
                _progression.EffectsSnapshotChanged -= HandleSnapshot;
                _progression.ShrineResolved -= HandleShrineResolved;
                _progression.TransactionCommitted -= HandleProgressionTransaction;
            }
            if (_run != null) { _run.HealthChanged -= HandleHealth; _run.RunEnded -= HandleRunEnded; }
            if (_floor != null)
            { _floor.OnPickupCollected -= HandlePickup; _floor.OnRoomDestruction -= HandleDestruction; _floor.OnRoomPhaseChanged -= HandleRoomPhase; }
            if (_run != null)
            { _run.PlayerMovementPublished -= HandleMovement; _run.PlayerTraversalPublished -= HandleTraversal; _run.TickAdvanced -= HandleTick; }
            if (_run != null)
            { _run.HunterArchetypePublished -= HandleArchetype; _run.HunterMutationPublished -= HandleMutation; }
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
                _hunterFactory = null; _hunterProfile = null; _hunterRoster = null; _effects = null; _chase = null; _chaseConfig = null;
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
                _effects?.BeginFloor(request.GenerationId, request.Effects);
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
            try
            {
                _procedural.Initialize(_proceduralConfig, _proceduralDriverConfig, request.Seed, request.Round,
                    request.IsShop, _effects == null ? 1f : _effects.OptionalWindowMultiplier, themeSeed: _progression.Snapshot.Seed);
            }
            finally { _controller.RecordGenerationOutcome(_procedural.UsedFallback, _procedural.LayoutManifest); }
            if (_state.UsedFallback || !_procedural.GenerationSucceeded)
                throw new InvalidOperationException("Procedural generation did not succeed; fallback layouts are not playable floors.");
            if (!_procedural.IsReady || _procedural.Graph == null)
                throw new InvalidOperationException("Procedural generation returned without a ready graph.");
            _level.InitializeGenerated(_procedural.Graph, _procedural.Interactables);
            BindWorld();
            _controller.RecordRooms(_procedural.PresentationRooms);
            _run.PrepareScene(_state.Scene, request.Seed);
            _playerFactory.Configure(_playerProfile, _run.RandomSource);
            _controller.RecordPlayer(_playerFactory.Spawn(_controller.PlayerSpawn(_playerProfile.ArchetypeKey,
                _procedural.PlayerSpawnPosition, _procedural.PlayerSpawnRotation)));
            if (!PlayerRegistry.TryGet(_state.Player, out var player) || player.ReadOnlyState == null)
                throw new InvalidOperationException("Generated player failed to register.");
            player.SetActiveEffects(_progression.EffectsSnapshot.ActiveEffects);
            player.BeginFloorHealth(request.Effects.MaximumHealth, request.Effects.MovementSpeedMultiplier);
            player.RestoreShield(_controller.CarriedShield);
            _controller.AdmitShieldTransfer();

            var spawns = _controller.HunterSpawns(_hunterProfile.ArchetypeKey, _procedural.HunterSpawnPositions,
                position => _procedural.ValidateHunterSpawn(position, out _));
            if (_state.HunterSpawnShortfall > 0)
                Debug.LogWarning("Floor " + request.Round + ", seed " + request.Seed + ": hunter spawn shortfall=" +
                    _state.HunterSpawnShortfall + ", spawning=" + spawns.Count + ", requested=" + request.Effects.ActiveThreatBudget, this);
            if (_hunterRoster != null && _hunterRoster.Length > 0)
                _hunterFactory.Configure(_hunterRoster, _run.RandomSource, player.ReadOnlyState, _level.ReadOnlyState);
            else _hunterFactory.Configure(_hunterProfile, _run.RandomSource, player.ReadOnlyState, _level.ReadOnlyState);
            foreach (var spawn in spawns)
            {
                EntityId id = _hunterFactory.Spawn(spawn);
                _controller.RecordHunter(id);
                if (!HunterRegistry.TryGet(id, out var hunter)) throw new InvalidOperationException("Generated hunter failed to register.");
                hunter.ApplyRunSpeedMultiplier(request.Effects.HunterSpeedMultiplier);
                hunter.SetTraits(request.Effects.Traits);
                BindHunterWorld(hunter);
                RestoreMutations(hunter);
            }

            if (request.IsShop) _run.BindGameplay(null, null, null);
            else
            {
                _chase.Initialize(_chaseConfig, player.ReadOnlyState);
                var activeEffects = _progression.EffectsSnapshot.ActiveEffects;
                _floor.Initialize(_floorConfig, _level.ReadOnlyState, new[] { player.ReadOnlyState },
                    _run.RandomSource, fasterCollapse: ExpeditionFloorEffectUtility.FasterCollapse(activeEffects),
                    shuffledCollapse: ExpeditionFloorEffectUtility.ShuffledCollapse(activeEffects), round: request.Round,
                    cakeHooks: ExpeditionFloorEffectUtility.CakeHooks(activeEffects), waxHeart: ExpeditionFloorEffectUtility.WaxHeart(activeEffects),
                    preferredAnchors: _state.FreezeAnchors, earlyCollapseRooms: _state.FreezeBehindRooms, handLook: _state.HandLook);
                foreach (var reward in _state.PuzzleRewards)
                    if (!_floor.RegisterPuzzleReward(reward.Key, reward.Value.Anchor, reward.Value.Position))
                        throw new InvalidOperationException("Floor rejected optional puzzle reward " + reward.Key + ".");
                _director.Initialize(_directorConfig, _run.RandomSource, _chase.ReadOnlyState, _floor.ReadOnlyState);
                _director.SetLevelView(_level.ReadOnlyState);
                _run.BindGameplay(_chase, _floor, _director);
                _controller.BeginCollection(_floor.ReadOnlyState.ActiveCakeAnchors,
                    activeEffects.Has(new EffectId("blind-faith")));
                AssembleShrines();
            }
            RouteClosedDoors();
            _controller.Ready();
            if (_effects != null)
            {
                _effects.ConfigureHazards(_progression, request.IsShop ? null : _floor, request.IsShop ? null : _director, levelService: _level);
                _effects.SetOptionalRooms(_controller.OptionalRooms());
                _effects.BindActors();
            }
            RoomsReady?.Invoke(_procedural.PresentationRooms);
            AssemblyReady?.Invoke(request, _procedural.PlayerSpawnPosition, _procedural.PlayerSpawnRotation);
            if (!_progression.ConfirmFloorReady(request.GenerationId))
                throw new InvalidOperationException("Progression rejected readiness for the generated floor.");
        }

        private void BindWorld()
        {
            if (_worldBound) return;
            _level.InteractableChanged += HandleInteractable;
            _run.BeforeTick += RouteClosedDoors;
            _worldBound = true;
        }

        private void UnbindWorld()
        {
            if (!_worldBound) return;
            if (_level != null) _level.InteractableChanged -= HandleInteractable;
            if (_run != null) _run.BeforeTick -= RouteClosedDoors;
            _director?.SetClosedDoors(null);
            foreach (var hunter in HunterRegistry.Items) if (hunter != null)
            { hunter.SetClosedDoors(null); hunter.SetInteractables(null); hunter.SetFloorView(null); hunter.SetActiveEffects(null); }
            _worldBound = false;
        }

        private void HandleInteractable(InteractableState before, InteractableState after)
        {
            _procedural.ApplyInteractableState(after);
            if (WickActive && after.Kind == InteractableKind.Light && after.Value != InteractableStateValue.Lit)
                _level.SetLit(after.Id, true);
            if (after.Kind == InteractableKind.Door) RouteClosedDoors();
        }

        private void RouteClosedDoors()
        {
            if (!_worldBound || !_level.ReadOnlyState.IsReady) return;
            _director.SetClosedDoors(_level.ClosedDoors);
            foreach (var hunter in HunterRegistry.Items) if (hunter != null) BindHunterWorld(hunter);
        }

        private void BindHunterWorld(HunterManager hunter)
        {
            hunter.SetClosedDoors(_level.ClosedDoors);
            hunter.SetInteractables(_level.Interactables);
            hunter.SetFloorView(_state != null && !_state.Request.IsShop ? _floor?.ReadOnlyState : null);
            hunter.SetActiveEffects(_progression?.EffectsSnapshot.ActiveEffects);
        }
        private void RestoreMutations(HunterManager hunter)
        {
            foreach (var mutation in _controller.RetainedMutations(hunter.ArchetypeKey))
                if (!hunter.ApplyMutation(mutation, announce: false))
                    throw new InvalidOperationException("Retained mutation rejected by " + hunter.ArchetypeKey + ": " + mutation.Tunable);
        }
        private void HandleMutation(HunterMutationFact fact) => _controller.RetainMutation(fact);
        private void HandleArchetype(HunterArchetypeFact fact)
        {
            if (_worldBound && _state != null && _state.Hunters.Contains(fact.Hunter) &&
                fact.Kind == HunterArchetypeFactKind.ReplayedDoorPassage) _level.CloseDoor(fact.ObjectId);
        }

        private void HandleDestruction(RoomDestructionSample sample) => _procedural.SetRoomDestruction(sample);
        private void HandleRoomPhase(RoomPhaseChangedFact fact)
        { foreach (EntityId id in _state.Hunters) if (HunterRegistry.TryGet(id, out var hunter)) hunter.SetRoomPhase(fact); }
        private void HandleMovement(PlayerMovementSample sample)
        {
            _controller.ObservePuzzleMovement(sample);
            _effects?.ObserveMovement(sample);
            if (_controller.ObserveCrossing(sample, out int door, out Vector3 position)) _effects?.RecordDoorCrossed(door, position);
        }
        private void HandleTraversal(PlayerTraversalFact fact)
        {
            _effects?.ObserveTraversal(fact);
            CompletePuzzleVault(fact, fact.SurfaceId);
        }
        private void HandleTick(InputFrame frame, float dt, long tick)
        {
            if (_controller.TryTickPuzzles(dt, tick, out var movement)) _procedural?.TickPuzzles(movement, dt);
            _effects?.Tick(frame, dt, tick);
            if (_controller.TickWick(dt, tick)) RestoreWick();
        }

        public void CompletePuzzleVault(PlayerTraversalFact fact, int surfaceId)
        {
            if (_controller != null && _controller.AcceptPuzzleVault(fact, surfaceId))
                _procedural?.CompletePuzzleVault(surfaceId, fact.Succeeded);
        }
        private void HandleTheme(string theme, string light, string sound, string fog, string hands)
        {
            _controller.RecordHandLook(hands);
            ThemePublished?.Invoke(theme, light, sound, fog, hands);
        }
        private void HandleRoomTheme(int room, string theme, string family) => RoomThemePublished?.Invoke(room, theme, family);
        private void HandleFreeze(int room, int behind, int anchor, Vector3 doorway, Vector3 hunter)
        {
            _controller.RecordFreeze(room, behind, anchor);
            ThresholdFreezePublished?.Invoke(room, behind, anchor, doorway, hunter);
        }
        private void HandlePuzzleReward(int puzzle, int anchor, Vector3 position) => _controller.RecordPuzzleReward(puzzle, anchor, position);
        private void HandlePassageOpened(int siteIndex, int pocket, IReadOnlyList<Vector3> tiles)
        {
            if (!_controller.AcceptsGameplay(_state.Player)) return;
            foreach (var anchor in _procedural.LinedPocketAnchors)
                if (_floor.RegisterPassageReward(anchor)) _controller.ObservePuzzleGoldCreated(anchor.Id);
        }
        private void HandlePuzzleSolved(int puzzle, int room, int anchor)
        {
            if (_controller.AcceptsGameplay(_state.Player) && _floor.SolvePuzzle(puzzle, room, anchor))
                _controller.ObservePuzzleGoldCreated(anchor);
        }

        private void AssembleShrines()
        {
            if (_shrineConfig == null) _shrineConfig = Resources.Load<ShrineConfig>("ScriptableObjects/Domain/Shrine/ShrineConfig");
            if (_shrineDriverConfig == null) _shrineDriverConfig = Resources.Load<ShrineDriverConfig>("ScriptableObjects/Domain/Shrine/ShrineDriverConfig");
            if (_spawnConfig == null) _spawnConfig = Resources.Load<ExpeditionSpawnDriverConfig>("ScriptableObjects/Session/Expedition/ExpeditionSpawnDriverConfig");
            if (_shrineConfig == null || _shrineDriverConfig == null || _spawnConfig == null)
                throw new InvalidOperationException("Shrine configuration is unwired. Run Worsen/Shrine/Ensure Shrine Assets.");
            _shrines = new GameObject("Floor Shrines").AddComponent<ShrineManager>();
            _spawnDriver = gameObject.AddComponent<ExpeditionSpawnDriver>();
            var sites = new List<ShrineSite>();
            foreach (var site in _procedural.ShrineSites) sites.Add(new ShrineSite(site.Position, site.RoomId, site.GapEdge));
            _shrines.Assemble(sites, _state.Request.Round, _shrineConfig, _shrineDriverConfig,
                new System.Random(ExpeditionSessionController.ShrineSeed(_progression.Snapshot.Seed, _state.Request.Round)));
            _run.BindShrines(_shrines, _progression, GenerationId, _state.Player, () => _controller.CollectedFraction);
        }

        private void HandleProgressionTransaction(ProgressionSnapshot before, ProgressionSnapshot after, string operation, string choice)
        { if (operation == nameof(ProgressionSessionManager.StartRun)) _controller.ResetRun(); }

        private void HandleShrineResolved(ShrineResolvedFact fact)
        {
            if (!_controller.AcceptShrine(fact)) return;
            if (fact.DropBeliefs)
                foreach (var hunter in HunterRegistry.Items) if (hunter != null) hunter.ClearBelief();
            if (fact.WickSeconds > 0f)
            {
                var lamps = new List<InteractableState>();
                foreach (var room in _level.ReadOnlyState.Graph.Rooms) lamps.AddRange(_level.Interactables.InRoom(room.Id));
                _controller.BeginWick(fact.WickSeconds, fact.Activation.Tick, lamps);
                foreach (var lamp in lamps) if (lamp.Kind == InteractableKind.Light) _level.SetLit(lamp.Id, true);
                WickActiveChanged?.Invoke(WickActive);
            }
            if (fact.ResolvedKind == ShrineKind.Passage)
            {
                bool opened = false;
                for (int siteIndex = 0; siteIndex < _procedural.ShrineSites.Count; siteIndex++)
                {
                    var site = _procedural.ShrineSites[siteIndex];
                    if (site.GapEdge && site.RoomId == fact.Activation.RoomId && site.Position == fact.Activation.Position)
                    {
                        opened = _procedural.ActivatePassage(siteIndex) && _floor.ActivatePocket(site.DestinationPocketRoomId);
                        break;
                    }
                }
                if (!opened) Unresolved(fact, "passage-pocket-unavailable");
            }
            for (int i = 0; i < fact.ExtraHunters; i++) SpawnShrineHunter(fact, i);
        }

        private void SpawnShrineHunter(ShrineResolvedFact fact, int index)
        {
            if (!PlayerRegistry.TryGet(_state.Player, out var player) || !player.ReadOnlyState.IsAlive) return;
            var roster = _state.Request.Effects.ActiveThreatIds;
            if (roster == null || roster.Count == 0) { Unresolved(fact, "purgatory-run-roster-empty"); return; }
            var random = new System.Random(unchecked(ExpeditionSessionController.ShrineSeed(_progression.Snapshot.Seed,
                _state.Request.Round) ^ fact.Activation.ShrineId * 397 ^ index));
            string key = roster[random.Next(roster.Count)];
            HunterProfile profile = _hunterProfile != null && _hunterProfile.ArchetypeKey == key ? _hunterProfile : null;
            if (_hunterRoster != null) foreach (var entry in _hunterRoster) if (entry != null && entry.ArchetypeKey == key) profile = entry;
            if (profile == null) { Unresolved(fact, "purgatory-profile-unavailable:" + key); return; }
            foreach (var position in _procedural.HunterSpawnPositions)
            {
                if (!_procedural.ValidateHunterSpawn(position, out _) || !LateSpawnRoomAvailable(position, player.ReadOnlyState.Position) ||
                    !_spawnDriver.Validate(position, player.ReadOnlyState.Position, _spawnConfig,
                        profile.MotorOverride != null ? profile.MotorOverride.NavigationAreaMask : 1)) continue;
                EntityId id = _hunterFactory.Spawn(_controller.HunterSpawn(key, position));
                _controller.RecordHunter(id);
                if (!HunterRegistry.TryGet(id, out var hunter)) throw new InvalidOperationException("Purgatory hunter failed to register.");
                hunter.ApplyRunSpeedMultiplier(_state.Request.Effects.HunterSpeedMultiplier);
                hunter.SetTraits(_state.Request.Effects.Traits); BindHunterWorld(hunter);
                RestoreMutations(hunter);
                foreach (var phase in _floor.ReadOnlyState.RoomPhases) hunter.SetRoomPhase(new RoomPhaseChangedFact(phase.Key, phase.Value, _run.Tick));
                _run.BindAdditionalHunter(hunter);
                if (fact.Mutation)
                {
                    var pool = profile.MutationPool;
                    bool applied = false;
                    if (pool != null && pool.Count > 0)
                    {
                        int first = random.Next(pool.Count);
                        for (int n = 0; n < pool.Count && !applied; n++)
                        { var entry = pool[(first + n) % pool.Count]; if (entry != null) applied = hunter.ApplyMutation(entry.Mutation); }
                    }
                    if (!applied) Unresolved(fact, "purgatory-mutation-unavailable:" + key);
                }
                return;
            }
            Unresolved(fact, "purgatory-no-safe-live-spawn");
        }

        private bool LateSpawnRoomAvailable(Vector3 candidate, Vector3 player)
        {
            foreach (var room in _level.ReadOnlyState.Graph.Rooms)
                if (room.ContainsXZ(candidate)) return !room.ContainsXZ(player) && !room.Pocket &&
                    _floor.ReadOnlyState.RoomPhases.TryGetValue(room.Id, out var phase) && phase == RoomPhase.Open;
            return false;
        }
        private void Unresolved(ShrineResolvedFact fact, string reason)
        { ShrineWorldEffectUnresolved?.Invoke(fact, reason); Debug.LogWarning("Shrine " + fact.Activation.ShrineId + ": " + reason, this); }
        private void RestoreWick()
        {
            if (_controller == null) return;
            var restore = _controller.EndWick();
            if (_level != null) foreach (var lamp in restore) _level.SetLit(lamp.Key, lamp.Value);
            WickActiveChanged?.Invoke(false);
        }

        private void HandleHealth(EntityId player, float health, float maximum)
        {
            if (health <= 0f && player == _state.Player) _controller.ResetRun();
            // Commit terminal health in HandleRunEnded after a confirmed consumption fact can reach presentation.
            if (health > 0f && _controller.AcceptsGameplay(player)) _progression.RecordHealth(GenerationId, health);
        }

        private void HandlePickup(PickupCollectedFact fact)
        {
            _controller.ObservePickup(fact, _floor.ReadOnlyState.CakeCount == _floor.ReadOnlyState.RequiredCakeCount);
            if (_controller.AcceptsGameplay(fact.PlayerId) && fact.Kind == PickupKind.GoldenCake)
            {
                _progression.RecordGoldenCollected(GenerationId, fact.AnchorId);
                if (PlayerRegistry.TryGet(fact.PlayerId, out var player))
                    _effects?.RecordGoldenCollected(fact.PlayerId, player.ReadOnlyState.Position, fact.Tick);
            }
        }

        private void HandleRunEnded(RunSummary summary)
        {
            _effects?.Suspend();
            if (summary.EndReason == RunEndReason.Unknown || !_controller.Resolve(summary.Scene)) return;
            int generationId = GenerationId;
            if (PlayerRegistry.TryGet(_state.Player, out var player) && player.ReadOnlyState != null)
                _progression.RecordHealth(generationId, player.ReadOnlyState.Health);
            if (summary.EndReason == RunEndReason.Escaped) _progression.CompleteFloor(generationId, summary.Bailed);
            else _progression.EndRun(generationId);
        }

        private void HandleSnapshot(ProgressionSnapshot snapshot, IReadOnlyActiveEffects activeEffects)
        {
            if (snapshot.GenerationId != GenerationId || snapshot.Round != _state.Request.Round ||
                snapshot.Revision != _progression.Snapshot.Revision) return;
            _effects?.UpdateEffects(snapshot.Effects);
            if (PlayerRegistry.TryGet(_state.Player, out var currentPlayer)) currentPlayer.SetActiveEffects(activeEffects);
            foreach (var hunter in HunterRegistry.Items) if (hunter != null) hunter.SetActiveEffects(activeEffects);
            if (_state.Phase != ExpeditionAssemblyPhase.Ready || !_state.Request.IsShop ||
                snapshot.GenerationId != GenerationId || snapshot.Phase != ProgressionPhase.Shop) return;
            // Unchanged baseline publication must not cancel Player's pending floor-start effects.
            if (snapshot.Effects.Health == _state.Request.Effects.Health &&
                snapshot.Effects.MaximumHealth == _state.Request.Effects.MaximumHealth &&
                snapshot.Effects.MovementSpeedMultiplier == _state.Request.Effects.MovementSpeedMultiplier) return;
            if (PlayerRegistry.TryGet(_state.Player, out var player))
                player.ApplyRunModifiers(snapshot.Effects.Health, snapshot.Effects.MaximumHealth, snapshot.Effects.MovementSpeedMultiplier);
        }

        private void ReleaseFloor()
        {
            var failures = new List<Exception>();
            if (_state != null && PlayerRegistry.TryGet(_state.Player, out var player) && player.ReadOnlyState != null)
                _controller.CaptureShield(player.ReadOnlyState.IsAlive, player.ReadOnlyShieldState.Shield);
            Release(RestoreWick, failures);
            if (_run != null) _run.BindShrines(null, null, 0, EntityId.None, null);
            if (_shrines != null)
            {
                Release(_shrines.Teardown, failures);
                if (Application.isPlaying) Destroy(_shrines.gameObject); else DestroyImmediate(_shrines.gameObject);
                _shrines = null;
            }
            if (_spawnDriver != null)
            { if (Application.isPlaying) Destroy(_spawnDriver); else DestroyImmediate(_spawnDriver); _spawnDriver = null; }
            UnbindWorld();
            Release(() => FloorReleased?.Invoke(), failures);
            if (_effects != null) { _effects.ClearHazards(); _effects.Suspend(); }
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
            _controller.Fail("Floor " + _state.Request.Round + ", seed " + _state.Request.Seed +
                ", usedFallback=" + _state.UsedFallback + ": " + exception.Message +
                (_state.UsedFallback ? "\n" + _state.LayoutManifest : string.Empty));
            Debug.LogError(_state.Failure, this);
            if (_progression != null) _progression.FailGeneration(generationId, _state.Failure);
        }

        private void OnDestroy()
        {
            ClearScene();
            if (Instance == this) Instance = null;
            AssemblyReady = null; RoomsReady = null; FloorReleased = null;
            ThemePublished = null; RoomThemePublished = null; ThresholdFreezePublished = null;
            WickActiveChanged = null; ShrineWorldEffectUnresolved = null;
        }
    }
}
