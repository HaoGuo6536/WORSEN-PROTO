// ============================================================================
// ExpeditionFloorEffectUtility.cs
// ============================================================================
// PURPOSE:
//   Translates the active catalogue view to floor-scoped Domain hooks at assembly.
// ARCHITECTURAL ROLE:
//   Utility (§2b) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Use exact registered ids; snapshot presence without inventing absent Blinder keys.
// DEPENDENCIES:
//   Core active-effect view and Domain Floor immutable hooks only.
// USAGE NOTES:
//   Stateless and default-off. More Traps and Silent Traps have no catalogue rows
//   yet, so those FloorCakeHooks remain false until their owner registers ids.
// ============================================================================
using Worsen.Core;
using Worsen.Domain.Floor;

namespace Worsen.Session.Expedition
{
    public static class ExpeditionFloorEffectUtility
    {
        public static FloorCakeHooks CakeHooks(IReadOnlyActiveEffects effects) => new FloorCakeHooks(
            sweetTooth: Has(effects, "sweet-tooth"), blindFaith: Has(effects, "blind-faith"),
            goldenSense: Has(effects, "golden-sense"), greedyDoor: Has(effects, "greedy-door"));
        public static bool FasterCollapse(IReadOnlyActiveEffects effects) => Has(effects, "faster-collapse");
        public static bool ShuffledCollapse(IReadOnlyActiveEffects effects) => Has(effects, "shuffled-collapse");
        public static bool WaxHeart(IReadOnlyActiveEffects effects) => Has(effects, "wax-heart");
        private static bool Has(IReadOnlyActiveEffects effects, string id) => effects != null && effects.Has(new EffectId(id));
    }
}
