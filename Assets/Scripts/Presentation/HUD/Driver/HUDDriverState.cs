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
//   - Retain remaining counter text, Hidden Count and phantom presentation lifetime.
//   - Retain independent objective/threat/Exit Sense guidance and supplied camera orientation.
//   - Retain shield and selected occupied inventory presentation.
//   - Retain chrome visibility and fade progress independently of guidance.
//
// DEPENDENCIES:
//   - No other project systems; values are presentation copies.
//
// USAGE NOTES:
//   - Scene-owned through HUDDriver; never authoritative gameplay data.
//
// ============================================================================

using UnityEngine;
using System.Collections.Generic;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDDriverState
    {
        public readonly Dictionary<EntityId, HUDThreatDriverState> Threats = new Dictionary<EntityId, HUDThreatDriverState>();
        public float Shield;
        public string ShieldText = "Shield: 0";
        public string CountText = "—";
        public int Collected = -1;
        public int Required = -1;
        public bool HiddenCount;
        public float PhantomSeconds;
        public bool GoldenSenseVisible;
        public Vector3 GoldenSenseDirection;
        public Vector3 GoldenSenseViewDirection;
        public float GoldenSenseDegrees, GoldenSensePitchDegrees, GoldenSenseArrowDegrees;
        public Worsen.Core.GuidanceTarget? ExitSenseTarget;
        public bool ExitSenseVisible;
        public Vector3 ExitSenseViewDirection;
        public float ExitSenseDegrees, ExitSensePitchDegrees, ExitSenseArrowDegrees;
        public string GoldenText = "—";
        public bool GoldenCountKnown;
        public Worsen.Core.GuidanceTarget? WhiteTarget, GoldenTarget;
        public bool ArrowInitialized, GoldenArrowInitialized;
        public float DisplayArrowDegrees, DisplayGoldenArrowDegrees;
        public float ArrowDegrees;
        public string ExitText = "Exit: —";
        public string DirectionCaption = "";
        public string SlotOverflowText = "";
        public int DisplayedSlots;
        public int SelectedDisplaySlot = -1;
        public string SelectedSlotText = "";
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
    public sealed class HUDThreatDriverState
    {
        public long Tick = -1;
        public Vector3 Direction;
        public bool Visible;
        public float ArrowDegrees;
    }
}
