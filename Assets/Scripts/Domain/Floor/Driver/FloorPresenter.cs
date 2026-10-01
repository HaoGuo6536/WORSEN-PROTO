// ============================================================================
// FloorPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes navigation path lengths, horizontal guidance and warning intensity without engine queries.
//   Guidance uses the sampled path origin so airborne players do not point at their
//   own mesh projection. Failed refreshes invalidate guidance immediately; a
//   straight line through walls is never substituted for a verified route.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Skip nearby or passed corners and normalize the next useful direction.
//   - Invalidate target-local history on failed queries, with zero stale-refresh grace.
//   - Preserve complete path lengths and reject incomplete guidance.
// DEPENDENCIES:
//   - Floor path candidates and DriverState; UnityEngine value types for pure math.
//   - No other system, live engine object, Session or Presentation dependency.
// USAGE NOTES:
//   Pure math only; FloorDriver supplies complete paths or null on sample/path failure.
//   History is target-local and is cleared by FloorDriver at scene teardown.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Floor
{
    public sealed class FloorPresenter
    {
        /// <summary>Added to failed-path distance estimates so they rank after every complete path.</summary>
        public const float FallbackRankOffset = 100000f;

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

        public Vector3 FirstDirection(Vector3 sampledOrigin, IReadOnlyList<Vector3> corners, float skipDistance)
        {
            if (corners == null || corners.Count == 0) return Vector3.zero;
            float threshold = Mathf.Max(0f, skipDistance);
            int segment = 0;
            float nearest = float.PositiveInfinity;
            // Project onto the polyline, not onto a stale first corner behind a
            // crossed doorway. Equal projections keep the earliest segment.
            for (int i = 0; i + 1 < corners.Count; i++)
            {
                var a = corners[i]; a.y = sampledOrigin.y;
                var delta = corners[i + 1] - a; delta.y = 0f;
                float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(sampledOrigin - a, delta) / delta.sqrMagnitude) : 0f;
                float distance = (sampledOrigin - a - delta * t).sqrMagnitude;
                if (distance < nearest) { nearest = distance; segment = i; }
            }
            for (int i = segment + 1; i < corners.Count; i++)
            {
                var delta = corners[i] - sampledOrigin;
                delta.y = 0f;
                if (delta.magnitude > threshold) return HorizontalDirection(delta);
            }
            return HorizontalDirection(corners[corners.Count - 1] - sampledOrigin);
        }

        public FloorPathCandidate PathCandidate(FloorDriverState state, int anchorId, Vector3 playerOrigin,
            Vector3 sampledOrigin, Vector3 target, IReadOnlyList<Vector3> corners, float skipDistance)
        {
            state.FallbackDirections.Remove(anchorId);
            state.HeldDirections.Remove(anchorId);
            float length = PathLength(corners);
            bool complete = !float.IsNaN(length) && !float.IsInfinity(length);
            var direction = complete ? FirstDirection(sampledOrigin, corners, skipDistance) : Vector3.zero;
            if (complete && direction.sqrMagnitude > 0f)
                state.LastGoodDirections[anchorId] = direction;
            else state.LastGoodDirections.Remove(anchorId);
            if (direction.sqrMagnitude == 0f || float.IsNaN(length) || float.IsInfinity(length))
                length = float.PositiveInfinity;
            return new FloorPathCandidate(anchorId, length, direction);
        }

        private static Vector3 HorizontalDirection(Vector3 delta)
        {
            delta.y = 0f;
            float squared = delta.sqrMagnitude;
            if (squared <= 0f || float.IsNaN(squared) || float.IsInfinity(squared)) return Vector3.zero;
            return delta / Mathf.Sqrt(squared);
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