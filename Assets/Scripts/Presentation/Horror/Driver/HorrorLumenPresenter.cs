// ============================================================================
// HorrorLumenPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts authoritative light dimensions into native Lumen 2 fake-light values.
//   Also computes restrained exit, thin-fog and silhouette hooks without touching renderers.
// ARCHITECTURAL ROLE:
//   Presenter (§7) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Account for the vendor shader's homogeneous distance and half-range cutoff.
//   - Keep visual cone bounds finite and retain illumination at an obstruction.
//   - Produce monotonic exit intensity and default-off, bounded silhouette treatment.
// DEPENDENCIES:
//   Core LumenMathUtility owns shared math; no scene, physics or vendor calls.
// USAGE NOTES:
//   Pure calculations. Obstruction distances are supplied by the owning driver.
//   These visual approximations never change gameplay range or visibility facts.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorLumenPresenter
    {
        public float ExitRayIntensity(float progress, float closed, float open)
            => LumenMathUtility.ExitRayIntensity(progress, closed, open);

        public float FogBoundaryGlow(float density, float thinLimit, float strength)
            => LumenMathUtility.FogBoundaryGlow(density, thinLimit, strength);

        public float HunterRim(bool enabled, bool lookBack, float strength)
            => LumenMathUtility.HunterRim(enabled, lookBack, strength);

        public float FanYaw(int index, int count, float spread)
            => LumenMathUtility.FanYaw(index, count, spread);

        public Vector3[] RayVertices(float width, float length)
            => LumenMathUtility.RayVertices(width, length);

        public float RangeMultiplier(float radius, float layerRange)
            => LumenMathUtility.RangeMultiplier(radius, layerRange);

        public Vector2 ConeAngles(float fullConeDegrees)
            => LumenMathUtility.ConeAngles(fullConeDegrees);

        public float ObstructedRange(float requested, float nearest)
            => LumenMathUtility.ObstructedRange(requested, nearest);
    }
}
