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
//   - Quantise pose time only; compute humanoid gaze/catch and independent foot blends.
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
        public int PoseSteps(HunterAnimationDriverState state, float dt, float rate, int maximum, out float step)
        {
            step = 0f;
            if (!(dt > 0f) || float.IsInfinity(dt)) return 0;
            if (!(rate > 0f) || float.IsInfinity(rate))
            { state.PoseRemainder = 0; state.SampleRate = 0f; step = dt; return 1; }
            if (state.SampleRate != rate) { state.PoseRemainder = 0; state.SampleRate = rate; }
            double interval = 1.0 / rate;
            double elapsed = state.PoseRemainder + dt;
            double count = System.Math.Floor(elapsed / interval + 0.000001);
            state.PoseRemainder = System.Math.Max(0, elapsed - count * interval);
            step = (float)interval;
            // Drop excess whole samples after a hitch, never carry unbounded catch-up debt.
            return (int)System.Math.Min(count, System.Math.Max(1, maximum));
        }
        public void Look(HunterAnimationDriverState state, float dt, float blendIn, float blendOut)
        {
            if (!state.HasHumanoidRig) { state.LookWeight = 0f; return; }
            if (state.CatchActive) { state.LookWeight = 1f; return; }
            float duration = state.Looking ? blendIn : blendOut;
            state.LookWeight = Mathf.MoveTowards(state.LookWeight, state.Looking ? 1f : 0f,
                duration <= 0f ? 1f : Mathf.Max(0f, dt) / duration);
        }
        public void Foot(HunterAnimationDriverState state, int foot, bool grounded, Vector3 position,
            Quaternion rotation, float dt, float blendSeconds)
        {
            if (!state.HasHumanoidRig) { state.FootWeights[foot] = 0f; return; }
            if (grounded) { state.FootPositions[foot] = position; state.FootRotations[foot] = rotation; }
            state.FootWeights[foot] = Mathf.MoveTowards(state.FootWeights[foot], grounded ? 1f : 0f,
                blendSeconds <= 0f ? 1f : Mathf.Max(0f, dt) / blendSeconds);
        }
        public bool FootTarget(Vector3 animated, Quaternion animatedRotation, Vector3 point, Vector3 normal,
            float offset, float maximumOffset, float slopeLimit, out Vector3 position, out Quaternion rotation)
        {
            position = point + Vector3.up * offset;
            rotation = Quaternion.FromToRotation(Vector3.up, normal) * animatedRotation;
            return normal.sqrMagnitude > 0f && Vector3.Angle(normal, Vector3.up) <= slopeLimit &&
                Mathf.Abs(position.y - animated.y) <= maximumOffset;
        }
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
