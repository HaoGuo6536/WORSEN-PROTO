// ============================================================================
// WeaverPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies ceiling-relative body height and stable firing probe ordering.
//   These calculations use only values, including rooms on elevated storeys.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Check heights, capsule-centered inversion/restoration and deterministic candidates.
// DEPENDENCIES:
//   - WeaverPresenter, UnityEngine values and NUnit only.
// USAGE NOTES:
//   No native scene or physics operations.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Hunter;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class WeaverPresenterTests
    {
        [TestCase(0f, 5f, false, 3.05f)]
        [TestCase(10f, 17f, false, 5.05f)]
        [TestCase(0f, 1f, false, 0f)]
        [TestCase(10f, 17f, true, 0f)]
        public void BodyFitsCurrentRoomAndDropsToFloor(float floor, float ceiling, bool drop, float expected)
            => Assert.That(new WeaverPresenter().CeilingOffset(floor, ceiling, 1.8f, .15f, drop), Is.EqualTo(expected).Within(.00001f));
        [Test] public void CeilingInvertsAroundCapsuleCenterWithoutChangingHeadingOrEnvelope()
        {
            var presenter = new WeaverPresenter(); var pivot = new Vector3(0f, .9f, 0f);
            var rotation = presenter.CeilingRotation(true); var offset = new Vector3(0f, 3.05f, 0f);
            Assert.That(rotation * Vector3.up, Is.EqualTo(Vector3.down));
            Assert.That(rotation * Vector3.forward, Is.EqualTo(Vector3.forward));
            Assert.That(presenter.CeilingPosition(Vector3.zero, pivot, offset, rotation).y, Is.EqualTo(4.85f).Within(.00001f));
            Assert.That(presenter.CeilingPosition(Vector3.up * 1.8f, pivot, offset, rotation).y, Is.EqualTo(3.05f).Within(.00001f));
            Assert.That(presenter.CeilingPosition(pivot, pivot, offset, rotation), Is.EqualTo(pivot + offset));
            Assert.That(presenter.CeilingPosition(Vector3.one, pivot, Vector3.zero, presenter.CeilingRotation(false)), Is.EqualTo(Vector3.one));
        }
        [Test] public void CandidateRingIsStableAndKeepsTheFloorPlane()
        {
            var presenter = new WeaverPresenter(); var origin = new Vector3(4, 10, 6);
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = presenter.Candidate(origin, i, 8, 2.5f);
                Assert.That(candidate, Is.EqualTo(presenter.Candidate(origin, i, 8, 2.5f)));
                Assert.That(candidate.y, Is.EqualTo(origin.y));
                Assert.That(Vector3.Distance(origin, candidate), Is.EqualTo(2.5f).Within(.00001f));
            }
        }
    }
}
