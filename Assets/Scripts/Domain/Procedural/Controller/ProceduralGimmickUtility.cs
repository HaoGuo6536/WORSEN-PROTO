// ============================================================================
// ProceduralGimmickUtility.cs
// ============================================================================
// PURPOSE:
//   Gives traversal obstacles, freeze situations and puzzles one shared budget.
//   Reserving that budget before geometry prevents independent feature passes
//   from filling early floors with obstacles or exceeding the depth curve.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Domain · Procedural.
// KEY RESPONSIBILITIES:
//   - Evaluate a bounded linear integer budget and classify occupied room slots.
//   - Reserve traversal rooms while leaving one slot for a freeze or puzzle.
// DEPENDENCIES:
//   - Own immutable plans and configuration only; no engine calls.
// USAGE NOTES:
//   Null challenge config preserves explicitly unwired legacy fixtures. The
//   deterministic setup wires the owner-approved pacing into playable configs.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace Worsen.Domain.Procedural
{
    public static class ProceduralGimmickUtility
    {
        public static int Budget(ProceduralChallengeConfig config, int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            if (config == null) return int.MaxValue;
            if (config.GimmickFirstRound < 3 || config.GimmickFullRound <= config.GimmickFirstRound ||
                config.GimmickInitialBudget < 0 || config.GimmickMaximumBudget < config.GimmickInitialBudget ||
                config.GimmickMaximumBudget > 256)
                throw new ArgumentException("Invalid gimmick-room pacing.");
            if (round < config.GimmickFirstRound) return 0;
            long depth = Math.Min(round, config.GimmickFullRound) - config.GimmickFirstRound;
            return config.GimmickInitialBudget + (int)(depth * (config.GimmickMaximumBudget - config.GimmickInitialBudget) /
                (config.GimmickFullRound - config.GimmickFirstRound));
        }

        public static bool IsTraversal(ProceduralRoomModule module) => module.TraversalObstacles &&
            module.Kind != ProceduralModuleKind.ExitHub && module.Kind != ProceduralModuleKind.MerchantRefuge;

        public static IReadOnlyCollection<int> Rooms(ProceduralLayout layout) => layout.Modules.Where(IsTraversal)
            .Select(m => m.RoomId).Concat(layout.Storeys.Select(s => s.RoomId))
            .Concat(layout.FreezeRooms.Select(f => f.RoomId)).Concat(layout.Puzzles.Select(p => p.RoomId))
            .Concat(layout.Doors.Where(d => d.IsOptional).SelectMany(d => new[] { d.FromRoomId, d.ToRoomId }))
            .Distinct().ToArray();

        public static ProceduralRoomModule[] Reserve(IReadOnlyList<ProceduralRoomModule> modules,
            ProceduralChallengeConfig config, int round, System.Random random)
        {
            if (config == null) return modules.ToArray();
            int budget = Budget(config, round);
            var candidates = modules.Where(m => m.PocketId == 0 && IsTraversal(m)).ToList();
            // Fisher-Yates: bounded work, independent of layout growth and theme draws.
            for (int i = candidates.Count - 1; i > 0; i--)
            { int j = random.Next(i + 1); var value = candidates[i]; candidates[i] = candidates[j]; candidates[j] = value; }
            var selected = new HashSet<int>(candidates.Take(Math.Max(0, budget - 1)).Select(m => m.RoomId));
            return modules.Select(m => new ProceduralRoomModule(m.RoomId, m.Kind, m.AlongX,
                m.Cells, m.PocketId, selected.Contains(m.RoomId))).ToArray();
        }
    }
}
