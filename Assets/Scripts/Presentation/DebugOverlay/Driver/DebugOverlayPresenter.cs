// ============================================================================
// DebugOverlayPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Formats primitive status samples into readable development-overlay text.
//   Formatting stays outside the engine boundary so number precision, missing
//   samples, and player teardown can be verified without opening a scene.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · DebugOverlay.
//   Pure calculations update a supplied DebugOverlayDriverState for the Driver.
//
// KEY RESPONSIBILITIES:
//   - Use culture-independent number formatting so debug captures are comparable.
//   - Distinguish a real zero-speed sample from missing or invalid telemetry.
//   - Clear player presentation without discarding the current run status.
//
// DEPENDENCIES:
//   - No other project systems. System.Globalization supplies number formatting.
//
// USAGE NOTES:
//   - The caller owns all state; this Presenter holds no state and makes no engine calls.
//   - Speed is a nonnegative magnitude in metres per second, supplied by the owner.
//
// ============================================================================

using System.Globalization;

namespace Worsen.Presentation.DebugOverlay
{
    public sealed class DebugOverlayPresenter
    {
        public void SetRunStatus(DebugOverlayDriverState state, long tick, string phase)
        {
            state.TickText = "Tick: " + tick.ToString(CultureInfo.InvariantCulture);
            state.PhaseText = "Run: " + (string.IsNullOrWhiteSpace(phase) ? "unknown" : phase);
        }

        public void SetPlayerStatus(DebugOverlayDriverState state, float speed, string movement, int decimalPlaces)
        {
            var precision = System.Math.Clamp(decimalPlaces, 0, 3);
            var validSpeed = !float.IsNaN(speed) && !float.IsInfinity(speed) && speed >= 0f;
            state.SpeedText = validSpeed
                ? "Speed: " + speed.ToString("F" + precision, CultureInfo.InvariantCulture) + " m/s"
                : "Speed: —";
            state.MovementText = "Movement: " + (string.IsNullOrWhiteSpace(movement) ? "unknown" : movement);
        }

        public void SetPlayerUnavailable(DebugOverlayDriverState state)
        {
            state.SpeedText = "Speed: —";
            state.MovementText = "Movement: awaiting player (M1)";
        }
    }
}
