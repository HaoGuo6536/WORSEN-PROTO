// ============================================================================
// IReadOnlyFloorState.cs
// ============================================================================
// PURPOSE:
//   Exposes collection and room facts without granting other systems mutation access.
//   This is the scene-owned Floor collection and collapse loop. Explicit data
//   inputs make its seeded behavior reproducible and its ownership reviewable.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Implement the Floor responsibility named by this file.
//   - Keep rules, passive state and engine operations in their owning roles.
// DEPENDENCIES:
//   - Core floor and level contracts; Floor owns all mutable data in this file.
//   - Floor reads injected Level and Player views; no Session or Presentation dependency.
// USAGE NOTES:
//   Typed Domain view consumed by Director and Session. Lists and dictionaries are read-only wrappers.
//   No persistent singleton or competing simulation tick is created.
// ============================================================================
using System.Collections.Generic;
using Worsen.Core;

namespace Worsen.Domain.Floor
{
    public interface IReadOnlyFloorState
    {
        bool IsReady { get; }
        int CakeCount { get; }
        int RequiredCakeCount { get; }
        int GoldenCakeCount { get; }
        ExitState ExitState { get; }
        IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; }
        IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; }
    }
}