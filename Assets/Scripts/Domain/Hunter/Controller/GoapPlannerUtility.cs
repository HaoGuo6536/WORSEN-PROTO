// ============================================================================
// GoapPlannerUtility.cs
// ============================================================================
// PURPOSE:
//   Finds the cheapest sequence of available Hunter actions that satisfies a goal.
//   Searching actual fact transitions allows blocked actions and alternate routes
//   to change the result, instead of presenting a fixed sequence as a plan.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Search Boolean fact masks using deterministic A* and reconstruct action ids.
//   - Report unreachable goals and expansion limits distinctly.
// DEPENDENCIES:
//   - Hunter-local plain action and result definitions; no other project systems.
// USAGE NOTES:
//   All mutable search data belongs to this invocation. Equal-cost routes follow
//   input action order. The heuristic is the cheapest action cost until the goal
//   is reached: admissible even when one action satisfies several goal facts.
//   This utility selects no goals and performs no actions; HunterController owns that.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Worsen.Domain.Hunter
{
    public static class GoapPlannerUtility
    {
        public static GoapPlanResult Plan(ulong initialFacts, ulong goalSet, ulong goalClear,
            IReadOnlyList<GoapActionDefinition> actions, int maximumExpandedStates = 512)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            if ((goalSet & goalClear) != 0) throw new ArgumentException("Goal masks conflict.");
            if (maximumExpandedStates < 1) throw new ArgumentOutOfRangeException(nameof(maximumExpandedStates));
            float minimumCost = float.PositiveInfinity;
            var ids = new HashSet<int>();
            foreach (GoapActionDefinition action in actions)
            {
                if ((action.RequireSet & action.RequireClear) != 0 || (action.SetEffects & action.ClearEffects) != 0 ||
                    float.IsNaN(action.Cost) || float.IsInfinity(action.Cost) || action.Cost < 0f || !ids.Add(action.Id))
                    throw new ArgumentException("Actions need unique ids, finite nonnegative costs and consistent masks.", nameof(actions));
                minimumCost = Math.Min(minimumCost, action.Cost);
            }
            if (Matches(initialFacts, goalSet, goalClear))
                return Result(GoapPlanStatus.AlreadySatisfied, 0);
            if (actions.Count == 0) return Result(GoapPlanStatus.Unreachable, 0);

            var open = new List<ulong> { initialFacts };
            var costs = new Dictionary<ulong, float> { [initialFacts] = 0f };
            var parents = new Dictionary<ulong, ulong>();
            var via = new Dictionary<ulong, int>();
            int expanded = 0;
            while (open.Count > 0)
            {
                int best = 0;
                float bestScore = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    ulong candidate = open[i];
                    float score = costs[candidate] + (Matches(candidate, goalSet, goalClear) ? 0f : minimumCost);
                    if (score < bestScore) { best = i; bestScore = score; }
                }
                ulong current = open[best];
                open.RemoveAt(best);
                if (Matches(current, goalSet, goalClear))
                {
                    var plan = new List<int>();
                    for (ulong node = current; node != initialFacts; node = parents[node]) plan.Add(via[node]);
                    plan.Reverse();
                    return new GoapPlanResult(GoapPlanStatus.Found, plan.ToArray(), costs[current], expanded);
                }
                if (expanded >= maximumExpandedStates) return Result(GoapPlanStatus.SearchLimitReached, expanded);
                expanded++;
                foreach (GoapActionDefinition action in actions)
                {
                    if (!Matches(current, action.RequireSet, action.RequireClear)) continue;
                    ulong next = (current & ~action.ClearEffects) | action.SetEffects;
                    float cost = costs[current] + action.Cost;
                    if (next == current || costs.TryGetValue(next, out float known) && cost >= known) continue;
                    costs[next] = cost;
                    parents[next] = current;
                    via[next] = action.Id;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return Result(GoapPlanStatus.Unreachable, expanded);
        }

        private static bool Matches(ulong facts, ulong required, ulong excluded)
            => (facts & required) == required && (facts & excluded) == 0;

        private static GoapPlanResult Result(GoapPlanStatus status, int expanded)
            => new GoapPlanResult(status, Array.Empty<int>(), 0f, expanded);
    }
}
