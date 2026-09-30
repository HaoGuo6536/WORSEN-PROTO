// ============================================================================
// ExpeditionSessionBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores the identity of the floor being assembled and the entities it owns.
//   It contains no scene references, wallet, choices or health authority, so
//   replacing geometry cannot reset progression or retain destroyed objects.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Retain the pending request and assembly phase across the teardown yield.
//   - Track immutable room presentation and genuine portal crossings for floor-scoped marks.
//   - Track factory identities for complete, idempotent cleanup.
//   - Retain generation fallback evidence and capacity shortfalls after failed assembly.
// DEPENDENCIES:
//   - Core progression, scene and entity definitions; System collections.
// USAGE NOTES:
//   Persistent only through ExpeditionSessionManager. Its Controller alone
//   changes this data; other systems receive primitive Manager properties.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionSessionBehaviorState
    {
        public ExpeditionAssemblyPhase Phase { get; internal set; }
        public SceneKey Scene { get; internal set; }
        public int LastGenerationId { get; internal set; }
        public ProgressionGenerationRequest Request { get; internal set; }
        public EntityId Player { get; internal set; }
        internal IReadOnlyList<GeneratedRoomSample> Rooms = System.Array.Empty<GeneratedRoomSample>();
        internal bool HasPreviousPosition;
        internal UnityEngine.Vector3 PreviousPosition;
        internal int PreviousRoom;
        public string Failure { get; internal set; } = string.Empty;
        public bool UsedFallback { get; internal set; }
        public string LayoutManifest { get; internal set; } = string.Empty;
        public int HunterSpawnShortfall { get; internal set; }
        internal List<EntityId> Hunters { get; } = new List<EntityId>();
    }
}
