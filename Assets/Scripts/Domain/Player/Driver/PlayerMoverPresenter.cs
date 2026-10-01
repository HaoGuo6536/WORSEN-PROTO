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
//   - Admit downward snap across separating rounded edges without accepting a rising jump.
//   - Distinguish overlap sentinels from real sweep contacts and retain slope-tangent motion.
//   - Decide support, wall-slide redirection and untagged ledge admission from supplied geometry.
//   - Remove only the hunter layer during grace and decide the once-per-session missing-layer warning.
//   - Select endpoints, knee-height probes and eased hop/grab/pull-up trajectories.
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
        public int MovementMask(int collisionMask, int hunterLayer, bool graceActive)
            => graceActive && hunterLayer >= 0 && hunterLayer < 32 ? collisionMask & ~(1 << hunterLayer) : collisionMask;

        public bool ShouldWarnMissingHunterLayer(PlayerDriverState session, int hunterLayer)
        {
            if (hunterLayer >= 0 || session.MissingHunterLayerWarned) return false;
            session.MissingHunterLayerWarned = true;
            return true;
        }

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

        public bool IsInitialOverlap(float distance, Vector3 point)
            => distance <= 0f && point.sqrMagnitude == 0f;

        public Vector3 PenetrationOffset(Vector3 direction, float depth, float skin)
            => direction.normalized * Mathf.Max(0f, depth + skin);

        public Vector3 ContactVelocity(Vector3 velocity, Vector3 normal, bool initialOverlap)
            => initialOverlap ? velocity : ProjectAfterHit(velocity, normal);

        public bool CanGround(Vector3 velocity, Vector3 normal, float slopeLimit)
            => IsWalkable(normal, slopeLimit) && Vector3.Dot(velocity, normal.normalized) <= 0.001f;

        public bool CanSnapToGround(Vector3 velocity, Vector3 normal, float slopeLimit)
            // A descending edge normal points along travel: separation is not a jump.
            // Positive vertical motion still requires slope-tangent/contact admission.
            => IsWalkable(normal, slopeLimit) && (velocity.y <= 0f || CanGround(velocity, normal, slopeLimit));

        public Vector3 RedirectSlide(Vector3 velocity, Vector3 normal, float retention)
        {
            Vector3 projected = ProjectAfterHit(velocity, normal);
            if (Vector3.Dot(velocity, normal) >= 0f || projected.sqrMagnitude < 0.0001f) return projected;
            return projected.normalized * Mathf.Max(projected.magnitude, velocity.magnitude * Mathf.Clamp01(retention));
        }

        public float EdgeProbeHeight(float minimumHeight, float chestHeight, float skin)
            => Mathf.Max(skin, Mathf.Min(minimumHeight, chestHeight > 0f ? chestHeight : minimumHeight) - skin);

        public Vector3 LedgePosition(Vector3 from, Vector3 to, float progress, float lift, float pullUpPortion)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f) return from;
            if (progress >= 1f) return to;
            float split = Mathf.Clamp(pullUpPortion, 0.1f, 0.8f);
            float rise = Mathf.Clamp01(progress / split);
            float cross = Mathf.Clamp01((progress - split) / (1f - split));
            // Grab holds at the near face, pulls the feet above it, then crosses.
            // Smooth joins avoid the old first-quarter vertical pop into a long hold.
            Vector3 position = Vector3.Lerp(from, to, cross * cross * (3f - 2f * cross));
            float apex = Mathf.Max(from.y, to.y) + Mathf.Max(0f, lift);
            position.y = progress < split ? Mathf.Lerp(from.y, apex, rise * rise * (3f - 2f * rise))
                : Mathf.Lerp(apex, to.y, cross * cross);
            return position;
        }

        public bool CanClimbLedge(bool chestBlocked, bool aboveBlocked, bool topFound, bool endpointBlocked,
            Vector3 feet, Vector3 top, Vector3 normal, float reach, float minimumHeight, float maximumHeight, float slopeLimit)
        {
            Vector3 delta = top - feet;
            return chestBlocked && !aboveBlocked && topFound && !endpointBlocked && Finite(delta)
                && delta.y >= minimumHeight && delta.y <= maximumHeight
                && new Vector2(delta.x, delta.z).magnitude <= reach && IsWalkable(normal, slopeLimit);
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

        public void TraversalPhases(Vector3 from, Vector3 to, Bounds? obstacle, float radius, float skin,
            float fallbackRise, float fallbackEnd, out float risePortion, out float traverseEnd)
        {
            risePortion = Mathf.Clamp(fallbackRise, 0.01f, 0.98f);
            traverseEnd = Mathf.Clamp(fallbackEnd, risePortion + 0.01f, 0.99f);
            if (!obstacle.HasValue || !Finite(from) || !Finite(to) || !Finite(radius) || !Finite(skin)
                || radius <= 0f || skin < 0f || skin >= radius) return;
            Bounds bounds = obstacle.Value;
            if (!Finite(bounds.center) || !Finite(bounds.extents) || bounds.extents.x < 0f
                || bounds.extents.y < 0f || bounds.extents.z < 0f) return;
            Vector3 path = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float length = path.magnitude;
            if (!Finite(length) || length <= 0.000001f) return;
            Vector3 direction = path / length;
            float center = Vector3.Dot(bounds.center - from, direction);
            // Project the enclosing world bounds, not its diagonal or the approach heading.
            // This is conservative for rotated/non-box colliders and works in either direction.
            float extent = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
            // The front reaches the near face's skin band; the back clears the far face
            // plus skin. Using the inset query radius on entry leaves a finite rise even
            // at the closest clear (full radius + skin) start, without a takeoff teleport.
            float near = (center - extent - radius + skin) / length;
            float far = (center + extent + radius + skin) / length;
            // No usable through-span (e.g. a mantle landing on top) keeps authored fallbacks.
            if (!Finite(near) || !Finite(far) || extent <= 0f || near <= 0f || far >= 1f || near >= far) return;
            risePortion = near;
            traverseEnd = far;
        }

        public Vector3 TraversalPosition(Vector3 from, Vector3 to, float progress, float obstacleHeight, float lift, float risePortion, float traverseEnd)
        {
            progress = Mathf.Clamp01(progress);
            if (progress <= 0f) return from;
            if (progress >= 1f) return to;
            // Horizontal travel uses the complete lock while the vertical envelope provides clearance.
            Vector3 position = Vector3.Lerp(from, to, progress);
            float top = Mathf.Max(from.y + Mathf.Max(0f, obstacleHeight), to.y) + Mathf.Max(0f, lift);
            // Numerical guards only: do not stretch a short geometry-derived rise or
            // start falling before a deep obstacle has cleared the capsule's back.
            risePortion = Mathf.Clamp(risePortion, 0.000001f, 0.999998f);
            traverseEnd = Mathf.Clamp(traverseEnd, risePortion, 0.999999f);
            if (progress < risePortion)
            {
                float t = progress / risePortion;
                position.y = Mathf.Lerp(from.y, top, 1f - (1f - t) * (1f - t));
            }
            else if (progress < traverseEnd)
                position.y = top;
            else
            {
                float t = (progress - traverseEnd) / (1f - traverseEnd);
                position.y = Mathf.Lerp(top, to.y, t * t);
            }
            return position;
        }
    }
}
