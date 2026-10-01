// ============================================================================
// BlinderDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names the Blinder's local shot decisions. The shared independent-attack
//   contract lives in Hunter Definitions so no sibling imports Blinder rules.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Separate Reposition from a committed warned throw.
// DEPENDENCIES:
//   - No foreign systems or engine operations.
// USAGE NOTES:
//   Attack ownership is declared by IHunterIndependentAttackRules in the parent.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public enum BlinderAction { None, Reposition, Throw }

}
