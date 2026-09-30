// ============================================================================
// HeraldController.cs
// ============================================================================
// PURPOSE:
//   Makes every Herald scream a sound plus a floor-wide player clue, with a
//   warned radius attack that deals low damage and temporarily removes hearing.
//   Shared Hunter rules still own sensing, pursuit, habits and loss decisions.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Hand out a read-only state view without exposing mutable runtime collections.
//   - Alternate fixed chase files with injected random pitch and spacing.
//   - Keep attack pitch fixed and publish deafen/Deaf Landing intent on radius hits.
// DEPENDENCIES:
//   - Own config/state, Core facts and Hunter-local independent attack interface.
// USAGE NOTES:
//   Tick captures context; ResolveAfterSensing runs once after shared Hunter.Tick,
//   so discovery/loss use this tick's sight. A warned scream commits even if sight
//   breaks; outranging it avoids the hit. Retreat, catch and death cancel warning.
//   Session routes facts to Director; Hunter never calls its upstream consumer.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter.Archetypes.Default;
using Worsen.Domain.Hunter.Archetypes.Blinder;
namespace Worsen.Domain.Hunter.Archetypes.Herald
{
    public sealed class HeraldController : DefaultHunterController, IHunterIndependentAttackRules
    {
        public static readonly EffectId LongerDeafness = new EffectId("herald-longer-deafness");
        public static readonly EffectId WiderScream = new EffectId("herald-wider-scream");
        public static readonly EffectId SharperEars = new EffectId("herald-sharper-ears");
        public static readonly EffectId RestlessThroat = new EffectId("herald-restless-throat");
        public static readonly EffectId DeafLanding = new EffectId("herald-deaf-landing");
        public const string DiscoveryClip = "ms_mangled_scream_03";
        public const string ChaseOneClip = "sb_mangled_scream_01";
        public const string ChaseTwoClip = "sb_mangled_scream_03";
        public const string AttackClip = "sb_mangled_scream_02";
        private readonly HeraldBehaviorState _state;
        private readonly HeraldConfig _config;
        private readonly System.Random _random;
        public HeraldController(HeraldBehaviorState state, HeraldConfig config, System.Random random)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public IReadOnlyHeraldState ReadOnlyState => _state;
        public bool AllowSharedAttack => false;
        public bool Hold => _state.Warning;
        public long LastTick => _state.LastTick;
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, _config.CurseStackCap);
        public float Radius => _config.Radius * Mathf.Pow(_config.WiderScreamMultiplier, Stacks(WiderScream));
        public float DeafenSeconds => _config.DeafenSeconds * Mathf.Pow(_config.LongerDeafnessMultiplier, Stacks(LongerDeafness));
        public float CadenceMultiplier => Mathf.Pow(_config.RestlessThroatMultiplier, Stacks(RestlessThroat));
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.LastTick = -1;
            _state.WasVisible = _state.Warning = _state.ChaseTwo = false;
            _state.WarningRemaining = _state.Cooldown = _state.ChaseRemaining = 0f;
            _state.Screams.Clear(); _state.Breaths.Clear(); _state.Hits.Clear();
        }
        public override void Tick(HunterArchetypeContext context) { _state.Context = context; }
        public void ResolveAfterSensing()
        {
            HunterArchetypeContext context = _state.Context;
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= LastTick) return;
            _state.LastTick = context.Tick;
            if (!context.Hunter.IsActive || !context.Player.IsAlive || !context.CanReplay ||
                (context.Hunter is IReadOnlyHunterPursuitState pursuit && pursuit.PursuitSuppressed))
            { SuspendAttack(); _state.WasVisible = false; return; }
            _state.Cooldown = Mathf.Max(0f, _state.Cooldown - context.DeltaTime);
            _state.ChaseRemaining = Mathf.Max(0f, _state.ChaseRemaining - context.DeltaTime);
            bool discovery = context.Hunter.PlayerVisible && !_state.WasVisible;
            _state.WasVisible = context.Hunter.PlayerVisible;
            if (_state.Warning)
            {
                _state.WarningRemaining = Mathf.Max(0f, _state.WarningRemaining - context.DeltaTime);
                if (_state.WarningRemaining > .000001f) return;
                Scream(HeraldSound.Attack, AttackClip);
                if (Vector3.Distance(context.Hunter.Position, context.Player.Position) <= Radius)
                    _state.Hits.Enqueue(new HeraldDeafenFact(context.Hunter.Id, context.Player.Id, context.Tick,
                        context.Hunter.Position, Radius, _config.Damage, DeafenSeconds, Stacks(DeafLanding) > 0));
                _state.Warning = false; _state.Cooldown = _config.AttackCooldown; return;
            }
            if (!context.Hunter.PlayerVisible) return;
            if (discovery) { Scream(HeraldSound.Discovery, DiscoveryClip); return; }
            if (_state.Cooldown <= 0f && Vector3.Distance(context.Hunter.Position, context.Player.Position) <= Radius)
            {
                _state.Warning = true; _state.WarningRemaining = _config.WarningSeconds;
                _state.Breaths.Enqueue(new HeraldBreathFact(context.Hunter.Id, context.Hunter.Position, context.Tick, _config.WarningSeconds)); return;
            }
            if (_state.ChaseRemaining <= 0f)
            {
                Scream(_state.ChaseTwo ? HeraldSound.ChaseTwo : HeraldSound.ChaseOne, _state.ChaseTwo ? ChaseTwoClip : ChaseOneClip);
                _state.ChaseTwo = !_state.ChaseTwo;
            }
        }
        private void Scream(HeraldSound sound, string clip)
        {
            HunterArchetypeContext c = _state.Context;
            bool exact = Stacks(SharperEars) > 0;
            float pitch = sound == HeraldSound.Attack ? 1f : 1f + ((float)_random.NextDouble() * 2f - 1f) * _config.PitchVariation;
            _state.Screams.Enqueue(new HeraldScreamFact(c.Hunter.Id, sound, clip, pitch,
                new NoiseEvent(c.Hunter.Id, c.Hunter.Position, _config.Loudness, c.Tick, NoiseSourceKind.Scream),
                new NoiseEvent(c.Hunter.Id, exact ? c.Player.Position : c.Hunter.LastKnownPosition, _config.Loudness, c.Tick, NoiseSourceKind.Scream),
                exact ? c.Tick : c.Hunter.LastKnownTick, exact));
            _state.ChaseRemaining = (_config.ChaseIntervalMin + (float)_random.NextDouble() *
                (_config.ChaseIntervalMax - _config.ChaseIntervalMin)) * CadenceMultiplier;
        }
        public void SuspendAttack() { _state.Warning = false; _state.WarningRemaining = 0f; }
        public override bool TryMovement(out Vector3 target, out float speed)
        { target = _state.Context.Hunter.Position; speed = 0f; return Hold; }
        public bool TakeScream(out HeraldScreamFact fact)
        { fact = default; if (_state.Screams.Count == 0) return false; fact = _state.Screams.Dequeue(); return true; }
        public bool TakeBreath(out HeraldBreathFact fact)
        { fact = default; if (_state.Breaths.Count == 0) return false; fact = _state.Breaths.Dequeue(); return true; }
        public bool TakeHit(out HeraldDeafenFact fact)
        { fact = default; if (_state.Hits.Count == 0) return false; fact = _state.Hits.Dequeue(); return true; }
    }
}
