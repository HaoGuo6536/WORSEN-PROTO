// ============================================================================
// RamController.cs
// ============================================================================
// PURPOSE:
//   Adds a stamped warning, locked straight charge and wall-punished stagger.
//   Shared sensing and approach remain intact; only the attack is replaced.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter Ram.
// KEY RESPONSIBILITIES:
//   - Lock heading before warning, account swept motion and emit breakable impacts.
//   - Apply capped neutral curse hooks and admit one normal HunterHit per charge.
// DEPENDENCIES:
//   - Own state/config, parent Hunter neutral rules, HunterProfile and injected Core views.
// USAGE NOTES:
//   Time is injected; this deterministic rule consumes no random numbers.
//   Wall hits always stagger, including Partition Breaker. Second Charge winds up
//   again once after that stagger, or immediately after unobstructed travel ends.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Ram
{
    public sealed class RamController : HunterArchetypeController, IHunterAttackRules
    {
        public static readonly EffectId LongerCharge = new EffectId("ram-longer-charge");
        public static readonly EffectId ShorterWindup = new EffectId("ram-shorter-windup");
        public static readonly EffectId PartitionBreaker = new EffectId("ram-partition-breaker");
        public static readonly EffectId SecondCharge = new EffectId("ram-second-charge");
        private readonly RamBehaviorState _state = new RamBehaviorState();
        private readonly RamConfig _config;
        private readonly HunterProfile _profile;
        public RamController(RamConfig config, HunterProfile profile)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _profile = profile ?? throw new ArgumentNullException(nameof(profile)); }
        public bool UsesSharedAttacks => false;
        public RamPhase Phase => _state.Phase;
        public override bool OwnsPursuit => Phase != RamPhase.Ready;
        public Vector3 Direction => _state.Direction;
        public Vector3 Motion => _state.Motion;
        public float WindupSeconds => _config.WindupSeconds * Mathf.Pow(_config.ShorterWindupMultiplier, Stacks(ShorterWindup));
        public float ChargeDistance => _config.ChargeDistance * Mathf.Pow(_config.LongerChargeMultiplier, Stacks(LongerCharge));
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, 3);
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.Phase = RamPhase.Ready; _state.LastTick = -1;
            _state.Motion = _state.Direction = Vector3.zero; _state.RemainingSeconds = _state.RemainingMeters = _state.StrideMeters = 0f;
            _state.Hit = _state.Second = _state.PendingMotion = false; _state.Facts.Clear();
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick; _state.Motion = Vector3.zero; _state.PendingMotion = false;
            if (!context.Hunter.IsActive || !context.Player.IsAlive || !context.CanReplay ||
                (context.Hunter is IReadOnlyHunterPursuitState pursuit && pursuit.PursuitSuppressed))
            { _state.Phase = RamPhase.Ready; _state.Second = false; return; }
            if (Phase == RamPhase.Ready)
            {
                if (context.Hunter.PlayerVisible && Vector3.Distance(context.Hunter.Position, context.Player.Position) <= ChargeDistance &&
                    Mathf.Abs(context.Hunter.Position.y - context.Player.Position.y) <= _profile.MaximumMeleeElevation)
                { _state.Second = false; BeginWindup(); }
                return;
            }
            if (Phase == RamPhase.Windup || Phase == RamPhase.Stagger)
            {
                _state.RemainingSeconds = Mathf.Max(0f, _state.RemainingSeconds - context.DeltaTime);
                if (_state.RemainingSeconds > .000001f) return;
                if (Phase == RamPhase.Stagger)
                { if (!TrySecond()) _state.Phase = RamPhase.Ready; return; }
                _state.Phase = RamPhase.Charge; _state.RemainingMeters = ChargeDistance;
                _state.Hit = false; _state.StrideMeters = 0f; Emit(RamFactKind.Bellow, _config.BellowSound);
                // Do not move on the same tick as the bellow; never spend warning time as travel.
                return;
            }
            _state.Motion = Direction * Mathf.Min(_state.RemainingMeters, _config.ChargeSpeed * context.DeltaTime);
            _state.PendingMotion = true;
        }
        private void BeginWindup(Vector3? origin = null)
        {
            Vector3 delta = _state.Context.Hunter.LastKnownPosition - (origin ?? _state.Context.Hunter.Position); delta.y = 0f;
            _state.Direction = delta.sqrMagnitude > .000001f ? delta.normalized : _state.Context.Hunter.Forward;
            _state.Phase = RamPhase.Windup; _state.RemainingSeconds = WindupSeconds;
            Emit(RamFactKind.Stamp, _config.StampSound);
        }
        private bool TrySecond(Vector3? origin = null)
        {
            if (_state.Second || Stacks(SecondCharge) == 0 || !_state.Context.Hunter.PlayerVisible) return false;
            _state.Second = true; BeginWindup(origin); return true;
        }
        public void CommitMotion(Vector3 position, bool blocked, int breakableId = -1)
        {
            if (Phase != RamPhase.Charge || !_state.PendingMotion) return;
            _state.PendingMotion = false;
            float traveled = Mathf.Clamp(Vector3.Dot(position - _state.Context.Hunter.Position, Direction), 0f, Motion.magnitude);
            _state.RemainingMeters -= traveled; _state.StrideMeters += traveled;
            while (_state.StrideMeters >= _config.StrideMeters)
            { _state.StrideMeters -= _config.StrideMeters; Emit(RamFactKind.Stride, _config.StrideSound, position: position); }
            if (blocked)
            {
                if (breakableId > 0 && (_config.PartitionBreakerVariant || Stacks(PartitionBreaker) > 0))
                    Emit(RamFactKind.PartitionImpact, string.Empty, breakableId, position);
                _state.Phase = RamPhase.Stagger; _state.RemainingSeconds = _config.StaggerSeconds;
                Emit(RamFactKind.WallStagger, _config.ImpactSound, position: position);
            }
            else if (_state.RemainingMeters <= .000001f && !TrySecond(position))
            { _state.Phase = RamPhase.Stagger; _state.RemainingSeconds = _config.StaggerSeconds; }
            _state.Motion = Vector3.zero;
        }
        public bool TryHit(EntityId target, out HunterHit hit)
            => TryHit(target, Direction, out hit);
        public bool TryHit(EntityId target, Vector3 contactNormal, out HunterHit hit)
        {
            hit = default;
            if (Phase != RamPhase.Charge || _state.Hit || !_state.Context.CanReplay ||
                !_state.Context.Player.IsAlive || target != _state.Context.Player.Id) return false;
            _state.Hit = true;
            Vector3 normal = Vector3.ProjectOnPlane(contactNormal, Vector3.up).normalized;
            bool glancing = normal.sqrMagnitude > 0f && Mathf.Abs(Vector3.Dot(Direction, normal)) < _config.GlancingDotThreshold;
            hit = new HunterHit(_state.Context.Hunter.Id, target, _profile.LungeDamage, _state.Context.Tick,
                _state.Context.Hunter.Position, contactNormal: contactNormal, ram: true, glancing: glancing,
                knockback: (glancing ? normal : Direction) * _config.KnockbackSpeed);
            return true;
        }
        public override bool TryMovement(out Vector3 target, out float speed)
        { target = _state.Context.Hunter.Position; speed = 0f; return OwnsPursuit; }
        public void Won() { Emit(RamFactKind.Won, _config.WinSound); }
        public bool TakeFact(out RamFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
        private void Emit(RamFactKind kind, string sound, int objectId = -1, Vector3? position = null)
            => _state.Facts.Enqueue(new RamFact(_state.Context.Hunter.Id, kind, _state.Context.Tick,
                position ?? _state.Context.Hunter.Position, Direction, sound, objectId));
    }
}
