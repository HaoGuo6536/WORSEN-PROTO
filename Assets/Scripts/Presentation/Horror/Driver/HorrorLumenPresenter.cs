// ============================================================================
// HorrorLumenPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts authoritative light dimensions into native Lumen 2 fake-light values.
// ARCHITECTURAL ROLE:
//   Presenter (§7) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Account for the vendor shader's homogeneous distance and half-range cutoff.
//   - Keep visual cone bounds finite and retain illumination at an obstruction.
// DEPENDENCIES:
//   Unity value types and math only; no scene, physics or vendor engine calls.
// USAGE NOTES:
//   Pure calculations. Obstruction distances are supplied by the owning driver.
//   These visual approximations never change gameplay range or visibility facts.
// ============================================================================
using UnityEngine;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorLumenPresenter
    {
        public float RangeMultiplier(float radius, float layerRange)
        {
            radius = FiniteRadius(radius);
            float basis = float.IsNaN(layerRange) || float.IsInfinity(layerRange) || layerRange <= 0f ? 2f : layerRange;
            return 2f * (Mathf.Sqrt(radius * radius + 1f) - .5f) / basis;
        }

        public Vector2 ConeAngles(float fullConeDegrees)
        {
            float full = float.IsNaN(fullConeDegrees) || float.IsInfinity(fullConeDegrees) ? 55f : fullConeDegrees;
            float outer = Mathf.Clamp(full, 1f, 179f) * .5f;
            return new Vector2(outer * .65f, outer);
        }

        public float ObstructedRange(float requested, float nearest)
        {
            requested = FiniteRadius(requested);
            if (float.IsNaN(nearest) || float.IsInfinity(nearest)) return requested;
            nearest = Mathf.Clamp(nearest, 0f, requested);
            // The radial edge fades to zero, so keep a small reach beyond the struck wall.
            return Mathf.Min(requested, nearest + Mathf.Max(.35f, nearest * .15f));
        }

        private static float FiniteRadius(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? .01f : Mathf.Clamp(value, .01f, 1000f);
    }
}
