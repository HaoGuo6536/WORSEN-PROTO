// ============================================================================
// MimicController.cs
// ============================================================================
// PURPOSE:
//   Poses as a false uncollected cake, then springs one damage-bearing bite on touch.
//   The ordinary white-arrow candidate rule is never weakened by disguise or rarity.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter Mimic.
// KEY RESPONSIBILITIES:
//   - Publish pose/hold/cue facts and return damage through the normal HunterHit path.
//   - Enable Faithless Arrow windows only while its ordinary catalogue curse is held.
// DEPENDENCIES:
//   - Own state/config, parent neutral/dormancy seam and injected Core world/effect views.
// USAGE NOTES:
//   Explicit time and seeded randomness only. Floor owns fake-cake rendering and
//   exclusion; Session pairs BiteStarted with accepted damage before Player holds.
//   More Mimics is an absolute extra-count fact, not recursive per-entity spawning.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter.Archetypes.Mimic
{
    public sealed class MimicController : HunterArchetypeController, IHunterDormancyRules
    {
        public static readonly EffectId MoreMimics = new EffectId("mimic-more-mimics");
        public static readonly EffectId GoldenMimic = new EffectId("mimic-golden-mimic");
        public static readonly EffectId FaithlessArrow = new EffectId("mimic-faithless-arrow");
        public static readonly EffectId LongerBite = new EffectId("mimic-longer-bite");
        private readonly MimicBehaviorState _state = new MimicBehaviorState();
        private readonly MimicConfig _config;
        private readonly System.Random _random;
        public MimicController(MimicConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _random = random ?? throw new ArgumentNullException(nameof(random)); }
        public bool Dormant => true;
        public override bool OwnsPursuit => true;
        public bool Posed => _state.Posed;
        public bool Holding => _state.Hold > 0f;
        public float TouchRadius => _config.TouchRadius;
        public float BiteSeconds => _config.BiteSeconds * Mathf.Pow(_config.LongerBiteMultiplier, Stacks(LongerBite));
        public bool FaithlessEnabled => Stacks(FaithlessArrow) > 0;
        private int Stacks(EffectId id) => Mathf.Clamp(_state.Context.Effects?.Stacks(id) ?? 0, 0, 3);
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Context = context; _state.Position = context.Hunter.Position; _state.LastTick = -1;
            _state.Hold = _state.FaithlessElapsed = 0f; _state.Posed = _state.Spent = _state.Golden = _state.PopulationPublished = false;
            _state.ExtraCount = 0; _state.GoldenRoll = (float)_random.NextDouble(); _state.Facts.Clear();
        }
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.Context = context; _state.LastTick = context.Tick;
            if (!context.Hunter.IsActive || !context.Player.IsAlive) { Teardown(); return; }
            if (Holding)
            { _state.Hold = Mathf.Max(0f, _state.Hold - context.DeltaTime); if (!Holding) Emit(MimicFactKind.BiteEnded); }
            if (_state.Spent) return;
            bool golden = Stacks(GoldenMimic) > 0 && _state.GoldenRoll < _config.GoldenChance;
            if (!_state.Posed || golden != _state.Golden)
            { _state.Golden = golden; _state.Posed = true; Emit(MimicFactKind.Pose); }
            int count = Stacks(MoreMimics);
            if (!_state.PopulationPublished || count != _state.ExtraCount)
            { _state.ExtraCount = count; _state.PopulationPublished = true; Emit(MimicFactKind.Population); }
            if (!FaithlessEnabled) { _state.FaithlessElapsed = 0f; return; }
            _state.FaithlessElapsed += context.DeltaTime;
            if (_state.FaithlessElapsed >= _config.FaithlessInterval)
            { _state.FaithlessElapsed = 0f; Emit(MimicFactKind.FaithlessWindow, _config.FaithlessSeconds); }
        }
        public bool Touch(EntityId player, out HunterHit hit)
        {
            hit = default;
            if (!Posed || _state.Spent || !_state.Context.CanReplay || !_state.Context.Hunter.IsActive ||
                !_state.Context.Player.IsAlive || player != _state.Context.Player.Id) return false;
            _state.Spent = true; _state.Posed = false; _state.Hold = BiteSeconds;
            Emit(MimicFactKind.PoseRemoved); Emit(MimicFactKind.BiteStarted, _state.Hold, _config.BiteSound);
            hit = new HunterHit(_state.Context.Hunter.Id, player, _config.BiteDamage, _state.Context.Tick,
                _state.Position, severity: HitSeverity.Light, source: HitSource.Trap);
            return true;
        }
        public override bool TryMovement(out Vector3 target, out float speed)
        { target = _state.Position; speed = 0f; return true; }
        public void Won() { Emit(MimicFactKind.Won, sound: _config.WinSound); }
        public void Teardown()
        {
            if (Posed) Emit(MimicFactKind.PoseRemoved);
            if (Holding) Emit(MimicFactKind.BiteEnded);
            _state.Posed = false; _state.Hold = 0f; _state.Spent = true;
        }
        public bool TakeFact(out MimicFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
        private void Emit(MimicFactKind kind, float seconds = 0f, string sound = "") => _state.Facts.Enqueue(new MimicFact(
            _state.Context.Hunter.Id, _state.Context.Player.Id, kind, _state.Context.Tick, _state.Position,
            seconds, _state.Golden, _state.ExtraCount, sound));
    }
}
