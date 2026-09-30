// ============================================================================
// ExpeditionLightBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores floor-local Mannequin overrides and the lights they temporarily own.
// ARCHITECTURAL ROLE:
//   BehaviorState (§1) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Retain room timers, absolute lamp budget and restoration snapshots.
// DEPENDENCIES:
//   - Core facts and standard collections.
// USAGE NOTES:
//   Recreated per floor; duplicate hunters share one absolute budget.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionLightBehaviorState
    {
        public readonly Dictionary<int, MannequinFact> Rooms = new Dictionary<int, MannequinFact>();
        public readonly Dictionary<int, float> Remaining = new Dictionary<int, float>();
        public readonly Dictionary<int, bool> Restore = new Dictionary<int, bool>();
        public float LampBudget = 1f;
    }
}
