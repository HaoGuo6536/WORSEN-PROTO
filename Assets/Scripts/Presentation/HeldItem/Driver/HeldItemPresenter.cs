// ============================================================================
// HeldItemPresenter.cs
// ============================================================================
// PURPOSE:
//   Resolves the physical selected consumable and computes its arm-free idle pose.
//   Selection only changes presentation: using an item remains the Session's press
//   action. Replaced items lower before the new silhouette rises into view.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Resolve valid, nonempty consumable selection without compacting physical slots.
//   - Advance interruptible lower/raise transitions using injected elapsed time.
//   - Compute bounded idle sway without changing the shared camera aim ray.
// DEPENDENCIES:
//   Core inventory snapshots, own DriverState and Unity value math only.
// USAGE NOTES:
//   Stateless. Sway is cosmetic; target admission uses the unshaken camera aim.
//   Unknown ids fail closed rather than claiming a usable item representation.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.HeldItem
{
    public sealed class HeldItemPresenter
    {
        public void SetSelection(HeldItemDriverState state, ConsumableInventorySnapshot snapshot)
        {
            int index = snapshot.SelectedIndex;
            string id = snapshot.Inventory != null && index >= 0 && index < snapshot.Inventory.Count
                ? snapshot.Inventory[index].Id : "";
            if (snapshot.RemainingUses != null && (index < 0 || index >= snapshot.RemainingUses.Count || snapshot.RemainingUses[index] <= 0)) id = "";
            state.SelectedId = Supported(id) ? id : "";
            state.SelectedSlot = state.SelectedId.Length > 0 ? index : -1;
        }
        public bool Supported(string id) => id == "firecracker" || id == "gauze" || id == "smelling-salts" ||
            id == "wax-ward" || id == "doorstop" || id == "oil-flask" || id == "glass-vial" || id == "adrenaline";
        public void SetSuppressed(HeldItemDriverState state, bool suppressed)
        {
            state.Suppressed = suppressed;
            if (suppressed) { state.Raise = state.IdlePhase = 0f; state.DisplayedId = ""; state.DisplayedSlot = -1; }
        }
        public void Tick(HeldItemDriverState state, float dt, float transitionSeconds, float swayPeriod)
        {
            if (state.Suppressed || !Finite(dt) || dt <= 0f || !Finite(transitionSeconds) || transitionSeconds <= 0f) return;
            float remaining = dt;
            bool changed = state.DisplayedId != state.SelectedId || state.DisplayedSlot != state.SelectedSlot;
            if (changed)
            {
                float lowerTime = state.Raise * transitionSeconds;
                if (remaining < lowerTime) { state.Raise -= remaining / transitionSeconds; remaining = 0f; }
                else
                {
                    remaining -= lowerTime; state.Raise = 0f;
                    state.DisplayedId = state.SelectedId; state.DisplayedSlot = state.SelectedSlot;
                }
            }
            if (state.DisplayedId == state.SelectedId && state.DisplayedSlot == state.SelectedSlot)
                state.Raise = state.DisplayedId.Length == 0 ? 0f : Math.Min(1f, state.Raise + remaining / transitionSeconds);
            if (Finite(swayPeriod) && swayPeriod > 0f)
                state.IdlePhase = (float)((state.IdlePhase + (double)dt / swayPeriod) % 1d);
        }
        public Vector3 Position(HeldItemDriverState state, Vector3 raisedPosition, float lowerDistance, float swayAmplitude)
        {
            double angle = state.IdlePhase * Math.PI * 2d;
            float eased = state.Raise * state.Raise * (3f - 2f * state.Raise);
            return raisedPosition + new Vector3((float)Math.Sin(angle) * swayAmplitude * eased,
                -(1f - eased) * lowerDistance + (float)Math.Sin(angle * 2d) * swayAmplitude * eased, 0f);
        }
        // Authored camera-local dimensions use a 60-degree vertical, 16:9 reference.
        // Preserve viewport placement and apparent size through player FOV/aspect changes.
        public float ProjectionScale(float verticalFov) => Finite(verticalFov) && verticalFov > 0f && verticalFov < 180f
            ? (float)(Math.Tan(verticalFov * Math.PI / 360d) / Math.Tan(Math.PI / 6d)) : 1f;
        public Vector3 ViewPosition(Vector3 position, float verticalFov, float aspect)
        {
            float scale = ProjectionScale(verticalFov);
            float horizontal = Finite(aspect) && aspect > 0f ? aspect / (16f / 9f) : 1f;
            return new Vector3(position.x * scale * horizontal, position.y * scale, position.z);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
