// ============================================================================
// LumenMathUtility.cs
// ============================================================================
// PURPOSE:
//   Converts supplied dimensions and intensities into bounded fake-light values.
//   Sharing these calculations keeps independent presentation systems consistent
//   without either system depending on the other's presenters or engine objects.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Core · shared lighting math.
// KEY RESPONSIBILITIES:
//   - Compute exit fans, thin-fog glow and opt-in silhouette intensity.
//   - Convert light radii and cones and bound sampled obstruction distances.
// DEPENDENCIES:
//   Unity value types and math only; no project systems or vendor SDK calls.
// USAGE NOTES:
//   Pure and stateless. Callers supply observations; no engine clock or randomness.
//   Existing finite-value fallbacks preserve the authored lighting behavior.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{
    public static class LumenMathUtility
    {
        public static float ExitRayIntensity(float progress, float closed, float open)
            => Mathf.Lerp(NonNegative(closed), Mathf.Max(NonNegative(closed), NonNegative(open)), Unit(progress));

        public static float FogBoundaryGlow(float density, float thinLimit, float strength)
        {
            float limit = Unit(thinLimit);
            if (limit <= 0f || density <= 0f || density >= limit || float.IsNaN(density)) return 0f;
            return NonNegative(strength) * Mathf.Sin(density / limit * Mathf.PI);
        }

        public static float HunterRim(bool enabled, bool lookBack, float strength)
            => enabled && lookBack ? Unit(strength) : 0f;

        public static float FanYaw(int index, int count, float spread)
            => count <= 1 ? 0f : Mathf.Lerp(-NonNegative(spread), NonNegative(spread), Unit((float)index / (count - 1)));

        public static Vector3[] RayVertices(float width, float length)
            => new[] { Vector3.zero, new Vector3(-NonNegative(width), 0f, NonNegative(length)),
                new Vector3(NonNegative(width), 0f, NonNegative(length)) };

        private static float NonNegative(float value)
            => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        private static float Unit(float value) => Mathf.Clamp01(NonNegative(value));

        public static float RangeMultiplier(float radius, float layerRange)
        {
            radius = FiniteRadius(radius);
            float basis = float.IsNaN(layerRange) || float.IsInfinity(layerRange) || layerRange <= 0f ? 2f : layerRange;
            return 2f * (Mathf.Sqrt(radius * radius + 1f) - .5f) / basis;
        }

        public static Vector2 ConeAngles(float fullConeDegrees)
        {
            float full = float.IsNaN(fullConeDegrees) || float.IsInfinity(fullConeDegrees) ? 55f : fullConeDegrees;
            float outer = Mathf.Clamp(full, 1f, 179f) * .5f;
            return new Vector2(outer * .65f, outer);
        }

        public static float ObstructedRange(float requested, float nearest)
        {
            requested = FiniteRadius(requested);
            if (float.IsNaN(nearest) || float.IsInfinity(nearest)) return requested;
            nearest = Mathf.Clamp(nearest, 0f, requested);
            // Retain illumination at the struck wall without extending gameplay range.
            return Mathf.Min(requested, nearest + Mathf.Max(.35f, nearest * .15f));
        }

        private static float FiniteRadius(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? .01f : Mathf.Clamp(value, .01f, 1000f);
    }
}
