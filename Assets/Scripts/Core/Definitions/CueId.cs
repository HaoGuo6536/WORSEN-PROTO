// ============================================================================
// CueId.cs
// ============================================================================
//
// PURPOSE:
//   Names the audio cues emitted by run and presentation event routing.
//   Values cross system boundaries without exposing mutable runtime state.
//
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Audio shared contracts.
//
// KEY RESPONSIBILITIES:
//   - Carry tick-stamped identity and immutable values between owning systems.
//   - Keep event payloads independent of Domain and Presentation implementations.
//
// DEPENDENCIES:
//   - Core definitions and pure UnityEngine value types only.
//
// USAGE NOTES:
//   Distances are metres and durations are seconds; ticks identify committed steps.
//   Constructors carry supplied values and perform no engine or gameplay operations.
//
// ============================================================================

using UnityEngine;

namespace Worsen.Core
{
    public enum CueId { Presence, Detection, Chase, Lose, Death, Footstep, ExitOpen, RoomTelegraph }
}
