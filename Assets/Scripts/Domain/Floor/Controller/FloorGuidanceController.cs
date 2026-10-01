// ============================================================================
// FloorGuidanceController.cs
// ============================================================================
// PURPOSE:
//   Applies temporary Mimic misdirection without admitting fake cakes to Floor rules.
//   Normal guidance is unchanged unless the ordinary Faithless Arrow curse is active
//   and a posed Mimic has published a live window for the current guidance player.
// ARCHITECTURAL ROLE:
//   Controller (§2) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Consume ordered pose/removal/window facts and expire windows with explicit time.
//   - Gate every instance by the same injected active-effect view.
//   - Replace only an existing white arrow, preserving suppression and optional senses.
//   - Resolve simultaneous windows deterministically without borrowing pickup identities.
// DEPENDENCIES:
//   - Own BehaviorState; Core Mimic, guidance and read-only effect contracts.
//   - UnityEngine value math only; no Hunter module or higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager. Session must route facts and effect replacements.
//   Window duration is supplied by the Mimic fact, not a second Floor tunable.
//   Reset clears poses, windows, watermarks and effects between generated floors.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorGuidanceController
    {
        public static readonly EffectId FaithlessArrow = new EffectId("mimic-faithless-arrow");
        private readonly FloorGuidanceBehaviorState _state;
        private bool Enabled => (_state.Effects?.Stacks(FaithlessArrow) ?? 0) > 0;

        public FloorGuidanceController(FloorGuidanceBehaviorState state)
        { _state = state ?? throw new ArgumentNullException(nameof(state)); }

        public void SetActiveEffects(IReadOnlyActiveEffects effects)
        {
            _state.Effects = effects;
            if (!Enabled) { _state.Windows.Clear(); _state.Expiries.Clear(); }
        }

        public bool ReceiveMimic(MimicFact fact)
        {
            if (fact.Hunter == EntityId.None || fact.Tick < 0 ||
                _state.LastTicks.TryGetValue(fact.Hunter, out var tick) && fact.Tick < tick) return false;
            switch (fact.Kind)
            {
                case MimicFactKind.Pose:
                    if (!Finite(fact.Position)) return false;
                    _state.Poses[fact.Hunter] = fact;
                    break;
                case MimicFactKind.PoseRemoved:
                    _state.Poses.Remove(fact.Hunter);
                    _state.Windows.Remove(fact.Hunter); _state.Expiries.Remove(fact.Hunter);
                    break;
                case MimicFactKind.FaithlessWindow:
                    if (!Enabled || fact.Player == EntityId.None || !Finite(fact.Seconds) || fact.Seconds <= 0f ||
                        !_state.Poses.TryGetValue(fact.Hunter, out var pose) || pose.Player != fact.Player ||
                        _state.WindowTicks.TryGetValue(fact.Hunter, out var windowTick) && fact.Tick <= windowTick) return false;
                    _state.Windows[fact.Hunter] = fact;
                    _state.Expiries[fact.Hunter] = _state.Elapsed + fact.Seconds;
                    _state.WindowTicks[fact.Hunter] = fact.Tick;
                    break;
                default: return false;
            }
            _state.LastTicks[fact.Hunter] = fact.Tick;
            return true;
        }

        public bool Tick(float dt)
        {
            if (!Finite(dt) || dt < 0f) throw new ArgumentOutOfRangeException(nameof(dt));
            _state.Elapsed += dt;
            var expired = new List<EntityId>();
            foreach (var pair in _state.Expiries)
                if (pair.Value <= _state.Elapsed || !Enabled) expired.Add(pair.Key);
            foreach (var id in expired) { _state.Expiries.Remove(id); _state.Windows.Remove(id); }
            return expired.Count > 0;
        }

        public IReadOnlyList<GuidanceTarget> Apply(IReadOnlyList<GuidanceTarget> normal, EntityId player, Vector3 position)
        {
            if (normal == null || normal.Count == 0 || !Enabled || !Finite(position)) return normal ?? Array.Empty<GuidanceTarget>();
            int white = -1;
            for (int index = 0; index < normal.Count; index++)
                if (normal[index].Kind == GuidanceKind.WhiteArrow) { white = index; break; }
            if (white < 0) return normal; // Blind Faith, no living player or ended floor remains suppressed.
            var nearest = EntityId.None; var target = Vector3.zero; var direction = Vector3.zero;
            float distance = float.PositiveInfinity;
            foreach (var pair in _state.Windows)
            {
                if (pair.Value.Player != player || !_state.Poses.TryGetValue(pair.Key, out var pose) ||
                    !_state.Expiries.TryGetValue(pair.Key, out var expiry) || expiry <= _state.Elapsed) continue;
                var delta = pose.Position - position; delta.y = 0f;
                float squared = delta.sqrMagnitude;
                if (!Finite(squared) || squared <= 0f || squared > distance ||
                    squared == distance && nearest != EntityId.None && pair.Key.Value >= nearest.Value) continue;
                nearest = pair.Key; target = pose.Position; direction = delta / Mathf.Sqrt(squared); distance = squared;
            }
            if (nearest == EntityId.None) return normal;
            var result = new List<GuidanceTarget>(normal);
            result[white] = new GuidanceTarget(GuidanceKind.WhiteArrow, direction, target,
                entityId: nearest, isFallback: true);
            return result.AsReadOnly();
        }

        public void Reset()
        {
            _state.Poses.Clear(); _state.Windows.Clear(); _state.Expiries.Clear();
            _state.LastTicks.Clear(); _state.WindowTicks.Clear(); _state.Effects = null; _state.Elapsed = 0d;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
