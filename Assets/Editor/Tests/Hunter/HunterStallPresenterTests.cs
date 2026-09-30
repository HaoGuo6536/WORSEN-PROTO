// ============================================================================
// HunterStallPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the fact-only stall contract with explicit deterministic samples.
//   No scene, physics, clock, randomness or mutable configuration asset is needed.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Check strict thresholds, sliding expiry, episode reset and signed path geometry.
// DEPENDENCIES:
//   - Hunter presenter/state/fact, Core identity, NUnit and UnityEngine values.
// USAGE NOTES:
//   Pure EditMode tests; the coordinator runs these in Unity after integration.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterStallPresenterTests
    {
        private readonly HunterStallPresenter _presenter = new HunterStallPresenter();
        private bool Sample(HunterStallDriverState state, double dt, double progress = 0, double remaining = 10, bool path = true)
            => _presenter.Tick(state, dt, path, remaining, progress, 0.75, 0.25, 1);
        [TestCase(0.749, false)] [TestCase(0.75, false)] [TestCase(0.751, true)]
        public void DurationIsStrict(double dt, bool expected)
            => Assert.That(Sample(new HunterStallDriverState(), dt), Is.EqualTo(expected));
        [TestCase(0.999, false)] [TestCase(1, false)] [TestCase(1.001, true)]
        public void RemainingIsStrict(double remaining, bool expected)
            => Assert.That(Sample(new HunterStallDriverState(), 1, 0, remaining), Is.EqualTo(expected));
        [TestCase(0.249, true)] [TestCase(0.25, false)] [TestCase(0.251, false)]
        public void ProgressIsStrict(double progressInWindow, bool expected)
            => Assert.That(Sample(new HunterStallDriverState(), 1, progressInWindow / 0.75), Is.EqualTo(expected));
        [Test] public void MovingHunterNeverStalls()
        {
            var state = new HunterStallDriverState();
            for (int i = 0; i < 1000; i++) Assert.That(Sample(state, 0.125, 0.125), Is.False);
        }
        [Test] public void ArrivedAndEmptyPathsNeverStall()
        {
            var arrived = new HunterStallDriverState(); var empty = new HunterStallDriverState();
            for (int i = 0; i < 100; i++)
            { Assert.That(Sample(arrived, 0.125, 0, 0.99), Is.False); Assert.That(Sample(empty, 0.125, 0, 10, false), Is.False); }
        }
        [Test] public void OneFactPerEpisodeAndProgressRearms()
        {
            var state = new HunterStallDriverState();
            Assert.That(Sample(state, 1), Is.True);
            for (int i = 0; i < 10; i++) Assert.That(Sample(state, 1), Is.False);
            Assert.That(Sample(state, 0.75, 0.5), Is.False);
            Assert.That(Sample(state, 1), Is.True);
        }
        [Test] public void SlidingWindowExpiresOldProgressAndDoesNotCountJitterDistance()
        {
            var state = new HunterStallDriverState();
            Assert.That(Sample(state, 0.5, 1), Is.False);
            Assert.That(Sample(state, 0.375), Is.False);
            Assert.That(Sample(state, 0.25), Is.False); // Exactly .25 m remains in the window.
            Assert.That(Sample(state, 0.125), Is.True);
            var jitter = new HunterStallDriverState();
            for (int i = 0; i < 6; i++) Assert.That(Sample(jitter, 0.125, i % 2 == 0 ? 0.01 : -0.01), Is.False);
            Assert.That(Sample(jitter, 0.125, 0.01), Is.True);
        }
        [Test] public void ResetRequiresAnotherFullWindow()
        {
            var state = new HunterStallDriverState();
            Assert.That(Sample(state, 1), Is.True);
            Assert.That(Sample(state, 1, 0, 10, false), Is.False);
            Assert.That(Sample(state, 0.75), Is.False);
            Assert.That(Sample(state, 0.001), Is.True);
        }
        [Test] public void IdenticalInputsGiveIdenticalFacts()
        {
            var a = new HunterStallDriverState(); var b = new HunterStallDriverState();
            for (int i = 0; i < 1000; i++)
                Assert.That(Sample(a, 0.125, i % 17 == 0 ? 0.5 : 0), Is.EqualTo(Sample(b, 0.125, i % 17 == 0 ? 0.5 : 0)));
        }
        [Test] public void ProjectionFollowsBentPathAndIgnoresLateralMotionOrRefresh()
        {
            var path = new[] { Vector3.zero, Vector3.right * 5, new Vector3(5, 0, 5) };
            Assert.That(_presenter.Remaining(Vector3.right * 2, path), Is.EqualTo(8).Within(0.00001));
            Assert.That(_presenter.Remaining(new Vector3(2, 0, -1), path), Is.EqualTo(8).Within(0.00001));
            Assert.That(_presenter.Remaining(new Vector3(5, 0, 2), path), Is.EqualTo(3).Within(0.00001));
            var state = new HunterStallDriverState();
            int facts = 0;
            for (int tick = 0; tick < 30; tick++)
            {
                // The destination and corner count change without any physical motion.
                var refreshed = tick % 2 == 0 ? path : new[] { Vector3.zero, Vector3.forward * 50 };
                if (_presenter.Observe(state, Vector3.zero, refreshed, true, 0.125, 0.75, 0.25, 1, out _)) facts++;
            }
            Assert.That(facts, Is.EqualTo(1));
        }
        [Test] public void ObservedMovingPoseDoesNotStallAcrossPathRefreshes()
        {
            var state = new HunterStallDriverState();
            for (int tick = 0; tick < 100; tick++)
            {
                Vector3 position = Vector3.right * tick;
                var path = new[] { position, position + Vector3.right * 10 };
                Assert.That(_presenter.Observe(state, position, path, true, 0.125, 0.75, 0.25, 1, out _), Is.False);
            }
        }
        [Test] public void InvalidSamplesResetAndFactsFreezeCorners()
        {
            var state = new HunterStallDriverState();
            Assert.That(Sample(state, double.NaN), Is.False);
            Assert.That(Sample(state, -1), Is.False);
            Assert.That(Sample(state, 0.75), Is.False);
            var path = new[] { Vector3.zero, Vector3.forward };
            var fact = new HunterStallFact(EntityId.None, 1, Vector3.zero, null, path, 0.5f, 0.4f, 0.4f, HunterAction.Patrol, 2);
            path[0] = Vector3.one;
            Assert.That(fact.PathCorners[0], Is.EqualTo(Vector3.zero));
            Assert.That(fact.NearestObstaclePoint, Is.Null);
        }
    }
}
