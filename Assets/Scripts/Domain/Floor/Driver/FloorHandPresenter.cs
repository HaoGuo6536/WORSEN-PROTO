// ============================================================================
// FloorHandPresenter.cs
// ============================================================================
// PURPOSE:
//   Selects a cosmetic scale for collapse hands from the published theme look.
//   It never receives hazard state, so appearance cannot alter reach or timing.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Preserve castle and unknown looks; scale provisional hospital glove silhouettes.
// DEPENDENCIES:
//   - Primitive look tags and injected FloorDriverConfig values only.
// USAGE NOTES:
//   Pure and stateless. RoomCollapseVolume applies this only to non-colliding art.
// ============================================================================
namespace Worsen.Domain.Floor
{
    public static class FloorHandPresenter
    {
        public static float VisualScale(string look, float baseScale, float glovedMultiplier)
            => baseScale * (look == "gloved-shadow-hands" ? glovedMultiplier : 1f);
    }
}
