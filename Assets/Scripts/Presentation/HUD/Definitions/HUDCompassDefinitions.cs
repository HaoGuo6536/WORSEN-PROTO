// ============================================================================
// HUDCompassDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries projected triangle geometry and white facet shading to the HUD painter.
// ARCHITECTURAL ROLE:
//   Definitions (section 5) - Presentation - HUD.
// KEY RESPONSIBILITIES:
//   - Keep calculated geometry independent of UI Toolkit and live engine objects.
// DEPENDENCIES:
//   Unity value types only; owned by the HUD presentation stack.
// USAGE NOTES:
//   Immutable output from HUDCompassPresenter, consumed during a vector repaint.
// ============================================================================
using UnityEngine;
namespace Worsen.Presentation.HUD
{
    public readonly struct HUDCompassFace
    {
        public readonly Vector2 A, B, C;
        public readonly Color Color;
        public readonly float Depth;
        public HUDCompassFace(Vector2 a, Vector2 b, Vector2 c, Color color, float depth)
        { A = a; B = b; C = c; Color = color; Depth = depth; }
    }
}
