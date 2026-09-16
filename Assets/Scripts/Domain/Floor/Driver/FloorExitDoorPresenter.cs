// ============================================================================
// FloorExitDoorPresenter.cs
// ============================================================================
// PURPOSE:
//   Calculates smooth hinge movement and requires an observed passage from one clear side to the other.
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
        public float OpeningProgress(float elapsed, float duration)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return 0f;
            return Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
        }
        public float HingeAngle(float progress, float maximumAngle)
        {
            float t = Mathf.Clamp01(progress);
            return Mathf.Clamp(maximumAngle, 90f, 120f) * t * t * (3f - 2f * t);
        }
        public bool ObserveCrossing(FloorExitCrossingDriverState state, Vector3 localPosition, Vector3 passageSize, float distance)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Completed) return false;
            if (!Finite(localPosition.x) || !Finite(localPosition.y) || !Finite(localPosition.z))
            { state.EntrySide = 0; return false; }
            bool within = Mathf.Abs(localPosition.x) < Mathf.Max(0.1f, passageSize.x * 0.5f - 0.1f) &&
                localPosition.y >= 0f && localPosition.y < passageSize.y &&
                Mathf.Abs(localPosition.z) <= Mathf.Max(passageSize.z * 0.5f, distance + 0.3f);
            if (!within) { state.EntrySide = 0; return false; }
            int side = localPosition.z <= -distance ? -1 : localPosition.z >= distance ? 1 : 0;
            if (state.EntrySide == 0) { state.EntrySide = side; return false; }
            if (side == 0 || side == state.EntrySide) return false;
            state.Completed = true; return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
