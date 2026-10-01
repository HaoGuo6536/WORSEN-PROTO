// ============================================================================
// EnvironmentThemeConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Checks theme fixtures without changing light identities or footprint sockets.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Environment.
// KEY RESPONSIBILITIES:
//   - Compare real castle/hospital dressing and exact Level light binding.
//   - Check deterministic, bounded fluorescent flicker and theme reset.
// DEPENDENCIES:
//   NUnit, Core, Environment and Unity temporary object creation.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Edit Mode. No imported Lumen assets required; actual illumination is a live gate.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Environment;
using Object = UnityEngine.Object;
namespace Worsen.Tests.CastleEnvironment
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EnvironmentThemeConsumerTests
    {
        [Test]
        public void HospitalReplacesTorchesButPreservesEveryIdentityAndSocket()
        {
            var root = new GameObject("Theme fixture"); var config = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<EnvironmentDriverConfig>();
            var driver = root.AddComponent<EnvironmentDriver>();
            typeof(EnvironmentDriver).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, config);
            var state = (EnvironmentDriverState)typeof(EnvironmentDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            try
            {
                var cells = new[] { new Bounds(new Vector3(0f, 3.5f, 0f), new Vector3(12f, 7f, 12f)), new Bounds(new Vector3(12f, 3.5f, 0f), new Vector3(12f, 7f, 12f)) };
                var bounds = new Bounds(new Vector3(6f, 3.5f, 0f), new Vector3(24f, 7f, 12f));
                driver.SetTheme("castle", "torch"); driver.SetRoomTheme(7, "castle", "Hall");
                driver.AddRoom(7, bounds, false, false, Array.Empty<Vector3>(), cells: cells);
                Assert.That(state.Flames, Is.Not.Empty); Assert.That(state.Flames.All(f => !f.Fluorescent && f.Panel == null), Is.True);
                var identities = state.Flames.Select(f => f.Identity).ToArray(); var sockets = state.Flames.Select(f => f.SocketPosition).ToArray();
                driver.BeginFloor(); driver.SetTheme("hospital", "fluorescent"); driver.SetRoomTheme(7, "hospital", "Ward");
                driver.BeginFloor(clearTheme: false); driver.AddRoom(7, bounds, false, false, Array.Empty<Vector3>(), cells: cells);
                Assert.That(state.Flames.Select(f => f.Identity), Is.EqualTo(identities));
                Assert.That(state.Flames.Select(f => f.SocketPosition), Is.EqualTo(sockets));
                Assert.That(state.Flames.All(f => f.Fluorescent && f.Panel != null), Is.True);
                Assert.That(root.GetComponentsInChildren<EnvironmentFluorescentFixture>(true), Has.Length.EqualTo(identities.Length));
                Assert.That(root.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
                Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
                driver.ApplyLight(new InteractableState(123, InteractableKind.Light, 7, sockets[0], InteractableStateValue.Inactive));
                Assert.That(state.Flames[0].Identity, Is.EqualTo(identities[0])); Assert.That(state.Flames[0].Lit, Is.False);
                driver.ApplyLight(new InteractableState(123, InteractableKind.Light, 7, sockets[0], InteractableStateValue.Lit));
                Assert.That(state.Flames[0].Lit, Is.True);
                driver.BeginFloor(); Assert.That(state.RoomThemes, Is.Empty); Assert.That(state.LightSource, Is.Null);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        [Test]
        public void FluorescentFlickerIsSlightDeterministicAndHonorsDestruction()
        {
            float first = EnvironmentThemePresenter.LampBrightness(true, .1f, 13, 0f, 0f, false, .04f, 9f);
            Assert.That(first, Is.InRange(.96f, 1f));
            Assert.That(EnvironmentThemePresenter.LampBrightness(true, .1f, 13, 0f, 0f, false, .04f, 9f), Is.EqualTo(first));
            Assert.That(EnvironmentThemePresenter.LampBrightness(true, .2f, 13, 0f, 0f, false, .04f, 9f), Is.Not.EqualTo(first));
            Assert.That(EnvironmentThemePresenter.LampBrightness(true, .1f, 13, 0f, 1f, false, .04f, 9f), Is.Zero);
            var state = new EnvironmentDriverState { ThemeId = "hospital", LightSource = "fluorescent" };
            Assert.That(EnvironmentThemePresenter.IsFluorescent(state, 7), Is.True);
            state.RoomThemes[7] = ("castle", "Hall"); Assert.That(EnvironmentThemePresenter.IsFluorescent(state, 7), Is.False);
        }
    }
}
