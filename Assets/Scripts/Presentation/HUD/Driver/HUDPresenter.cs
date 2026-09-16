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
//   - Express objective direction in the supplied camera frame, including height and rear targets.
//
// DEPENDENCIES:
//   - Worsen.Core ExitState and UnityEngine vector/math value operations only.
//
// USAGE NOTES:
//   - Stateless calculator over caller-owned HUDDriverState. No engine calls.
//   - Direction is a world-space vector; camera orientation supersedes the heading-only fallback.
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
            state.DirectionCaption = "";
        }

        public void SetDirection(HUDDriverState state, Vector3 direction, bool visible)
        {
            state.WorldDirection = direction;
            state.DirectionVisible = visible && IsFinite(direction.x) && IsFinite(direction.y) &&
                IsFinite(direction.z) && (direction.x != 0f || direction.y != 0f || direction.z != 0f);
            UpdateDirection(state);
        }

        public void SetHeading(HUDDriverState state, float headingDegrees)
        {
            if (!IsFinite(headingDegrees)) return;
            state.HeadingDegrees = headingDegrees;
            UpdateDirection(state);
        }

        public void SetViewRotation(HUDDriverState state, Quaternion rotation)
        {
            if (!IsFinite(rotation.x) || !IsFinite(rotation.y) || !IsFinite(rotation.z) || !IsFinite(rotation.w)) return;
            double length = Math.Sqrt((double)rotation.x * rotation.x + (double)rotation.y * rotation.y +
                (double)rotation.z * rotation.z + (double)rotation.w * rotation.w);
            if (length < 0.000001) return;
            state.ViewRotation = new Quaternion((float)(rotation.x / length), (float)(rotation.y / length),
                (float)(rotation.z / length), (float)(rotation.w / length));
            state.HasViewRotation = true;
            UpdateDirection(state);
        }

        private static void UpdateDirection(HUDDriverState state)
        {
            if (!state.DirectionVisible)
            {
                state.ViewDirection = Vector3.zero;
                state.DirectionDegrees = state.DirectionPitchDegrees = 0f;
                return;
            }
            Vector3 world = state.WorldDirection;
            double length = Math.Sqrt((double)world.x * world.x + (double)world.y * world.y + (double)world.z * world.z);
            Vector3 direction = new Vector3((float)(world.x / length), (float)(world.y / length), (float)(world.z / length));
            if (state.HasViewRotation)
            {
                Quaternion rotation = state.ViewRotation;
                Vector3 imaginary = new Vector3(-rotation.x, -rotation.y, -rotation.z);
                Vector3 twiceCross = 2f * Vector3.Cross(imaginary, direction);
                state.ViewDirection = direction + rotation.w * twiceCross + Vector3.Cross(imaginary, twiceCross);
            }
            else
            {
                double radians = state.HeadingDegrees * Math.PI / 180.0;
                float sine = (float)Math.Sin(radians), cosine = (float)Math.Cos(radians);
                state.ViewDirection = new Vector3(direction.x * cosine - direction.z * sine, direction.y,
                    direction.x * sine + direction.z * cosine);
            }
            Vector3 local = state.ViewDirection;
            state.DirectionDegrees = (float)(Math.Atan2(local.x, local.z) * 180.0 / Math.PI);
            state.DirectionPitchDegrees = (float)(Math.Atan2(local.y, Math.Sqrt(local.x * local.x + local.z * local.z)) * 180.0 / Math.PI);
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
