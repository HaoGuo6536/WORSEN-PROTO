// ============================================================================
// FloorHandBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Retains one independently timed boundary grab per player and one-shot Wax Ward charges.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorHandBehaviorState
    {
        public readonly Dictionary<EntityId, FloorHandContactBehaviorState> Contacts = new Dictionary<EntityId, FloorHandContactBehaviorState>();
        public readonly HashSet<EntityId> WaxWards = new HashSet<EntityId>();
    }
    public sealed class FloorHandContactBehaviorState
    {
        public FloorHandPhase Phase;
        public float Elapsed;
        public int RoomId;
        public int HandId;
        public Vector3 Position;
        public Vector3 Outward;
        public Vector3 GrabOrigin;
        public bool AwaitingDamageResult;
        public long HitTick;
    }
}

