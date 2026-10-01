// ============================================================================
// MannequinController.cs
// ============================================================================
// PURPOSE:
//   Gates shared movement and attacks on the player's camera observation, not light.
//   An unseen Mannequin pursues the player in lit and dark rooms alike; a clear
//   view or the Wick shrine freezes it. Ordinary feedback remains silent, with
//   the accepted snap/crunch catch published separately by its Manager.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Freeze while observed or under Wick; fail closed on missing camera evidence.
//   - Pursue unseen prey without shared flashlight, retreat or Stalk reveal rules.
//   - Apply capped peripheral-creep/longer-strides curses and publish silence facts.
//   - Admit the distinct catch fact once per life for the Manager to publish.
// DEPENDENCIES:
//   - Parent Hunter neutral rules, injected camera/occlusion, Core effects and facts.
// USAGE NOTES:
//   Time and camera/occlusion evidence are injected. Room lighting is irrelevant.
//   Legacy afterglow entry points are inert until the coordinator retires their
//   Session callers. Shared navigation, collision and attack timing remain intact.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Hunter.Archetypes.Mannequin
{
    public sealed class MannequinController : HunterArchetypeController, IHunterObservationRules
    {
        private readonly MannequinConfig _config;
        private readonly MannequinBehaviorState _state = new MannequinBehaviorState();
        public MannequinController(MannequinConfig config, System.Random random)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); if (random == null) throw new ArgumentNullException(nameof(random)); }
        public override bool OwnsPursuit => true;
        public bool Hold => _state.Hold || _state.Wick;
        public bool Silent => true;
        public float SpeedMultiplier => _state.Speed;
        public float LossMultiplier => 1f;
        // Compatibility only: curses are read from the tick context; light is never a gate.
        public void SetEffects(IReadOnlyActiveEffects effects) { }
        public float BeginAfterglow(int roomId) => 0f;
        public void Observe(HunterPlayerView view, bool clear, bool illuminated, IReadOnlyHunterWorldView world, bool wick)
        { _state.View = view; _state.Clear = clear; _state.Wick = wick; }
        public override void Reset(HunterArchetypeContext context)
        {
            _state.Hold = true; _state.Wick = false; _state.View = default; _state.Clear = false;
            _state.CatchPublished = false;
            _state.LastTick = -1; _state.Speed = 1f; _state.Facts.Clear();
            _state.Facts.Enqueue(new MannequinFact(context.Hunter.Id, MannequinFactKind.SilentSoundSet, context.Tick));
        }
        private static int Stacks(HunterArchetypeContext context, string id, int cap) =>
            Mathf.Clamp(context.Effects?.Stacks(new EffectId(id)) ?? 0, 0, cap);
        public override void Tick(HunterArchetypeContext context)
        {
            if (!(context.DeltaTime > 0f) || float.IsInfinity(context.DeltaTime) || context.Tick <= _state.LastTick) return;
            _state.LastTick = context.Tick; _state.Hold = true;
            _state.Speed = Mathf.Pow(_config.LongerStridesMultiplier, Stacks(context, "mannequin-longer-strides", 3));
            if (_state.Wick || !context.Player.IsAlive || !HunterViewUtility.Fresh(_state.View, context.Tick)) return;
            _state.Hold = _state.Clear && HunterViewUtility.Contains(_state.View,
                context.Hunter.Position + Vector3.up * _config.ObservationHeight,
                Stacks(context, "mannequin-peripheral-creep", 1) > 0 ? _config.DirectLookHalfAngle : 0f);
        }
        public bool TryCatch()
        {
            if (_state.CatchPublished) return false;
            _state.CatchPublished = true; return true;
        }
        // The player's view, not the hunter's sight cone or body-heading proxy,
        // decides when pursuit is permitted. Occluded prey still supplies its target.
        public override bool FilterVisibility(bool visible, SightProbe probe, HunterArchetypeContext context) => !Hold;
        public override float GoalUtility(HunterGoal goal, float utility) => goal == HunterGoal.LocatePrey ? utility : 0f;
        public bool TakeFact(out MannequinFact fact)
        { fact = default; if (_state.Facts.Count == 0) return false; fact = _state.Facts.Dequeue(); return true; }
    }
}
