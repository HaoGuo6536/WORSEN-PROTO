// ============================================================================
// HorrorMicroEventPresenter.cs
// ============================================================================
// PURPOSE:
//   Schedules rare, non-gameplay anomalies using injected randomness and run time.
//   Decisions are bounded per run and never queue a burst behind a chase or a long frame.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Admit available kinds, enforce spacing and validate conservative door/view evidence.
// DEPENDENCIES:
//   - Core interactable values; Horror config/state; pure Unity math only.
// USAGE NOTES:
//   Stateless. Kind codes crossing the routing boundary: 0 none, 1 door, 2 silhouette, 3 counter.
//   Admission spends budget even if world application subsequently fails; facts report that failure.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public static class HorrorMicroEventPresenter
    {
        public static void ResetRun(HorrorMicroEventDriverState s, HorrorDriverConfig c, System.Random random)
        {
            s.Random = random;
            s.Used = 0;
            s.LastSeconds = double.NegativeInfinity;
            s.NextSeconds = Wait(c, random);
        }

        public static int Select(HorrorMicroEventDriverState s, HorrorDriverConfig c, double now,
            bool door, bool silhouette, bool counter)
        {
            if (s.Random == null || double.IsNaN(now) || double.IsInfinity(now) || now < 0d
                || now < s.NextSeconds || s.Used >= Mathf.Max(0, c.MicroEventsPerRun)) return 0;
            s.NextSeconds = now + Wait(c, s.Random);
            if (!s.ChaseKnown || s.Chases.Count != 0 || now - s.LastSeconds < Math.Max(60d, c.MicroEventSpacingSeconds)) return 0;
            int count = (door ? 1 : 0) + (silhouette ? 1 : 0) + (counter ? 1 : 0);
            if (count == 0) return 0;
            int choice = s.Random.Next(count);
            int kind = door && choice-- == 0 ? 1 : silhouette && choice-- == 0 ? 2 : 3;
            s.Used++;
            s.LastSeconds = now;
            return kind;
        }

        public static bool DoorEligible(InteractableState door, Bounds bounds, bool playerOpened,
            bool inView, Vector3 eye, Vector3 forward, float minimumDistance)
            => playerOpened && door.Id > 0 && door.Kind == InteractableKind.Door
                && door.Value == InteractableStateValue.Open && !inView && forward.sqrMagnitude > 0f
                && Vector3.Distance(eye, bounds.ClosestPoint(eye)) >= minimumDistance
                && Vector3.Dot(bounds.center - eye, forward.normalized) < -bounds.extents.magnitude;

        public static bool AtViewEdge(Vector3 viewport, float edge)
            => viewport.z > 0f && viewport.y > 0f && viewport.y < 1f && viewport.x > 0f && viewport.x < 1f
                && (viewport.x <= Mathf.Clamp(edge, 0f, 0.5f) || viewport.x >= 1f - Mathf.Clamp(edge, 0f, 0.5f));

        private static double Wait(HorrorDriverConfig c, System.Random random)
        {
            if (random == null) return double.PositiveInfinity;
            double mean = float.IsNaN(c.MicroEventMeanWaitSeconds) || float.IsInfinity(c.MicroEventMeanWaitSeconds)
                ? double.PositiveInfinity : Math.Max(1d, c.MicroEventMeanWaitSeconds);
            return Math.Max(60d, c.MicroEventSpacingSeconds) - Math.Log(Math.Max(double.Epsilon, 1d - random.NextDouble())) * mean;
        }
    }
}
