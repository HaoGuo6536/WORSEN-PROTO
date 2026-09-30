// ============================================================================
// BlinderDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names the Blinder's local shot decisions and the optional attack-ownership seam.
//   Modules with independent weapons can keep shared pursuit without accidentally
//   starting a simultaneous shared lunge. Unimplemented modules remain unchanged.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter archetype rules.
// KEY RESPONSIBILITIES:
//   - Separate Reposition from a committed warned throw.
// DEPENDENCIES:
//   - No foreign systems or engine operations.
// USAGE NOTES:
//   The Herald also consumes this Hunter-local optional interface; no Core change.
// ============================================================================
namespace Worsen.Domain.Hunter.Archetypes.Blinder
{
    public enum BlinderAction { None, Reposition, Throw }
    public interface IHunterIndependentAttackRules
    {
        bool AllowSharedAttack { get; }
    }
}
