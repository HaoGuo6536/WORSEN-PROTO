// ============================================================================
// InputButtons.cs
// ============================================================================
//
// PURPOSE:
//   Names the gameplay buttons shared by input capture and fixed-tick consumers.
//   A bit mask preserves independent held, pressed, and released facts without
//   exposing devices or the Input System package outside Presentation.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Input contract.
//
// KEY RESPONSIBILITIES:
//   - Give each action a stable bit for recording and tick consumers.
//
// DEPENDENCIES:
//   - System.FlagsAttribute only; Core references no project systems.
//
// USAGE NOTES:
//   - These values are part of the input recording contract; do not renumber them.
//   - None is the neutral default. A frame may contain both press and release.
//
// ============================================================================

using System;

namespace Worsen.Core
{
    [Flags]
    public enum InputButtons
    {
        None = 0,
        Sprint = 1,
        Jump = 2,
        Crouch = 4,
        LookBack = 8,
        Interact = 16,
        UseItem = 32
    }
}

