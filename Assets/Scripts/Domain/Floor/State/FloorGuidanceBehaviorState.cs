// ============================================================================
// FloorGuidanceBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains false-cake poses and temporary Faithless Arrow windows for one floor.
//   This data is separate from real pickup identities, counters and collapse hands.
//   Session delivers immutable facts; only FloorGuidanceController changes this state.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Store poses and per-instance, player-scoped window expiry times.
//   - Retain fact watermarks and the injected active-effect view until reset.
// DEPENDENCIES:
//   - Core Mimic facts, entity identities and read-only effect contracts only.
// USAGE NOTES:
//   Scene-owned through FloorManager; no engine operations or event subscriptions.
//   No Mimic is registered as a real cake or optional golden reward.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public sealed class FloorGuidanceBehaviorState
    {
        internal readonly Dictionary<EntityId, MimicFact> Poses = new Dictionary<EntityId, MimicFact>();
        internal readonly Dictionary<EntityId, MimicFact> Windows = new Dictionary<EntityId, MimicFact>();
        internal readonly Dictionary<EntityId, double> Expiries = new Dictionary<EntityId, double>();
        internal readonly Dictionary<EntityId, long> LastTicks = new Dictionary<EntityId, long>();
        internal readonly Dictionary<EntityId, long> WindowTicks = new Dictionary<EntityId, long>();
        internal IReadOnlyActiveEffects Effects;
        internal double Elapsed;
    }
}
