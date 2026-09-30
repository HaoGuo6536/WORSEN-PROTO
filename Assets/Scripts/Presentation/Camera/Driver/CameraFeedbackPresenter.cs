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
//   - Compose progress-driven vault and comfort-gated landing/stumble without locking look.
//   - Apply runtime lens/comfort overrides without mutating designer configuration.
//   - Snap fully behind and forward on committed edges, ignoring rear-view look deltas.
//   - Keep gameplay aim unshaken while composing bounded cosmetic effects.
//   - Compose speed, detection, slide and rebound effects with comfort settings.
//   - Approach a captured hunter/hand close-up, then hold a stable pose with timing facts.
//   - Convert horizontal view angle to the camera's vertical lens angle.
//
// DEPENDENCIES:
//   - Core player movement/traversal values only; no Domain or Session references.
//
// USAGE NOTES:
//   - Stateless calculator; CameraDriver owns the supplied state.
//   - Head-look is degrees per committed tick; positive vertical input looks upward.
//   - SetProximity stores a routed primitive; peripheral rendering belongs to PostFX.
//   - Catch hold time begins on the first presented endpoint; approach overshoot is discarded.
//   - Hunter input is a root position plus configured focus height; hands use the grab point.
//
// ============================================================================

using UnityEngine;
using Worsen.Core;

namespace Worsen.Presentation.Camera
{
    public sealed class CameraFeedbackPresenter
    {
        public void ApplySettings(CameraDriverState state, PlayerSettingsRecord settings)
        {
            state.BaseFieldOfView = Mathf.Clamp(Finite(settings.FieldOfView), 1f, 179f);
            state.TiltEnabled = settings.CameraTilt;
            state.PunchEnabled = settings.CameraPunch;
            if (!state.PunchEnabled) state.DetectionElapsed = -1f;
            if (!settings.CameraTilt) state.Roll = state.SlideBank = 0f;
        }

        public void Reset(CameraDriverState state)
        {
            state.HasMovement = false;
            state.PlayerId = default;
            state.MovementTick = -1;
            state.TraversalTick = -1;
            CameraTraversalPresenter.Reset(state);
            state.EyePosition = Vector3.zero;
            state.Velocity = Vector3.zero;
            state.HeadingDegrees = 0f;
            state.Movement = MovementState.Ground;
            state.LookBack = false;
            state.Pitch = state.HeadYaw = state.LookYaw = 0f;
            state.SlideTurnRateDegrees = state.SlideBank = state.ShakeElapsed = state.ShakeDuration = state.ShakeStrength = 0f;
            state.AimRotation = Quaternion.identity;

            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.ReboundSign = 1f;
            state.Proximity = 0f;
            state.Consumed = false;
            state.CatchElapsed = state.CatchApproachDuration = state.CatchHoldElapsed = state.CatchHoldDuration = 0f;
            state.CatchHoldStarted = state.CatchHoldEnded = false;
            state.CatchStartPosition = state.CatchTargetPosition = Vector3.zero;
            state.CatchStartRotation = Quaternion.identity;
            state.DeathSnapped = false;
            state.DeathRotation = state.Rotation = Quaternion.identity;
            state.Position = Vector3.zero;
            state.HorizontalFieldOfView = state.VerticalFieldOfView = state.Roll = 0f;
        }

        public void SetMovement(CameraDriverState state, CameraDriverConfig config, PlayerMovementSample sample)
        {
            if (state.DeathSnapped) return;
            if (state.HasMovement && state.PlayerId.Equals(sample.Id) && sample.Tick <= state.MovementTick) return;
            if (state.HasMovement && !state.PlayerId.Equals(sample.Id)) Reset(state);
            state.HasMovement = true;
            state.PlayerId = sample.Id;
            state.MovementTick = sample.Tick;
            state.EyePosition = Finite(sample.EyePosition) ? sample.EyePosition : state.EyePosition;
            state.Velocity = Finite(sample.Velocity) ? sample.Velocity : Vector3.zero;
            state.HeadingDegrees = Finite(sample.HeadingDegrees);
            state.Movement = sample.MovementState;
            state.SlideTurnRateDegrees = Finite(sample.SlideTurnRateDegrees);
            SetLookBack(state, config, sample.LookBack);
            if (state.LookBack) return;
            state.Pitch = Mathf.Clamp(state.Pitch - Finite(sample.HeadLookDelta.y),
                -config.ForwardPitchLimit, config.ForwardPitchLimit);
        }

