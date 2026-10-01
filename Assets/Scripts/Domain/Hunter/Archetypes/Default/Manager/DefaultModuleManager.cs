// ============================================================================
// DefaultModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers the null-config compatibility module for the shared Hunter rules.
//   It needs no specialised engine work and inherits the neutral lifecycle hooks.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Default.
// KEY RESPONSIBILITIES:
//   - Register per-life Default rules without a config type fallback.
// DEPENDENCIES:
//   - Parent Hunter module contracts and the local Default controller.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, HunterManager owns tick and teardown.
//   No subscriptions, independent state, time source or random draws.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Default
{
    public sealed class DefaultModuleManager : HunterArchetypeManager, Worsen.Core.IEntityHandle
    {
        public static void Register(HunterArchetypeFactory factory)
            => factory.RegisterDefault<DefaultModuleManager>((profile, random) => new DefaultHunterController());
    }
}
