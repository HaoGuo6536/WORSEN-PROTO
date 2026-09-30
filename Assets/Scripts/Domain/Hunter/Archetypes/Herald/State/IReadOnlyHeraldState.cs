// ============================================================================
// IReadOnlyHeraldState.cs
// ============================================================================
// PURPOSE:
//   Exposes one Herald's current warning and tick without mutation access.
//   Consumers cannot drain facts or change the owning controller's runtime data.
// ARCHITECTURAL ROLE:
//   Definitions (§5), read-only BehaviorState view (§3) · Domain · Hunter Herald.
// KEY RESPONSIBILITIES:
//   - Publish warning and tick observations through getters only.
// DEPENDENCIES:
//   - Primitive values only.
// USAGE NOTES:
//   HeraldController hands out this view; its lifetime is one controller instance.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Herald
{
    public interface IReadOnlyHeraldState
    {
        long LastTick { get; }
        bool Warning { get; }
    }
}
