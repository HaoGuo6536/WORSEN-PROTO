// ============================================================================
// PlayerLimbPresenter.cs
// ============================================================================
// PURPOSE:
//   Anchors relaxed arms to yaw-only shoulders beneath the eye. Computes gentle
//   speed-scaled walking swing and conservative near-plane clearance without
//   rotating the hanging pose with head pitch or roll.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Compute yaw-only shoulder anchors and projected renderer clearance.
//   - Reject arms below the original view before clearance can pull them onscreen.
//   - Ease an injected walking envelope and return opposite-phase swing angles.
// DEPENDENCIES:
//   - Core MovementState and UnityEngine value math; the sub-driver supplies facts.
// USAGE NOTES:
//   Stateless pure math. Offsets come from PlayerMoverDriverConfig; the tiny
//   depth epsilon is numerical clearance, not a gameplay or art tuning value.
//   A zero swing target settles the existing envelope over the configured ease
//   time, then remains exactly zero. Eight degrees is the owner's hard limit.
// ============================================================================
using System;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public sealed class PlayerLimbPresenter
    {
        public Quaternion YawRotation(Vector3 viewForward, Vector3 fallbackForward)
        {
            Vector3 heading = viewForward.x * viewForward.x + viewForward.z * viewForward.z > 0.000001f
                ? viewForward : fallbackForward;
            double halfYaw = Math.Atan2(heading.x, heading.z) * 0.5;
            return new Quaternion(0f, (float)Math.Sin(halfYaw), 0f, (float)Math.Cos(halfYaw));
        }

        public Quaternion ArmRotation(Quaternion yaw, float swingDegrees)
        {
            double halfAngle = swingDegrees * Math.PI / 360.0;
            return yaw * new Quaternion((float)Math.Sin(halfAngle), 0f, 0f, (float)Math.Cos(halfAngle));
        }

        public Vector3 ShoulderOffset(Vector3 offset, bool left, Quaternion yaw)
            => yaw * new Vector3(left ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x), offset.y, offset.z);

        public Vector3 ClearNearPlane(Vector3 shoulderFromEye, Vector3 viewForward, float nearClip, float rearExtent)
        {
            // Upward clearance must not lift a shoulder above the eye. Translate
            // horizontally in that case; BelowView keeps these hidden arms hidden.
            Vector3 horizontal = new Vector3(viewForward.x, 0f, viewForward.z);
            Vector3 direction = viewForward.y > 0f && horizontal.sqrMagnitude > 0.000001f
                ? horizontal.normalized : viewForward;
            float deficit = Mathf.Max(0f, nearClip + rearExtent + 0.0001f - Vector3.Dot(shoulderFromEye, viewForward));
            return shoulderFromEye + direction * (deficit / Vector3.Dot(direction, viewForward));
        }

        public bool BelowView(Vector3 centerFromEye, Vector3 worldExtents, Vector3 viewForward,
            Vector3 viewUp, float verticalFov)
        {
            // Evaluate BEFORE clearance. Otherwise looking up pushes invisible arms
            // far forward and makes their shoulders appear in the middle of the view.
            Vector3 bottomNormal = viewUp + viewForward * (float)Math.Tan(verticalFov * Math.PI / 360.0);
            return Vector3.Dot(centerFromEye, bottomNormal) + ProjectedExtent(worldExtents, bottomNormal) < 0f;
        }

        public float SwingTarget(MovementState movement, bool crouched, float horizontalSpeed,
            float maximumDegrees, float referenceSpeed)
            => movement == MovementState.Ground && !crouched && referenceSpeed > 0f
                ? Mathf.Clamp(maximumDegrees, 0f, 8f) * Mathf.Clamp01(horizontalSpeed / referenceSpeed) : 0f;

        public void StepSwing(float phase, float envelope, float target, float maximumDegrees,
            float frequency, float easeSeconds, float deltaTime, out float nextPhase, out float nextEnvelope)
        {
            float limit = Mathf.Clamp(maximumDegrees, 0f, 8f);
            float dt = Mathf.Max(0f, deltaTime);
            nextEnvelope = Mathf.MoveTowards(Mathf.Clamp(envelope, 0f, limit), Mathf.Clamp(target, 0f, limit),
                easeSeconds > 0f ? limit * dt / easeSeconds : limit);
            // Phase is continuous through speed changes, and resets only once settled.
            nextPhase = nextEnvelope > 0f
                ? (phase + dt * Mathf.Max(0f, frequency) * 2f * Mathf.PI) % (2f * Mathf.PI) : 0f;
        }

        public float SwingAngle(float phase, float envelope, bool left)
            => (left ? 1f : -1f) * (float)Math.Sin(phase) * Mathf.Clamp(envelope, 0f, 8f);

        private float ProjectedExtent(Vector3 extents, Vector3 axis)
            => Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

        public float RearExtent(Vector3 centerFromRoot, Vector3 worldExtents, Vector3 viewForward)
        {
            return Mathf.Abs(viewForward.x) * worldExtents.x
                + Mathf.Abs(viewForward.y) * worldExtents.y
                + Mathf.Abs(viewForward.z) * worldExtents.z
                - Vector3.Dot(centerFromRoot, viewForward);
        }

        public Vector3 HandOffset(Vector3 offset, bool left, float nearClip, float boundsRadius)
        {
            return new Vector3(left ? -Mathf.Abs(offset.x) : Mathf.Abs(offset.x), offset.y,
                Mathf.Max(offset.z, nearClip + boundsRadius + 0.0001f));
        }
    }
}
