// ============================================================================
// FloorHandPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Specifies bounded hand flailing and near-player strain without scene objects.
//   Identical owner times must produce identical poses regardless of tick history.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Verify deterministic time motion, per-hand variation and finite displacement bounds.
//   - Verify continuous range entry, directional strain and neutral distant targets.
// DEPENDENCIES:
//   - NUnit, Unity value types and FloorHandPresenter.
// USAGE NOTES:
//   Pure tests. Hand art, silhouettes and actual grab feedback need owner playtest.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Floor;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorHandPresenterTests
    {
        [Test]
        public void FlailIsContinuousDeterministicVariedAndBounded()
        {
            for (int hand = 0; hand < 25; hand++)
                for (int sample = 0; sample < 120; sample++)
                {
                    float time = sample * .07f;
                    var pose = Pose(null, time, hand);
                    Assert.That(pose, Is.EqualTo(Pose(null, time, hand)));
                    Assert.That(pose.Offset.magnitude, Is.LessThanOrEqualTo(.28001f));
                    Assert.That(pose.Direction.magnitude, Is.EqualTo(1f).Within(.0001f));
                    Assert.That(pose.Grip, Is.InRange(15f, 65f));
                    Assert.That(Vector3.Distance(pose.Offset, Pose(null, time + .0001f, hand).Offset), Is.LessThan(.001f));
                }
            Assert.That(Pose(null, 0f, 0).Offset, Is.Not.EqualTo(Pose(null, .5f, 0).Offset));
            Assert.That(Pose(null, 0f, 0).Direction, Is.Not.EqualTo(Pose(null, 0f, 1).Direction));
        }

        [Test]
        public void NearTargetPullsTowardPlayerWhileFarTargetDoesNotChangeSearch()
        {
            Vector3 target = Vector3.right;
            for (int i = 0; i < 25; i++)
            {
                var idle = Pose(null, 1f, i);
                var near = Pose(target, 1f, i);
                Assert.That(near.Offset.x, Is.GreaterThan(idle.Offset.x));
                Assert.That(near.Direction.x, Is.GreaterThan(idle.Direction.x));
                Assert.That(near.Offset.magnitude, Is.LessThanOrEqualTo(1.08001f));
                Assert.That(Pose(Vector3.right * 40f, 1f, i), Is.EqualTo(idle));
                Assert.That(Vector3.Distance(Pose(Vector3.right * 3.9999f, 1f, i).Offset,
                    Pose(Vector3.right * 4.0001f, 1f, i).Offset), Is.LessThan(.001f));
            }
        }

        [Test]
        public void CoincidentAndInvalidInputsStayFiniteAndZeroTunablesStopMotion()
        {
            var pose = FloorHandPresenter.Pose(Vector3.zero, Vector3.zero, float.NaN, 0, 0f, 0f, 0f, 0f);
            Assert.That(pose.Offset, Is.EqualTo(Vector3.zero));
            Assert.That(float.IsNaN(pose.Direction.x), Is.False);
            Assert.That(Pose(new Vector3(float.NaN, 0f, 0f), 0f, 0), Is.EqualTo(Pose(null, 0f, 0)));
        }
        private static (Vector3 Offset, Vector3 Direction, float Grip) Pose(Vector3? target, float time, int index)
            => FloorHandPresenter.Pose(Vector3.zero, target, time, index, .28f, 2.4f, 4f, .8f);
    }
}
