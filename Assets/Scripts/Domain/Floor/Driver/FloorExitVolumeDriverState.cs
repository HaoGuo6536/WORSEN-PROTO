// ============================================================================
// FloorExitVolumeDriverState.cs
// ============================================================================
// PURPOSE:
//   Retains collider membership and resolved identities for the legacy exit volume.
//   Cached identities survive a collider being destroyed before Unity reports exit.
// ARCHITECTURAL ROLE:
//   DriverState (§7c) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Hold per-entity overlap sets and the owned trigger reference without engine calls.
// DEPENDENCIES:
//   - Core EntityId and passive Unity collider references only.
// USAGE NOTES:
//   Scene-owned by FloorExitVolume; cleared on disable and reconfiguration.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public sealed class FloorExitVolumeDriverState
    {
        public BoxCollider Trigger;
        public readonly Dictionary<EntityId, HashSet<Collider>> Contacts = new Dictionary<EntityId, HashSet<Collider>>();
    }
}
