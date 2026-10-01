// ============================================================================
// PlayerTraversalSmoothnessTests.cs
// ============================================================================
// PURPOSE:
//   Measures ideal-clock traversal sampling independently of the live renderer.
//   The legacy and pass-one references are intentionally test-only; their numbers are
//   mathematical predictions, not a substitute for the Play Mode measurements.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player.
// KEY RESPONSIBILITIES:
//   - Compare old/new 60 Hz paths sampled at 144 Hz using the same measurement window.
//   - Verify zero-motion, displacement-ratio and second-difference calculations.
//   - Predict descent speed and near-face capsule clearance before/after geometry timing.
// DEPENDENCIES:
//   PlayerMoverPresenter, CameraInterpolationPresenter, NUnit and pure value math.
// USAGE NOTES:
//   No physics or renderer is simulated. The window excludes the first two ticks
//   but includes the short final descent through its one-tick presentation delay.
//   Live tests use the same metrics without resampling or dropping slow frames.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Player;
using Worsen.Presentation.Camera;

namespace Worsen.Tests.Player
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class PlayerTraversalSmoothnessTests
    {
        [TestCase(1f)]
        [TestCase(.2f)]
        public void Ideal144HzSamplingRejectsLegacyTickHoldAndAcceptsInterpolatedC1Path(float obstacleDepth)
        {
            const float dt = 1f / 60f, duration = .25f;
            var mover = new PlayerMoverPresenter();
            var state = new CameraDriverState();
            var passOneState = new CameraDriverState();
            var before = new List<Vector3>();
            var passOne = new List<Vector3>();
            var after = new List<Vector3>();
            var target = Vector3.forward * 2.2f;
            var obstacle = new Bounds(new Vector3(0f, .5f, .6f + obstacleDepth * .5f), new Vector3(2f, 1f, obstacleDepth));
            mover.TraversalPhases(Vector3.zero, target, obstacle, .3f, .02f, .25f, .95f, out float rise, out float end);
            int lastTick = -1;
            for (int frame = 0; frame < 60; frame++)
            {
                float now = frame / 144f;
                int tick = Math.Min(15, (int)Math.Floor(now * 60f + .00001f));
                for (int step = lastTick + 1; step <= tick; step++)
                {
                    Vector3 eye = mover.TraversalPosition(Vector3.zero, target,
                        step * dt / duration, 1f, .08f, rise, end) + Vector3.up * 1.6f;
                    CameraInterpolationPresenter.SetPosition(state, eye, step * dt, dt, 3f);
                    state.HasMovement = true;
                    Vector3 oldEye = PassOnePosition(Vector3.zero, target, step * dt / duration) + Vector3.up * 1.6f;
                    CameraInterpolationPresenter.SetPosition(passOneState, oldEye, step * dt, dt, 3f);
                    passOneState.HasMovement = true;
                }
                lastTick = tick;
                if (now < 2f * dt || now >= duration + dt) continue;
                float p = Mathf.Clamp01(tick * dt / duration);
                float height = p < .25f ? 1.08f * p / .25f : p < .95f ? 1.08f : 1.08f * (1f - p) / .05f;
                before.Add(new Vector3(0f, height + 1.6f, 2.2f * p));
                passOne.Add(CameraInterpolationPresenter.Position(passOneState, now));
                after.Add(CameraInterpolationPresenter.Position(state, now));
            }
            VaultFrameMetrics oldMetrics = VaultFrameMetrics.Measure(before);
            VaultFrameMetrics passOneMetrics = VaultFrameMetrics.Measure(passOne);
            VaultFrameMetrics newMetrics = VaultFrameMetrics.Measure(after);
            float beforePeak = 1.5f * 1.08f / (duration * (1f - .95f));
            float afterPeak = 2f * 1.08f / (duration * (1f - end));
            TestContext.WriteLine($"IDEAL_60_144 depth={obstacleDepth:R}; rise={rise:R}; end={end:R}; " +
                $"fallMsBefore={duration * (1f - .95f) * 1000f:R}; fallMsAfter={duration * (1f - end) * 1000f:R}; " +
                $"peakDescentBefore={beforePeak:R}; peakDescentAfter={afterPeak:R}; legacy={oldMetrics}; pass1={passOneMetrics}; pass2={newMetrics}");
            Assert.That(oldMetrics.ZeroMotionFrames, Is.GreaterThan(0));
            Assert.That(oldMetrics.MaximumRatio, Is.GreaterThan(12f));
            Assert.That(oldMetrics.MaximumSecondDifference, Is.GreaterThan(.7f));
            Assert.That(newMetrics.ZeroMotionFrames, Is.Zero);
            Assert.That(newMetrics.MaximumRatio, Is.LessThan(12f));
            Assert.That(newMetrics.MaximumSecondDifference, Is.LessThan(.7f));
            Assert.That(newMetrics.MaximumSecondDifference, Is.LessThan(passOneMetrics.MaximumSecondDifference));
            Assert.That(newMetrics.MaximumRatio, Is.LessThan(passOneMetrics.MaximumRatio));
            Assert.That(afterPeak, Is.LessThan(beforePeak));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ClosestClearFacePredictionRegressesPassOneWithoutRelaxingPhysicalTolerance(bool reverse)
        {
            const float radius = .3f, skin = .02f, tolerance = .041f;
            var obstacle = new Bounds(new Vector3(2.5f, .5f, 0f), new Vector3(1f, 1f, 2f));
            var from = Vector3.right * (reverse ? 3f + radius + skin : 2f - radius - skin);
            var to = Vector3.right * (reverse ? 1.4f : 3.6f);
            var mover = new PlayerMoverPresenter();
            mover.TraversalPhases(from, to, obstacle, radius, skin, .25f, .95f, out float rise, out float end);
            float firstOldDepth = PredictedPenetration(PassOnePosition(from, to, 1f / 15f), obstacle, radius);
            Assert.That(firstOldDepth, Is.EqualTo(.108f).Within(.00001f), "The first failing tick in the physical regression.");
            float oldDepth = 0f, newDepth = 0f;
            for (int tick = 0; tick <= 15; tick++)
            {
                oldDepth = Mathf.Max(oldDepth, PredictedPenetration(PassOnePosition(from, to, tick / 15f), obstacle, radius));
                newDepth = Mathf.Max(newDepth, PredictedPenetration(mover.TraversalPosition(from, to, tick / 15f,
                    1f, .08f, rise, end), obstacle, radius));
            }
            TestContext.WriteLine($"NEAR_FACE_PREDICTION reverse={reverse}; pass1FirstMetres={firstOldDepth:R}; pass1PeakMetres={oldDepth:R}; pass2PeakMetres={newDepth:R}; tolerance={tolerance:R}");
            Assert.That(oldDepth, Is.GreaterThan(tolerance));
            Assert.That(newDepth, Is.LessThanOrEqualTo(tolerance));
            // Also inspect between committed samples; this is analytic capsule/box
            // clearance on the centerline, not PhysX or a claim about other colliders.
            for (int sample = 0; sample <= 10000; sample++)
                Assert.That(PredictedPenetration(mover.TraversalPosition(from, to, sample / 10000f,
                    1f, .08f, rise, end), obstacle, radius), Is.LessThanOrEqualTo(tolerance));
        }

        private static float PredictedPenetration(Vector3 feet, Bounds obstacle, float radius)
        {
            float horizontalGap = Mathf.Max(obstacle.min.x - feet.x, Mathf.Max(0f, feet.x - obstacle.max.x));
            float verticalGap = Mathf.Max(0f, feet.y + radius - obstacle.max.y);
            return Mathf.Max(0f, radius - Mathf.Sqrt(horizontalGap * horizontalGap + verticalGap * verticalGap));
        }

        private static Vector3 PassOnePosition(Vector3 from, Vector3 to, float progress)
        {
            progress = Mathf.Clamp01(progress);
            var position = Vector3.Lerp(from, to, progress);
            float t = progress < .25f ? progress / .25f : (progress - .95f) / (1f - .95f);
            float eased = t * t * (3f - 2f * t);
            position.y = progress < .25f ? Mathf.Lerp(from.y, 1.08f, eased)
                : progress < .95f ? 1.08f : Mathf.Lerp(1.08f, to.y, eased);
            return position;
        }

        [Test]
        public void MetricCountsZeroStepsAndDoesNotHideTheirInfiniteRatio()
        {
            var result = VaultFrameMetrics.Measure(new[] { Vector3.zero, Vector3.right, Vector3.right, Vector3.right * 2f });
            Assert.That(result.ZeroMotionFrames, Is.EqualTo(1));
            Assert.That(float.IsPositiveInfinity(result.MaximumRatio), Is.True);
            Assert.That(result.MaximumSecondDifference, Is.EqualTo(1f));
        }
    }

    internal readonly struct VaultFrameMetrics
    {
        public readonly int Frames, ZeroMotionFrames;
        public readonly float MaximumRatio, MaximumSecondDifference;
        private VaultFrameMetrics(int frames, int zeros, float ratio, float second)
        { Frames = frames; ZeroMotionFrames = zeros; MaximumRatio = ratio; MaximumSecondDifference = second; }

        public static VaultFrameMetrics Measure(IReadOnlyList<Vector3> positions)
        {
            const float zero = .000001f;
            int zeros = 0;
            float maximumRatio = 1f, maximumSecond = 0f;
            Vector3 previous = Vector3.zero;
            for (int i = 1; i < positions.Count; i++)
            {
                Vector3 displacement = positions[i] - positions[i - 1];
                float length = displacement.magnitude;
                if (length <= zero) zeros++;
                if (i > 1)
                {
                    float smaller = Mathf.Min(length, previous.magnitude), larger = Mathf.Max(length, previous.magnitude);
                    float ratio = smaller <= zero ? (larger <= zero ? 1f : float.PositiveInfinity) : larger / smaller;
                    maximumRatio = Mathf.Max(maximumRatio, ratio);
                    maximumSecond = Mathf.Max(maximumSecond, (displacement - previous).magnitude);
                }
                previous = displacement;
            }
            return new VaultFrameMetrics(positions.Count, zeros, maximumRatio, maximumSecond);
        }

        public override string ToString() => $"frames={Frames}, zero={ZeroMotionFrames}, maxRatio={MaximumRatio:R}, maxSecondMetres={MaximumSecondDifference:R}";
    }
}
