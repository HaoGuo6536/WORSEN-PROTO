// ============================================================================
// HunterAnimationPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes the Hunter presentation calculation named by this file.
//   Plain values separate geometry, animation or route admission math from Unity
//   engine calls, making boundary conditions independently reproducible in tests.
// ARCHITECTURAL ROLE:
//   Presenter (section 7b) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Keep per-life state separate from shared configuration and foreign systems.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   No engine calls or owned mutable state; caller supplies all inputs and time.
// ============================================================================
using UnityEngine;
namespace Worsen.Domain.Hunter
{
    public sealed class HunterAnimationPresenter
    {
        public int Tick(HunterAnimationDriverState state, float dt, float speed, int phase, float runThreshold, float blendSeconds)
        {
            int slot = phase >= 1 && phase <= 3 ? phase + 2 : speed < 0.1f ? 0 : speed < runThreshold ? 1 : 2;
            float step = blendSeconds <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, dt) / blendSeconds);
            float total = 0f;
            for (int i = 0; i < 6; i++)
            { state.Weights[i] = Mathf.MoveTowards(state.Weights[i], i == slot ? 1f : 0f, step); total += state.Weights[i]; }
            if (total <= 0.0001f) { state.Weights[slot] = 1f; total = 1f; }
            for (int i = 0; i < 6; i++) state.Weights[i] /= total;
            return slot;
        }
    }
}
