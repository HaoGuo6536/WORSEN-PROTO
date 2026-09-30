// ============================================================================
// ExpeditionFloorEffectUtility.cs
// ============================================================================
// PURPOSE:
//   Translates the active catalogue view to floor-scoped Domain hooks at assembly.
//   Explicit tuning and a separate seeded stream keep rewards and extra hunters reproducible.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Use exact registered ids; snapshot presence without inventing absent Blinder keys.
//   - Add one roster draw per Nothing stack without changing the retained hunter list.
// DEPENDENCIES:
//   Core active-effect view and Domain Floor immutable hooks only.
// USAGE NOTES:
//   Stateless and default-off. More Traps and Silent Traps have no catalogue rows
//   yet, so those FloorCakeHooks remain false until their owner registers ids.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using Worsen.Core;
using Worsen.Domain.Floor;

namespace Worsen.Session.Expedition
{
    public static class ExpeditionFloorEffectUtility
    {
        public static FloorCakeHooks CakeHooks(IReadOnlyActiveEffects effects, float fasterCollapseGoldenCakeMultiplier = 1f) => new FloorCakeHooks(
            sweetTooth: Has(effects, "sweet-tooth"), blindFaith: Has(effects, "blind-faith"),
            goldenSense: Has(effects, "golden-sense"), greedyDoor: Has(effects, "greedy-door"),
            hiddenCount: Has(effects, "hidden-count"),
            goldenCakeMultiplier: FasterCollapse(effects) ? fasterCollapseGoldenCakeMultiplier : 1f);
        public static IReadOnlyList<string> ExtraHunters(IReadOnlyActiveEffects effects, IReadOnlyList<string> roster, int runSeed, int round)
        {
            int count = effects == null ? 0 : Math.Max(0, effects.Stacks(new EffectId("nothing")));
            if (count == 0) return Array.Empty<string>();
            var pool = roster == null ? Array.Empty<string>() : roster.Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (pool.Length == 0) throw new InvalidOperationException("Nothing requires an available hunter roster.");
            var random = new Random(unchecked(runSeed ^ round * 397 ^ 0x4E4F5448));
            var result = new string[count];
            for (int index = 0; index < count; index++) result[index] = pool[random.Next(pool.Length)];
            return Array.AsReadOnly(result);
        }
        public static bool FasterCollapse(IReadOnlyActiveEffects effects) => Has(effects, "faster-collapse");
        public static bool ShuffledCollapse(IReadOnlyActiveEffects effects) => Has(effects, "shuffled-collapse");
        public static bool WaxHeart(IReadOnlyActiveEffects effects) => Has(effects, "wax-heart");
        private static bool Has(IReadOnlyActiveEffects effects, string id) => effects != null && effects.Has(new EffectId(id));
    }
}
