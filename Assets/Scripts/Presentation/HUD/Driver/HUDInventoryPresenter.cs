// ============================================================================
// HUDInventoryPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes a stable three-slot inventory row and a separate flashlight readout.
//   Physical indices never compact after consumption, so a highlight always means
//   the same slot that Progression will consume. Charge comes from gameplay, not time.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
// KEY RESPONSIBILITIES:
//   - Format three physical slots, selection and explicit overflow diagnostics.
//   - Compute selected geometry and dimming without engine objects.
//   - Format supplied flashlight charge/aim and animate only its ready pulse.
// DEPENDENCIES:
//   Core inventory snapshots, own HUDDriverState/Config and Unity value types.
// USAGE NOTES:
//   Stateless; the owner injects pulse time. Unknown flashlight data never claims ready.
//   Bigger Pockets capacity is owned by Progression, not this fixed display contract.
// ============================================================================
using System;
using System.Globalization;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDInventoryPresenter
    {
        public const int ItemSlotCount = 3;

        public void SetSlots(HUDDriverState state, ConsumableInventorySnapshot snapshot)
        {
            state.DisplayedSlots = ItemSlotCount;
            int count = snapshot.Inventory?.Count ?? 0;
            int selected = snapshot.SelectedIndex;
            state.SelectedDisplaySlot = selected >= 0 && selected < Math.Min(count, ItemSlotCount) ? selected : -1;
            for (int i = 0; i < ItemSlotCount; i++) state.SlotLabels[i] = Label(snapshot, i);
            state.SelectedSlotText = selected >= 0 && selected < count
                ? (selected + 1).ToString(CultureInfo.InvariantCulture) + ": " + Label(snapshot, selected) : "";
            state.SlotOverflowText = count > ItemSlotCount
                ? "+" + (count - ItemSlotCount).ToString(CultureInfo.InvariantCulture) + " slots outside HUD" : "";
        }

        private static string Label(ConsumableInventorySnapshot snapshot, int index)
        {
            if (snapshot.Inventory == null || index >= snapshot.Inventory.Count ||
                string.IsNullOrEmpty(snapshot.Inventory[index].Id)) return "Empty";
            var item = snapshot.Inventory[index];
            string title = string.IsNullOrEmpty(item.Title) ? item.Id : item.Title;
            return title + (snapshot.RemainingUses != null && index < snapshot.RemainingUses.Count
                ? " ×" + Math.Max(0, snapshot.RemainingUses[index]).ToString(CultureInfo.InvariantCulture) : "");
        }

        public Rect SlotRect(int index, bool selected, float width, float height, float gap, float selectedScale)
        {
            float scale = selected ? selectedScale : 1f;
            return new Rect(index * (width + gap) - width * (scale - 1f) * .5f,
                -height * (scale - 1f) * .5f, width * scale, height * scale);
        }

        public float FlashlightLeft(float width, float gap, float separation) => ItemSlotCount * width + (ItemSlotCount - 1) * gap + separation;
        public float SlotOpacity(bool selected, float dimOpacity) => selected ? 1f : dimOpacity;
        public bool FlashlightReady(HUDDriverState state) => state.FlashlightKnown && state.FlashlightCharge >= 1f;

        public void SetFlashlight(HUDDriverState state, bool enabled, float charge, float aim)
        {
            state.FlashlightKnown = true;
            state.FlashlightOn = enabled;
            state.FlashlightCharge = Fraction(charge);
            state.FlashlightAim = enabled && state.FlashlightCharge >= 1f ? Fraction(aim) : 0f;
            state.FlashlightText = enabled ? "Flashlight ON" : "Flashlight OFF";
            state.FlashlightStatusText = state.FlashlightAim > 0f ? "Aim " + Percent(state.FlashlightAim)
                : FlashlightReady(state) ? "Stun ready" : "Recharge " + Percent(state.FlashlightCharge);
            if (!FlashlightReady(state)) state.FlashlightPulsePhase = 0f;
        }

        public void Tick(HUDDriverState state, float dt, float pulseSeconds)
        {
            if (!Finite(dt) || dt <= 0f || !Finite(pulseSeconds) || pulseSeconds <= 0f || !FlashlightReady(state)) return;
            state.FlashlightPulsePhase = (float)((state.FlashlightPulsePhase + (double)dt / pulseSeconds) % 1d);
        }

        public float ReadyPulse(HUDDriverState state) => FlashlightReady(state)
            ? (float)(.5d + .5d * Math.Cos(state.FlashlightPulsePhase * Math.PI * 2d)) : 0f;
        private static string Percent(float fraction) => Math.Round(fraction * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        private static float Fraction(float value) => Finite(value) ? Mathf.Clamp01(value) : 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
