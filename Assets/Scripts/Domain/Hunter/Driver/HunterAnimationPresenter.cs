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
//   - Match clip stride rates to committed speed and blend hysteretic gait changes.
//   - Resolve module phases and one-shot clocks separately from lunge recovery.
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
        public float PlaybackRate(float speed, float authoredSpeed, float minimum, float maximum)
        {
            if (!(speed > 0f) || float.IsInfinity(speed) || !(authoredSpeed > 0f) || float.IsInfinity(authoredSpeed)) return 0f;
            float low = minimum >= 0f && !float.IsInfinity(minimum) ? minimum : 0f;
            float high = maximum > 0f && !float.IsInfinity(maximum) ? Mathf.Max(low, maximum) : Mathf.Max(low, 1f);
            return Mathf.Clamp(speed / authoredSpeed, low, high);
        }
        public static HunterAnimationPhase FromRamPhase(Worsen.Core.RamPhase phase)
        {
            switch (phase)
            {
                case Worsen.Core.RamPhase.Windup: return HunterAnimationPhase.Ready;
                case Worsen.Core.RamPhase.Charge: return HunterAnimationPhase.Run;
                case Worsen.Core.RamPhase.Stagger: return HunterAnimationPhase.Hit;
                default: return HunterAnimationPhase.None;
            }
        }
        public void Trigger(HunterAnimationDriverState state, HunterAnimationPhase phase)
        {
            if (phase != HunterAnimationPhase.Attack && phase != HunterAnimationPhase.Hit) return;
            state.TriggeredPhase = phase; state.TriggerElapsed = 0f; state.RestartTriggeredPose = true;
        }
        public int ResolvePhase(HunterAnimationDriverState state, float dt, int sharedPhase, float sharedProgress,
            HunterAnimationPhase module, float readyLength, float attackLength, float hitLength, out float progress)
        {
            float delta = dt > 0f && !float.IsInfinity(dt) ? dt : 0f;
            if (state.LastModulePhase != module) state.ModuleElapsed = 0f;
            state.LastModulePhase = module;
            state.ModuleElapsed += delta;
            progress = sharedProgress;
            // A committed release lasts a clip, even when its rule fact lasted one tick.
            if (state.TriggeredPhase != HunterAnimationPhase.None)
            {
                float length = state.TriggeredPhase == HunterAnimationPhase.Hit ? hitLength : attackLength;
                if (state.TriggerElapsed < length || state.RestartTriggeredPose)
                {
                    progress = length > 0f ? Mathf.Clamp01(state.TriggerElapsed / length) : 1f;
                    state.TriggerElapsed += delta; state.RestartTriggeredPose = false;
                    return (int)state.TriggeredPhase;
                }
                state.TriggeredPhase = HunterAnimationPhase.None;
            }
            if (module == HunterAnimationPhase.None) return sharedPhase;
            float duration = module == HunterAnimationPhase.Ready ? readyLength : module == HunterAnimationPhase.Hit ? hitLength : attackLength;
            progress = duration > 0f ? Mathf.Clamp01(state.ModuleElapsed / duration) : 1f;
            return (int)module;
        }
        public int Tick(HunterAnimationDriverState state, float dt, float speed, int phase, float runThreshold, float blendSeconds,
            float runHysteresis = 0f)
        {
            float velocity = speed > 0f && !float.IsInfinity(speed) ? speed : 0f;
            float band = Mathf.Max(0f, runHysteresis);
            if (velocity < 0.1f) state.Running = false;
            else if (state.Running) { if (velocity < Mathf.Max(0.1f, runThreshold - band)) state.Running = false; }
            else if (velocity >= runThreshold + band) state.Running = true;
            int slot = phase == (int)HunterAnimationPhase.Hit ? 6 : phase == (int)HunterAnimationPhase.Run ? 2 :
                phase >= 1 && phase <= 3 ? phase + 2 : velocity < 0.1f ? 0 : state.Running ? 2 : 1;
            float step = blendSeconds <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, dt) / blendSeconds);
            float total = 0f;
            for (int i = 0; i < state.Weights.Length; i++)
            { state.Weights[i] = Mathf.MoveTowards(state.Weights[i], i == slot ? 1f : 0f, step); total += state.Weights[i]; }
            if (total <= 0.0001f) { state.Weights[slot] = 1f; total = 1f; }
            for (int i = 0; i < state.Weights.Length; i++) state.Weights[i] /= total;
            return slot;
        }
    }
}
