// ============================================================================
// HunterSteeringPresenter.cs
// ============================================================================
// PURPOSE:
//   Converts supplied path corners and movement parameters into inertial motion.
//   Navigation provides the route but never chooses the Hunter's acceleration,
//   heading changes or committed lunge movement; those calculations live here.
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Limit normal velocity changes and heading turns with injected time.
//   - Hold recovery motion at zero and cap each committed lunge's total travel.
//   - Preserve an active segment only when a fresh route validates its direction.
//   - Permit rejected-gap recovery only from a verified, connected entry bank.
//   - Keep terminal velocity at rest when collision resolution realizes arrival.
// DEPENDENCIES:
//   - HunterSteeringDriverState and UnityEngine Vector3 value data only.
// USAGE NOTES:
//   Stateless, planar math; the Driver supplies paths and applies collision/grounding.
//   Begin active lunge on a false-to-true lungeActive transition. Direction is captured
//   once from the Controller's committed direction and cannot be retargeted mid-lunge.
//   Terminal path arrival stops at the endpoint; normal path motion retains inertia.
//   Terminal speed accounts for heading and the remaining forward projection so
//   a finite turning rate cannot sustain an orbit around the final point.
//   Terminal turn speed is bounded by the circle through the current heading and
//   endpoint, preventing each discrete turn from sustaining a nearby-goal spiral.
//   Gap refreshes require a direct, freshly validated local crossing and target tail.
//   A bank-sampled path alone cannot authorize skipping the gap's entry corner.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Worsen.Domain.Hunter
{
    public sealed class HunterSteeringPresenter
    {
        public void Reset(HunterSteeringDriverState state, Vector3 position, Vector3 forward)
        {
            state.Position = position;
            state.Velocity = Vector3.zero;
            state.Forward = Direction(forward, Vector3.forward);
            state.Corners = Array.Empty<Vector3>();
            state.CornerIndex = 0;
            state.LungeDirection = Vector3.zero;
            state.LungeDistanceTravelled = 0f;
            state.LungeWasActive = false;
        }

        public void SetPath(HunterSteeringDriverState state, IReadOnlyList<Vector3> corners)
        {
            int count = corners == null ? 0 : corners.Count;
            state.Corners = new Vector3[count];
            for (int i = 0; i < count; i++) state.Corners[i] = corners[i];
            state.CornerIndex = 0;
        }

        public bool IsOnNavigationSample(Vector3 position, Vector3 sample)
            => Horizontal(position - sample).sqrMagnitude <= 0.0001f;

        public bool HasActiveSegmentProgress(HunterSteeringDriverState state, Vector3 position, float cornerTolerance)
        {
            if (state.CornerIndex <= 0 || state.CornerIndex >= state.Corners.Length) return false;
            Vector3 entry = state.Corners[state.CornerIndex - 1];
            Vector3 segment = Horizontal(state.Corners[state.CornerIndex] - entry);
            Vector3 offset = Horizontal(position - entry);
            float lengthSquared = segment.sqrMagnitude;
            float progress = Vector3.Dot(offset, segment);
            if (lengthSquared <= 0.0001f || progress <= 0f || progress >= lengthSquared) return false;
            Vector3 lateral = offset - segment * (progress / lengthSquared);
            float tolerance = Nonnegative(cornerTolerance);
            return lateral.sqrMagnitude <= tolerance * tolerance;
        }

        public bool CanReleaseGap(HunterSteeringDriverState state, Vector3 entry, Vector3 exit, Vector3 position, Vector3 sample)
        {
            if (!IsOnNavigationSample(position, sample)) return false;
            Vector3 segment = Horizontal(exit - entry);
            float progress = Vector3.Dot(Horizontal(position - entry), segment);
            return progress <= 0f || progress >= segment.sqrMagnitude || (state.Corners.Length > 0 && state.CornerIndex > 1);
        }

        public bool IsNavigationAnchor(Vector3 anchor, Vector3 sample, float verticalTolerance)
            => IsOnNavigationSample(anchor, sample) && Math.Abs(anchor.y - sample.y) <= Nonnegative(verticalTolerance);

        public bool CanLeaveRejectedGap(Vector3 position, Vector3 sample, Vector3 entry, Vector3 sampledEntry,
            bool directReturnToEntry, float verticalTolerance)
            => directReturnToEntry && IsNavigationAnchor(position, sample, verticalTolerance) &&
                IsNavigationAnchor(entry, sampledEntry, verticalTolerance);

        public bool IsDirectSegmentPath(IReadOnlyList<Vector3> corners, Vector3 entry, Vector3 exit, float verticalTolerance)
        {
            if (corners == null || corners.Count < 2 || !IsNavigationAnchor(entry, corners[0], verticalTolerance) ||
                !IsNavigationAnchor(exit, corners[corners.Count - 1], verticalTolerance)) return false;
            Vector3 segment = Horizontal(exit - entry);
            float length = segment.magnitude;
            if (length <= 0.01f) return false;
            Vector3 direction = segment / length;
            float previous = 0f;
            foreach (Vector3 corner in corners)
            {
                Vector3 offset = Horizontal(corner - entry);
                float progress = Vector3.Dot(offset, direction);
                Vector3 projected = entry + (exit - entry) * (progress / length);
                if (progress < previous - 0.01f || progress > length + 0.01f ||
                    !IsNavigationAnchor(projected, corner, verticalTolerance)) return false;
                previous = progress;
            }
            return true;
        }

        public bool TrySetGapPath(HunterSteeringDriverState state, Vector3 entry, Vector3 exit,
            IReadOnlyList<Vector3> forwardTail, IReadOnlyList<Vector3> reverseTail, bool preferReverse, out bool reverse)
        {
            float forwardCost = TailCost(state.Position, exit, forwardTail);
            float reverseCost = TailCost(state.Position, entry, reverseTail);
            reverse = reverseCost < forwardCost || (preferReverse && reverseCost == forwardCost);
            IReadOnlyList<Vector3> tail = reverse ? reverseTail : forwardTail;
            if (tail == null || tail.Count == 0)
            { SetPath(state, null); return false; }
            Vector3[] corners = new Vector3[tail.Count + 2];
            corners[0] = reverse ? exit : entry;
            corners[1] = reverse ? entry : exit;
            for (int i = 0; i < tail.Count; i++) corners[i + 2] = tail[i];
            SetPath(state, corners);
            state.CornerIndex = 1;
            return true;
        }

        private static float TailCost(Vector3 position, Vector3 bank, IReadOnlyList<Vector3> tail)
        {
            if (tail == null || tail.Count == 0) return float.PositiveInfinity;
            float cost = Horizontal(bank - position).magnitude;
            for (int i = 1; i < tail.Count; i++) cost += Horizontal(tail[i] - tail[i - 1]).magnitude;
            return cost;
        }

        public void ReconcileMovement(HunterSteeringDriverState state, Vector3 start, Vector3 actualPosition,
            Vector3 requestedMovement, float dt)
        {
            Vector3 displacement = actualPosition - start;
            Vector3 velocity = displacement / dt;
            if (Horizontal(displacement - requestedMovement).sqrMagnitude <= 0.00000001f)
                velocity = new Vector3(state.Velocity.x, velocity.y, state.Velocity.z);
            state.Position = actualPosition;
            state.Velocity = velocity;
        }

        public Vector3 Tick(HunterSteeringDriverState state, float dt, float speed, float acceleration,
            float turnRateDegrees, bool recovery, bool lungeActive, Vector3 committedLungeDirection,
            float lungeSpeed, float maximumLungeDistance, float cornerTolerance = 0.25f)
        {
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return Vector3.zero;
            if (recovery)
            {
                state.Velocity = Vector3.zero;
                state.LungeWasActive = false;
                return Vector3.zero;
            }
            if (lungeActive)
            {
                if (!state.LungeWasActive)
                {
                    state.LungeDirection = Direction(committedLungeDirection, state.Forward);
                    state.LungeDistanceTravelled = 0f;
                }
                state.LungeWasActive = true;
                float remaining = Math.Max(0f, Nonnegative(maximumLungeDistance) - state.LungeDistanceTravelled);
                float travel = Math.Min(Nonnegative(lungeSpeed) * dt, remaining);
                Vector3 displacement = state.LungeDirection * travel;
                state.LungeDistanceTravelled += travel;
                state.Forward = state.LungeDirection;
                state.Velocity = displacement / dt;
                state.Position += displacement;
                return displacement;
            }
            state.LungeWasActive = false;
            return FollowPath(state, dt, Nonnegative(speed), Nonnegative(acceleration), Nonnegative(turnRateDegrees), Nonnegative(cornerTolerance));
        }

        private static Vector3 FollowPath(HunterSteeringDriverState state, float dt, float speed, float acceleration, float turnRate, float cornerTolerance)
        {
            Vector3 targetDelta = Vector3.zero;
            while (state.CornerIndex < state.Corners.Length)
            {
                targetDelta = Horizontal(state.Corners[state.CornerIndex] - state.Position);
                float arrivalDistance = state.CornerIndex == state.Corners.Length - 1 ? 0.0001f : cornerTolerance;
                if (targetDelta.sqrMagnitude > arrivalDistance * arrivalDistance) break;
                state.CornerIndex++;
            }
            bool hasCorner = state.CornerIndex < state.Corners.Length;
            float targetDistance = hasCorner ? targetDelta.magnitude : 0f;
            bool finalCorner = hasCorner && state.CornerIndex == state.Corners.Length - 1;
            if (hasCorner) state.Forward = Turn(state.Forward, targetDelta, turnRate * dt);
            float desiredSpeed = hasCorner ? speed : 0f;
            if (finalCorner)
            {
                Vector3 heading = Direction(state.Forward, Vector3.forward);
                Vector3 targetDirection = Direction(targetDelta, state.Forward);
                float alignment = Math.Max(0f, Vector3.Dot(heading, targetDirection));
                desiredSpeed = Math.Min(desiredSpeed, (float)Math.Sqrt(2f * acceleration * targetDistance));
                desiredSpeed = Math.Min(desiredSpeed, targetDistance / dt) * alignment;
                float lateral = Math.Abs(heading.x * targetDirection.z - heading.z * targetDirection.x);
                if (lateral > 0f)
                    desiredSpeed = Math.Min(desiredSpeed, (float)(turnRate * Math.PI / 180.0) * targetDistance / (2f * lateral));
            }
            Vector3 desiredVelocity = Direction(state.Forward, Vector3.forward) * desiredSpeed;
            Vector3 difference = desiredVelocity - state.Velocity;
            float change = difference.magnitude;
            state.Velocity += change > 0f ? difference * Math.Min(1f, acceleration * dt / change) : Vector3.zero;
            Vector3 movement = state.Velocity * dt;
            if (finalCorner && movement.sqrMagnitude >= targetDelta.sqrMagnitude && Vector3.Dot(movement, targetDelta) > 0f &&
                Vector3.Dot(Direction(movement, Vector3.forward), Direction(targetDelta, Vector3.forward)) > 0.99f)
            {
                movement = targetDelta;
                state.Velocity = Vector3.zero;
                state.CornerIndex++;
            }
            state.Position += movement;
            return movement;
        }

        private static Vector3 Turn(Vector3 forward, Vector3 target, float maximumDegrees)
        {
            forward = Direction(forward, Vector3.forward);
            target = Direction(target, forward);
            double angle = Math.Atan2(forward.x * target.z - forward.z * target.x, Vector3.Dot(forward, target));
            double limit = maximumDegrees * Math.PI / 180.0;
            angle = Math.Max(-limit, Math.Min(limit, angle));
            float cosine = (float)Math.Cos(angle), sine = (float)Math.Sin(angle);
            return new Vector3(forward.x * cosine - forward.z * sine, 0f, forward.x * sine + forward.z * cosine);
        }

        private static Vector3 Horizontal(Vector3 value) => new Vector3(value.x, 0f, value.z);
        private static Vector3 Direction(Vector3 value, Vector3 fallback)
        {
            value = Horizontal(value);
            if (value.sqrMagnitude < 0.000001f) value = Horizontal(fallback);
            return value.sqrMagnitude > 0.000001f ? value / value.magnitude : Vector3.forward;
        }
        private static float Nonnegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(0f, value);
    }
}
