// ============================================================================
// WeaverController.cs
// ============================================================================
// PURPOSE:
//   Plans a verified firing position before committing a warned, fixed web line.
//   Shared Hunter rules retain sensing, memory, habits and close-range catches;
//   this module adds a small Reposition/Shoot plan without an archetype branch
//   in the shared controller or knowledge of physics and rendering objects.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Require fresh radius-matched sweep evidence both before warning and launch.
//   - Publish slow/cue facts and deterministic doorway nests with capped curses.
// DEPENDENCIES:
//   - Own state/config, Hunter planner/default module and injected Core views.
// USAGE NOTES:
//   Time and randomness are injected. Floor navigation continues under a ceiling
//   body; warnings and shared catches force a drop. Outward DTOs await Core promotion.
//   Web Cutter is a Player-side strength modifier, never a grab or slide lock.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public sealed class WeaverController : DefaultHunterController
    {
        public static readonly EffectId StickierWebs = new EffectId("weaver-stickier-webs");
        public static readonly EffectId WiderWebs = new EffectId("weaver-wider-webs");
        public static readonly EffectId DoorwayNests = new EffectId("weaver-doorway-nests");
        public static readonly EffectId QuickSpin = new EffectId("weaver-quick-spin");
        public static readonly EffectId WebCutter = new EffectId("web-cutter");
        private const ulong Clear = 1, Candidate = 2, Fired = 4;
        private static readonly GoapActionDefinition[] Actions = {
            new GoapActionDefinition((int)WeaverAction.Reposition, Candidate, Clear, Clear, 0, 1),
            new GoapActionDefinition((int)WeaverAction.Shoot, Clear, 0, Fired, 0, 1) };
        private readonly WeaverBehaviorState _state;
        private readonly WeaverConfig _config;
        private readonly HunterProfile _profile;
        private readonly System.Random _random;
        public WeaverController(WeaverBehaviorState state, WeaverConfig config, HunterProfile profile, System.Random random)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile)); _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public WeaverAction Action => _state.Action;
        public bool Hold => _state.Hold;
        public bool Ceiling => _state.Ceiling;
        public bool Fire => _state.Fire;
        public bool Warning => _state.Warning;
        public long LastTick => _state.LastTick;
        public void SuspendAttack() { CancelWarning(); _state.Ceiling = false; }
        public float CeilingHeight
        {
            get
            {
                if (_state.Context.Level.Graph != null)
                    foreach (LevelRoom room in _state.Context.Level.Graph.Rooms)
                        if (room.Bounds.Contains(_state.Context.Hunter.Position)) return room.Bounds.max.y;
                return _state.Context.Hunter.Position.y;
            }
        }
        public Vector3 Aim(float height) => _state.Warning ? _state.Aim : _state.Context.Hunter.LastKnownPosition + Vector3.up * height;
        public float Radius => Mathf.Min(.1f, _config.ProjectileRadius * Multiplier(WiderWebs, _config.WiderMultiplier));
        public float WarningSeconds => _config.WarningSeconds * Multiplier(QuickSpin, _config.QuickSpinMultiplier);
        public float SlowSeconds => _config.SlowSeconds * Multiplier(StickierWebs, _config.StickierMultiplier);
        public int Serial => _state.Serial;
        public float WarnedRadius => _state.Radius;
        public Vector3 WarnedOrigin => _state.Origin;
        public Vector3 WarnedTarget => _state.Aim;
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, _config.CurseStackCap);
        private float Multiplier(EffectId id, float multiplier) => Mathf.Pow(multiplier, Stacks(id));
        public void Observe(WeaverObservation observation) { _state.Observation = observation; }
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.Observation = default; _state.LastTick = -1;
            _state.PreviousPosition = context.Hunter.Position;
            _state.Serial = 0; _state.LastRoom = HunterNavigationUtility.RoomAt(context.Level.Graph, context.Hunter.Position);
            _state.Action = WeaverAction.None; _state.Warning = _state.Fire = _state.Hold = false; _state.Ceiling = true;
            _state.Cooldown = _state.SkitterRemaining = _state.WarningRemaining = 0f;
            _state.Facts.Clear(); _state.Webs.Clear(); _state.SeededDoors.Clear(); _state.PassedDoors.Clear();
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick; _state.Fire = _state.Hold = false;
            _state.Action = WeaverAction.None;
            foreach (int serial in new List<int>(_state.Webs.Keys))
            { _state.Webs[serial] -= context.DeltaTime; if (_state.Webs[serial] <= 0f) _state.Webs.Remove(serial); }
            _state.Cooldown = Mathf.Max(0f, _state.Cooldown - context.DeltaTime);
            Doorways();
            _state.PreviousPosition = context.Hunter.Position;
            bool catching = !context.CanReplay || (context.Hunter.PlayerVisible &&
                Vector3.Distance(context.Hunter.Position, context.Player.Position) <= _profile.LungeDistance);
            _state.Ceiling = !catching;
            if (catching || !context.Hunter.IsActive || !context.Player.IsAlive ||
                (context.Hunter is IReadOnlyHunterPursuitState pursuit && pursuit.PursuitSuppressed))
            { CancelWarning(); return; }
            if (_state.Warning)
            {
                _state.Ceiling = false; _state.Hold = true; _state.Action = WeaverAction.Shoot;
                if (!Verified(_state.Radius) || _state.Observation.Origin != _state.Origin || _state.Observation.Target != _state.Aim)
                { CancelWarning(); return; }
                _state.WarningRemaining = Mathf.Max(0f, _state.WarningRemaining - context.DeltaTime);
                _state.Fire = _state.WarningRemaining <= .000001f && _state.Observation.Grounded;
                return;
            }
            if (_state.Cooldown <= 0f && context.Hunter.BeliefConfidence > 0f)
            {
                WeaverObservation observation = _state.Observation;
                bool fresh = observation.Tick == context.Tick && Mathf.Abs(observation.Radius - Radius) < .000001f;
                bool clear = Verified(Radius);
                WeaverShotSpot? candidate = null;
                if (fresh && observation.Spots != null)
                    foreach (WeaverShotSpot spot in observation.Spots)
                        if (spot.Reachable && spot.Clear && (!candidate.HasValue ||
                            (spot.Position - context.Hunter.Position).sqrMagnitude < (candidate.Value.Position - context.Hunter.Position).sqrMagnitude)) candidate = spot;
                GoapPlanResult plan = GoapPlannerUtility.Plan((clear ? Clear : 0) | (candidate.HasValue ? Candidate : 0), Fired, 0, Actions);
                if (plan.Status == GoapPlanStatus.Found)
                {
                    _state.Action = (WeaverAction)plan.ActionIds[0];
                    if (_state.Action == WeaverAction.Reposition) _state.Target = candidate.Value.Position;
                    else
                    {
                        _state.Ceiling = false; _state.Hold = true;
                        if (observation.Grounded)
                        {
                            _state.Warning = true; _state.Origin = observation.Origin; _state.Aim = observation.Target;
                            _state.Radius = Radius; _state.WarningDuration = _state.WarningRemaining = WarningSeconds;
                            Emit(WeaverFactKind.WetClick, _state.Origin, _state.Aim, _state.Radius, _state.WarningDuration);
                        }
                    }
                }
            }
            _state.SkitterRemaining -= context.DeltaTime;
            if (_state.Ceiling && context.Hunter.Velocity.sqrMagnitude > 0f && _state.SkitterRemaining <= 0f)
            { Emit(WeaverFactKind.SkitteringAbove, new Vector3(context.Hunter.Position.x, CeilingHeight, context.Hunter.Position.z));
                _state.SkitterRemaining = _config.SkitterIntervalSeconds; }
        }
        private bool Verified(float radius) => _state.Observation.Tick == _state.Context.Tick && _state.Observation.Clear &&
            Mathf.Abs(_state.Observation.Radius - radius) < .000001f &&
            Vector3.Distance(_state.Observation.Origin, _state.Observation.Target) <= _config.ShotRange;
        public void CommitLaunch(bool launched)
        {
            if (!_state.Fire) return;
            if (launched)
            {
                _state.Serial++; _state.Webs[_state.Serial] = _config.ShotRange / _config.ProjectileSpeed;
                Emit(WeaverFactKind.WebLaunched, _state.Origin, _state.Aim, _state.Radius, serial: _state.Serial);
                Emit(WeaverFactKind.WebGlow, _state.Origin, _state.Aim, _state.Radius, _config.ShotRange / _config.ProjectileSpeed, _state.Serial);
            }
            else Emit(WeaverFactKind.WarningCancelled, _state.Origin);
            _state.Warning = _state.Fire = false; _state.Cooldown = _config.ShotCooldownSeconds;
        }
        private void CancelWarning()
        {
            if (_state.Warning) Emit(WeaverFactKind.WarningCancelled, _state.Origin);
            _state.Warning = _state.Fire = _state.Hold = false; _state.Action = WeaverAction.None;
        }
        public bool TryHit(EntityId target, int serial, long tick, out WebHitFact fact)
        {
            fact = default;
            if (target != _state.Context.Player.Id || !_state.Context.Player.IsAlive || !_state.Webs.Remove(serial)) return false;
            fact = new WebHitFact(_state.Context.Hunter.Id, target, tick, serial, _config.SlowMultiplier, SlowSeconds,
                (_state.Context.Effects?.Has(WebCutter) ?? false) ? .5f : 1f);
            return true;
        }
        public override bool TryMovement(out Vector3 target, out float speed)
        {
            target = _state.Hold ? _state.Context.Hunter.Position : _state.Target;
            // Reposition is deliberate walking; the shared controller applies the
            // run multiplier once after this override (the replay ratio includes it).
            speed = _state.Hold ? 0f : _profile.InvestigateSpeed;
            return _state.Hold || _state.Action == WeaverAction.Reposition;
        }
        public bool TryTakeWeaverFact(out WeaverFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
        private void Doorways()
        {
            HunterArchetypeContext context = _state.Context;
            if (context.Level.Graph == null || context.Interactables == null) return;
            int room = HunterNavigationUtility.RoomAt(context.Level.Graph, context.Hunter.Position);
            foreach (LevelRoom levelRoom in context.Level.Graph.Rooms)
                foreach (InteractableState door in context.Interactables.InRoom(levelRoom.Id))
                {
                    if (door.Kind != InteractableKind.Door) continue;
                    bool seed = Stacks(DoorwayNests) > 0 && _state.SeededDoors.Add(door.Id) &&
                        _random.NextDouble() < Mathf.Clamp01(_config.NestChancePerStack * Stacks(DoorwayNests));
                    bool passed = room != 0 && room != _state.LastRoom && (door.RoomId == room || door.RoomId == _state.LastRoom) &&
                        PassedNear(door.Position) && _state.PassedDoors.Add(door.Id);
                    if ((!seed && !passed) || _state.Webs.Count >= _config.MaximumNests) continue;
                    _state.Serial++; _state.Webs[_state.Serial] = _config.NestLifetimeSeconds;
                    Emit(WeaverFactKind.DoorwayWebbed, door.Position, radius: _config.NestRadius, duration: _config.NestLifetimeSeconds, serial: _state.Serial);
                    Emit(WeaverFactKind.WebGlow, door.Position, radius: _config.NestRadius, duration: _config.NestLifetimeSeconds, serial: _state.Serial);
                }
            if (room != 0) _state.LastRoom = room;
        }
        private bool PassedNear(Vector3 doorway)
        {
            Vector3 leg = Vector3.ProjectOnPlane(_state.Context.Hunter.Position - _state.PreviousPosition, Vector3.up);
            Vector3 offset = Vector3.ProjectOnPlane(doorway - _state.PreviousPosition, Vector3.up);
            float along = leg.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(offset, leg) / leg.sqrMagnitude) : 0f;
            return (offset - leg * along).sqrMagnitude <= _config.NestRadius * _config.NestRadius;
        }
        private void Emit(WeaverFactKind kind, Vector3 position, Vector3 end = default, float radius = 0f, float duration = 0f, int serial = 0)
            => _state.Facts.Enqueue(new WeaverFact(_state.Context.Hunter.Id, kind, _state.Context.Tick, position, end, radius, duration, serial));
    }
}
