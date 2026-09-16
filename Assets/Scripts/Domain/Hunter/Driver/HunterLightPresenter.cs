// ============================================================================
// HunterLightPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes the Hunter presentation calculation named by this file.
//   Plain values separate geometry, animation or route admission math from Unity
//   engine calls, making boundary conditions independently reproducible in tests.
// ARCHITECTURAL ROLE:
//   Presenter (section 7b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   No engine calls or owned mutable state; caller supplies all inputs and time.
// ============================================================================
using UnityEngine;
using Worsen.Core;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterLightPresenter
    {
        public bool IsFresh(FlashlightSample sample, long tick, int maxAge)
            => sample.Enabled && sample.Source.IsValid && sample.Tick <= tick && tick - sample.Tick <= maxAge &&
               Finite(sample.Origin) && Finite(sample.Direction) && sample.Direction.sqrMagnitude > 0.0001f &&
               Finite(sample.Range) && sample.Range > 0f && Finite(sample.ConeDegrees) && sample.ConeDegrees > 0f && sample.ConeDegrees < 180f;
        public bool InBeam(FlashlightSample sample, Vector3 point)
        {
            if (!Finite(point)) return false;
            Vector3 offset = point - sample.Origin;
            return offset.sqrMagnitude <= sample.Range * sample.Range &&
                (offset.sqrMagnitude <= 0.0001f || Vector3.Dot(offset.normalized, sample.Direction.normalized) >=
                 Mathf.Cos(sample.ConeDegrees * 0.5f * Mathf.Deg2Rad));
        }
        public bool InSight(Vector3 origin, Vector3 forward, Vector3 point, float range, float coneDegrees)
        {
            Vector3 offset = point - origin;
            return offset.sqrMagnitude <= range * range && (offset.sqrMagnitude <= 0.0001f ||
                Vector3.Dot(forward.normalized, offset.normalized) >= Mathf.Cos(coneDegrees * 0.5f * Mathf.Deg2Rad));
        }
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
