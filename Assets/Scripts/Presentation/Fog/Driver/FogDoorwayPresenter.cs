// ============================================================================
// FogDoorwayPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a zero-depth haze sheet just inside a published doorway plane.
//   Exact geometry avoids particle billboard protrusion and voxel interpolation leaks.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Fog.
// KEY RESPONSIBILITIES:
//   - Clip doorway sheets to an exterior footprint face and its vertical opening.
//   - Reject unmatched portals and internal cell seams instead of inventing geometry.
// DEPENDENCIES:
//   - Core room samples and pure Unity value math only.
// USAGE NOTES:
//   Stateless; supplied portal positions are floor-level opening centers in world metres.
//   A sheet has no collision or volume. Both sides are rendered by the owning Driver.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Fog
{
    public static class FogDoorwayPresenter
    {
        public static Vector3[] Vertices(GeneratedRoomSample room, Vector3 portal, float width,
            float height, float inset, float tolerance)
        {
            if (room.Cells == null || !Finite(portal.x) || !Finite(portal.y) || !Finite(portal.z) ||
                !Finite(width) || !Finite(height) || !Finite(inset) || !Finite(tolerance) || width <= 0f || height <= 0f)
                return Array.Empty<Vector3>();
            tolerance = Mathf.Max(0f, tolerance);
            foreach (var cell in room.Cells)
                for (int axis = 0; axis <= 2; axis += 2)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float plane = side < 0 ? cell.min[axis] : cell.max[axis];
                        int across = 2 - axis;
                        if (Mathf.Abs(portal[axis] - plane) > tolerance || portal[across] < cell.min[across] ||
                            portal[across] > cell.max[across]) continue;
                        float bottom = Mathf.Max(cell.min.y, portal.y), top = Mathf.Min(cell.max.y, portal.y + height);
                        if (top <= bottom) continue;
                        Vector3 outside = portal; outside[axis] = plane + side * 0.001f; outside.y = (bottom + top) * .5f;
                        bool seam = false;
                        foreach (var other in room.Cells)
                            if (Contains(other, outside)) { seam = true; break; }
                        if (seam) continue;
                        float low = Mathf.Max(cell.min[across], portal[across] - width * .5f);
                        float high = Mathf.Min(cell.max[across], portal[across] + width * .5f);
                        if (high <= low) continue;
                        Vector3 a = portal;
                        a[axis] = plane - side * Mathf.Clamp(inset, 0f, Mathf.Min(.1f, cell.size[axis]));
                        a[across] = low; a.y = bottom;
                        Vector3 b = a; b[across] = high;
                        Vector3 c = b; c.y = top;
                        Vector3 d = a; d.y = top;
                        return new[] { a, b, c, d };
                    }
            return Array.Empty<Vector3>();
        }

        private static bool Contains(Bounds bounds, Vector3 point) => point.x >= bounds.min.x && point.x <= bounds.max.x &&
            point.y >= bounds.min.y && point.y <= bounds.max.y && point.z >= bounds.min.z && point.z <= bounds.max.z;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
