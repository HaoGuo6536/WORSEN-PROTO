// ============================================================================
// BlinderController.cs
// ============================================================================
// PURPOSE:
//   Implements the Blinder's independent Reposition-before-throw rule using the
//   Weaver's radius-matched sweep observations, not visibility rays. It publishes
//   blindness and Floor-owned trap policy without creating another trap system.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Hand out a read-only state view without exposing mutable runtime collections.
//   - Require a fresh sweep before warning, throughout warning and at launch.
//   - Admit each live projectile contact once and publish neutral curse hooks.
//   - Describe presence, discovery, chase, hiss, floor ticks and the accepted catch.
// DEPENDENCIES:
//   - Own config/state, shared Hunter pursuit and Weaver sweep value definitions.
// USAGE NOTES:
//   Explicit tick/delta time only; this rule has no random decisions. The shared
//   pursuit receives injected randomness. Floor reports real trap ticks; silence
//   never suppresses the throw hiss. Mirror Skin belongs to the effect receiver.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public sealed class BlinderController : DefaultHunterController, IHunterIndependentAttackRules
    {
        public static readonly EffectId MoreTraps = new EffectId("blinder-more-traps");
        public static readonly EffectId LongerDark = new EffectId("blinder-longer-dark");
        public static readonly EffectId MuffledDark = new EffectId("blinder-muffled-dark");
        public static readonly EffectId SilentTraps = new EffectId("blinder-silent-traps");
        private readonly BlinderBehaviorState _state;
        private readonly BlinderConfig _config;
        private readonly HunterProfile _profile;
        public BlinderController(BlinderBehaviorState state, BlinderConfig config, HunterProfile profile)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile)); }
        public IReadOnlyBlinderState ReadOnlyState => _state;
        public BlinderAction Action => _state.Action;
        public bool Warning => _state.Warning;
        public bool Fire => _state.Fire;
        public bool Hold => _state.Warning || _state.Fire;
        public bool AllowSharedAttack => !Hold && Action != BlinderAction.Reposition;
        public long LastTick => _state.LastTick;
        public int Serial => _state.Serial;
        public Vector3 Origin => _state.Origin;
        public Vector3 Target => _state.Aim;
        public float Radius => _config.ProjectileRadius;
        public Vector3 Aim(float height) => Warning ? Target : _state.Context.Hunter.LastKnownPosition + Vector3.up * height;
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Effects?.Stacks(id) ?? 0, 0, _config.CurseStackCap);
        public void SetEffects(IReadOnlyActiveEffects effects) { _state.Effects = effects; }
        public BlinderTrapPolicyFact TrapPolicy => new BlinderTrapPolicyFact(_state.Context.Hunter.Id, _state.Context.Tick,
            Stacks(MoreTraps), Stacks(SilentTraps) > 0, _config.BlindSeconds * Mathf.Pow(_config.LongerDarkMultiplier, Stacks(LongerDark)), Stacks(MuffledDark) > 0);
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.LastTick = -1; _state.Observation = default;
            _state.Effects = context.Effects;
            _state.Action = BlinderAction.None; _state.Warning = _state.Fire = _state.WasVisible = _state.CatchPublished = _state.PolicyPublished = false;
            _state.WarningRemaining = _state.Cooldown = _state.SoundRemaining = 0f; _state.Serial = 0;
            _state.Origin = _state.Aim = _state.Target = default;
            _state.Projectiles.Clear(); _state.Sounds.Clear(); _state.Policies.Clear(); _state.Throws.Clear();
        }
        public void Observe(WeaverObservation observation) { _state.Observation = observation; }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick; _state.Fire = false; _state.Action = BlinderAction.None;
            _state.Effects = context.Effects;
            foreach (int serial in new List<int>(_state.Projectiles.Keys))
            { _state.Projectiles[serial] -= context.DeltaTime; if (_state.Projectiles[serial] <= 0f) _state.Projectiles.Remove(serial); }
            PublishPolicy();
            _state.Cooldown = Mathf.Max(0f, _state.Cooldown - context.DeltaTime);
            if (!context.Hunter.IsActive || !context.Player.IsAlive || !context.CanReplay ||
                (context.Hunter is IReadOnlyHunterPursuitState pursuit && pursuit.PursuitSuppressed))
            { SuspendAttack(); return; }
            if (context.Hunter.PlayerVisible && !_state.WasVisible) Sound(BlinderSound.Detection, context.Hunter.Position);
            _state.WasVisible = context.Hunter.PlayerVisible;
            _state.SoundRemaining -= context.DeltaTime;
            if (_state.SoundRemaining <= 0f)
            {
                Sound(context.Hunter.PlayerVisible ? BlinderSound.Chase : BlinderSound.Presence, context.Hunter.Position);
                _state.SoundRemaining = context.Hunter.PlayerVisible ? _config.ChaseInterval : _config.PresenceInterval;
            }
            if (Warning)
            {
                if (!Verified() || _state.Observation.Origin != Origin || _state.Observation.Target != Target)
                { SuspendAttack(); return; }
                _state.Action = BlinderAction.Throw;
                _state.WarningRemaining = Mathf.Max(0f, _state.WarningRemaining - context.DeltaTime);
                _state.Fire = _state.WarningRemaining <= .000001f;
                return;
            }
            if (_state.Cooldown > 0f || context.Hunter.BeliefConfidence <= 0f ||
                (context.Hunter.PlayerVisible && Vector3.Distance(context.Hunter.Position, context.Player.Position) <= _profile.LungeDistance)) return;
            if (Verified())
            {
                _state.Warning = true; _state.Action = BlinderAction.Throw;
                _state.Origin = _state.Observation.Origin; _state.Aim = _state.Observation.Target;
                _state.WarningRemaining = _config.WarningSeconds;
                Sound(BlinderSound.ThrowHiss, Origin, _config.WarningSeconds);
            }
            else if (_state.Observation.Tick == context.Tick && _state.Observation.Radius == Radius && _state.Observation.Spots != null)
            {
                float nearest = float.PositiveInfinity;
                foreach (WeaverShotSpot spot in _state.Observation.Spots)
                {
                    float distance = (spot.Position - context.Hunter.Position).sqrMagnitude;
                    if (!spot.Reachable || !spot.Clear || !(distance < nearest)) continue;
                    nearest = distance; _state.Target = spot.Position; _state.Action = BlinderAction.Reposition;
                }
            }
        }
        private bool Verified() => _state.Observation.Tick == _state.Context.Tick && _state.Observation.Clear && _state.Observation.Grounded &&
            _state.Observation.Radius == Radius && Vector3.Distance(_state.Observation.Origin, _state.Observation.Target) > 0f &&
            Vector3.Distance(_state.Observation.Origin, _state.Observation.Target) <= _config.Range;
        public void SuspendAttack() { _state.Warning = _state.Fire = false; _state.Action = BlinderAction.None; }
        public void CommitLaunch(bool launched)
        {
            if (!Fire) return;
            if (launched)
            {
                _state.Serial++; _state.Projectiles[_state.Serial] = _config.Range / _config.ProjectileSpeed;
                _state.Throws.Enqueue(new BlinderThrowFact(_state.Context.Hunter.Id, _state.Context.Tick, Serial, Origin, Target,
                    Radius, _config.ProjectileSpeed, _config.Range));
            }
            SuspendAttack(); _state.Cooldown = _config.CooldownSeconds;
        }
        public bool TryHit(EntityId player, int serial, long tick, out BlinderHitFact hit)
        {
            hit = default;
            if (!_state.Context.Hunter.IsActive || !_state.Context.Player.IsAlive || player != _state.Context.Player.Id ||
                tick < LastTick || !_state.Projectiles.Remove(serial)) return false;
            BlinderTrapPolicyFact policy = TrapPolicy;
            hit = new BlinderHitFact(_state.Context.Hunter.Id, player, tick, serial, policy.Duration, policy.MuffledDark); return true;
        }
        public override bool TryMovement(out Vector3 target, out float speed)
        { target = Hold ? _state.Context.Hunter.Position : _state.Target; speed = Hold ? 0f : _profile.InvestigateSpeed;
            return Hold || Action == BlinderAction.Reposition; }
        private void PublishPolicy()
        {
            BlinderTrapPolicyFact next = TrapPolicy, old = _state.Policy;
            if (_state.PolicyPublished && old.MoreTrapsStacks == next.MoreTrapsStacks && old.SilentTraps == next.SilentTraps &&
                old.Duration == next.Duration && old.MuffledDark == next.MuffledDark) return;
            _state.Policy = next; _state.PolicyPublished = true; _state.Policies.Enqueue(next);
        }
        public void ReportTrapTick(Vector3 position, long tick)
        {
            if (tick != _state.Context.Tick || !_state.Context.Hunter.IsActive || TrapPolicy.SilentTraps) return;
            Sound(BlinderSound.TrapTick, position);
        }
        public void BeginCatch()
        {
            if (_state.CatchPublished) return;
            _state.CatchPublished = true; SuspendAttack(); Sound(BlinderSound.Catch, _state.Context.Hunter.Position);
        }
        private void Sound(BlinderSound sound, Vector3 position, float duration = 0f)
        {
            float loudness = sound == BlinderSound.TrapTick ? _config.TrapTickLoudness : _config.SoundLoudness;
            _state.Sounds.Enqueue(new BlinderSoundFact(_state.Context.Hunter.Id, sound, position, _state.Context.Tick, duration,
                new NoiseEvent(_state.Context.Hunter.Id, position, loudness, _state.Context.Tick,
                    sound == BlinderSound.TrapTick ? NoiseSourceKind.Trap : NoiseSourceKind.Other)));
        }
        public bool TakeSound(out BlinderSoundFact fact)
        { fact = default; if (_state.Sounds.Count == 0) return false; fact = _state.Sounds.Dequeue(); return true; }
        public bool TakeTrapPolicy(out BlinderTrapPolicyFact fact)
        { fact = default; if (_state.Policies.Count == 0) return false; fact = _state.Policies.Dequeue(); return true; }
        public bool TakeThrow(out BlinderThrowFact fact)
        { fact = default; if (_state.Throws.Count == 0) return false; fact = _state.Throws.Dequeue(); return true; }
    }
}
