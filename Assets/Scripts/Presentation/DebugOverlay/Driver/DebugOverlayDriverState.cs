// ============================================================================
// DebugOverlayDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Retains the text most recently prepared for the development overlay. The
//   user interface can be recreated when its document is enabled, so keeping
//   the strings separately allows a new document root to show the same sample.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · DebugOverlay.
//   Passive presentation data updated by DebugOverlayPresenter and read by its Driver.
//
// KEY RESPONSIBILITIES:
//   - Store the four formatted status lines independently of UI Toolkit objects.
//   - Represent unavailable player telemetry explicitly until a player is wired.
//
// DEPENDENCIES:
//   - No other project systems or engine objects.
//
// USAGE NOTES:
//   - Owned by the persistent DebugOverlay Driver stack; not independently persistent.
//   - These strings are presentation data, never authoritative gameplay state.
//
// ============================================================================

namespace Worsen.Presentation.DebugOverlay
{
    public sealed class DebugOverlayDriverState
    {
        public string TickText = "Tick: 0";
        public string PhaseText = "Run: awaiting scene";
        public string SpeedText = "Speed: —";
        public string MovementText = "Movement: awaiting player (M1)";
    }
}
