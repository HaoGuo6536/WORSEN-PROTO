// ============================================================================
// HorrorPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Calculates bounded flashlight, fog-distance and enemy warning outputs from pushed facts.
//   It never discovers enemies or touches Unity objects, so invalid inputs and phase edges can be tested.
//   Injected gameplay tick durations advance a whole-run clock independently of floor resets.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Validate authoritative light samples and preserve exact gameplay range.
//   - Compose default-off fog hooks and gate earned intrusions with an injected random source.
//   - Advance finite, non-negative run-clock deltas and zero the clock only on ResetRun.
//   - Compute warning color, contracting ring, directional pose and one growl per windup.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and pure UnityEngine value math.
//
// USAGE NOTES:
//   All transient values live in caller-owned DriverState objects.
//   ResetRound clears attack state and restores the lamp switch while preserving run modifiers.
//   ResetRound also preserves the run clock and startle history; no engine time is sampled.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorPresenter
    {
        public bool AdvanceRunClock(HorrorDriverState state, float deltaSeconds)
        {
            if (!Finite(deltaSeconds) || deltaSeconds < 0f) return false;
            state.RunElapsedSeconds += deltaSeconds;
            return true;
        }

        public bool TryStartle(HorrorDriverState state, HorrorDriverConfig config, double runSeconds,
            bool earned, System.Random random)
        {
            if (double.IsNaN(runSeconds) || double.IsInfinity(runSeconds) || runSeconds < 0d
                || runSeconds <= state.LastIntrusionSeconds) return false;
            state.LastIntrusionSeconds = runSeconds;
            if (!earned || state.StartlesUsed >= Mathf.Max(0, config.StartlesPerRun)
                || runSeconds - state.LastStartleSeconds < NonNegativeOr(config.StartleSpacingSeconds, 0f)
                || random == null || random.NextDouble() >= Mathf.Clamp01(NonNegativeOr(config.EarnedStartleChance, 0f))) return false;
            state.StartlesUsed++;
            state.LastStartleSeconds = runSeconds;
            return true;
        }

        public void ResetRun(HorrorDriverState state)
        {
            ResetRound(state);
            state.RunElapsedSeconds = 0d;
            state.StartlesUsed = 0;
            state.LastStartleSeconds = state.LastIntrusionSeconds = double.NegativeInfinity;
            state.HookFogDistanceMultiplier = state.HookFogStartMultiplier = 1f;
        }

        public void SetLightingHooks(HorrorDriverState state, HorrorDriverConfig config, bool darkerFloors, bool catEyes)
        {
            state.HookFogDistanceMultiplier = darkerFloors ? Mathf.Clamp(PositiveOr(config.DarkerFogDistanceMultiplier, 1f), 0.01f, 1f) : 1f;
            state.HookFogStartMultiplier = catEyes ? Mathf.Max(1f, PositiveOr(config.CatEyesFogStartMultiplier, 1f)) : 1f;
        }

        public void SetEffects(HorrorDriverState state, float fogMultiplier, float flashlightMultiplier)
        {
            state.FogMultiplier = PositiveOr(fogMultiplier, 1f);
            state.FlashlightMultiplier = PositiveOr(flashlightMultiplier, 1f);
        }

        public void CalculateAtmosphere(HorrorDriverState state, HorrorPresentationSettings settings, float cameraFarClip)
        {
            float farClip = PositiveOr(cameraFarClip, 100f);
            float fogFar = PositiveOr(settings.FogFarMeters, farClip) / state.FogMultiplier * state.HookFogDistanceMultiplier;
            float fogNear = Mathf.Min(fogFar, NonNegativeOr(settings.FogNearMeters, 0f) / state.FogMultiplier
                * state.HookFogDistanceMultiplier * state.HookFogStartMultiplier);
            state.FogCurveStart = Mathf.Clamp(fogNear / farClip, 0f, 0.999f);
            state.FogCurveEnd = Mathf.Clamp(fogFar / farClip, state.FogCurveStart + 0.001f, 1f);
            state.FlashlightRange = Mathf.Clamp(
                PositiveOr(settings.FlashlightRange, 1f) * state.FlashlightMultiplier, 0.01f, farClip);
            if (state.HasAuthoritativeFlashlight) state.FlashlightRange = state.AuthoritativeFlashlight.Range;
            state.FlashlightIntensity = NonNegativeOr(settings.FlashlightIntensity, 0f);
        }

        public bool SetFlashlight(HorrorDriverState state, FlashlightSample sample)
        {
            if (!sample.Source.IsValid || !Finite(sample.Origin) || !Finite(sample.Direction)
                || sample.Direction.sqrMagnitude < 0.0001f || float.IsNaN(sample.Range)
                || float.IsInfinity(sample.Range) || sample.Range < 0f || sample.Range > 1000f
                || float.IsNaN(sample.ConeDegrees) || float.IsInfinity(sample.ConeDegrees)
                || sample.ConeDegrees < 1f || sample.ConeDegrees > 179f) return false;
            if (state.HasAuthoritativeFlashlight && sample.Source == state.AuthoritativeFlashlight.Source
                && sample.Tick < state.AuthoritativeFlashlight.Tick) return false;
            state.HasAuthoritativeFlashlight = true;
            state.AuthoritativeFlashlight = sample;
            state.FlashlightEnabled = sample.Enabled;
            return true;
        }

        public void ToggleFlashlight(HorrorDriverState state)
        { if (!state.HasAuthoritativeFlashlight) state.FlashlightEnabled = !state.FlashlightEnabled; }

        public void ResetRound(HorrorDriverState state)
        {
            state.HasAuthoritativeFlashlight = false;
            state.AuthoritativeFlashlight = default;
            state.FlashlightEnabled = true;
            state.Attacks.Clear();
        }

        public HorrorAttackVisual PresentAttack(
            HorrorAttackDriverState state, HunterAttackSample sample, HorrorPresentationSettings settings)
        {
            int phase = sample.Phase >= 0 && sample.Phase <= 3 ? sample.Phase : 0;
            float progress = Mathf.Clamp01(NonNegativeOr(sample.Progress, 0f));
            bool valid = sample.Hunter.IsValid && Finite(sample.Position) && Finite(sample.Direction);
            bool growl = valid && phase == 1 && (!state.HasSample || state.Phase != 1);
            state.HasSample = true;
            state.Phase = valid ? phase : 0;
            state.Progress = progress;
            Vector3 direction = valid ? new Vector3(sample.Direction.x, 0f, sample.Direction.z) : Vector3.forward;
            if (direction.sqrMagnitude < 0.000001f) direction = Vector3.forward;
            direction.Normalize();
            float halfYaw = Mathf.Atan2(direction.x, direction.z) * 0.5f;
            float radius = PositiveOr(settings.AttackRadius, 0.1f);
            Color color = settings.ActiveColor;
            if (phase == 1)
            {
                radius *= Mathf.Lerp(PositiveOr(settings.WindupStartScale, 1f),
                    PositiveOr(settings.WindupEndScale, 1f), progress);
                color = Color.Lerp(settings.WindupColor, settings.ActiveColor, progress);
            }
            else if (phase == 3) color = Color.Lerp(settings.RecoveryColor, Color.clear, progress);
            return new HorrorAttackVisual
            {
                Position = valid ? sample.Position + Vector3.up * NonNegativeOr(settings.AttackHeight, 0f) : Vector3.zero,
                Rotation = new Quaternion(0f, Mathf.Sin(halfYaw), 0f, Mathf.Cos(halfYaw)),
                Color = color,
                Radius = radius,
                ArrowLength = PositiveOr(settings.AttackArrowLength, 1f),
                Visible = valid && phase != 0 && (phase != 3 || progress < 1f),
                PlayGrowl = growl
            };
        }

        public Vector3[] BuildRing(int segmentCount)
        {
            int count = Mathf.Clamp(segmentCount, 8, 64);
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = i * (Mathf.PI * 2f / count);
                points[i] = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            }
            return points;
        }

        public Vector3[] BuildArrow(float halfWidth, float headFraction)
        {
            float width = PositiveOr(halfWidth, 0.1f);
            float head = Mathf.Clamp(PositiveOr(headFraction, 0.25f), 0.01f, 1f);
            return new[]
            {
                new Vector3(-width, 0f, 1f - head), Vector3.forward,
                new Vector3(width, 0f, 1f - head), Vector3.forward, Vector3.zero
            };
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static float PositiveOr(float value, float fallback) => Finite(value) && value > 0f ? value : fallback;
        private static float NonNegativeOr(float value, float fallback) => Finite(value) && value >= 0f ? value : fallback;
    }
}
