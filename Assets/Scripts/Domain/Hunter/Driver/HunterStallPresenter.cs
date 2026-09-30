// ============================================================================
// HunterStallPresenter.cs
// ============================================================================
// PURPOSE:
//   Detects insufficient signed path progress without changing navigation.
//   A complete sliding window must elapse before one fact can begin an episode.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Integrate signed progress, interpolate the oldest interval, and re-arm on progress.
//   - Measure remaining arc length on a supplied polyline rather than distance walked.
// DEPENDENCIES:
//   - HunterStallDriverState, System math and UnityEngine value types only.
// USAGE NOTES:
//   Time and thresholds are injected. Equality is not a stall: duration and remaining
//   must be above their minima, progress below its minimum. A window is the duration;
//   no extra second persistence window is imposed. Invalid/no-path/arrival resets it.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterStallPresenter
    {
        public bool Observe(HunterStallDriverState state, Vector3 position, Vector3[] corners, bool hasPath,
            double dt, double duration, double minimumProgress, double minimumRemaining, out double remaining)
        {
            remaining = Remaining(position, corners);
            if (!hasPath || corners == null || corners.Length == 0 || remaining <= minimumRemaining || !Finite(dt) || dt <= 0)
            { Reset(state); return false; }
            // Freeze the reference, not steering state. SetPath replaces its array.
            // Compare both poses against one path so a refresh cannot invent progress.
            bool report = state.PreviousCorners != null && Tick(state, dt, true, remaining,
                Remaining(state.PreviousPosition, state.PreviousCorners) - Remaining(position, state.PreviousCorners),
                duration, minimumProgress, minimumRemaining);
            state.PreviousPosition = position; state.PreviousCorners = corners;
            return report;
        }
        public bool Tick(HunterStallDriverState state, double dt, bool hasPath, double remaining,
            double progress, double duration, double minimumProgress, double minimumRemaining)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!Finite(dt) || dt <= 0 || !Finite(progress) || !Finite(remaining) ||
                !Finite(duration) || duration <= 0 || !Finite(minimumProgress) || minimumProgress <= 0 ||
                !Finite(minimumRemaining) || minimumRemaining < 0 || !hasPath || remaining <= minimumRemaining)
            { Reset(state); return false; }
            state.Elapsed += dt;
            state.Samples.AddLast((dt, progress));
            state.WindowSeconds += dt; state.WindowProgress += progress;
            while (state.WindowSeconds > duration && state.Samples.First != null)
            {
                var first = state.Samples.First.Value;
                double removed = Math.Min(first.Seconds, state.WindowSeconds - duration);
                double distance = first.Progress * (removed / first.Seconds);
                state.WindowSeconds -= removed; state.WindowProgress -= distance;
                if (removed >= first.Seconds) state.Samples.RemoveFirst();
                else { state.Samples.First.Value = (first.Seconds - removed, first.Progress - distance); break; }
            }
            if (state.WindowProgress >= minimumProgress) { state.Reported = false; return false; }
            if (state.Elapsed <= duration || state.Reported) return false;
            state.Reported = true;
            return true;
        }
        public void Reset(HunterStallDriverState state)
        {
            state.Samples.Clear(); state.Elapsed = 0; state.WindowSeconds = 0;
            state.WindowProgress = 0; state.Reported = false; state.PreviousCorners = null;
        }
        public double Remaining(Vector3 position, IReadOnlyList<Vector3> corners)
        {
            if (corners == null || corners.Count == 0) return 0;
            if (corners.Count == 1) return Vector3.Distance(position, corners[0]);
            double total = 0, along = 0, nearest = double.PositiveInfinity, projection = 0;
            for (int i = 1; i < corners.Count; i++)
            {
                Vector3 segment = corners[i] - corners[i - 1];
                double length = segment.magnitude;
                float t = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(position - corners[i - 1], segment) / segment.sqrMagnitude) : 0;
                double distance = (position - (corners[i - 1] + segment * t)).sqrMagnitude;
                if (distance < nearest) { nearest = distance; projection = along + length * t; }
                along += length; total += length;
            }
            return Math.Max(0, total - projection);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
