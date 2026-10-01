// ============================================================================
// ExpeditionSessionController.cs
// ============================================================================
// PURPOSE:
//   Validates generation admission and records each owned entity by identity.
//   It prevents duplicate requests and terminal callbacks from assembling or
//   resolving a floor twice while leaving all engine work to its Manager.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Preserve retained hunters, allocate duplicates and reserve separate false-cake Mimic sites.
//   - Reject stale/fallback/incomplete admission and retain diagnostic shortfalls.
//   - Buffer challenge, movement, traversal and room-crossing facts without engine queries.
//   - Preserve shields/mutations, time Wick and count physical golden pickups.
//   - Coordinate queued generation and complete actor cleanup without selecting effect rules.
// DEPENDENCIES:
//   - Own BehaviorState/Definitions and immutable Core progression/spawn values.
// USAGE NOTES:
//   Pure C#: no clock, engine objects, factories or foreign mutable state.
//   Generation supplies placements. Shrine stream seeds are separated from actor/layout draws.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSessionController
    {
        private readonly ExpeditionSessionBehaviorState _state;
        public ExpeditionSessionController(ExpeditionSessionBehaviorState state)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        public void Bind(SceneKey scene)
        {
            if (scene == SceneKey.None) throw new ArgumentException("An expedition requires a concrete scene.", nameof(scene));
            ClearScene();
            _state.Scene = scene;
            _state.Phase = ExpeditionAssemblyPhase.Waiting;
        }

        public bool Queue(ProgressionGenerationRequest request)
        {
            if (_state.Phase == ExpeditionAssemblyPhase.Unbound)
                throw new InvalidOperationException("Bind a scene before generation.");
            if (request.GenerationId <= _state.LastGenerationId) return false;
            if (_state.Phase == ExpeditionAssemblyPhase.Queued || _state.Phase == ExpeditionAssemblyPhase.Generating)
                throw new InvalidOperationException("A floor replacement is already pending.");
            Validate(request);
            _state.LastGenerationId = request.GenerationId;
            _state.Request = request;
            _state.Failure = string.Empty;
            _state.UsedFallback = false;
            _state.LayoutManifest = string.Empty;
            _state.HunterSpawnShortfall = 0;
            _state.Phase = ExpeditionAssemblyPhase.Queued;
            _state.RequestedHunterCount = request.IsShop ? 0 : request.Effects.ActiveThreatBudget;
            return true;
        }

        public bool Begin(int generationId)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Queued || _state.Request.GenerationId != generationId) return false;
            if (_state.Player.IsValid || _state.Hunters.Count != 0)
                throw new InvalidOperationException("Previous floor actors must be released before generation.");
            _state.Phase = ExpeditionAssemblyPhase.Generating;
            return true;
        }

        public SpawnRequest PlayerSpawn(string archetype, Vector3 position, Quaternion rotation)
        {
            RequireGenerating();
            ValidatePlacement(archetype, position);
            return new SpawnRequest(archetype, position, rotation);
        }

        public int RequiredHunterCount(int extraHunters)
        {
            RequireGenerating();
            if (extraHunters < 0) throw new ArgumentOutOfRangeException(nameof(extraHunters));
            return _state.Request.IsShop ? 0 : checked(_state.Request.Effects.ActiveThreatBudget + extraHunters);
        }
        public void RequireHunterCapacity(int extraHunters, int validatedCapacity)
        {
            int required = RequiredHunterCount(extraHunters);
            if (validatedCapacity < required)
                throw new InvalidOperationException("hunter-spawn-capacity-shortfall: required=" + required + ", validated=" + validatedCapacity);
        }

        public IReadOnlyList<SpawnRequest> HunterSpawns(string archetype, IReadOnlyList<Vector3> positions,
            Func<Vector3, bool> validate = null, IReadOnlyList<string> extraHunters = null, IReadOnlyList<Vector3> mimicSites = null)
        {
            RequireGenerating();
            int requested = _state.Request.IsShop ? 0 : _state.Request.Effects.ActiveThreatBudget;
            if (!_state.Request.IsShop && _state.Request.Effects.ActiveThreatIds != null && _state.Request.Effects.ActiveThreatIds.Count != requested)
                throw new InvalidOperationException("Selected hunter identities must exactly match their budget.");
            int retained = requested;
            requested = RequiredHunterCount(extraHunters?.Count ?? 0);
            _state.RequestedHunterCount = requested;
            var valid = new List<Vector3>();
            if (positions != null) foreach (var position in positions)
                if ((validate == null || validate(position)) && !valid.Contains(position)) valid.Add(position);
            var requests = new List<SpawnRequest>();
            int ordinary = 0, mimic = 0;
            var usedMimicSites = new HashSet<Vector3>();
            for (int index = 0; index < requested; index++)
            {
                string selected = index >= retained ? extraHunters[index - retained] :
                    _state.Request.Effects.ActiveThreatIds == null ? archetype : _state.Request.Effects.ActiveThreatIds[index];
                if (selected == "mimic")
                {
                    while (mimicSites != null && mimic < mimicSites.Count && usedMimicSites.Contains(mimicSites[mimic])) mimic++;
                    if (mimicSites == null || mimic >= mimicSites.Count) continue;
                    var site = mimicSites[mimic++]; usedMimicSites.Add(site);
                    requests.Add(HunterSpawn(selected, site));
                }
                else if (ordinary < valid.Count) requests.Add(HunterSpawn(selected, valid[ordinary++]));
            }
            _state.HunterSpawnShortfall = requested - requests.Count;
            return requests;
        }

        public int MissingMimics(int extraCount, IReadOnlyList<string> extraHunters = null)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Ready || _state.Request.IsShop) return 0;
            int baseline = 0;
            if (_state.Request.Effects.ActiveThreatIds != null)
                foreach (var key in _state.Request.Effects.ActiveThreatIds) if (key == "mimic") baseline++;
            if (extraHunters != null) foreach (var key in extraHunters) if (key == "mimic") baseline++;
            if (baseline == 0) return 0;
            _state.NextDuplicate.TryGetValue("mimic", out int spawned);
            return Math.Max(0, baseline + Mathf.Clamp(extraCount, 0, 3) - spawned);
        }

        public SpawnRequest HunterSpawn(string archetype, Vector3 position)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Generating && _state.Phase != ExpeditionAssemblyPhase.Ready)
                throw new InvalidOperationException("Hunter spawning requires an assembling or ready floor.");
            ValidatePlacement(archetype, position);
            _state.NextDuplicate.TryGetValue(archetype, out int duplicate);
            _state.NextDuplicate[archetype] = checked(duplicate + 1);
            return new SpawnRequest(archetype, position, Quaternion.identity, duplicateIndex: duplicate);
        }

        public void RetainMutation(HunterMutationFact fact)
        {
            if (!fact.Mutation.HasValue || !_state.Hunters.Contains(fact.Hunter) || string.IsNullOrWhiteSpace(fact.ArchetypeKey)) return;
            if (!_state.Mutations.TryGetValue(fact.ArchetypeKey, out var rules))
                _state.Mutations.Add(fact.ArchetypeKey, rules = new Dictionary<HunterTunable, HunterMutation>());
            rules[fact.Mutation.Value.Tunable] = fact.Mutation.Value;
        }
        public IReadOnlyList<HunterMutation> RetainedMutations(string archetype) =>
            _state.Mutations.TryGetValue(archetype, out var rules) ? new List<HunterMutation>(rules.Values).AsReadOnly() : Array.Empty<HunterMutation>();

        public void RecordGenerationOutcome(bool usedFallback, string layoutManifest)
        {
            RequireGenerating();
            _state.UsedFallback = usedFallback;
            _state.LayoutManifest = layoutManifest ?? string.Empty;
        }

        public void RecordRooms(IReadOnlyList<GeneratedRoomSample> rooms)
        {
            RequireGenerating();
            _state.Rooms = rooms ?? Array.Empty<GeneratedRoomSample>();
            _state.HasPreviousPosition = false;
        }

        public int[] OptionalRooms()
        {
            var ids = new List<int>();
            foreach (var room in _state.Rooms) if (room.OptionalRoom) ids.Add(room.RoomId);
            return ids.ToArray();
        }

        public bool ObserveCrossing(PlayerMovementSample sample, out int doorId, out Vector3 position)
        {
            doorId = 0; position = Vector3.zero;
            if (sample.Id != _state.Player || _state.Phase != ExpeditionAssemblyPhase.Ready) return false;
            int current = 0;
            foreach (var room in _state.Rooms) if (room.Bounds.Contains(sample.Position)) { current = room.RoomId; break; }
            bool crossed = false;
            if (_state.HasPreviousPosition && current != 0 && _state.PreviousRoom != 0 && current != _state.PreviousRoom
                && Vector3.Distance(sample.Position, _state.PreviousPosition) < 3f)
            {
                Vector3 midpoint = (sample.Position + _state.PreviousPosition) * 0.5f;
                float nearest = 4f;
                foreach (var from in _state.Rooms) if (from.RoomId == _state.PreviousRoom && from.PortalCenters != null)
                    foreach (var to in _state.Rooms) if (to.RoomId == current && to.PortalCenters != null)
                        for (int i = 0; i < from.PortalCenters.Length; i++)
                            for (int j = 0; j < to.PortalCenters.Length; j++)
                            {
                                Vector3 a = from.PortalCenters[i], b = to.PortalCenters[j];
                                float distance = new Vector2(a.x-midpoint.x,a.z-midpoint.z).sqrMagnitude;
                                if ((a-b).sqrMagnitude >= 0.01f || distance >= nearest) continue;
                                int index = from.RoomId < to.RoomId ? i : j;
                                doorId = Mathf.Min(from.RoomId,to.RoomId)*100000 + Mathf.Max(from.RoomId,to.RoomId)*256 + index;
                                position = a; nearest = distance; crossed = true;
                            }
            }
            _state.HasPreviousPosition = true; _state.PreviousPosition = sample.Position;
            if (current != 0) _state.PreviousRoom = current;
            return crossed;
        }

        public void RecordPlayer(EntityId id)
        {
            RequireGenerating();
            if (!id.IsValid || _state.Player.IsValid || _state.Hunters.Contains(id))
                throw new ArgumentException("A generated floor requires one unique player identity.", nameof(id));
            _state.Player = id;
        }

        public void RecordHunter(EntityId id)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Generating && _state.Phase != ExpeditionAssemblyPhase.Ready)
                throw new InvalidOperationException("Hunters require an assembling or ready floor.");
            if (!id.IsValid || id == _state.Player || _state.Hunters.Contains(id))
                throw new ArgumentException("Hunter identities must be valid and unique.", nameof(id));
            _state.Hunters.Add(id);
        }

        public void Ready()
        {
            RequireGenerating();
            if (_state.UsedFallback) throw new InvalidOperationException("A fallback layout cannot be admitted as a floor.");
            int count = _state.RequestedHunterCount;
            if (_state.HunterSpawnShortfall != 0 || !_state.Player.IsValid || _state.Hunters.Count != count)
                throw new InvalidOperationException("Cannot admit a floor before all requested actors exist.");
            _state.Phase = ExpeditionAssemblyPhase.Ready;
        }

        public bool Resolve(SceneKey scene)
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Ready || _state.Request.IsShop || scene != _state.Scene) return false;
            _state.Phase = ExpeditionAssemblyPhase.Resolved;
            return true;
        }

        public bool AcceptsGameplay(EntityId player) => _state.Phase == ExpeditionAssemblyPhase.Ready &&
            !_state.Request.IsShop && player == _state.Player;

        public void RecordHandLook(string look) => _state.HandLook = look;
        public void RecordFreeze(int roomId, int behindRoomId, int anchorId)
        {
            RequireGenerating();
            if (roomId == 0 || behindRoomId == 0 || anchorId == 0) return;
            if (!_state.FreezeAnchors.Contains(anchorId)) _state.FreezeAnchors.Add(anchorId);
            if (!_state.FreezeBehindRooms.Contains(behindRoomId)) _state.FreezeBehindRooms.Add(behindRoomId);
        }
        public void RecordPuzzleReward(int puzzleId, int anchorId, Vector3 position)
        { RequireGenerating(); _state.PuzzleRewards[puzzleId] = (anchorId, position); }
        public void ObservePuzzleMovement(PlayerMovementSample sample)
        {
            if (AcceptsGameplay(sample.Id) && sample.Tick > _state.PuzzleTick &&
                (!_state.PuzzleMovement.Id.IsValid || sample.Tick >= _state.PuzzleMovement.Tick)) _state.PuzzleMovement = sample;
        }
        public bool TryTickPuzzles(float dt, long tick, out PlayerMovementSample sample)
        {
            sample = _state.PuzzleMovement;
            if (!Finite(dt) || dt < 0f || !AcceptsGameplay(sample.Id) || tick <= _state.PuzzleTick || sample.Tick != tick) return false;
            _state.PuzzleTick = tick;
            return true;
        }
        public bool AcceptPuzzleVault(PlayerTraversalFact fact, int surfaceId)
        {
            if (!AcceptsGameplay(fact.Id) || fact.Kind != TraversalKind.Vault || surfaceId == 0 ||
                fact.Tick <= _state.PuzzleVaultTick) return false;
            _state.PuzzleVaultTick = fact.Tick;
            return true;
        }
        public void ObservePuzzleGoldCreated(int anchorId) => _state.PuzzleGoldenEligible.Add(anchorId);

        public void ReleaseActors()
        { _state.Player = EntityId.None; _state.Hunters.Clear(); _state.NextDuplicate.Clear(); _state.Rooms = Array.Empty<GeneratedRoomSample>();
          _state.FreezeAnchors.Clear(); _state.FreezeBehindRooms.Clear(); _state.PuzzleRewards.Clear(); _state.HandLook = null;
          _state.PuzzleMovement = default; _state.PuzzleTick = _state.PuzzleVaultTick = -1; _state.PuzzleGoldenEligible.Clear();
          _state.RequiredAnchors.Clear(); _state.GoldenEligible.Clear(); _state.GoldenCollected.Clear();
          _state.ResolvedShrines.Clear(); _state.GoldCreated = false;
          _state.HasPreviousPosition = false; _state.PreviousRoom = 0; }

        public static int ShrineSeed(int runSeed, int round) => unchecked((runSeed * 397) ^ round ^ 0x534852);
        public void CaptureShield(bool alive, float shield)
        { _state.CarriedShield = _state.ShieldTransferAllowed && alive && Finite(shield) ? Mathf.Max(0f, shield) : 0f; }
        public float CarriedShield => _state.CarriedShield;
        public void ResetRun()
        { _state.CarriedShield = 0f; _state.ShieldTransferAllowed = false; _state.Mutations.Clear(); }
        public void AdmitShieldTransfer() => _state.ShieldTransferAllowed = true;
        public bool AcceptShrine(ShrineResolvedFact fact) => _state.Phase == ExpeditionAssemblyPhase.Ready &&
            !_state.Request.IsShop && fact.GenerationId == _state.Request.GenerationId && _state.ResolvedShrines.Add(fact.Activation.ShrineId);
        public void BeginCollection(IReadOnlyList<LevelAnchor> required, bool blindFaith)
        {
            _state.PuzzleGoldenEligible.Clear();
            _state.RequiredAnchors.Clear(); _state.GoldenEligible.Clear(); _state.GoldenCollected.Clear();
            _state.BlindFaith = blindFaith; _state.GoldCreated = false;
            foreach (var anchor in required) _state.RequiredAnchors.Add(anchor.Id);
        }
        public void ObservePickup(PickupCollectedFact fact, bool goldCreated)
        {
            if (!AcceptsGameplay(fact.PlayerId)) return;
            if (!_state.GoldCreated && fact.Kind == PickupKind.Cake && (_state.BlindFaith || _state.RequiredAnchors.Contains(fact.AnchorId)))
                _state.GoldenEligible.Add(fact.AnchorId);
            if (fact.Kind == PickupKind.GoldenCake) _state.GoldenCollected.Add(fact.AnchorId);
            _state.GoldCreated |= goldCreated;
        }
        public float CollectedFraction => (_state.GoldCreated || _state.PuzzleGoldenEligible.Count > 0) &&
            _state.GoldenEligible.Count + _state.PuzzleGoldenEligible.Count > 0 ?
            Mathf.Clamp01((float)_state.GoldenCollected.Count / (_state.GoldenEligible.Count + _state.PuzzleGoldenEligible.Count)) : 0f;
        public bool WickActive => _state.WickRemaining > 0f;
        public void BeginWick(float seconds, long tick, IEnumerable<InteractableState> lamps)
        {
            if (!Finite(seconds) || seconds <= 0f) return;
            foreach (var lamp in lamps)
                if (lamp.Kind == InteractableKind.Light && !_state.LampStates.ContainsKey(lamp.Id))
                    _state.LampStates.Add(lamp.Id, lamp.Value == InteractableStateValue.Lit);
            _state.WickRemaining = Mathf.Max(_state.WickRemaining, seconds); _state.WickTick = tick;
        }
        public bool TickWick(float dt, long tick)
        {
            if (!WickActive || !Finite(dt) || dt <= 0f || tick <= _state.WickTick) return false;
            _state.WickTick = tick; _state.WickRemaining = Mathf.Max(0f, _state.WickRemaining - dt);
            return !WickActive;
        }
        public IReadOnlyDictionary<int, bool> EndWick()
        {
            var restore = new Dictionary<int, bool>(_state.LampStates);
            _state.LampStates.Clear(); _state.WickRemaining = 0f; _state.WickTick = -1;
            return restore;
        }

        // The nearest room footprint along a producer-supplied gap facing must be a
        // pocket; never pick a pocket behind an intervening connected room or behind us.
        public static int PassagePocket(LevelGraph graph, ShrineSite site, Vector3 facing)
        {
            if (!site.GapEdge || graph == null || facing.sqrMagnitude == 0f) return 0;
            int result = 0; float nearest = float.PositiveInfinity;
            foreach (var room in graph.Rooms)
            {
                if (room.Id == site.RoomId) continue;
                foreach (var cell in room.Cells)
                {
                    var start = site.Position; start.y = cell.center.y;
                    if (!cell.IntersectRay(new Ray(start, facing), out float distance) || distance >= nearest) continue;
                    nearest = distance; result = room.Pocket ? room.Id : 0;
                }
            }
            return result;
        }

        public void Fail(string reason)
        {
            _state.Failure = string.IsNullOrWhiteSpace(reason) ? "Floor assembly failed." : reason;
            _state.Phase = ExpeditionAssemblyPhase.Failed;
        }

        public void ClearScene()
        {
            ReleaseActors();
            ResetRun(); EndWick();
            _state.Scene = SceneKey.None;
            _state.Request = default;
            _state.Failure = string.Empty;
            _state.UsedFallback = false;
            _state.LayoutManifest = string.Empty;
            _state.HunterSpawnShortfall = 0;
            _state.Phase = ExpeditionAssemblyPhase.Unbound;
        }

        private void RequireGenerating()
        {
            if (_state.Phase != ExpeditionAssemblyPhase.Generating)
                throw new InvalidOperationException("The floor is not being assembled.");
        }

        private static void Validate(ProgressionGenerationRequest request)
        {
            var effects = request.Effects;
            if (request.GenerationId <= 0 || request.Round <= 0 || effects.ActiveThreatBudget < 0 ||
                !Positive(effects.Health) || !Positive(effects.MaximumHealth) || effects.Health > effects.MaximumHealth ||
                !Positive(effects.MovementSpeedMultiplier) || !Positive(effects.HunterSpeedMultiplier) ||
                !Positive(effects.FogDensityMultiplier) || !Positive(effects.FlashlightRangeMultiplier))
                throw new ArgumentException("Generation requires a living player and finite positive loadout values.", nameof(request));
        }

        private static void ValidatePlacement(string archetype, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(archetype) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
                throw new ArgumentException("An actor requires an archetype and finite spawn position.");
        }
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
