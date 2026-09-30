// ============================================================================
// FloorBoundsUtility.cs
// ============================================================================
// PURPOSE:
//   Tests room-cell occupancy without Bounds.Contains's native engine call.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Preserve inclusive axis-aligned bounds membership using managed value math.
// DEPENDENCIES:
//   - UnityEngine Bounds and Vector3 values only; no engine operations.
// USAGE NOTES:
//   Stateless; used by cake admission and route-safe collapse occupancy alike.
//   Negative extents and nonfinite points are not inside a room cell.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Floor
{
    public static class FloorBoundsUtility
    {
        public static bool Contains(Bounds cell, Vector3 point)
        {
            var min = cell.min; var max = cell.max;
            return Finite(point.x) && Finite(point.y) && Finite(point.z) &&
                point.x >= min.x && point.x <= max.x &&
                point.y >= min.y && point.y <= max.y &&
                point.z >= min.z && point.z <= max.z;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
