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
//   - Track factory identities for complete, idempotent cleanup.
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
        public string Failure { get; internal set; } = string.Empty;
        internal List<EntityId> Hunters { get; } = new List<EntityId>();
    }
}
