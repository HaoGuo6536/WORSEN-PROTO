// ============================================================================
// FloorPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes navigation path lengths, local direction cues and warning intensity without engine queries.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Pure math only; paths are complete, validated corner arrays supplied by FloorDriver.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorPresenter
    {
        public float PathLength(IReadOnlyList<Vector3> corners)
        {
            if (corners == null || corners.Count < 2) return float.PositiveInfinity;
            float length = 0f;
            for (int index = 1; index < corners.Count; index++)
            {
                var delta = corners[index] - corners[index - 1];
                var segment = delta.magnitude;
                if (float.IsNaN(segment) || float.IsInfinity(segment)) return float.PositiveInfinity;
                length += segment;
            }
            return length;
        }

        public Vector3 FirstDirection(Vector3 origin, IReadOnlyList<Vector3> corners)
        {
            if (corners == null) return Vector3.zero;
            foreach (var corner in corners)
            {
                var delta = corner - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.0001f) return delta.normalized;
            }
            return Vector3.zero;
        }

        public float WarningIntensity(float elapsed, float period, float maximum)
        {
            if (period <= 0f || maximum <= 0f) return 0f;
            return maximum * (0.5f + 0.5f * Mathf.Sin(elapsed * (2f * Mathf.PI / period)));
        }

        public Bounds[] BoundaryBlockers(Bounds room, float thickness)
        {
            return new[]
            {
                new Bounds(new Vector3(room.min.x, room.center.y, room.center.z), new Vector3(thickness, room.size.y, room.size.z)),
                new Bounds(new Vector3(room.max.x, room.center.y, room.center.z), new Vector3(thickness, room.size.y, room.size.z)),
                new Bounds(new Vector3(room.center.x, room.center.y, room.min.z), new Vector3(room.size.x, room.size.y, thickness)),
                new Bounds(new Vector3(room.center.x, room.center.y, room.max.z), new Vector3(room.size.x, room.size.y, thickness))
            };
        }
    }
}