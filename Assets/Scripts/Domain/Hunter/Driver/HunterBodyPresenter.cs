// ============================================================================
// HunterBodyPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes which physics layers a Hunter's own queries leave out. Hunters
//   ignore each other's bodies (owner, 2026-10-01), so the HunterBody layer is
//   removed from motor, sight, attack, web and foot probe masks, and the motor
//   keeps passing through its route-gate layer. Plain integers keep this mask
//   math reproducible in tests without a scene or provisioned project layers.
// ARCHITECTURAL ROLE:
//   Presenter (section 7b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Remove one layer's bit from a configured query mask, leaving every other bit.
//   - Treat a missing (-1) or out-of-range layer index as nothing to remove.
// DEPENDENCIES:
//   - None; HunterDriver and its sub-drivers resolve layer names and run the queries.
// USAGE NOTES:
//   No engine calls or owned mutable state; callers supply resolved layer indices.
//   Only the named layers are removed, so walls and the player still block or hit.
// ============================================================================
namespace Worsen.Domain.Hunter
{
    public sealed class HunterBodyPresenter
    {
        public int WithoutLayer(int mask, int layer) => layer >= 0 && layer < 32 ? mask & ~(1 << layer) : mask;
        public int WithoutLayers(int mask, int routeGateLayer, int hunterBodyLayer)
            => WithoutLayer(WithoutLayer(mask, routeGateLayer), hunterBodyLayer);
    }
}
