// ============================================================================
// HUDDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains display samples and restoration progress independently of UI Toolkit.
//   Recreated documents can bind the same values without asking gameplay systems for state.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Store only transient UI text, direction, slot counts, gauge fill, and fade progress.
//
// DEPENDENCIES:
//   - No other project systems; values are presentation copies.
//
// USAGE NOTES:
//   - Scene-owned through HUDDriver; never authoritative gameplay data.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDDriverState
    {
        public string CountText = "Cakes: —";
        public string ExitText = "Exit: —";
        public string DirectionCaption = "NEXT CAKE";
        public string SlotOverflowText = "";
        public int DisplayedSlots;
        public float CountFraction;
        public bool CountKnown;
        public bool ExitOpen;
        public bool DirectionVisible;
        public Vector3 WorldDirection;
        public float HeadingDegrees;
        public float DirectionDegrees;
        public bool ChaseMode;
        public float ExtraOpacity = 1f;
    }
}
