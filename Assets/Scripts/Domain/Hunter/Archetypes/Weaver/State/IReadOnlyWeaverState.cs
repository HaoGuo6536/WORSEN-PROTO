// ============================================================================
// IReadOnlyWeaverState.cs
// ============================================================================
// PURPOSE:
//   Exposes one Weaver's current attack observations without mutation access.
//   Consumers cannot drain facts or change the owning controller's runtime data.
// ARCHITECTURAL ROLE:
//   Definitions (§5), read-only BehaviorState view (§3) · Domain · Hunter Weaver.
// KEY RESPONSIBILITIES:
//   - Publish scalar attack and tick observations through getters only.
// DEPENDENCIES:
//   - Weaver action definitions only.
// USAGE NOTES:
//   WeaverController hands out this view; its lifetime is one controller instance.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Weaver
{
    public interface IReadOnlyWeaverState
    {
        WeaverAction Action { get; }
        long LastTick { get; }
        bool Warning { get; }
        bool Fire { get; }
        bool Hold { get; }
        bool Ceiling { get; }
    }
}
