// ============================================================================
// LumenMathUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Pins shared light math independently of either presentation system.
//   Tests cover authored values, invalid inputs and boundary behavior so moving
//   calculations to Core cannot alter the fake-light envelope or effect gating.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · Core.
// KEY RESPONSIBILITIES:
//   - Verify exit monotonicity, fan symmetry, thin fog and opt-in rim gating.
//   - Verify radius conversion, cone feathering and bounded obstruction reach.
// DEPENDENCIES:
//   NUnit, Core LumenMathUtility and Unity value types only.
// USAGE NOTES:
//   Pure tests: no scene, rendering, config assets, time or random source.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class LumenMathUtilityTests
    {
        [Test]
        public void ExitIntensityIsMonotonicAndClamped()
        {
            float previous = .04f;
            for (int i = 0; i <= 100; i++)
            {
                float value = LumenMathUtility.ExitRayIntensity(i / 100f, .04f, .65f);
                Assert.That(value, Is.InRange(previous, .65f)); previous = value;
            }
            Assert.That(previous, Is.EqualTo(.65f));
            Assert.That(LumenMathUtility.ExitRayIntensity(float.NaN, .04f, .65f), Is.EqualTo(.04f));
            Assert.That(LumenMathUtility.ExitRayIntensity(2f, 1f, .5f), Is.EqualTo(1f));
            Assert.That(LumenMathUtility.ExitRayIntensity(1f, -1f, float.PositiveInfinity), Is.Zero);
        }

        [Test]
        public void FansAndVerticesHaveBoundedSymmetricGeometry()
        {
            Assert.That(LumenMathUtility.FanYaw(0, 5, 30f), Is.EqualTo(-30f));
            Assert.That(LumenMathUtility.FanYaw(2, 5, 30f), Is.Zero);
            Assert.That(LumenMathUtility.FanYaw(4, 5, 30f), Is.EqualTo(30f));
            Assert.That(LumenMathUtility.FanYaw(8, 1, 30f), Is.Zero);
            Assert.That(LumenMathUtility.FanYaw(0, 5, float.NaN), Is.Zero);
            Assert.That(LumenMathUtility.RayVertices(.45f, 4f), Is.EqualTo(new[] {
                Vector3.zero, new Vector3(-.45f, 0f, 4f), new Vector3(.45f, 0f, 4f) }));
            Assert.That(LumenMathUtility.RayVertices(-1f, float.NaN), Is.EqualTo(new[] { Vector3.zero, Vector3.zero, Vector3.zero }));
        }

        [TestCase(0f)] [TestCase(.35f)] [TestCase(1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void FogOutsideThinBandHasNoGlow(float density)
            => Assert.That(LumenMathUtility.FogBoundaryGlow(density, .35f, .08f), Is.Zero);

        [Test]
        public void FogPeakAndRimRespectStrengthAndOptIn()
        {
            Assert.That(LumenMathUtility.FogBoundaryGlow(.175f, .35f, .08f), Is.EqualTo(.08f).Within(.00001f));
            Assert.That(LumenMathUtility.FogBoundaryGlow(.175f, 0f, .08f), Is.Zero);
            Assert.That(LumenMathUtility.FogBoundaryGlow(.175f, .35f, -1f), Is.Zero);
            Assert.That(LumenMathUtility.HunterRim(false, true, 1f), Is.Zero);
            Assert.That(LumenMathUtility.HunterRim(true, false, 1f), Is.Zero);
            Assert.That(LumenMathUtility.HunterRim(true, true, 2f), Is.EqualTo(1f));
            Assert.That(LumenMathUtility.HunterRim(true, true, float.NaN), Is.Zero);
        }

        [TestCase(.2f, 2f)] [TestCase(2.4f, 2f)] [TestCase(18f, 2f)] [TestCase(40f, 8f)]
        public void RadiusInvertsTheShaderCutoff(float meters, float basis)
            => Assert.That(LumenMathUtility.RangeMultiplier(meters, basis) * basis * .5f,
                Is.EqualTo(Mathf.Sqrt(meters * meters + 1f) - .5f).Within(.0001f));

        [TestCase(25f, 12.5f)] [TestCase(55f, 27.5f)] [TestCase(-10f, .5f)]
        [TestCase(360f, 89.5f)] [TestCase(float.NaN, 27.5f)]
        public void ConeUsesFiniteHalfAngleAndFeather(float input, float expected)
        {
            var angles = LumenMathUtility.ConeAngles(input);
            Assert.That(angles.y, Is.EqualTo(expected));
            Assert.That(angles.x, Is.GreaterThan(0f).And.LessThan(angles.y));
        }

        [TestCase(18f, 3f, 3.45f)] [TestCase(18f, 90f, 18f)]
        [TestCase(.1f, 0f, .1f)] [TestCase(18f, float.NaN, 18f)]
        public void ObstructionDoesNotExtendRequestedRange(float requested, float hit, float expected)
            => Assert.That(LumenMathUtility.ObstructedRange(requested, hit), Is.EqualTo(expected).Within(.0001f));

        [Test]
        public void InvalidRadiusAndBasisUseFiniteFallbacks()
        {
            Assert.That(LumenMathUtility.RangeMultiplier(float.NaN, 0f),
                Is.EqualTo(LumenMathUtility.RangeMultiplier(.01f, 2f)));
            Assert.That(LumenMathUtility.RangeMultiplier(float.PositiveInfinity, float.NaN),
                Is.EqualTo(LumenMathUtility.RangeMultiplier(.01f, 2f)));
        }
    }
}
