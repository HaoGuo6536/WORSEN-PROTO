// ============================================================================
// HorrorAfterglowDriverState.cs
// ============================================================================
// PURPOSE:
//   Stores independent dying-light envelopes for an assembled floor.
//   The records are presentation-only and never establish a room's safety state.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Presentation · Horror.
// KEY RESPONSIBILITIES:
//   - Retain light identity, placement and supplied visual lifetime.
// DEPENDENCIES:
//   Core immutable interactable observations and ordinary collections.
// USAGE NOTES:
//   Owned by HorrorAfterglowDriver; reset on floor release and owner disable.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Presentation.Horror
{
    public sealed class HorrorAfterglowDriverState
    {
        public readonly Dictionary<int, HorrorAfterglowLightDriverState> Lights = new Dictionary<int, HorrorAfterglowLightDriverState>();
        public readonly List<int> Expired = new List<int>();
    }
    public sealed class HorrorAfterglowLightDriverState
    {
        public InteractableState Light;
        public float Remaining;
        public float Duration;
    }
}
