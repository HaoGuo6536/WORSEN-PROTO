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
//   - Store quiet golden/count text, flat arrow rotation, occupied slots and fade progress.
//   - Retain presenter-computed chrome visibility separately from guidance visibility.
//   - Retain a supplied camera orientation and full three-dimensional compass direction.
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
        public string GoldenText = "Golden: —";
        public float ArrowDegrees;
        public string ExitText = "Exit: —";
        public string DirectionCaption = "";
        public string SlotOverflowText = "";
        public int DisplayedSlots;
        public float CountFraction;
        public bool CountKnown;
        public bool ExitOpen;
        public bool DirectionVisible;
        public Vector3 WorldDirection;
        public float HeadingDegrees;
        public float DirectionDegrees;
        public float DirectionPitchDegrees;
        public bool HasViewRotation;
        public Quaternion ViewRotation = Quaternion.identity;
        public Vector3 ViewDirection;
        public bool ChaseMode;
        public bool ChromeVisible = true;
        public float ExtraOpacity = 1f;
    }
}
