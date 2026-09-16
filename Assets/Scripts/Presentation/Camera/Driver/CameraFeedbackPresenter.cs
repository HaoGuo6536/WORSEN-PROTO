// ============================================================================
// CameraFeedbackPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Computes first-person pose and visual feedback from committed movement facts.
//   Explicit time and aspect ratio keep the intended field of view and timings testable.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Camera.
//
// KEY RESPONSIBILITIES:
//   - Consume head-look deltas once and ease look-back from the current pose.
//   - Compose speed, detection, slide and rebound effects with comfort settings.
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
            state.LookTweenFrom = state.LookTweenTo = state.LookTweenElapsed = state.LookTweenDuration = 0f;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.ReboundSign = 1f;
            state.Proximity = 0f;
            state.DeathSnapped = false;
            state.DeathRotation = state.Rotation = Quaternion.identity;
            state.Position = Vector3.zero;
            state.HorizontalFieldOfView = state.VerticalFieldOfView = state.Roll = 0f;
        }

        public void SetMovement(CameraDriverState state, CameraDriverConfig config, PlayerMovementSample sample)
        {
            if (state.HasMovement && state.PlayerId.Equals(sample.Id) && sample.Tick <= state.MovementTick) return;
            if (state.HasMovement && !state.PlayerId.Equals(sample.Id)) Reset(state);
            state.HasMovement = true;
            state.PlayerId = sample.Id;
            state.MovementTick = sample.Tick;
            state.EyePosition = Finite(sample.EyePosition) ? sample.EyePosition : state.EyePosition;
            state.Velocity = Finite(sample.Velocity) ? sample.Velocity : Vector3.zero;
            state.HeadingDegrees = Finite(sample.HeadingDegrees);
            state.Movement = sample.MovementState;
            SetLookBack(state, config, sample.LookBack);
            if (state.DeathSnapped) return;
            state.Pitch = Mathf.Clamp(state.Pitch - Finite(sample.HeadLookDelta.y),
                -PitchLimit(state, config), PitchLimit(state, config));
            if (state.LookBack)
                state.HeadYaw = Mathf.Clamp(state.HeadYaw + Finite(sample.HeadLookDelta.x),
                    -config.LookBackHeadYawLimit, config.LookBackHeadYawLimit);
        }

        public void SetLookBack(CameraDriverState state, CameraDriverConfig config, bool held)
        {
            if (state.DeathSnapped || state.LookBack == held) return;
            state.LookBack = held;
            state.LookTweenFrom = state.LookYaw + state.HeadYaw;
            state.HeadYaw = 0f;
            state.LookYaw = state.LookTweenFrom;
            state.LookTweenTo = held ? config.LookBackYaw : 0f;
            state.LookTweenElapsed = 0f;
            state.LookTweenDuration = Mathf.Max(0.001f, held ? config.LookBackSeconds : config.LookForwardSeconds);
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

        public void PlayTraversal(CameraDriverState state, PlayerTraversalFact fact)
        {
            if (!state.HasMovement || !state.PlayerId.Equals(fact.Id) || fact.Tick <= state.TraversalTick) return;
            state.TraversalTick = fact.Tick;
            if (!fact.Succeeded || fact.Kind != TraversalKind.Rebound || state.DeathSnapped) return;
            var right = Quaternion.Euler(0f, state.HeadingDegrees, 0f) * Vector3.right;
            state.ReboundSign = Vector3.Dot(fact.Direction, right) < 0f ? -1f : 1f;
            state.ReboundElapsed = 0f;
        }

        public void PlayDeathSnap(CameraDriverState state, Vector3 killerPosition)
        {
            if (!state.HasMovement || !Finite(killerPosition)) return;
            var direction = killerPosition - state.EyePosition;
            if (direction.sqrMagnitude < 0.000001f) return;
            state.DeathRotation = Quaternion.LookRotation(direction.normalized,
                Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up);
            state.DeathSnapped = true;
            state.LookBack = false;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.HeadYaw = state.LookYaw = 0f;
        }

        public void Tick(CameraDriverState state, CameraDriverConfig config, float dt, float aspectRatio)
        {
            dt = Mathf.Max(0f, Finite(dt));
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
            var right = Quaternion.Euler(0f, state.HeadingDegrees, 0f) * Vector3.right;
            var slideSign = Vector3.Dot(state.Velocity, right) < 0f ? -1f : 1f;
            state.Roll = state.Movement == MovementState.Slide ? config.SlideRoll * slideSign : 0f;
            if (state.ReboundElapsed >= 0f)
            {
                state.ReboundElapsed += dt;
                var rebound = Mathf.Clamp01(1f - state.ReboundElapsed / Mathf.Max(0.001f, config.ReboundSeconds));
                state.Roll = config.ReboundRoll * state.ReboundSign * rebound;
                if (rebound <= 0f) state.ReboundElapsed = -1f;
            }
            if (!config.TiltEnabled || state.DeathSnapped) state.Roll = 0f;
            state.Position = state.EyePosition;
            state.Rotation = state.DeathSnapped ? state.DeathRotation
                : Quaternion.Euler(state.Pitch, state.HeadingDegrees + state.LookYaw + state.HeadYaw, state.Roll);
        }

        public float HorizontalToVerticalFieldOfView(float horizontal, float aspectRatio)
        {
            var aspect = Finite(aspectRatio);
            if (aspect <= 0f) aspect = 16f / 9f;
            horizontal = Mathf.Clamp(Finite(horizontal), 1f, 179f);
            return 2f * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
        }

        private float PitchLimit(CameraDriverState state, CameraDriverConfig config)
            => state.LookBack ? config.LookBackPitchLimit : config.ForwardPitchLimit;

        private float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
