// ============================================================================
// IReadOnlyBlinderState.cs
// ============================================================================
// PURPOSE:
//   Exposes one Blinder's current attack observations without mutation access.
//   Consumers cannot drain facts or change the owning controller's runtime data.
// ARCHITECTURAL ROLE:
//   Definitions (§5), read-only BehaviorState view (§3) · Domain · Hunter Blinder.
// KEY RESPONSIBILITIES:
//   - Publish scalar attack and tick observations through getters only.
// DEPENDENCIES:
//   - Blinder action definitions only.
// USAGE NOTES:
//   BlinderController hands out this view; its lifetime is one controller instance.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public interface IReadOnlyBlinderState
    {
        BlinderAction Action { get; }
        long LastTick { get; }
        bool Warning { get; }
        bool Fire { get; }
    }
}
