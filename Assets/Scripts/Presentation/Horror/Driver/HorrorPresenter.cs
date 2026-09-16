// ============================================================================
// HorrorPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Calculates bounded flashlight, fog-distance and enemy warning outputs from pushed facts.
//   It never discovers enemies or touches Unity objects, so invalid inputs and phase edges can be tested.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Horror.
//
// KEY RESPONSIBILITIES:
//   - Convert effect multipliers into real fog distances and flashlight reach.
//   - Compute warning color, contracting ring, directional pose and one growl per windup.
//
// DEPENDENCIES:
//   - Core HunterAttackSample and pure UnityEngine value math.
//
// USAGE NOTES:
//   All transient values live in caller-owned DriverState objects.
//   ResetRound clears attack state and restores the lamp switch while preserving run modifiers.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Horror
{
    public sealed class HorrorPresenter
    {
        public void SetEffects(HorrorDriverState state, float fogMultiplier, float flashlightMultiplier)
        {
            state.FogMultiplier = PositiveOr(fogMultiplier, 1f);
            state.FlashlightMultiplier = PositiveOr(flashlightMultiplier, 1f);
        }

        public void CalculateAtmosphere(HorrorDriverState state, HorrorPresentationSettings settings, float cameraFarClip)
        {
            float farClip = PositiveOr(cameraFarClip, 100f);
            float fogNear = NonNegativeOr(settings.FogNearMeters, 0f) / state.FogMultiplier;
            float fogFar = PositiveOr(settings.FogFarMeters, farClip) / state.FogMultiplier;
            state.FogCurveStart = Mathf.Clamp(fogNear / farClip, 0f, 0.999f);
            state.FogCurveEnd = Mathf.Clamp(fogFar / farClip, state.FogCurveStart + 0.001f, 1f);
            state.FlashlightRange = Mathf.Clamp(
                PositiveOr(settings.FlashlightRange, 1f) * state.FlashlightMultiplier, 0.01f, farClip);
            state.FlashlightIntensity = NonNegativeOr(settings.FlashlightIntensity, 0f);
        }

        public void ToggleFlashlight(HorrorDriverState state) => state.FlashlightEnabled = !state.FlashlightEnabled;

        public void ResetRound(HorrorDriverState state)
        {
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