        public void SetLookBack(CameraDriverState state, CameraDriverConfig config, bool held)
        {
            if (state.DeathSnapped || state.LookBack == held) return;
            state.LookBack = held;
            state.HeadYaw = 0f;
            // Fixed-frame contract also applies to assets retaining the old 160-degree/blend values.
            state.LookYaw = held ? 180f : 0f;
        }

        public void PlayDetectionBeat(CameraDriverState state)
        {
            if (!state.DeathSnapped && state.PunchEnabled) state.DetectionElapsed = 0f;
        }

        public Vector3 DetectionImpulseVelocity(CameraDriverConfig config)
            => Vector3.down * Mathf.Max(0f, config.ImpulseDisplacement);

        public void SetProximity(CameraDriverState state, float closeness)
        {
            state.Proximity = Mathf.Clamp01(Finite(closeness));
        }

        public void PlayTraversal(CameraDriverState state, PlayerTraversalFact fact, CameraDriverConfig config = null, float landingSeverity = 0f)
        {
            if (!state.HasMovement || !state.PlayerId.Equals(fact.Id) || fact.Tick <= state.TraversalTick) return;
            state.TraversalTick = fact.Tick;
            if (!fact.Succeeded || state.DeathSnapped) return;
            if (config != null && fact.Kind == TraversalKind.Land)
                CameraTraversalPresenter.Land(state, config, landingSeverity);
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

        public void PlayDeathSnap(CameraDriverState state, CameraDriverConfig config, Vector3 killerPosition)
            => BeginCatch(state, config, killerPosition + Vector3.up * Mathf.Max(0f, Finite(config.CatchHunterFocusHeight)), false);

        public float ConsumptionSeconds(CameraDriverConfig config)
            => Mathf.Max(0f, Finite(config.CatchApproachSeconds)) + Mathf.Max(0f, Finite(config.CatchHoldSeconds));

        public void PlayConsumed(CameraDriverState state, CameraDriverConfig config, Vector3 handPosition)
            => BeginCatch(state, config, handPosition, true);

        private void BeginCatch(CameraDriverState state, CameraDriverConfig config, Vector3 focus, bool consumed)
        {
            if (!state.HasMovement || !Finite(focus) || state.Consumed
                || (state.DeathSnapped && (!consumed || state.CatchHoldStarted))) return;
            var start = state.VerticalFieldOfView > 0f ? state.Position : state.EyePosition;
            var direction = focus - start;
            if (!Finite(direction) || float.IsInfinity(direction.sqrMagnitude)) return;
            direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : AimRotation(state) * Vector3.forward;
            state.CatchStartPosition = start;
            state.CatchTargetPosition = focus - direction * Mathf.Max(0.05f, Finite(config.CatchDistance));
            state.CatchStartRotation = state.VerticalFieldOfView > 0f ? state.Rotation : AimRotation(state);
            state.Position = start;
            state.AimRotation = state.Rotation = state.CatchStartRotation;
            state.DeathRotation = Quaternion.LookRotation(direction,
                Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.999f ? Vector3.forward : Vector3.up);
            state.Consumed = consumed;
            state.DeathSnapped = true;
            state.CatchElapsed = state.CatchHoldElapsed = 0f;
            state.CatchApproachDuration = Mathf.Max(0f, Finite(config.CatchApproachSeconds));
            state.CatchHoldDuration = Mathf.Max(0f, Finite(config.CatchHoldSeconds));
            state.CatchHoldStarted = state.CatchHoldEnded = false;
            state.HorizontalFieldOfView = Mathf.Clamp(Finite(state.BaseFieldOfView ?? config.HorizontalFieldOfView), 1f, 179f);
            state.LookBack = false;
            state.HeadYaw = state.LookYaw = 0f;
            state.DetectionElapsed = state.ReboundElapsed = -1f;
            state.ShakeStrength = state.ShakeDuration = state.SlideBank = 0f;
        }

        private void TickCatch(CameraDriverState state, float dt, float aspectRatio)
        {
            if (state.CatchHoldStarted)
            {
                state.CatchHoldElapsed = Mathf.Min(state.CatchHoldDuration, state.CatchHoldElapsed + dt);
                state.CatchHoldEnded = state.CatchHoldElapsed >= state.CatchHoldDuration;
            }
            state.CatchElapsed = Mathf.Min(state.CatchApproachDuration, state.CatchElapsed + dt);
            float progress = state.CatchApproachDuration > 0f ? state.CatchElapsed / state.CatchApproachDuration : 1f;
            float eased = progress * progress * (3f - 2f * progress);
            state.Roll = 0f;
            state.Position = Vector3.Lerp(state.CatchStartPosition, state.CatchTargetPosition, eased);
            state.AimRotation = state.Rotation = Quaternion.Slerp(state.CatchStartRotation, state.DeathRotation, eased);
            if (progress >= 1f) state.CatchHoldStarted = true;
            state.VerticalFieldOfView = HorizontalToVerticalFieldOfView(state.HorizontalFieldOfView, aspectRatio);
        }

        public void Tick(CameraDriverState state, CameraDriverConfig config, float dt, float aspectRatio)
        {
            dt = Mathf.Max(0f, Finite(dt));
            if (state.DeathSnapped) { TickCatch(state, dt, aspectRatio); return; }
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
            state.HorizontalFieldOfView = Mathf.Clamp((state.BaseFieldOfView ?? config.HorizontalFieldOfView) + config.SpeedFieldOfView * normalized
                + (state.PunchEnabled ? config.DetectionFieldOfView * kick * Mathf.Max(0f, config.PunchIntensity) : 0f), 1f, 179f);
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
            if (!(state.TiltEnabled ?? config.TiltEnabled)) state.Roll = 0f;
            state.AimRotation = Quaternion.Euler(state.Pitch, state.HeadingDegrees + state.LookYaw + state.HeadYaw, 0f);
            state.ShakeElapsed += dt;
            float envelope = state.ShakeDuration > 0f ? Mathf.Clamp01(1f - state.ShakeElapsed / state.ShakeDuration) : 0f;
            float amount = envelope * state.ShakeStrength * Mathf.Clamp01(config.ShakeIntensity);
            if (envelope <= 0f) state.ShakeStrength = 0f;
            var wave = new Vector3(Mathf.Sin(state.ShakeElapsed * 93f), Mathf.Sin(state.ShakeElapsed * 117f), Mathf.Sin(state.ShakeElapsed * 71f));
            state.Position = state.EyePosition + state.AimRotation * (Vector3.ClampMagnitude(wave, 1f) * config.MaximumShakeDisplacement * amount);
            state.Rotation = state.AimRotation * Quaternion.Euler(wave.x * config.MaximumShakeDegrees * amount,
                    wave.y * config.MaximumShakeDegrees * amount, state.Roll + wave.z * config.MaximumShakeDegrees * amount);
            CameraTraversalPresenter.Tick(state, config, dt);
        }

        public Quaternion AimRotation(CameraDriverState state)
            => state.DeathSnapped ? state.AimRotation
                : Quaternion.Euler(state.Pitch, state.HeadingDegrees + state.LookYaw + state.HeadYaw, 0f);

        public float HorizontalToVerticalFieldOfView(float horizontal, float aspectRatio)
        {
            var aspect = Finite(aspectRatio);
            if (aspect <= 0f) aspect = 16f / 9f;
            horizontal = Mathf.Clamp(Finite(horizontal), 1f, 179f);
            return 2f * Mathf.Atan(Mathf.Tan(horizontal * Mathf.Deg2Rad * 0.5f) / aspect) * Mathf.Rad2Deg;
        }

        private float Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        private bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
