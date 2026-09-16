// ============================================================================
// HUDGeometryPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Computes cut-corner panel and gauge geometry from supplied rectangles.
//   It keeps clipping and layout arithmetic independent of live UI objects,
//   so narrow or uninitialized bounds cannot produce inverted vector paths.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · HUD.
//
// KEY RESPONSIBILITIES:
//   - Return bounded panel vertices, gauge rectangles and item-slot positions.
//
// DEPENDENCIES:
//   Unity value types and System only; no engine calls.
//
// USAGE NOTES:
//   Stateless and pure; the drawing sub-driver supplies current layout values.
//
// ============================================================================

using System;
using UnityEngine;

namespace Worsen.Presentation.HUD
{
    public sealed class HUDGeometryPresenter
    {
        public Vector2[] Panel(Rect bounds, float cut)
        {
            float width = Positive(bounds.width), height = Positive(bounds.height);
            float corner = Math.Min(Positive(cut), Math.Min(width, height) * 0.5f);
            float x = Finite(bounds.x) ? bounds.x : 0f, y = Finite(bounds.y) ? bounds.y : 0f;
            return new[] { new Vector2(x + corner, y), new Vector2(x + width, y),
                new Vector2(x + width, y + height - corner), new Vector2(x + width - corner, y + height),
                new Vector2(x, y + height), new Vector2(x, y + corner) };
        }

        public Rect Gauge(Rect bounds, float fraction)
        {
            return new Rect(bounds.x, bounds.y, Positive(bounds.width) * (Finite(fraction) ? Mathf.Clamp01(fraction) : 0f), Positive(bounds.height));
        }

        public Rect Slot(int index, float size, float gap)
        {
            float edge = Positive(size);
            return new Rect(Math.Max(0, index) * (edge + Positive(gap)), 0f, edge, edge);
        }

        public Vector2[] Arrow(Rect bounds)
        {
            float width = Positive(bounds.width), height = Positive(bounds.height);
            return new[] { new Vector2(bounds.x + width * 0.5f, bounds.y),
                new Vector2(bounds.x + width * 0.88f, bounds.y + height),
                new Vector2(bounds.x + width * 0.5f, bounds.y + height * 0.7f),
                new Vector2(bounds.x + width * 0.12f, bounds.y + height) };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Positive(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    }
}
