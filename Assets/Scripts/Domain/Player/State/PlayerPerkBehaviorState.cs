// ============================================================================
// PlayerPerkBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores chase identity, contact allowances and the short delayed-footstep trail.
//   These values belong to one Player life, not to the retained upgrade catalogue.
//   Explicit resets prevent contacts and room allowances leaking across floors.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Retain chase admission, one body bounce and heartbeat delivery state.
//   - Retain bounded position history and rooms whose first sprint door was latched.
// DEPENDENCIES:
//   - Core noise values and pure Unity position values only.
// USAGE NOTES:
//   Passive data owned by PlayerPerkController; never independently persistent.
//   The Player controller resets this state on pooled life and floor boundaries.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public sealed class PlayerPerkBehaviorState
    {
        internal int ChaseId;
        internal int LastChaseId;
        internal long ChaseTick = -1;
        internal bool BodyBounceSpent;
        internal long NextHeartbeatTick;
        internal NoiseEvent? Heartbeat;
        internal readonly List<(long Tick, Vector3 Position)> Positions = new List<(long, Vector3)>();
        internal readonly HashSet<int> LatchedRooms = new HashSet<int>();
    }
}
