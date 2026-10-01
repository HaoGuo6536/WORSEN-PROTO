// ============================================================================
// ConsumableBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains floor-local item lifetimes and the run-scoped flashlight charge.
//   This data is separate from curse schedules so revival can clear player effects
//   without restarting the collapse or silently refilling the flashlight.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Store flight sweeps, noise, contacts, charge duration, deadlines and revival identity.
// DEPENDENCIES:
//   - Core immutable identities/facts and Unity value types only.
// USAGE NOTES:
//   Owned by HorrorEffectsManager through ConsumableController. No engine calls.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.HorrorEffects
{
    public sealed class ConsumableBehaviorState
    {
        internal bool Active, Hold;
        internal long LastTick = -1;
        internal double Clock, RechargeRemaining, RechargeDuration, AimSeconds, GauzeRemaining, BurstRemaining;
        internal EntityId AimTarget, Reviving;
        internal int NextId;
        internal readonly Dictionary<int, (Vector3 Position, Vector3 Velocity, double Expires)> Flights = new Dictionary<int, (Vector3, Vector3, double)>();
        internal readonly Dictionary<int, (Vector3 Position, double Expires)> Patches = new Dictionary<int, (Vector3, double)>();
        internal readonly HashSet<(int Patch, EntityId Hunter)> OilContacts = new HashSet<(int, EntityId)>();
        internal readonly Dictionary<int, (Vector3 Position, double Expires)> Jams = new Dictionary<int, (Vector3, double)>();
        internal readonly List<HunterStunFact> Stuns = new List<HunterStunFact>();
        internal readonly List<HunterSlipFact> Slips = new List<HunterSlipFact>();
        internal readonly List<DoorJamFact> DoorFacts = new List<DoorJamFact>();
        internal readonly List<HorrorNoiseFact> Noises = new List<HorrorNoiseFact>();
    }
}
