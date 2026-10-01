// ============================================================================
// PlayerTraversalSmoothnessTests.cs
// ============================================================================
// PURPOSE:
//   Measures ideal-clock traversal sampling independently of the live renderer.
//   The legacy C0/tick-held reference is intentionally test-only; its numbers are
//   mathematical predictions, not a substitute for the Play Mode measurements.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Player.
// KEY RESPONSIBILITIES:
//   - Compare old/new 60 Hz paths sampled at 144 Hz using the same measurement window.
//   - Verify zero-motion, displacement-ratio and second-difference calculations.
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
        [Test]
        public void Ideal144HzSamplingRejectsLegacyTickHoldAndAcceptsInterpolatedC1Path()
        {
            const float dt = 1f / 60f, duration = .25f;
            var mover = new PlayerMoverPresenter();
            var state = new CameraDriverState();
            var before = new List<Vector3>();
            var after = new List<Vector3>();
            int lastTick = -1;
            for (int frame = 0; frame < 60; frame++)
            {
                float now = frame / 144f;
                int tick = Math.Min(15, (int)Math.Floor(now * 60f + .00001f));
                for (int step = lastTick + 1; step <= tick; step++)
                {
                    Vector3 eye = mover.TraversalPosition(Vector3.zero, Vector3.forward * 2.2f,
                        step * dt / duration, 1f, .08f, .25f, .95f) + Vector3.up * 1.6f;
                    CameraInterpolationPresenter.SetPosition(state, eye, step * dt, dt, 3f);
                    state.HasMovement = true;
                }
                lastTick = tick;
                if (now < 2f * dt || now >= duration + dt) continue;
                float p = Mathf.Clamp01(tick * dt / duration);
                float height = p < .25f ? 1.08f * p / .25f : p < .95f ? 1.08f : 1.08f * (1f - p) / .05f;
                before.Add(new Vector3(0f, height + 1.6f, 2.2f * p));
                after.Add(CameraInterpolationPresenter.Position(state, now));
            }
            VaultFrameMetrics oldMetrics = VaultFrameMetrics.Measure(before);
            VaultFrameMetrics newMetrics = VaultFrameMetrics.Measure(after);
            TestContext.WriteLine("IDEAL_60_144 legacy=" + oldMetrics + "; interpolated=" + newMetrics);
            Assert.That(oldMetrics.ZeroMotionFrames, Is.GreaterThan(0));
            Assert.That(oldMetrics.MaximumRatio, Is.GreaterThan(12f));
            Assert.That(oldMetrics.MaximumSecondDifference, Is.GreaterThan(.7f));
            Assert.That(newMetrics.ZeroMotionFrames, Is.Zero);
            Assert.That(newMetrics.MaximumRatio, Is.LessThan(12f));
            Assert.That(newMetrics.MaximumSecondDifference, Is.LessThan(.7f));
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
