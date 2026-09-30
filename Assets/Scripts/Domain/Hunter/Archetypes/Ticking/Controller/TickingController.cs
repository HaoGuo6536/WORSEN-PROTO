// ============================================================================
// TickingController.cs
// ============================================================================
// PURPOSE:
//   Runs a moving maintenance threat whose spring gates the shared Hunter chase.
//   It proposes nearby keys, accepts only proven placements and winds on identity-
//   checked contact. All time, randomness and navigation evidence are injected.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Slow the tick tell, wake at zero and suppress attacks while wound.
//   - Keep one timed key, publish ThreatArrow guidance and catalogue curse facts.
// DEPENDENCIES:
//   - Default Hunter seam, injected Player/Level/Floor views, Core effects and facts.
// USAGE NOTES:
//   Runs Faster and Farther Keys cap at three; binary effects cap at one, matching
//   the catalogue. Double Spring remembers the first half-wind through decay.
//   Hunting remains enabled after shared sight loss, until a key winds the spring.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public sealed class TickingController : DefaultHunterController, IHunterDormancyRules
    {
        public static readonly EffectId RunsFaster = new EffectId("ticking-runs-faster");
        public static readonly EffectId FartherKeys = new EffectId("ticking-farther-keys");
        public static readonly EffectId LoudKeys = new EffectId("ticking-loud-keys");
        public static readonly EffectId DoubleSpring = new EffectId("ticking-double-spring");
        public static readonly EffectId SpareKey = new EffectId("spare-key");
        private readonly TickingBehaviorState _state = new TickingBehaviorState();
        private readonly TickingConfig _config;
        private readonly System.Random _random;
        public TickingController(TickingConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool Dormant => _state.Charge > 0;
        public override bool OwnsPursuit => Dormant;
        public float SpringFraction => (float)_state.Charge;
        public float EffectiveSpringSeconds => _config.SpringSeconds * Mathf.Pow(_config.RunsFasterMultiplier, Stacks(RunsFaster, 3));
        public Vector2 EffectiveKeyDistance => _config.KeyDistance * Mathf.Pow(_config.FartherKeysMultiplier, Stacks(FartherKeys, 3)) *
            (Stacks(SpareKey, 1) > 0 ? _config.SpareKeyMultiplier : 1f);
        public float TickInterval => Mathf.Lerp(_config.EmptyTickInterval, _config.FullTickInterval, SpringFraction);
        public bool HasKey => _state.HasKey;
        public int KeySerial => _state.KeySerial;
        public Vector3 KeyPosition => _state.KeyPosition;
        public bool KeyDue => !HasKey && _state.Now >= _state.NextKeyAt && _state.Context.Player.IsAlive;
        private int Stacks(EffectId id, int cap) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, cap);
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.Now = 0; _state.LastTick = -1; _state.Charge = 1;
            _state.NextTickAt = _config.FullTickInterval; _state.NextKeyAt = _config.KeySeconds;
            _state.HalfWound = false; _state.HasKey = false; _state.KeySerial = 0;
            _state.FollowProbed = false; _state.HasFollowTarget = false;
            _state.Sounds.Clear(); _state.Noises.Clear();
            _state.FollowAngle = ((float)_random.NextDouble() * 2f - 1f) * _config.BehindArcDegrees;
            _state.FollowRadius = Mathf.Lerp(_config.FollowDistance.x, _config.FollowDistance.y, (float)_random.NextDouble());
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick;
            if (!context.Player.IsAlive) { RemoveKey(); _state.Sounds.Clear(); _state.Noises.Clear(); return; }
            double until = _state.Now + context.DeltaTime;
            if (Dormant)
            {
                double emptyAt = _state.Now + _state.Charge * EffectiveSpringSeconds;
                while (_state.NextTickAt < emptyAt && _state.NextTickAt <= until)
                {
                    float fraction = (float)Math.Max(0, _state.Charge - (_state.NextTickAt - _state.Now) / EffectiveSpringSeconds);
                    float interval = Mathf.Lerp(_config.EmptyTickInterval, _config.FullTickInterval, fraction);
                    Emit(TickingSound.Tick, context.Hunter.Position, interval);
                    _state.NextTickAt += interval;
                }
                _state.Charge = Math.Max(0, _state.Charge - context.DeltaTime / EffectiveSpringSeconds);
                if (until >= emptyAt || _state.Charge < .00000001)
                { _state.Charge = 0; Emit(TickingSound.Stop, context.Hunter.Position); Emit(TickingSound.Wake, context.Hunter.Position); }
            }
            _state.Now = until;
            if (HasKey && !Available(_state.KeyPosition, _state.KeyPosition)) RemoveKey();
        }
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context) => !Dormant && visible;
        public override float GoalUtility(HunterGoal goal, float utility) => goal == HunterGoal.LocatePrey ? utility : 0f;
        public override bool TryMovement(out Vector3 target, out float speed)
        {
            var player = _state.Context.Player;
            Vector3 behind = Quaternion.Euler(0, player.HeadingDegrees + _state.FollowAngle, 0) * Vector3.back;
            target = player.Position + behind * _state.FollowRadius;
            if (_state.FollowProbed)
            {
                target = _state.HasFollowTarget ? _state.FollowTarget : _state.Context.Hunter.Position;
                if (!_state.HasFollowTarget) { speed = 0f; return Dormant; }
            }
            Vector3 offset = _state.Context.Hunter.Position - player.Position; offset.y = 0;
            bool inBand = offset.magnitude >= _config.FollowDistance.x && offset.magnitude <= _config.FollowDistance.y &&
                Vector3.Dot(offset.normalized, behind) >= Mathf.Cos(_config.BehindArcDegrees * Mathf.Deg2Rad);
            // Hold the rear pocket, rather than continuously circling a stationary player.
            speed = inBand ? player.Velocity.magnitude : player.SprintSpeed * _config.FollowSpeedRatio;
            return Dormant;
        }
        public Vector3 FollowCandidate(int attempt)
        {
            float angle = attempt % 3 == 0 ? _state.FollowAngle : attempt % 3 == 1 ? -_state.FollowAngle : 0f;
            Vector2 range = _config.FollowDistance;
            float radius = attempt / 3 % 3 == 0 ? _state.FollowRadius : attempt / 3 % 3 == 1 ? range.x : range.y;
            return _state.Context.Player.Position + Quaternion.Euler(0, _state.Context.Player.HeadingDegrees + angle, 0) * Vector3.back * radius;
        }
        public bool SetFollowTarget(Vector3 position, bool reachable)
        {
            _state.FollowProbed = true;
            Vector3 offset = position - _state.Context.Player.Position;
            Vector3 forward = Quaternion.Euler(0, _state.Context.Player.HeadingDegrees, 0) * Vector3.forward;
            _state.HasFollowTarget = reachable && offset.magnitude >= _config.FollowDistance.x && offset.magnitude <= _config.FollowDistance.y &&
                Vector3.Dot(offset, forward) < 0 && Available(position, position);
            _state.FollowTarget = position; return _state.HasFollowTarget;
        }
        public Vector3 KeyCandidate()
        {
            Vector2 range = EffectiveKeyDistance;
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
            return _state.Context.Player.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * distance;
        }
        public bool PlaceKey(Vector3 position, bool reachable)
        {
            Vector2 range = EffectiveKeyDistance;
            float distance = Vector3.Distance(position, _state.Context.Player.Position);
            if (!KeyDue || !reachable || float.IsNaN(distance) || distance < range.x || distance > range.y ||
                !Available(_state.Context.Player.Position, position)) return false;
            _state.HasKey = true; _state.KeyPosition = position; _state.KeySerial++;
            Emit(TickingSound.KeyAppeared, position); return true;
        }
        public void DeferPlacement() { if (!HasKey) _state.NextKeyAt = _state.Now + _config.PlacementRetrySeconds; }
        public void RemoveKey()
        { _state.HasKey = false; _state.NextKeyAt = _state.Now + _config.PlacementRetrySeconds; }
        public bool TakeKey(Worsen.Core.EntityId collector, int serial)
        {
            if (!HasKey || serial != KeySerial || collector != _state.Context.Player.Id || !_state.Context.Player.IsAlive) return false;
            Vector3 position = _state.KeyPosition;
            bool doubleSpring = Stacks(DoubleSpring, 1) > 0;
            _state.Charge = doubleSpring && !_state.HalfWound ? Math.Max(_state.Charge, .5) : 1;
            _state.HalfWound = doubleSpring && !_state.HalfWound;
            _state.HasKey = false; _state.NextKeyAt = _state.Now + _config.KeySeconds;
            _state.NextTickAt = _state.Now + TickInterval;
            Emit(TickingSound.Winding, position);
            if (Stacks(LoudKeys, 1) > 0) _state.Noises.Enqueue(new NoiseEvent(collector, position, _config.LoudKeyLoudness,
                _state.Context.Tick, NoiseSourceKind.Other));
            return true;
        }
        public GuidanceTarget Guidance => new GuidanceTarget(GuidanceKind.ThreatArrow,
            HasKey ? (_state.KeyPosition - _state.Context.Player.Position).normalized : Vector3.zero,
            _state.KeyPosition, entityId: _state.Context.Hunter.Id, isFallback: true);
        private bool Available(Vector3 start, Vector3 end)
        {
            if (_state.Context.UnavailableRooms != null)
                foreach (Bounds bounds in _state.Context.UnavailableRooms) if (Intersects(bounds, start, end)) return false;
            if (_state.Context.Floor != null && _state.Context.Level?.Graph != null)
                foreach (LevelRoom room in _state.Context.Level.Graph.Rooms)
                    if (_state.Context.Floor.RoomPhases.TryGetValue(room.Id, out RoomPhase phase) && phase == RoomPhase.Closed &&
                        Intersects(room.Bounds, start, end)) return false;
            return true;
        }
        private static bool Intersects(Bounds bounds, Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            return bounds.Contains(start) || bounds.Contains(end) || (delta.sqrMagnitude > 0 &&
                bounds.IntersectRay(new Ray(start, delta.normalized), out float distance) && distance <= delta.magnitude);
        }
        private void Emit(TickingSound sound, Vector3 position, float interval = 0)
            => _state.Sounds.Enqueue(new TickingSoundFact(sound, position, interval, _state.Context.Tick));
        public bool TakeSound(out TickingSoundFact fact)
        { fact = default; if (_state.Sounds.Count == 0) return false; fact = _state.Sounds.Dequeue(); return true; }
        public bool TakeNoise(out NoiseEvent noise)
        { noise = default; if (_state.Noises.Count == 0) return false; noise = _state.Noises.Dequeue(); return true; }
    }
}
