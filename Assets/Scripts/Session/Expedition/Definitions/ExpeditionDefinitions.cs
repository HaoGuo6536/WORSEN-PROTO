// ============================================================================
// ExpeditionDefinitions.cs
// ============================================================================
// PURPOSE:
//   Names the stages of replacing one generated floor with the next.
//   Progression owns the expedition's choices and rewards; these stages describe
//   only whether its requested physical floor can safely receive the Run tick.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Give pending generation, active assembly and admission distinct identities.
//   - Preserve a visible failure state instead of admitting partial geometry.
// DEPENDENCIES:
//   - None. Shared progression payloads remain in Core.
// USAGE NOTES:
//   This is internal assembly lifecycle data, not a second gameplay phase graph.
// ============================================================================
namespace Worsen.Session.Expedition
{
    public enum ExpeditionAssemblyPhase { Unbound, Waiting, Queued, Generating, Ready, Resolved, Failed }
}
