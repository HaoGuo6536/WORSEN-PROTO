// ============================================================================
// PlayerMoverPresenter.cs
// ============================================================================
// PURPOSE:
//   Computes capsule geometry, collision projection and render interpolation without physics access.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Player.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
//   - Select the opposite authored traversal endpoint from feet and approach, rejecting invalid pairs.
//   - Use the full traversal duration for horizontal travel while retaining the vertical clearance envelope.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Stateless pure math. Collision distances and normals are inputs obtained by PlayerDriver.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;

namespace Worsen.Domain.Player
{
    public sealed class PlayerMoverPresenter
    {
        public void Capsule(Vector3 feet, float height, float radius, out Vector3 bottom, out Vector3 top)
        {
            radius = Mathf.Max(0.001f, radius);
            height = Mathf.Max(height, radius * 2f);
            bottom = feet + Vector3.up * radius;
            top = feet + Vector3.up * (height - radius);
        }

        public Vector3 TravelBeforeHit(Vector3 displacement, float hitDistance, float skinWidth)
        {
            float length = displacement.magnitude;
            return length > 0f ? displacement / length * Mathf.Clamp(hitDistance - skinWidth, 0f, length) : Vector3.zero;
        }

        public Vector3 ProjectAfterHit(Vector3 displacement, Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.0001f) return Vector3.zero;
            normal.Normalize();
            return Vector3.Dot(displacement, normal) < 0f ? Vector3.ProjectOnPlane(displacement, normal) : displacement;
        }

        public bool IsWalkable(Vector3 normal, float slopeLimit)
            => normal.sqrMagnitude > 0.0001f && Vector3.Dot(normal.normalized, Vector3.up) >= Mathf.Cos(slopeLimit * Mathf.Deg2Rad);

        public Vector3 GroundSnap(Vector3 position, float hitDistance, float skinWidth, float maximumDistance)
            => position + Vector3.down * Mathf.Clamp(hitDistance - skinWidth, 0f, maximumDistance);

        public bool IsValidStep(Vector3 from, Vector3 to, float stepHeight)
            => to.y >= from.y - 0.001f && to.y - from.y <= stepHeight + 0.001f;

        public bool TrySelectTraversalEndpoint(Vector3 feet, Vector3 approach, Vector3 endpointA,
            Vector3 endpointB, out Vector3 target)
        {
            target = Vector3.zero;
            if (!Finite(feet) || !Finite(approach) || !Finite(endpointA) || !Finite(endpointB)) return false;
            Vector3 axis = new Vector3(endpointB.x - endpointA.x, 0f, endpointB.z - endpointA.z);
            Vector3 forward = new Vector3(approach.x, 0f, approach.z);
            float axisLengthSquared = axis.sqrMagnitude;
            float forwardLengthSquared = forward.sqrMagnitude;
            if (!Finite(axisLengthSquared) || !Finite(forwardLengthSquared)
                || axisLengthSquared <= 0.00000001f || forwardLengthSquared <= 0.00000001f) return false;
            axis.Normalize();
            forward.Normalize();
            Vector3 midpoint = endpointA * 0.5f + endpointB * 0.5f;
            Vector3 offset = new Vector3(feet.x - midpoint.x, 0f, feet.z - midpoint.z);
            float side = Vector3.Dot(offset, axis);
            if (!Finite(offset) || !Finite(side)) return false;
            float alongAxis = Vector3.Dot(forward, axis);
            bool chooseB = Mathf.Abs(side) <= 0.00001f ? alongAxis > 0f : side < 0f;
            if ((chooseB ? alongAxis : -alongAxis) <= 0.00001f) return false;
            Vector3 selected = chooseB ? endpointB : endpointA;
            Vector3 toTarget = new Vector3(selected.x - feet.x, 0f, selected.z - feet.z);
            float ahead = Vector3.Dot(toTarget, forward);
            if (!Finite(toTarget) || !Finite(ahead) || ahead <= 0.00001f) return false;
            target = selected;
            return true;
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public Vector3 InterpolatePosition(PlayerDriverState state, float now)
        {
            float alpha = state.LastStepDuration > 0f ? Mathf.Clamp01((now - state.LastStepTime) / state.LastStepDuration) : 1f;
            return Vector3.Lerp(state.PreviousPosition, state.Position, alpha);
        }

        public float InterpolateHeading(PlayerDriverState state, float now)
        {
            float alpha = state.LastStepDuration > 0f ? Mathf.Clamp01((now - state.LastStepTime) / state.LastStepDuration) : 1f;
            return Mathf.LerpAngle(state.PreviousHeading, state.Heading, alpha);
        }

        public Vector3 EyePosition(PlayerDriverState state, float eyeHeight, float standingHeight)
            => state.Position + Vector3.up * eyeHeight * (state.Height / Mathf.Max(0.001f, standingHeight));

        public Vector3 LimitHorizontalDisplacement(Vector3 displacement, float maximumSpeed, float dt)
        {
            Vector3 horizontal = Vector3.ClampMagnitude(new Vector3(displacement.x, 0f, displacement.z),
                Mathf.Max(0f, maximumSpeed) * Mathf.Max(0f, dt));
            return new Vector3(horizontal.x, displacement.y, horizontal.z);
        }

        public Vector3 TraversalPosition(Vector3 from, Vector3 to, float progress, float obstacleHeight, float lift, float risePortion, float traverseEnd)
        {
            progress = Mathf.Clamp01(progress);
            // Horizontal travel uses the complete lock while the vertical envelope provides clearance.
            Vector3 position = Vector3.Lerp(from, to, progress);
            float top = Mathf.Max(from.y + Mathf.Max(0f, obstacleHeight), to.y) + Mathf.Max(0f, lift);
            risePortion = Mathf.Clamp(risePortion, 0.01f, 0.98f);
            traverseEnd = Mathf.Clamp(traverseEnd, risePortion + 0.01f, 0.99f);
            if (progress < risePortion)
                position.y = Mathf.Lerp(from.y, top, progress / risePortion);
            else if (progress < traverseEnd)
                position.y = top;
            else
                position.y = Mathf.Lerp(top, to.y, (progress - traverseEnd) / (1f - traverseEnd));
            return position;
        }
    }
}
