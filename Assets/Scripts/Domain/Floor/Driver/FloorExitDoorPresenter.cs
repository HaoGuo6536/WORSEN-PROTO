// ============================================================================
// FloorExitDoorPresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates smooth hinge movement and requires front-to-back passage through the opening.
//   The doorway distinguishes collecting the last cake from deliberately leaving.
//   Explicit timing and crossing observations keep the transition reproducible.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep visible door movement and physical passage in agreement.
//   - Prevent a stationary overlap from becoming an accidental floor transition.
// DEPENDENCIES:
//   - Core shared values and Floor-owned visual configuration only.
// USAGE NOTES:
//   Scene-owned through FloorDriver. Session supplies elapsed time; no Update loop.
//   No global settings. Reinitialization clears crossing and opening state.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Domain.Floor
{
    public sealed class FloorExitDoorPresenter
    {
        public void Reset(FloorExitDoorDriverState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.Opening = false; state.FullyOpen = false;
            state.Elapsed = 0f; state.LastClock = 0f; state.Contacts.Clear();
        }
        public bool Open(FloorExitDoorDriverState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Opening || state.FullyOpen) return false;
            state.Opening = true; state.Elapsed = 0f; state.Contacts.Clear();
            return true;
        }
        public void Tick(FloorExitDoorDriverState state, float clock, float duration)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!Finite(clock) || clock < state.LastClock) return;
            float dt = clock - state.LastClock;
            state.LastClock = clock;
            if (!state.Opening || state.FullyOpen) return;
            state.Elapsed += dt;
            if (OpeningProgress(state.Elapsed, duration) < 1f) return;
            state.FullyOpen = true; state.Opening = false; state.Contacts.Clear();
        }
        public float OpeningProgress(float elapsed, float duration)
        {
            if (!Finite(elapsed) || !Finite(duration)) return 0f;
            return Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
        }
        public float HingeAngle(float progress, float maximumAngle)
        {
            float t = Finite(progress) ? Mathf.Clamp01(progress) : 0f;
            if (!Finite(maximumAngle)) return 0f;
            return Mathf.Clamp(maximumAngle, 90f, 120f) * t * t * (3f - 2f * t);
        }
        public bool ObserveCrossing(FloorExitCrossingDriverState state, Vector3 localPosition, Vector3 passageSize, float distance)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Completed) return false;
            if (!Finite(localPosition.x) || !Finite(localPosition.y) || !Finite(localPosition.z) ||
                !Finite(passageSize.x) || !Finite(passageSize.y) || !Finite(passageSize.z) ||
                !Finite(distance) || distance <= 0f || passageSize.x <= 0.2f || passageSize.y <= 0f || passageSize.z <= 0f)
            { state.EntrySide = 0; return false; }
            bool within = Mathf.Abs(localPosition.x) < passageSize.x * 0.5f - 0.1f &&
                localPosition.y >= 0f && localPosition.y < passageSize.y &&
                Mathf.Abs(localPosition.z) <= Mathf.Max(passageSize.z * 0.5f, distance + 0.3f);
            if (!within) { state.EntrySide = 0; return false; }
            int side = localPosition.z <= -distance ? -1 : localPosition.z >= distance ? 1 : 0;
            // The visible escape is on local -Z. Back-to-front never completes;
            // reaching the front arms a subsequent genuine front-to-back pass.
            if (side == -1) { state.EntrySide = -1; return false; }
            if (side != 1 || state.EntrySide != -1) return false;
            state.Completed = true; return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
