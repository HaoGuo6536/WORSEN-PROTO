// ============================================================================
// InputFrame.cs
// ============================================================================
//
// PURPOSE:
//   Carries a single immutable input sample across the architecture layers.
//   Movement and held buttons persist between ticks; look motion and button
//   edges describe only the interval since the preceding published frame.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Input contract.
//
// KEY RESPONSIBILITIES:
//   - Carry movement, look delta, and separate held/pressed/released masks.
//
// DEPENDENCIES:
//   - UnityEngine.Vector2 as a value type only; no engine interactions.
//
// USAGE NOTES:
//   - LookDelta is angular motion in degrees, not an angular rate.
//   - Pressed and Released may both contain a button tapped between ticks.
//   - The default frame is neutral input.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public readonly struct InputFrame
    {
        public InputFrame(Vector2 move, Vector2 lookDelta, InputButtons held,
            InputButtons pressed, InputButtons released)
        {
            Move = move;
            LookDelta = lookDelta;
            Held = held;
            Pressed = pressed;
            Released = released;
        }

        public Vector2 Move { get; }
        public Vector2 LookDelta { get; }
        public InputButtons Held { get; }
        public InputButtons Pressed { get; }
        public InputButtons Released { get; }
    }
}

