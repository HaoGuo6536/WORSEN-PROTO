// ============================================================================
// BlinderProjectilePresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a straight cosmetic flight from an authoritative launch fact.
//   It does not raycast, choose targets or cause hits; Session ticks supply time
//   and confirmed hit facts retire the matching visual independently of blindness.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Reject malformed launch facts and advance a bounded flight with injected time.
//   - Match projectile hits by hunter and serial without consuming floor-trap hits.
// DEPENDENCIES:
//   Core Blinder facts and own DriverState; Unity value math only.
// USAGE NOTES:
//   No independent clock. Radius, speed and range come from the gameplay launch;
//   the line covers the most recent accepted simulation step, not a second trail timer.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class BlinderProjectilePresenter
    {
        public BlinderProjectileDriverState Create(BlinderThrowFact fact)
        {
            if (!Finite(fact.Origin) || !Finite(fact.Target) || !Positive(fact.Radius) ||
                !Positive(fact.Speed) || !Positive(fact.Range) ||
                !Positive((fact.Target - fact.Origin).sqrMagnitude)) return null;
            return new BlinderProjectileDriverState { Fact = fact, Position = fact.Origin, PreviousPosition = fact.Origin };
        }
        public bool Tick(BlinderProjectileDriverState state, float delta)
        {
            if (!Positive(delta)) return state.Traveled < state.Fact.Range;
            state.PreviousPosition = state.Position;
            state.Traveled = Mathf.Min(state.Fact.Range, state.Traveled + state.Fact.Speed * delta);
            state.Position = state.Fact.Origin + (state.Fact.Target - state.Fact.Origin).normalized * state.Traveled;
            return state.Traveled < state.Fact.Range;
        }
        public bool Matches(BlinderProjectileDriverState state, BlinderHitFact hit)
            => !hit.Trap && state.Fact.Hunter == hit.Hunter && state.Fact.Serial == hit.Serial;
        private static bool Positive(float value) => value > 0f && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
