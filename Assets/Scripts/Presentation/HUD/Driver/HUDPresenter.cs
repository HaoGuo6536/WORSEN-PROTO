// ============================================================================
// HUDPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Converts incoming display facts into HUD text and an interruptible restoration.
//   Time is supplied explicitly, so the count-and-exit chase transition and return
//   to direction and slots can be verified without a document or running scene.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Format supplied counts, clamp the display-only gauge and validate direction samples.
//   - Restore extra HUD elements using the supplied duration; a new chase cancels it.
//   - Clear transient chase suppression immediately at an explicit new-run boundary.
//
// DEPENDENCIES:
//   - Worsen.Core ExitState and UnityEngine vector/math value operations only.
//
// USAGE NOTES:
//   - Stateless calculator over caller-owned HUDDriverState. No engine calls.
//   - Direction is a world-space vector; heading is clockwise yaw from world positive z.
//
// ============================================================================

using System;
using System.Globalization;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDPresenter
    {
        public void SetCount(HUDDriverState state, int collected, int total)
        {
            state.CountKnown = collected >= 0 && total > 0;
            state.CountFraction = state.CountKnown ? Mathf.Clamp01((float)collected / total) : 0f;
            state.CountText = collected < 0 || total < 0 ? "Cakes: —"
                : "Cakes: " + collected.ToString(CultureInfo.InvariantCulture) + " / " + total.ToString(CultureInfo.InvariantCulture);
        }

        public void SetExitState(HUDDriverState state, ExitState exitState)
        {
            state.ExitOpen = exitState == ExitState.Open;
            state.ExitText = exitState == ExitState.Open ? "Exit: OPEN" : exitState == ExitState.Locked ? "Exit: LOCKED" : "Exit: —";
            state.DirectionCaption = exitState == ExitState.Open ? "EXIT" : exitState == ExitState.Locked ? "NEXT CAKE" : "DIRECTION";
        }

        public void SetDirection(HUDDriverState state, Vector3 direction, bool visible)
        {
            state.WorldDirection = direction;
            state.DirectionVisible = visible && IsFinite(direction.x) && IsFinite(direction.y) &&
                IsFinite(direction.z) && (direction.x != 0f || direction.z != 0f);
            UpdateDirection(state);
        }

        public void SetHeading(HUDDriverState state, float headingDegrees)
        {
            if (!IsFinite(headingDegrees)) return;
            state.HeadingDegrees = headingDegrees;
            UpdateDirection(state);
        }

        private static void UpdateDirection(HUDDriverState state)
        {
            state.DirectionDegrees = state.DirectionVisible
                ? Mathf.DeltaAngle(state.HeadingDegrees,
                    (float)(Math.Atan2(state.WorldDirection.x, state.WorldDirection.z) * 180.0 / Math.PI)) : 0f;
        }

        public void SetItemSlots(HUDDriverState state, int emptySlotCount, int maximumDisplayedSlots)
        {
            int count = Math.Max(0, emptySlotCount);
            state.DisplayedSlots = Math.Min(count, Math.Max(1, maximumDisplayedSlots));
            int overflow = count - state.DisplayedSlots;
            state.SlotOverflowText = overflow > 0 ? "+" + overflow.ToString(CultureInfo.InvariantCulture) + " empty slots" : "";
        }

        public void SetChaseMode(HUDDriverState state, bool chasing)
        {
            state.ChaseMode = chasing;
            if (chasing) state.ExtraOpacity = 0f;
        }

        public void ResetRunView(HUDDriverState state)
        {
            state.ChaseMode = false;
            state.ExtraOpacity = 1f;
        }

        public void Tick(HUDDriverState state, float deltaTime, float restoreSeconds)
        {
            if (state.ChaseMode || !IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (!IsFinite(restoreSeconds) || restoreSeconds <= 0f)
            {
                state.ExtraOpacity = 1f;
                return;
            }
            state.ExtraOpacity = Mathf.Clamp01(state.ExtraOpacity + deltaTime / restoreSeconds);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
