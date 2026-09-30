// ============================================================================
// TickingDefinitions.cs
// ============================================================================
// PURPOSE:
//   Defines an optional, archetype-neutral dormancy gate for shared Hunter rules.
//   Sound, guidance and noise payloads live in Core for cross-layer routing.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype contracts.
// KEY RESPONSIBILITIES:
//   - Expose dormancy without coupling shared decisions to a concrete controller.
// DEPENDENCIES:
//   - No external dependencies.
// USAGE NOTES:
//   The shared controller tests the interface, never a concrete Ticking class.
// ============================================================================

namespace Worsen.Domain.Hunter.Archetypes.Ticking
{
    public interface IHunterDormancyRules { bool Dormant { get; } }

}
