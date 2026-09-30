// ============================================================================
// FloorPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes navigation path lengths, horizontal guidance and warning intensity without engine queries.
//   Guidance uses the sampled path origin so airborne players do not point at their
//   own mesh projection. Failed refreshes use straight-line or retained directions.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Skip nearby horizontal corners and normalize the next useful direction.
//   - Record per-target fallback or held guidance in the supplied DriverState.
//   - Preserve complete path lengths; failed guidance paths rank after all complete paths.
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
            foreach (var corner in corners)
            {
                var delta = corner - sampledOrigin;
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
            if (direction.sqrMagnitude == 0f)
            {
                direction = HorizontalDirection(target - playerOrigin);
                if (direction.sqrMagnitude > 0f) state.FallbackDirections.Add(anchorId);
                else if (state.LastGoodDirections.TryGetValue(anchorId, out var previous))
                {
                    direction = previous;
                    state.HeldDirections.Add(anchorId);
                }
            }
            if (direction.sqrMagnitude > 0f && !state.HeldDirections.Contains(anchorId))
                state.LastGoodDirections[anchorId] = direction;
            // SelectCue rejects infinite lengths. Failed paths need an estimate to
            // publish fallback/held guidance, but they rank after every complete path:
            // a straight line is never longer than a real path, so an unreachable target
            // must not outrank a reachable one.
            if (!complete) length = FallbackRankOffset + (target - playerOrigin).magnitude;
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