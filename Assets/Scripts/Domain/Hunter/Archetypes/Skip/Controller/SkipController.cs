// ============================================================================
// SkipController.cs
// ============================================================================
// PURPOSE:
//   Learns completed route uses, then proposes a silent interception on a cooldown.
//   Engine placement must succeed before the arrival and its environmental mark exist.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter Skip.
// KEY RESPONSIBILITIES:
//   - Reset per floor, deduplicate deliveries and apply capped catalogue hooks.
//   - Keep ordinary motion slow and prevent shared chase/lunge feedback.
// DEPENDENCIES:
//   - Own state/config, Hunter profile, Default/dormancy seam and injected Core views.
// USAGE NOTES:
//   Session supplies committed uses, not overlap occupancy or guessed room changes.
//   Seeded randomness selects among equally well-used routes in stable id order.
//   No sound or teleport visual is emitted, including under No Tell.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Hunter.Archetypes.Ticking;
namespace Worsen.Domain.Hunter.Archetypes.Skip
{
    public sealed class SkipController : DefaultHunterController, IHunterDormancyRules
    {
        public static readonly EffectId ShorterCooldown = new EffectId("skip-shorter-cooldown");
        public static readonly EffectId QuickerLearner = new EffectId("skip-quicker-learner");
        public static readonly EffectId WiderReach = new EffectId("skip-wider-reach");
        public static readonly EffectId NoTell = new EffectId("skip-no-tell");
        private readonly SkipBehaviorState _state = new SkipBehaviorState();
        private readonly SkipConfig _config;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        public SkipController(SkipConfig config, HunterProfile profile, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool Dormant => true;
        public override bool OwnsPursuit => true;
        public int Threshold => Mathf.Max(1, _config.UsesRequired - Stacks(QuickerLearner));
        public float Cooldown => _config.CooldownSeconds * Mathf.Pow(_config.ShorterCooldownMultiplier, Stacks(ShorterCooldown));
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, 3);
        public int Uses(int routeId) => _state.Counts.TryGetValue(routeId, out int count) ? count : 0;
        public override void Reset(HunterArchetypeContext context)
        { _state.Context = context; _state.Floor = -1; _state.LastTick = -1; _state.Elapsed = 0f;
            _state.Pending = false; _state.Candidate = _state.WalkRoute = default; _state.Counts.Clear(); _state.Routes.Clear(); _state.Facts.Clear(); }
        public bool BeginFloor(long floor)
        {
            if (floor < 0 || floor <= _state.Floor) return false;
            _state.Floor = floor; _state.Elapsed = 0f; _state.Pending = false; _state.Candidate = _state.WalkRoute = default;
            _state.LastTick = -1;
            _state.Counts.Clear(); _state.Routes.Clear(); _state.Facts.Clear(); return true;
        }
        public bool RecordUse(SkipTraversalUse use)
        {
            if (use.Floor != _state.Floor || use.Floor < 0 || use.RouteId <= 0 || use.Sequence < 0 ||
                use.Player != _state.Context.Player.Id || !Finite(use.Position) || !Enum.IsDefined(typeof(SkipRouteKind), use.Kind) ||
                (_state.Routes.TryGetValue(use.RouteId, out var previous) && use.Sequence <= previous.Sequence)) return false;
            _state.Counts[use.RouteId] = Uses(use.RouteId) == int.MaxValue ? int.MaxValue : Uses(use.RouteId) + 1;
            _state.Routes[use.RouteId] = use;
            if (use.Kind == SkipRouteKind.Doorway || use.Kind == SkipRouteKind.VaultWindow || Stacks(WiderReach) > 0)
                _state.WalkRoute = use;
            return true;
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick; _state.Pending = false;
            _state.Elapsed = Mathf.Min(_config.CooldownSeconds, _state.Elapsed + context.DeltaTime);
            if (_state.Elapsed < Cooldown || !context.CanReplay || !context.Player.IsAlive || !context.Hunter.IsActive) return;
            var choices = new List<int>(); int best = Threshold;
            foreach (int id in _state.Routes.Keys)
            {
                SkipTraversalUse route = _state.Routes[id];
                if ((route.Kind == SkipRouteKind.StairHead || route.Kind == SkipRouteKind.Drop) && Stacks(WiderReach) == 0) continue;
                if (!Available(route.Position) || Uses(id) < best) continue;
                if (Uses(id) > best) { choices.Clear(); best = Uses(id); }
                choices.Add(id);
            }
            if (choices.Count == 0) return;
            choices.Sort(); _state.Candidate = _state.Routes[choices[_random.Next(choices.Count)]]; _state.Pending = true;
        }
        public bool TryTeleport(out Vector3 position)
        { position = _state.Candidate.Position; return _state.Pending; }
        public void CommitTeleport(bool placed, Vector3 position)
        {
            if (!_state.Pending) return;
            _state.Pending = false;
            if (!placed || position != _state.Candidate.Position || !Available(position)) return;
            _state.Elapsed = 0f; Emit(SkipFactKind.Teleported, position);
            if (Stacks(NoTell) == 0) Emit(SkipFactKind.Marked, position);
        }
        private bool Available(Vector3 point)
        {
            if (_state.Context.Level == null || !_state.Context.Level.IsReady) return false;
            if (_state.Context.UnavailableRooms != null)
                foreach (Bounds bounds in _state.Context.UnavailableRooms) if (bounds.Contains(point)) return false;
            if (_state.Context.Floor != null && _state.Context.Level.Graph != null)
                foreach (LevelRoom room in _state.Context.Level.Graph.Rooms)
                    if (room.Bounds.Contains(point) && _state.Context.Floor.RoomPhases.TryGetValue(room.Id, out RoomPhase phase) &&
                        phase == RoomPhase.Closed) return false;
            return true;
        }
        private static bool Finite(Vector3 point) => !float.IsNaN(point.sqrMagnitude) && !float.IsInfinity(point.sqrMagnitude);
        public override bool TryMovement(out Vector3 target, out float speed)
        {
            target = _state.Context.Hunter.Position;
            // Walk only toward the learned anchor, never omnisciently chase the player.
            if (_state.WalkRoute.RouteId > 0 && Available(_state.WalkRoute.Position)) target = _state.WalkRoute.Position;
            speed = _profile.PatrolSpeed; return true;
        }
        public bool TakeFact(out SkipFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
        private void Emit(SkipFactKind kind, Vector3 position) => _state.Facts.Enqueue(new SkipFact(_state.Context.Hunter.Id,
            _state.Floor, _state.Context.Tick, kind, _state.Candidate.RouteId, _state.Candidate.MarkId, position));
    }
}
