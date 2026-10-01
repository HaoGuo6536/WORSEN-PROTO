// ============================================================================
// HUDLayoutPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes safe-area positions for the compact HUD in panel coordinates.
//   Inventory stays above the guidance band, so wide rows never cover the arrow
//   or its count. Resizing does not depend on a particular display aspect ratio.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
// KEY RESPONSIBILITIES:
//   - Reserve a proportional inset and separate inventory, caption and guidance bands.
//   - Place persistent health away from the lower guidance and held-item region.
// DEPENDENCIES:
//   Unity value types and System math only; all dimensions are supplied.
// USAGE NOTES:
//   Stateless, pure panel-space math. PanelSettings must use Expand scaling.
// ============================================================================
using System;
using UnityEngine;
namespace Worsen.Presentation.HUD
{
    public sealed class HUDLayoutPresenter
    {
        public Rect SafeRect(float width, float height, float inset)
        {
            float fraction = Math.Max(0f, Math.Min(.15f, inset));
            return new Rect(width * fraction, height * fraction, width * (1f - 2f * fraction), height * (1f - 2f * fraction));
        }
        public Rect Arrow(Rect safe, float size) => new Rect(safe.center.x - size * .5f, safe.yMax - size, size, size);
        public Rect Inventory(Rect safe, float width, float height, float arrowSize, float countClearance, float countFont, float gap)
            => new Rect(safe.xMin, safe.yMax - countClearance - countFont - gap - height, Math.Min(width, safe.width), height);
        public Rect Caption(Rect safe, float width, float height, float arrowSize)
            => new Rect(safe.xMin, safe.yMax - arrowSize - height, Math.Min(width, safe.width), height);
        public Rect Health(Rect safe, float width, float height) => new Rect(safe.xMin, safe.yMin, Math.Min(width, safe.width), height);
    }
}
