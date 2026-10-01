// ============================================================================
// ExpeditionHunterBehaviorState.cs
// ============================================================================
// PURPOSE:
//   Stores floor-scoped hunter world evidence and delivery identities.
// ARCHITECTURAL ROLE:
//   BehaviorState (§3) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Retain world references, jam revisions and per-route committed sequences.
// DEPENDENCIES:
//   - Core contracts and System collections only.
// USAGE NOTES:
//   Mutated only by ExpeditionHunterController; recreated for every floor.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;
namespace Worsen.Session.Expedition
{
    public sealed class ExpeditionHunterBehaviorState
    {
        internal long Floor;
        internal long Revision;
        internal LevelGraph Graph;
        internal IReadOnlyInteractableSet Interactables;
        internal readonly Dictionary<int, HunterDoorJam> Jams = new Dictionary<int, HunterDoorJam>();
        internal readonly HashSet<EntityId> BoundHunters = new HashSet<EntityId>();
        internal readonly Dictionary<(SkipRouteKind, int), long> Routes = new Dictionary<(SkipRouteKind, int), long>();
    }
}
