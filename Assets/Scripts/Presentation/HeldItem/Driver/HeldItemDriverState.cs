// ============================================================================
// HeldItemDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains the selected and displayed item independently during a handoff.
//   The old silhouette lowers before the replacement rises; inventory facts never
//   become authoritative here and no state survives the scene-owned service.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · HeldItem.
// KEY RESPONSIBILITIES:
//   - Retain selection identity, visible identity, raise amount and idle phase.
// DEPENDENCIES:
//   None; plain presentation copies only.
// USAGE NOTES:
//   Owned by HeldItemDriver. A hidden/modal view retains selection but resets pose.
// ============================================================================
namespace Worsen.Presentation.HeldItem
{
    public sealed class HeldItemDriverState
    {
        public string SelectedId = "", DisplayedId = "";
        public int SelectedSlot = -1, DisplayedSlot = -1;
        public float Raise, IdlePhase;
        public bool Suppressed;
    }
}
