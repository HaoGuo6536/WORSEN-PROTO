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
//   - Reject stale requests and preserve the queued phase during deferred cleanup.
//   - Preserve selected hunter identities and validate real room crossings for retained perks.
//   - Produce spawn requests and enforce the requested active hunter budget.
//   - Commit readiness only after every required actor has been registered.
// DEPENDENCIES:
//   - Own BehaviorState/Definitions and immutable Core progression/spawn values.
// USAGE NOTES:
//   Pure C#: no clock, engine objects, factories or foreign mutable state.
//   Generation supplies all seed-derived placements; this Controller draws no randomness.
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
            _state.Phase = ExpeditionAssemblyPhase.Queued;
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

        public IReadOnlyList<SpawnRequest> HunterSpawns(string archetype, IReadOnlyList<Vector3> positions)
        {
            RequireGenerating();
            int count = _state.Request.IsShop ? 0 : _state.Request.Effects.ActiveThreatBudget;
            if (positions == null || positions.Count < count)
                throw new InvalidOperationException("Generated floor has fewer hunter spawns than the active threat budget.");
            if (!_state.Request.IsShop && _state.Request.Effects.ActiveThreatIds != null && _state.Request.Effects.ActiveThreatIds.Count != count)
                throw new InvalidOperationException("Selected hunter identities must exactly match their budget.");
            var requests = new SpawnRequest[count];
            for (int index = 0; index < count; index++)
            {
                string selected = _state.Request.Effects.ActiveThreatIds == null ? archetype : _state.Request.Effects.ActiveThreatIds[index];
                ValidatePlacement(selected, positions[index]);
                requests[index] = new SpawnRequest(selected, positions[index], Quaternion.identity);
            }
            return requests;
        }

        public float OptionalWindowMultiplier() => (_state.Request.Effects.Traits & ProgressionTraits.SealedSills) != 0 ? 0.4f : 1f;

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
            RequireGenerating();
            if (!id.IsValid || id == _state.Player || _state.Hunters.Contains(id))
                throw new ArgumentException("Hunter identities must be valid and unique.", nameof(id));
            _state.Hunters.Add(id);
        }

        public void Ready()
        {
            RequireGenerating();
            int count = _state.Request.IsShop ? 0 : _state.Request.Effects.ActiveThreatBudget;
            if (!_state.Player.IsValid || _state.Hunters.Count != count)
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

        public void ReleaseActors()
        { _state.Player = EntityId.None; _state.Hunters.Clear(); _state.Rooms = Array.Empty<GeneratedRoomSample>();
          _state.HasPreviousPosition = false; _state.PreviousRoom = 0; }

        public void Fail(string reason)
        {
            _state.Failure = string.IsNullOrWhiteSpace(reason) ? "Floor assembly failed." : reason;
            _state.Phase = ExpeditionAssemblyPhase.Failed;
        }

        public void ClearScene()
        {
            ReleaseActors();
            _state.Scene = SceneKey.None;
            _state.Request = default;
            _state.Failure = string.Empty;
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
