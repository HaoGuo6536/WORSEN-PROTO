// ============================================================================
// HorrorLumenPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies native fake-light radius/cone conversion and sampled wall limits.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Invert the actual shader cutoff to check meter-space radius agreement.
//   - Reject invalid angle bounds and retain struck-wall visibility within range.
// DEPENDENCIES:
//   NUnit, Unity value types, HorrorLumenPresenter.
// USAGE NOTES:
//   Pure tests; no camera, physics, renderer or scene initialization.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.Horror;

namespace Worsen.Tests.Horror
{
    public sealed class HorrorLumenPresenterTests
    {
        private readonly HorrorLumenPresenter _presenter = new HorrorLumenPresenter();

        [TestCase(.2f, 2f)] [TestCase(2.4f, 2f)] [TestCase(18f, 2f)] [TestCase(40f, 8f)]
        public void RadiusMatchesActualShaderCutoff(float meters, float baseRange)
        {
            float halfRange = _presenter.RangeMultiplier(meters, baseRange) * baseRange * .5f;
            float shaderDistance = Mathf.Sqrt(meters * meters + 1f) - .5f;
            Assert.That(halfRange, Is.EqualTo(shaderDistance).Within(.0001f));
            Assert.That(halfRange - (Mathf.Sqrt(meters * .9f * meters * .9f + 1f) - .5f), Is.GreaterThan(0f));
        }

        [TestCase(25f, 12.5f)] [TestCase(55f, 27.5f)] [TestCase(-10f, .5f)] [TestCase(360f, 89.5f)]
        public void ConeUsesHalfAngleWithNonzeroFeatherWidth(float input, float expected)
        {
            Vector2 angles = _presenter.ConeAngles(input);
            Assert.That(angles.y, Is.EqualTo(expected));
            Assert.That(angles.x, Is.GreaterThan(0f).And.LessThan(angles.y));
        }

        [Test]
        public void InvalidValuesRemainFinite()
        {
            Assert.That(float.IsNaN(_presenter.RangeMultiplier(float.NaN, 0f)), Is.False);
            Assert.That(float.IsInfinity(_presenter.RangeMultiplier(float.PositiveInfinity, float.NaN)), Is.False);
            Assert.That(_presenter.ConeAngles(float.NaN).y, Is.EqualTo(27.5f));
            Assert.That(_presenter.ObstructedRange(18f, float.NaN), Is.EqualTo(18f));
        }

        [TestCase(18f, 3f, 3.45f)] [TestCase(18f, 18f, 18f)] [TestCase(18f, 90f, 18f)] [TestCase(.1f, 0f, .1f)]
        public void CentralObstructionKeepsWallLitWithoutExtendingAuthoritativeRange(float requested, float hit, float expected)
        {
            float result = _presenter.ObstructedRange(requested, hit);
            Assert.That(result, Is.EqualTo(expected).Within(.0001f));
            Assert.That(result, Is.LessThanOrEqualTo(requested));
        }
    }
}
