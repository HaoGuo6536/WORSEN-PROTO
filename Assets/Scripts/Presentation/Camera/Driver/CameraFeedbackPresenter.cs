// ============================================================================
// CameraFeedbackPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Computes first-person pose and visual feedback from committed movement facts.
//   Explicit time and aspect ratio keep the intended field of view and timings testable.
//
// ARCHITECTURAL ROLE:
//   Presenter (Â§7b) Â· Presentation Â· Camera.
//
// KEY RESPONSIBILITIES:
//   - Consume mouse-directed free-look once and ease release back to the movement frame.
//   - Keep gameplay aim unshaken while composing bounded cosmetic effects.
//   - Compose speed, detection, slide and rebound effects with comfort settings.
//   - Give confirmed consumption precedence over ordinary death and freeze its final pose.
//   - Convert horizontal view angle to the camera's vertical lens angle.
//
// DEPENDENCIES:
//   - Core player movement/traversal values only; no Domain or Session references.
//
// USAGE NOTES:
//   - Stateless calculator; CameraDriver owns the supplied state.
//   - Head-look is degrees per committed tick; positive vertical input looks upward.
//   - SetProximity stores a routed primitive; peripheral rendering belongs to PostFX.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraFeedbackPresenter
    {
        public void Reset(CameraDriverState state)
        {
            state.HasMovement = false;
            state.PlayerId = default;
            state.MovementTick = -1;
            state.TraversalTick = -1;
            state.EyePosition = Vector3.zero;
            state.Velocity = Vector3.zero;
            state.HeadingDegrees = 0f;
            state.Movement = MovementState.Ground;
            state.LookBack = false;
            state.Pitch = state.HeadYaw = state.LookYaw = 0f;
            state.SlideTurnRateDegrees = state.SlideBank = state.ShakeElapsed = state.ShakeDuration = state.ShakeStrength = 0f;
            state.AimRotation = Quaternion.identity;
            state.LookTweenFrom = state.LookTweenTo = state.LookTweenElapsed = state.LookTweenDuration = 0f;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.ReboundSign = 1f;
            state.Proximity = 0f;
            state.Consumed = false;
            state.ConsumptionElapsed = state.ConsumptionDuration = 0f;
            state.ConsumptionStartPosition = state.ConsumptionTargetPosition = Vector3.zero;
            state.ConsumptionStartRotation = state.ConsumptionTargetRotation = Quaternion.identity;
            state.DeathSnapped = false;
            state.DeathRotation = state.Rotation = Quaternion.identity;
            state.Position = Vector3.zero;
            state.HorizontalFieldOfView = state.VerticalFieldOfView = state.Roll = 0f;
        }

        public void SetMovement(CameraDriverState state, CameraDriverConfig config, PlayerMovementSample sample)
        {
            if (state.HasMovement && state.PlayerId.Equals(sample.Id) && sample.Tick <= state.MovementTick) return;
            if (state.HasMovement && !state.PlayerId.Equals(sample.Id)) Reset(state);
            if (state.Consumed) return;
            state.HasMovement = true;
            state.PlayerId = sample.Id;
            state.MovementTick = sample.Tick;
            state.EyePosition = Finite(sample.EyePosition) ? sample.EyePosition : state.EyePosition;
            state.Velocity = Finite(sample.Velocity) ? sample.Velocity : Vector3.zero;
            state.HeadingDegrees = Finite(sample.HeadingDegrees);
            state.Movement = sample.MovementState;
            state.SlideTurnRateDegrees = Finite(sample.SlideTurnRateDegrees);
            SetLookBack(state, config, sample.LookBack);
            if (state.DeathSnapped) return;
            state.Pitch = Mathf.Clamp(state.Pitch - Finite(sample.HeadLookDelta.y),
                -PitchLimit(state, config), PitchLimit(state, config));
            if (state.LookBack)
                state.HeadYaw = Mathf.Clamp(state.HeadYaw + Finite(sample.HeadLookDelta.x),
                    -config.FreeLookYawLimit - state.LookYaw, config.FreeLookYawLimit - state.LookYaw);
        }

        public void SetLookBack(CameraDriverState state, CameraDriverConfig config, bool held)
        {
            if (state.DeathSnapped || state.LookBack == held) return;
            state.LookBack = held;
            state.LookTweenFrom = state.LookYaw + state.HeadYaw;
            state.HeadYaw = 0f;
            state.LookYaw = state.LookTweenFrom;
            state.LookTweenTo = held ? state.LookTweenFrom : 0f;
            state.LookTweenElapsed = 0f;
            state.LookTweenDuration = held ? 0f : Mathf.Max(0.001f, config.LookForwardSeconds);
            state.Pitch = Mathf.Clamp(state.Pitch, -PitchLimit(state, config), PitchLimit(state, config));
        }

        public void PlayDetectionBeat(CameraDriverState state)
        {
            if (!state.DeathSnapped) state.DetectionElapsed = 0f;
        }

        public Vector3 DetectionImpulseVelocity(CameraDriverConfig config)
            => Vector3.down * Mathf.Max(0f, config.ImpulseDisplacement);

        public void SetProximity(CameraDriverState state, float closeness)
        {
            state.Proximity = Mathf.Clamp01(Finite(closeness));
        }

        public void PlayTraversal(CameraDriverState state, PlayerTraversalFact fact, CameraDriverConfig config = null)
        {
            if (!state.HasMovement || !state.PlayerId.Equals(fact.Id) || fact.Tick <= state.TraversalTick) return;
            state.TraversalTick = fact.Tick;
            if (!fact.Succeeded || state.DeathSnapped) return;
            if (config != null && fact.Kind == TraversalKind.Land)
                PlayShake(state, config.LandingShakeStrength, config.LandingShakeSeconds);
            if (config != null && fact.Kind == TraversalKind.Rebound)
                PlayShake(state, config.ReboundShakeStrength, config.ReboundShakeSeconds);
            if (fact.Kind != TraversalKind.Rebound) return;
            var right = Quaternion.Euler(0f, state.HeadingDegrees, 0f) * Vector3.right;
            state.ReboundSign = Vector3.Dot(fact.Direction, right) < 0f ? -1f : 1f;
            state.ReboundElapsed = 0f;
        }

        public void PlayShake(CameraDriverState state, float strength, float seconds)
        {
            if (state.DeathSnapped) return;
            state.ShakeStrength = Mathf.Clamp01(Mathf.Max(state.ShakeStrength, Finite(strength)));
            state.ShakeDuration = Mathf.Clamp(Finite(seconds), 0f, 1f);
            state.ShakeElapsed = 0f;
        }

        public void PlayDeathSnap(CameraDriverState state, Vector3 killerPosition)
        {
            if (state.Consumed || !state.HasMovement || !Finite(killerPosition)) return;
            var direction = killerPosition - state.EyePosition;
            if (direction.sqrMagnitude < 0.000001f) return;
            state.DeathRotation = Quaternion.LookRotation(direction.normalized,
                Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up);
            state.DeathSnapped = true;
            state.LookBack = false;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.HeadYaw = state.LookYaw = 0f;
            state.ShakeStrength = state.ShakeDuration = 0f;
        }

        public float ConsumptionSeconds(CameraDriverConfig config)
            => Mathf.Clamp(Finite(config.ConsumptionSeconds), 0.1f, 2f);

        public void PlayConsumed(CameraDriverState state, CameraDriverConfig config, Vector3 handPosition)
        {
            if (state.Consumed || !state.HasMovement || !Finite(handPosition)) return;
            var start = state.VerticalFieldOfView > 0f ? state.Position : state.EyePosition;
            var direction = handPosition - start;
            if (!Finite(direction) || float.IsInfinity(direction.sqrMagnitude)) return;
            var horizontal = new Vector3(direction.x, 0f, direction.z);
            float motion = Mathf.Clamp01(Finite(config.ConsumptionMotionIntensity));
            var drag = horizontal.sqrMagnitude > 0.000001f ? horizontal.normalized
                * Mathf.Min(horizontal.magnitude, Mathf.Clamp(Finite(config.ConsumptionDragDistance), 0f, 3f)) : Vector3.zero;
            state.ConsumptionStartPosition = start;
            state.ConsumptionTargetPosition = start + motion * (drag
                - Vector3.up * Mathf.Clamp(Finite(config.ConsumptionSinkDistance), 0f, 0.75f));
            state.ConsumptionStartRotation = state.VerticalFieldOfView > 0f ? state.Rotation : AimRotation(state);
            var targetRotation = direction.sqrMagnitude > 0.000001f
                ? Quaternion.LookRotation(direction.normalized,
                    Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up)
                : state.ConsumptionStartRotation;
            state.ConsumptionTargetRotation = Quaternion.Slerp(state.ConsumptionStartRotation, targetRotation, motion);
            state.Consumed = state.DeathSnapped = true;
            state.ConsumptionElapsed = 0f;
            state.ConsumptionDuration = ConsumptionSeconds(config);
            state.LookBack = false;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.ShakeStrength = state.ShakeDuration = state.SlideBank = 0f;
        }

        private void TickConsumed(CameraDriverState state, CameraDriverConfig config, float dt, float aspectRatio)
        {
            state.ConsumptionElapsed = Mathf.Min(state.ConsumptionDuration, state.ConsumptionElapsed + dt);
            float progress = state.ConsumptionElapsed / state.ConsumptionDuration;
            float eased = progress * progress * (3f - 2f * progress);
            float pull = Mathf.Sin(progress * Mathf.PI);
            float motion = Mathf.Clamp01(Finite(config.ConsumptionMotionIntensity));
            state.Roll = config.TiltEnabled ? Mathf.Clamp(Finite(config.ConsumptionRoll), 0f, 10f) * pull * motion : 0f;
            float shake = pull * Mathf.Clamp01(Finite(config.ShakeIntensity)) * motion;
            state.Position = Vector3.Lerp(state.ConsumptionStartPosition, state.ConsumptionTargetPosition, eased);
            state.AimRotation = Quaternion.Slerp(state.ConsumptionStartRotation, state.ConsumptionTargetRotation, eased);
            state.Rotation = state.AimRotation * Quaternion.Euler(
                Mathf.Sin(state.ConsumptionElapsed * 47f) * Mathf.Clamp(Finite(config.MaximumShakeDegrees), 0f, 5f) * shake, 0f, state.Roll);
            state.HorizontalFieldOfView = Mathf.Clamp(Finite(config.HorizontalFieldOfView), 1f, 179f);
            state.VerticalFieldOfView = HorizontalToVerticalFieldOfView(state.HorizontalFieldOfView, aspectRatio);
        }

        public void Tick(CameraDriverState state, CameraDriverConfig config, float dt, float aspectRatio)
        {
            dt = Mathf.Max(0f, Finite(dt));
            if (state.Consumed) { TickConsumed(state, config, dt, aspectRatio); return; }
            state.LookTweenElapsed = Mathf.Min(state.LookTweenDuration, state.LookTweenElapsed + dt);
            var fraction = state.LookTweenDuration > 0f ? state.LookTweenElapsed / state.LookTweenDuration : 1f;
            state.LookYaw = Mathf.Lerp(state.LookTweenFrom, state.LookTweenTo, fraction * fraction * (3f - 2f * fraction));
            var kick = 0f;
            if (state.DetectionElapsed >= 0f)
            {
                state.DetectionElapsed += dt;
                var attack = Mathf.Max(0.001f, config.DetectionAttackSeconds);
                var decay = Mathf.Max(0.001f, config.DetectionDecaySeconds);
                kick = state.DetectionElapsed <= attack ? state.DetectionElapsed / attack
                    : Mathf.Clamp01(1f - (state.DetectionElapsed - attack) / decay);
                if (state.DetectionElapsed >= attack + decay) state.DetectionElapsed = -1f;
            }
            var speed = new Vector2(state.Velocity.x, state.Velocity.z).magnitude;
            var normalized = Mathf.Clamp01(speed / Mathf.Max(0.1f, config.MaxDesignSpeed));
            state.HorizontalFieldOfView = Mathf.Clamp(config.HorizontalFieldOfView + config.SpeedFieldOfView * normalized
                + config.DetectionFieldOfView * kick * Mathf.Max(0f, config.PunchIntensity), 1f, 179f);
            state.VerticalFieldOfView = HorizontalToVerticalFieldOfView(state.HorizontalFieldOfView, aspectRatio);
            float targetBank = state.Movement == MovementState.Slide
                ? -config.SlideRoll * Mathf.Clamp(state.SlideTurnRateDegrees / Mathf.Max(1f, config.SlideBankFullTurnRate), -1f, 1f) : 0f;
            state.SlideBank = Mathf.Lerp(state.SlideBank, targetBank, 1f - Mathf.Exp(-Mathf.Max(0f, config.SlideBankResponse) * dt));
            state.Roll = state.SlideBank;
            if (state.ReboundElapsed >= 0f)
            {
                state.ReboundElapsed += dt;
                var rebound = Mathf.Clamp01(1f - state.ReboundElapsed / Mathf.Max(0.001f, config.ReboundSeconds));
                state.Roll = config.ReboundRoll * state.ReboundSign * rebound;
                if (rebound <= 0f) state.ReboundElapsed = -1f;
            }
            if (!config.TiltEnabled || state.DeathSnapped) state.Roll = 0f;
            state.AimRotation = Quaternion.Euler(state.Pitch, state.HeadingDegrees + state.LookYaw + state.HeadYaw, 0f);
            state.ShakeElapsed += dt;
            float envelope = state.ShakeDuration > 0f ? Mathf.Clamp01(1f - state.ShakeElapsed / state.ShakeDuration) : 0f;
            float amount = envelope * state.ShakeStrength * Mathf.Clamp01(config.ShakeIntensity);
            if (envelope <= 0f) state.ShakeStrength = 0f;
            var wave = new Vector3(Mathf.Sin(state.ShakeElapsed * 93f), Mathf.Sin(state.ShakeElapsed * 117f), Mathf.Sin(state.ShakeElapsed * 71f));
            state.Position = state.EyePosition + state.AimRotation * (Vector3.ClampMagnitude(wave, 1f) * config.MaximumShakeDisplacement * amount);
            state.Rotation = state.DeathSnapped ? state.DeathRotation
                : state.AimRotation * Quaternion.Euler(wave.x * config.MaximumShakeDegrees * amount,
                    wave.y * config.MaximumShakeDegrees * amount, state.Roll + wave.z * config.MaximumShakeDegrees * amount);
        }

        public Quaternion AimRotation(CameraDriverState state)
            => Quaternion.Euler(state.Pitch, state.HeadingDegrees + state.LookYaw + state.HeadYaw, 0f);

        public float HorizontalToVerticalFieldOfView(float horizontal, float aspectRatio)
        {
            var aspect = Finite(aspectRatio);
            if (aspect <= 0f) aspect = 16f / 9f;
            horizontal = Mathf.Clamp(Finite(horizontal), 1f, 179f);
            return 2f * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
        }

        private float PitchLimit(CameraDriverState state, CameraDriverConfig config)
            => state.LookBack ? config.FreeLookPitchLimit : config.ForwardPitchLimit;

        private float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
