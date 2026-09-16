// ============================================================================
// GoapDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes the facts, actions and outcomes exchanged with the Hunter planner.
//   Integer identifiers let the Hunter controller choose its own actions without
//   coupling this small search contract to sensing, motion or engine objects.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Carry immutable action requirements, effects and costs as plain data.
//   - Distinguish a completed search from an unreachable or bounded search.
// DEPENDENCIES:
//   - No other project systems.
// USAGE NOTES:
//   Each bit represents one Boolean fact. Set and clear masks must not overlap.
//   ActionIds preserves execution order; an already satisfied goal has no actions.
// ============================================================================
namespace Worsen.Domain.Hunter
{
    public readonly struct GoapActionDefinition
    {
        public readonly int Id;
        public readonly ulong RequireSet;
        public readonly ulong RequireClear;
        public readonly ulong SetEffects;
        public readonly ulong ClearEffects;
        public readonly float Cost;

        public GoapActionDefinition(int id, ulong requireSet, ulong requireClear, ulong setEffects, ulong clearEffects, float cost)
        {
            Id = id;
            RequireSet = requireSet;
            RequireClear = requireClear;
            SetEffects = setEffects;
            ClearEffects = clearEffects;
            Cost = cost;
        }
    }

    public enum GoapPlanStatus { Found, AlreadySatisfied, Unreachable, SearchLimitReached }

    public readonly struct GoapPlanResult
    {
        public readonly GoapPlanStatus Status;
        public readonly int[] ActionIds;
        public readonly float TotalCost;
        public readonly int ExpandedStates;

        public GoapPlanResult(GoapPlanStatus status, int[] actionIds, float totalCost, int expandedStates)
        {
            Status = status;
            ActionIds = actionIds;
            TotalCost = totalCost;
            ExpandedStates = expandedStates;
        }
    }
}
