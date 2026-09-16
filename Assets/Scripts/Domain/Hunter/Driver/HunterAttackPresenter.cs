// ============================================================================
// HunterAttackPresenter.cs
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
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAttackPresenter
    {
        public Vector3[] Directions(Vector3 origin, Vector3 target, bool split)
        {
            Vector3 delta = target - origin;
            Vector3 forward = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector3.forward;
            return split ? new[] { Quaternion.AngleAxis(-14f, Vector3.up) * forward, forward, Quaternion.AngleAxis(14f, Vector3.up) * forward } : new[] { forward };
        }
        public Vector3[] GroundPoints(Vector3 target, float radius, bool ring)
        {
            if (!ring) return new[] { target };
            var points = new Vector3[7]; points[0] = target;
            for (int i = 1; i < points.Length; i++) points[i] = target + Quaternion.Euler(0f, (i - 1) * 60f, 0f) * Vector3.forward * radius * 1.8f;
            return points;
        }
    }
}
