// ============================================================================
// HorrorVisibilityPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies a nonblack navigation floor and gradual atmospheric haze without Unity.
//   Legacy serialized black fog and equal near/far values must remain readable.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Horror.
// KEY RESPONSIBILITIES:
//   - Test colour/fill floors and malformed numeric samples.
//   - Test legacy config migration bounds and collapse-independent fog gradients.
// DEPENDENCIES:
//   - NUnit, Horror pure calculations and managed config field injection.
// USAGE NOTES:
//   No ScriptableObject creation or native rendering. These are output contracts,
//   not screenshot evidence or proof of a material's response to ambient light.
// ============================================================================
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Presentation.Horror;
namespace Worsen.Tests.Horror
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HorrorVisibilityPresenterTests
    {
        [Test]
        public void BlackAmbientAndHazeHaveCoolFiniteFloorsWithoutRaisingAlreadyBrightChannels()
        {
            var floor = new Color(.09f, .115f, .15f, 1f);
            Assert.That(HorrorVisibilityPresenter.AtLeast(Color.black, floor), Is.EqualTo(floor));
            var result = HorrorVisibilityPresenter.AtLeast(new Color(float.NaN, -.1f, .3f), floor);
            Assert.That(result, Is.EqualTo(new Color(.09f, .115f, .3f, 1f)));
        }
        [Test]
        public void FlashlightOffAndZeroLampBudgetCannotDisableNavigationFill()
        {
            Assert.That(HorrorVisibilityPresenter.Fill(0f, 0f, false, .16f), Is.EqualTo(.16f));
            Assert.That(HorrorVisibilityPresenter.Fill(.8f, .2f, false, .16f), Is.EqualTo(.16f).Within(.0001f));
            Assert.That(HorrorVisibilityPresenter.Fill(.8f, .2f, true, .16f), Is.EqualTo(.8f));
            Assert.That(HorrorVisibilityPresenter.Fill(float.NaN, float.PositiveInfinity, false, .16f), Is.EqualTo(.16f));
        }
        [Test]
        public void LegacyBlackFogAndTwentyFourMetreWallBecomeNonblackGradient()
        {
            var config = (HorrorDriverConfig)FormatterServices.GetUninitializedObject(typeof(HorrorDriverConfig));
            Set(config, "_fogColor", Color.black); Set(config, "_minimumFogColor", new Color(.07f, .09f, .105f));
            Set(config, "_fogNearMeters", 24f); Set(config, "_fogFarMeters", 24f);
            Set(config, "_sweepFogNearMeters", 24f); Set(config, "_collapsedFogNearMeters", 10f);
            Assert.That(config.FogColor.r, Is.GreaterThan(0f));
            Assert.That(config.Settings.FogNearMeters, Is.EqualTo(6f));
            Assert.That(config.Settings.FogFarMeters, Is.EqualTo(25f));
            Assert.That(config.SweepFogNearMeters, Is.EqualTo(6f));
            Assert.That(config.CollapsedFogNearMeters, Is.EqualTo(6f));
            var state = new HorrorDriverState();
            new HorrorPresenter().CalculateAtmosphere(state, config.Settings, 100f);
            Assert.That(state.FogCurveEnd - state.FogCurveStart, Is.GreaterThan(.18f));
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
