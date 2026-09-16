// ============================================================================
// ProgressionUIDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Stores the latest formatted menu and its one-action-per-revision latch.
//   Keeping these values outside live UI elements allows safe rebinding after
//   document recreation without dropping feedback or allowing repeated actions.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Retain display copies, revision and pending interaction state.
//
// DEPENDENCIES:
//   Core progression snapshots and own ProgressionUI stack only.
//
// USAGE NOTES:
//   Scene-owned through ProgressionUIDriver; data only, never authoritative rules.
//
// ============================================================================

using System;
using Worsen.Core;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUIDriverState
    {
        public bool HasSnapshot, Hidden, ModalVisible, Pending, CanContinue, CanRestart;
        public int Revision;
        public ProgressionPhase Phase;
        public string Title = "", Subtitle = "", RoundText = "", WalletText = "", HealthText = "";
        public string BurdenText = "", RetainedText = "", Message = "";
        public float HealthFraction;
        public ProgressionUICard[] Cards = Array.Empty<ProgressionUICard>();
    }
}
