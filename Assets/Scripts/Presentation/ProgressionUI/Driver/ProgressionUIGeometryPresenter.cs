// ============================================================================
// ProgressionUIGeometryPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Computes vector chrome, health fill and small choice emblems from plain bounds.
//   The drawing drivers use these paths at any panel resolution. Clamping remains
//   testable without asking a live VisualElement for layout.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · ProgressionUI.
//
// KEY RESPONSIBILITIES:
//   - Return finite, bounded panel vertices, fill widths and recognizable emblems.
//
// DEPENDENCIES:
//   Own display action enum, System and Unity value types only.
//
// USAGE NOTES:
//   Stateless and pure. No gameplay decisions or engine calls.
//
// ============================================================================

using System;
using UnityEngine;

namespace Worsen.Presentation.ProgressionUI
{
    public sealed class ProgressionUIGeometryPresenter
    {
        public Vector2[] Panel(Rect bounds, float cut)
        {
            float width = Positive(bounds.width), height = Positive(bounds.height);
            float corner = Math.Min(Positive(cut), Math.Min(width, height) * 0.5f);
            return new[] { new Vector2(corner, 0), new Vector2(width, 0),
                new Vector2(width, height - corner), new Vector2(width - corner, height),
                new Vector2(0, height), new Vector2(0, corner) };
        }

        public Rect Gauge(Rect bounds, float fraction)
        {
            float amount = Finite(fraction) ? Math.Max(0f, Math.Min(1f, fraction)) : 0f;
            return new Rect(0, 0, Positive(bounds.width) * amount, Positive(bounds.height));
        }

        public Vector2[] Emblem(Rect bounds, ProgressionUIAction kind)
        {
            Vector2[] normalized;
            if (kind == ProgressionUIAction.Purchase)
                normalized = new[] { new Vector2(.4f,.1f),new Vector2(.6f,.1f),new Vector2(.6f,.4f),
                    new Vector2(.9f,.4f),new Vector2(.9f,.6f),new Vector2(.6f,.6f),new Vector2(.6f,.9f),
                    new Vector2(.4f,.9f),new Vector2(.4f,.6f),new Vector2(.1f,.6f),new Vector2(.1f,.4f),new Vector2(.4f,.4f) };
            else if (kind == ProgressionUIAction.ChooseCurse)
                normalized = new[] { new Vector2(.5f,.05f),new Vector2(.62f,.38f),new Vector2(.95f,.38f),
                    new Vector2(.69f,.59f),new Vector2(.8f,.94f),new Vector2(.5f,.73f),
                    new Vector2(.2f,.94f),new Vector2(.31f,.59f),new Vector2(.05f,.38f),new Vector2(.38f,.38f) };
            else
                normalized = new[] { new Vector2(.05f,.5f),new Vector2(.5f,.15f),
                    new Vector2(.95f,.5f),new Vector2(.5f,.85f) };
            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = new Vector2(normalized[i].x * Positive(bounds.width), normalized[i].y * Positive(bounds.height));
            return normalized;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Positive(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    }
}
