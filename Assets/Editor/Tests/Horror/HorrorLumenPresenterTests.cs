// ============================================================================
// HorrorLumenPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies native fake-light radius/cone conversion and sampled wall limits.
//   Exit fans and optional fog/rim hooks are checked without a renderer.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Horror.
// KEY RESPONSIBILITIES:
//   - Invert the actual shader cutoff to check meter-space radius agreement.
//   - Reject invalid angle bounds and retain struck-wall visibility within range.
//   - Keep the existing Horror API equivalent to the shared Core implementation.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorLumenPresenterTests
    {
        private readonly HorrorLumenPresenter _presenter = new HorrorLumenPresenter();

        [TestCase(-1f)] [TestCase(.5f)] [TestCase(float.NaN)]
        public void ExistingApiForwardsSharedMathWithoutChangingItsOutputs(float value)
        {
            Assert.That(_presenter.ExitRayIntensity(value, .04f, .65f), Is.EqualTo(Worsen.Core.LumenMathUtility.ExitRayIntensity(value, .04f, .65f)));
            Assert.That(_presenter.FogBoundaryGlow(value, .35f, .08f), Is.EqualTo(Worsen.Core.LumenMathUtility.FogBoundaryGlow(value, .35f, .08f)));
            Assert.That(_presenter.HunterRim(true, true, value), Is.EqualTo(Worsen.Core.LumenMathUtility.HunterRim(true, true, value)));
            Assert.That(_presenter.FanYaw(1, 3, value), Is.EqualTo(Worsen.Core.LumenMathUtility.FanYaw(1, 3, value)));
            Assert.That(_presenter.RayVertices(value, value), Is.EqualTo(Worsen.Core.LumenMathUtility.RayVertices(value, value)));
            Assert.That(_presenter.RangeMultiplier(value, 2f), Is.EqualTo(Worsen.Core.LumenMathUtility.RangeMultiplier(value, 2f)));
            Assert.That(_presenter.ConeAngles(value), Is.EqualTo(Worsen.Core.LumenMathUtility.ConeAngles(value)));
            Assert.That(_presenter.ObstructedRange(18f, value), Is.EqualTo(Worsen.Core.LumenMathUtility.ObstructedRange(18f, value)));
        }

        [Test]
        public void ExitRaysIntensifyMonotonicallyAndFanSymmetrically()
        {
            float previous = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float value = _presenter.ExitRayIntensity(i / 100f, .04f, .65f);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous)); previous = value;
            }
            Assert.That(previous, Is.EqualTo(.65f).Within(.00001f));
            Assert.That(_presenter.ExitRayIntensity(float.NaN, .04f, .65f), Is.EqualTo(.04f));
            Assert.That(_presenter.FanYaw(0, 5, 30f), Is.EqualTo(-30f));
            Assert.That(_presenter.FanYaw(2, 5, 30f), Is.Zero);
            Assert.That(_presenter.FanYaw(4, 5, 30f), Is.EqualTo(30f));
            Assert.That(_presenter.RayVertices(.45f, 4f), Is.EqualTo(new[] { Vector3.zero,
                new Vector3(-.45f, 0f, 4f), new Vector3(.45f, 0f, 4f) }));
        }

        [Test]
        public void FogGlowsOnlyInThinRegionsAndRimRequiresBothOptInAndLookBack()
        {
            foreach (float density in new[] { 0f, .35f, 1f, float.NaN })
                Assert.That(_presenter.FogBoundaryGlow(density, .35f, .08f), Is.Zero);
            Assert.That(_presenter.FogBoundaryGlow(.175f, .35f, .08f), Is.EqualTo(.08f).Within(.00001f));
            Assert.That(_presenter.HunterRim(false, true, .08f), Is.Zero);
            Assert.That(_presenter.HunterRim(true, false, .08f), Is.Zero);
            Assert.That(_presenter.HunterRim(true, true, .08f), Is.EqualTo(.08f));
        }

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
