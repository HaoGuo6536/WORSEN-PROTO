// ============================================================================
// FogThemeConsumerTests.cs
// ============================================================================
// PURPOSE:
//   Checks look selection independently of authoritative fog density/progress.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Fog.
// KEY RESPONSIBILITIES:
//   - Keep a pending hospital look through room construction and clear on teardown.
//   - Confirm look changes leave density and progress byte-for-byte unchanged.
// DEPENDENCIES:
//   NUnit, Core, Fog and Unity transient configs/objects.
// USAGE NOTES:
//   Edit Mode. Shader appearance and scene subscription timing need coordinator QA.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Fog;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Fog
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FogThemeConsumerTests
    {
        [Test]
        public void HospitalLookSurvivesConstructionWithoutChangingProgressOrDensity()
        {
            var config = ScriptableObject.CreateInstance<FogDriverConfig>(); var root = new GameObject("Fog theme fixture");
            var driver = root.AddComponent<FogDriver>();
            try
            {
                driver.Initialize(config); driver.SetLook("cold-black-mist");
                var bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
                var rooms = new[] { new GeneratedRoomSample(1, bounds, false, false, Array.Empty<Vector3>()) };
                var graph = new LevelGraph(new[] { new LevelRoom(1, bounds.center, bounds.size) }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
                driver.SetRooms(rooms, graph);
                var field = typeof(FogDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic);
                var state = (FogDriverState)field.GetValue(driver); Assert.That(state.Look, Is.EqualTo("cold-black-mist"));
                driver.SetRoomProgress(1, .5f); FogDensityPresenter.Rebuild(state, config, true);
                var density = (byte[])state.Density.Clone();
                Assert.That(FogLookPresenter.Body(state.Look, config), Is.EqualTo(config.ColdBodyColor));
                Assert.That(FogLookPresenter.Thin(state.Look, config), Is.EqualTo(config.ColdThinColor));
                driver.SetLook("black-mist");
                CollectionAssert.AreEqual(density, state.Density); Assert.That(state.Rooms[1].Progress, Is.EqualTo(.5f));
                Assert.That(FogLookPresenter.Body(state.Look, config), Is.EqualTo(config.BodyColor));
                Assert.That(FogLookPresenter.Thin("unknown", config), Is.EqualTo(config.ThinColor));
                driver.ResetFloor(); Assert.That(((FogDriverState)field.GetValue(driver)).Look, Is.Null);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
    }
}
