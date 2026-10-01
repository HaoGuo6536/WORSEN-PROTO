// ============================================================================
// EchoModuleManager.cs
// ============================================================================
// PURPOSE:
//   Registers Echo's recording rules with the Hunter factory. Ordered replay
//   remains on the shared replay contract, so no specialised driver tick is needed.
// ARCHITECTURAL ROLE:
//   Manager (§1), Entity system facet · Domain · Hunter Echo.
// KEY RESPONSIBILITIES:
//   - Register a fresh recording controller for each entity life.
// DEPENDENCIES:
//   - Parent Hunter module contracts and local Echo config/controller.
// USAGE NOTES:
//   Scene-owned; factory creates the facet, HunterManager owns tick and teardown.
//   No subscriptions or independent state. Shared replay acknowledgements are unchanged.
//   IEntityHandle uses the root identity inherited through HunterArchetypeManager.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Echo
{
    public sealed class EchoModuleManager : HunterArchetypeManager, Worsen.Core.IEntityHandle
    {
        public static void Register(HunterArchetypeFactory factory)
            => factory.Register<EchoConfig, EchoModuleManager>((config, profile, random) => new EchoController(config));
    }
}
