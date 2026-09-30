// ============================================================================
// HUDPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Converts incoming display facts into HUD text and an interruptible restoration.
//   Time is supplied explicitly, so chase suppression and chrome restoration can
//   be verified without a document or running scene. Guidance stays independent.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Replace both typed guidance channels atomically; phantom cakes never change supplied counts.
//   - Format cake/golden counts and show only occupied consumable slots, never empty capacity.
//   - Compute a flat arrow bearing; vertical-only targets point up or down.
//   - Hide all chrome during a confirmed chase without producing chase text or hiding guidance.
//   - Restore chrome using the supplied duration; a new chase cancels it.
//   - Clear transient chase suppression immediately at an explicit new-run boundary.
//   - Express objective direction in the supplied camera frame, including height and rear targets.
//
// DEPENDENCIES:
//   - Worsen.Core ExitState/GuidanceTarget and UnityEngine vector/math value operations only.
//
// USAGE NOTES:
//   - Stateless calculator over caller-owned HUDDriverState. No engine calls.
//   - Direction is a world-space vector; camera orientation supersedes the heading-only fallback.
//
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDPresenter
    {
        public void SetCount(HUDDriverState state, int collected, int total)
        {
            state.Collected = collected; state.Required = total;
            if (collected < 0 || total <= 0 || collected == int.MaxValue) state.PhantomSeconds = 0f;
            FormatCount(state);
        }

        private static void FormatCount(HUDDriverState state)
        {
            int collected = state.Collected + (state.PhantomSeconds > 0f ? 1 : 0), total = state.Required;
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

        public void SetGoldenCount(HUDDriverState state, int count)
            => state.GoldenText = count < 0 ? "Golden: —" : "Golden: " + count.ToString(CultureInfo.InvariantCulture);

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
            Direction(state.WorldDirection, state.DirectionVisible, state.HeadingDegrees, state.HasViewRotation, state.ViewRotation,
                out state.ViewDirection, out state.DirectionDegrees, out state.DirectionPitchDegrees, out state.ArrowDegrees);
            Direction(state.GoldenSenseDirection, state.GoldenSenseVisible, state.HeadingDegrees, state.HasViewRotation, state.ViewRotation,
                out state.GoldenSenseViewDirection, out state.GoldenSenseDegrees, out state.GoldenSensePitchDegrees, out state.GoldenSenseArrowDegrees);
        }

        public void SetGuidance(HUDDriverState state, IReadOnlyList<GuidanceTarget> targets)
        {
            SetDirection(state, Vector3.zero, false);
            state.GoldenSenseDirection = Vector3.zero; state.GoldenSenseVisible = false;
            if (targets != null) foreach (var target in targets)
            {
                if (target.Kind == GuidanceKind.WhiteArrow) SetDirection(state, target.WorldDirection, true);
                else if (target.Kind == GuidanceKind.GoldenSense)
                {
                    state.GoldenSenseDirection = target.WorldDirection;
                    state.GoldenSenseVisible = IsFinite(target.WorldDirection.x) && IsFinite(target.WorldDirection.y) &&
                        IsFinite(target.WorldDirection.z) && target.WorldDirection.sqrMagnitude > 0f;
                }
            }
            UpdateDirection(state);
        }

        private static void Direction(Vector3 world, bool visible, float heading, bool hasRotation, Quaternion rotation,
            out Vector3 view, out float yaw, out float pitch, out float arrow)
        {
            view = Vector3.zero; yaw = pitch = arrow = 0f;
            if (!visible) return;
            double length = Math.Sqrt((double)world.x * world.x + (double)world.y * world.y + (double)world.z * world.z);
            Vector3 direction = new Vector3((float)(world.x / length), (float)(world.y / length), (float)(world.z / length));
            if (hasRotation)
            {
                Vector3 imaginary = new Vector3(-rotation.x, -rotation.y, -rotation.z);
                Vector3 twiceCross = 2f * Vector3.Cross(imaginary, direction);
                view = direction + rotation.w * twiceCross + Vector3.Cross(imaginary, twiceCross);
            }
            else
            {
                double radians = heading * Math.PI / 180.0;
                float sine = (float)Math.Sin(radians), cosine = (float)Math.Cos(radians);
                view = new Vector3(direction.x * cosine - direction.z * sine, direction.y,
                    direction.x * sine + direction.z * cosine);
            }
            yaw = (float)(Math.Atan2(view.x, view.z) * 180.0 / Math.PI);
            pitch = (float)(Math.Atan2(view.y, Math.Sqrt(view.x * view.x + view.z * view.z)) * 180.0 / Math.PI);
            arrow = view.x == 0f && view.z == 0f ? (view.y < 0f ? 180f : 0f) : yaw;
        }

        public void SetItemSlots(HUDDriverState state, int emptySlotCount, int maximumDisplayedSlots)
        {
            // Compatibility entry point: empty capacity is not an item and must not draw outlines.
            SetHeldItemCount(state, 0, maximumDisplayedSlots);
        }

        public void SetHeldItemCount(HUDDriverState state, int heldItemCount, int maximumDisplayedSlots)
        {
            int count = Math.Max(0, heldItemCount);
            state.DisplayedSlots = Math.Min(count, Math.Max(1, maximumDisplayedSlots));
            int overflow = count - state.DisplayedSlots;
            state.SlotOverflowText = overflow > 0 ? "+" + overflow.ToString(CultureInfo.InvariantCulture) + " items" : "";
        }

        public void SetChaseMode(HUDDriverState state, bool chasing)
        {
            state.ChaseMode = chasing;
            state.ChromeVisible = !chasing;
            if (chasing) state.ExtraOpacity = 0f;
        }

        public void ResetRunView(HUDDriverState state)
        {
            ClearPhantomCake(state);
            state.ChaseMode = false;
            state.ChromeVisible = true;
            state.ExtraOpacity = 1f;
        }

        public void Tick(HUDDriverState state, float deltaTime, float restoreSeconds)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (state.PhantomSeconds > 0f)
            { state.PhantomSeconds = Math.Max(0f, state.PhantomSeconds - deltaTime); FormatCount(state); }
            if (state.ChaseMode) return;
            if (!IsFinite(restoreSeconds) || restoreSeconds <= 0f)
            {
                state.ExtraOpacity = 1f;
                return;
            }
            state.ExtraOpacity = Mathf.Clamp01(state.ExtraOpacity + deltaTime / restoreSeconds);
        }

        public bool TryShowPhantomCake(HUDDriverState state, float seconds)
        {
            if (!IsFinite(seconds) || seconds <= 0f || !state.CountKnown || state.Collected == int.MaxValue ||
                !state.ChromeVisible || state.ExtraOpacity <= 0f) return false;
            state.PhantomSeconds = seconds; FormatCount(state); return true;
        }

        public void ClearPhantomCake(HUDDriverState state) { state.PhantomSeconds = 0f; FormatCount(state); }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
