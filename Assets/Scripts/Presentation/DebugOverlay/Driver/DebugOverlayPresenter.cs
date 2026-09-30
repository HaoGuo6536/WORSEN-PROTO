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
//   - Format retained warning/grab/escape/hit/release/consumption facts per room and tick.
//   - Use culture-independent number formatting so debug captures are comparable.
//   - Distinguish a real zero-speed sample from missing or invalid telemetry.
//   - Clear player presentation without discarding the current run status.
//
// DEPENDENCIES:
//   - Core hand facts only. System.Globalization supplies number formatting.
//
// USAGE NOTES:
//   - The caller owns all state; this Presenter holds no state and makes no engine calls.
//   - Speed is a nonnegative magnitude in metres per second, supplied by the owner.
//
// ============================================================================

using System.Globalization;
using System.Text;
using Worsen.Core;

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

        public void SetCollapseHand(DebugOverlayDriverState state, CollapseHandFact fact)
        {
            // Ward cancellation and lethal consumption may be nested inside the original event.
            if (state.Hands.TryGetValue(fact.RoomId, out var previous) &&
                (previous.Tick > fact.Tick || previous.Tick == fact.Tick && previous.Kind > fact.Kind)) return;
            state.Hands[fact.RoomId] = fact;
            var text = new StringBuilder("Hands:");
            foreach (var entry in state.Hands)
                text.Append("\nRoom ").Append(entry.Key.ToString(CultureInfo.InvariantCulture)).Append(": ")
                    .Append(entry.Value.Kind.ToString()).Append(" @ ").Append(entry.Value.Tick.ToString(CultureInfo.InvariantCulture));
            state.HandsText = text.ToString();
        }

        public void ResetHands(DebugOverlayDriverState state) { state.Hands.Clear(); state.HandsText = "Hands: —"; }

        public void SetPlayerUnavailable(DebugOverlayDriverState state)
        {
            state.SpeedText = "Speed: —";
            state.MovementText = "Movement: awaiting player (M1)";
        }
    }
}
