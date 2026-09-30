// ============================================================================
// StareController.cs
// ============================================================================
// PURPOSE:
//   Runs an attention threat that inhabits the camera edge instead of stalking.
//   Pinning it with a continuous look removes it until its cadence returns;
//   missing the window starts shared pursuit with a harder time/distance loss.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Propose front-edge placements, pause to meet a held look and seed Find me.
//   - Publish spatial voice ids and presence facts, never engine/audio commands.
// DEPENDENCIES:
//   - Default Hunter seam, optional dormancy/observation rules and Core effects.
// USAGE NOTES:
//   Placement must be acknowledged after navigation/occlusion checks. No valid
//   placement means no cue/window. Find me stays put so searching remains possible.
//   Winning holds the shared catch and emits the configured death-sting identity.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Hunter.Archetypes.Ticking;
namespace Worsen.Domain.Hunter.Archetypes.Stare
{
    public sealed class StareController : DefaultHunterController, IHunterDormancyRules, IHunterObservationRules
    {
        private readonly StareConfig _config;
        private readonly System.Random _random;
        private readonly StareBehaviorState _state = new StareBehaviorState();
        public StareController(StareConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool Dormant => !_state.Hunting;
        public override bool OwnsPursuit => Dormant;
        public bool Hold => Dormant;
        public bool Silent => true; // Replaces generic feedback with the explicit sound set below.
        public float SpeedMultiplier => 1f;
        public float LossMultiplier => _state.Hunting ? _config.LossMultiplier : 1f;
        public bool Present => _state.Present && (_state.Hunting || (_state.PlacementValid && _state.Fresh));
        public bool Hidden => _state.Hidden;
        public bool NeedsPlacement => Dormant && _state.Fresh && (_state.Pending || (_state.Present && !_state.Hidden && !_state.Looking));
        public float WindowSeconds => _config.WindowSeconds * Mathf.Pow(_config.ShorterWindowMultiplier, Stacks("stare-shorter-window"));
        public float ReturnSeconds => _config.ReturnSeconds * Mathf.Pow(_config.SoonerReturnMultiplier, Stacks("stare-sooner-return"));
        public float CallVolume => Mathf.Pow(_config.QuieterCallMultiplier, Stacks("stare-quieter-call"));
        private int Stacks(string id) => Mathf.Clamp(_state.Context.Effects?.Stacks(new EffectId(id)) ?? 0, 0, 3);
        public void Observe(HunterPlayerView view, bool clear, bool illuminated, IReadOnlyHunterWorldView world, bool wick)
        { _state.View = view; _state.Clear = clear; }
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.Present = _state.Hunting = _state.Looking = _state.Fresh = false;
            _state.PlacementValid = false;
            _state.ReturnRemaining = _state.Elapsed = _state.Held = 0f; _state.LastTick = -1;
            _state.Facts.Clear(); BeginEncounter();
        }
        private void BeginEncounter()
        {
            _state.Pending = true; _state.HalfCalled = _state.ForceSight = false;
            _state.Hidden = _random.NextDouble() < _config.SubversionChance;
            _state.Side = _random.NextDouble() < .5 ? -1f : 1f;
            _state.Wander = (float)_random.NextDouble(); _state.Looking = false;
            _state.WanderRemaining = _config.WanderSeconds;
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.LastTick = context.Tick; _state.Context = context;
            _state.Fresh = HunterViewUtility.Fresh(_state.View, context.Tick);
            if (!context.Player.IsAlive) { _state.Present = _state.Pending = false; return; }
            if (_state.Hunting) { _state.Position = context.Hunter.Position; return; }
            if (!_state.Present)
            {
                if (!_state.Pending)
                {
                    _state.ReturnRemaining = Mathf.Max(0f, _state.ReturnRemaining - context.DeltaTime);
                    if (_state.ReturnRemaining <= .000001f) BeginEncounter();
                }
                return;
            }
            if (!_state.Fresh) { _state.Held = 0f; _state.Looking = false; return; }
            if (!_state.PlacementValid) { _state.Held = 0f; _state.Looking = false; return; }
            _state.Looking = _state.Clear && HunterViewUtility.Contains(_state.View,
                _state.Position + Vector3.up * _config.ObservationHeight, _config.LookHalfAngle);
            if (!_state.Looking && !_state.Hidden && Stacks("stare-wider-wander") > 0)
            {
                _state.WanderRemaining -= context.DeltaTime;
                if (_state.WanderRemaining <= 0f)
                { _state.WanderRemaining = _config.WanderSeconds; _state.Wander = (float)_random.NextDouble(); }
            }
            float available = Mathf.Min(context.DeltaTime, Mathf.Max(0f, _state.Window - _state.Elapsed));
            _state.Held = _state.Looking ? _state.Held + available : 0f;
            _state.Elapsed += available;
            if (_state.Held + .000001f >= _config.HoldSeconds)
            {
                _state.Present = false; _state.Pending = false; _state.ReturnRemaining = ReturnSeconds;
                Emit(StareFactKind.Disappeared); return;
            }
            if (!_state.HalfCalled && _state.Elapsed >= _state.Window * .5f)
            { _state.HalfCalled = true; Call(); }
            if (_state.Elapsed + .000001f >= _state.Window)
            {
                _state.Hunting = true; _state.ForceSight = true;
                Emit(StareFactKind.ChaseStarted); Sound(_config.ChaseId, HunterCueSlot.ChaseLayer);
            }
        }
        public Vector3 Candidate(int attempt = 0)
        {
            float wander = _config.WanderFractionPerStack * Stacks("stare-wider-wander");
            float fraction = Mathf.Clamp(_config.EdgeFraction + (_state.Wander * 2f - 1f) * wander, .5f, .97f);
            float angle = _state.Hidden ? _config.HiddenAngle : _state.View.HorizontalFov * .5f * fraction;
            float side = attempt % 2 == 0 ? _state.Side : -_state.Side;
            Vector3 ray = _state.View.Rotation * (Quaternion.Euler(0f, angle * side, 0f) * Vector3.forward);
            Vector3 point = _state.Context.Player.Position + ray * _config.Distance;
            point.y = _state.Context.Player.Position.y; return point;
        }
        public bool AcceptPlacement(Vector3 point, bool reachable, bool clear)
        {
            if (!NeedsPlacement || !reachable || !clear || !float.IsFinite(point.sqrMagnitude)) return false;
            bool visible = HunterViewUtility.Contains(_state.View, point + Vector3.up * _config.ObservationHeight);
            Vector3 local = Quaternion.Inverse(_state.View.Rotation) * (point + Vector3.up * _config.ObservationHeight - _state.View.Origin);
            if (_state.Hidden ? visible : (!visible || local.z <= 0f || Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) < _state.View.HorizontalFov * .25f)) return false;
            if (_state.Context.UnavailableRooms != null)
                foreach (Bounds room in _state.Context.UnavailableRooms) if (room.Contains(point)) return false;
            int roomId = HunterNavigationUtility.RoomAt(_state.Context.Level?.Graph, point);
            if (_state.Context.Floor != null && _state.Context.Floor.RoomPhases.TryGetValue(roomId, out RoomPhase phase) && phase == RoomPhase.Closed) return false;
            _state.Position = point; _state.PlacementValid = true;
            if (!_state.Present)
            {
                _state.Present = true; _state.Pending = false; _state.Elapsed = _state.Held = 0;
                _state.Window = Mathf.Max(_config.HoldSeconds, WindowSeconds);
                Emit(StareFactKind.Appeared); Call(true);
            }
            return true;
        }
        public void PlacementFailed() { _state.PlacementValid = false; _state.Held = 0f; _state.Looking = false; }
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context)
        { bool forced = _state.ForceSight; _state.ForceSight = false; return !Dormant && (visible || forced); }
        public override float GoalUtility(HunterGoal goal, float utility) => goal == HunterGoal.LocatePrey ? utility : 0f;
        public void AttackCue() { if (!Dormant) Sound(_config.AttackId, HunterCueSlot.AttackTiming); }
        public void CatchCue() { if (!Dormant) Sound(_config.DeathId, HunterCueSlot.DeathSting); }
        private void Call(bool initial = false) => Sound(_state.Hidden ? _config.HiddenCallId : _config.CallId,
            initial ? HunterCueSlot.Presence : HunterCueSlot.Detection, CallVolume);
        private void Sound(string id, HunterCueSlot slot, float volume = 1f) =>
            _state.Facts.Enqueue(new StareFact(_state.Context.Hunter.Id, StareFactKind.Sound, _state.Position, _state.Context.Tick, id, volume, slot));
        private void Emit(StareFactKind kind) => _state.Facts.Enqueue(new StareFact(_state.Context.Hunter.Id, kind, _state.Position, _state.Context.Tick));
        public bool TakeFact(out StareFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
    }
}
