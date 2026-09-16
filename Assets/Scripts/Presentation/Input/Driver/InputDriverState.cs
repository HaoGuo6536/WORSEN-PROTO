// ============================================================================
// InputDriverState.cs
// ============================================================================
//
// PURPOSE:
//   Holds the input values waiting for a fixed tick and the input gating flags.
//   The Driver collects device facts and the Presenter updates this data, so
//   transitions and look accumulation can be verified without loading a scene.
//
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Input.
//
// KEY RESPONSIBILITIES:
//   - Retain movement and held buttons while accumulating consumable edges.
//   - Track focus, owner availability, and the requested input gate.
//   - Retain the cursor state that this service must restore during teardown.
//
// DEPENDENCIES:
//   - Core InputButtons and UnityEngine.Vector2/CursorLockMode value types only.
//
// USAGE NOTES:
//   - Persistent runtime data owned by PlayerInputDriver; never an asset.
//   - Input starts gated until scene assembly explicitly signals readiness.
//   - Reset pending data whenever any input gate closes.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Input
{
    public sealed class InputDriverState
    {
        public bool InputEnabled;
        public bool OwnerEnabled;
        public bool HasFocus = true;
        public bool OwnsCursorState;
        public CursorLockMode PreviousCursorLockMode;
        public bool PreviousCursorVisible;
        public Vector2 Move;
        public Vector2 LookDelta;
        public Vector2 GamepadLook;
        public InputButtons Held;
        public InputButtons Pressed;
        public InputButtons Released;
    }
}
