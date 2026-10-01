// ============================================================================
// HunterAnimationDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names visual phases independently of shared lunge damage phases. Archetype
//   modules can request a ready pose, charge gait or reaction without inventing
//   a gameplay attack or changing its contact admission.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Define module animation commands consumed by the Hunter presentation stack.
// DEPENDENCIES:
//   - None; this is a Hunter-local command, not an outward event payload.
// USAGE NOTES:
//   None defers to the shared lunge/locomotion path. Hit is never lunge recovery.
// ============================================================================
namespace Worsen.Domain.Hunter
{
    public enum HunterAnimationPhase { None, Ready, Attack, Recovery, Hit, Run }
}
