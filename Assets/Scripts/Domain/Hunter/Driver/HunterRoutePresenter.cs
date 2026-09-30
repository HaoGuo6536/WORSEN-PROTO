// ============================================================================
// HunterRoutePresenter.cs
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
//   - Select the last hidden approach corner before visibility without removing any path legs.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   No engine calls or owned mutable state; caller supplies all inputs and time.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterRoutePresenter
    {
        public int EmergenceCorner(IReadOnlyList<Vector3> corners, IReadOnlyList<bool> occluded, bool enabled, int budget)
        {
            if (!enabled || corners == null || occluded == null) return -1;
            int count = System.Math.Min(System.Math.Min(corners.Count, occluded.Count), System.Math.Max(0, budget));
            int chosen = -1;
            for (int i = 1; i < count - 1; i++)
                if (occluded[i] && !occluded[i + 1]) chosen = i;
            return chosen;
        }
        public bool Allowed(IReadOnlyList<Vector3> corners, IReadOnlyList<Bounds> unavailable)
        {
            if (corners == null || corners.Count == 0) return false;
            if (unavailable == null) return true;
            foreach (Bounds room in unavailable)
                for (int i = 0; i < corners.Count; i++)
                {
                    if (room.Contains(corners[i])) return false;
                    if (i == 0) continue;
                    Vector3 delta = corners[i] - corners[i - 1];
                    if (delta.sqrMagnitude > 0.0001f && room.IntersectRay(new Ray(corners[i - 1], delta.normalized), out float distance) &&
                        distance <= delta.magnitude) return false;
                }
            return true;
        }
    }
}
