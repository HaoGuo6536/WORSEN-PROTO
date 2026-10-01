// ============================================================================
// FogDoorwayDriverTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that the default fog route builds confined transparent sheets, not
//   particles or a volumetric field, and releases native resources on replacement.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Editor · Fog.
// KEY RESPONSIBILITIES:
//   - Check that the doorway face waits for full consumption, theme colour and replacement.
//   - Check shader reference failure, disabled ownership and private-resource teardown.
// DEPENDENCIES:
//   - NUnit, Core, Fog and UnityEditor asset lookup with native Unity rendering objects.
// USAGE NOTES:
//   Native Edit Mode for coordinator. No assets saved; ResetFloor always runs in finally.
//   GPU composition from both doorway sides still needs owner screenshots.
// ============================================================================
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Worsen.Core;
using Worsen.Presentation.Fog;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Fog
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FogDoorwayDriverTests
    {
        [TestCase(false)] [TestCase(true)]
        public void DoorwayModeNeverUploadsVolumeAndRebuildReleasesOldSheets(bool cold)
        {
            var root = new GameObject("Doorway test");
            var driver = root.AddComponent<FogDriver>();
            var config = ScriptableObject.CreateInstance<FogDriverConfig>();
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Scripts/Presentation/Fog/Driver/FogDoorway.shader");
                Assert.That(shader, Is.Not.Null); Set(config, "_doorwayShader", shader);
                Assert.That(config.DoorwayOnly, Is.True); Assert.That(config.DoorwayOpacity, Is.EqualTo(.22f));
                driver.Initialize(config); driver.SetLook(cold ? "cold-black-mist" : "black-mist");
                var bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
                var portal = new Vector3(6f, 0f, 0f);
                var rooms = new[] { new GeneratedRoomSample(1, bounds, false, false, new[] { portal, portal }) };
                var graph = new LevelGraph(new[] { new LevelRoom(1, bounds.center, bounds.size) },
                    Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
                driver.SetRooms(rooms, graph); driver.SetEnabled(true); driver.SetRoomProgress(1, 1f); driver.Flush();
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(1), "Duplicate portals must not stack alpha.");
                var sheet = renderers[0]; var material = sheet.sharedMaterial;
                var mesh = sheet.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(sheet.gameObject.activeInHierarchy, Is.True);
                Assert.That(sheet.bounds.max.x, Is.LessThan(6f)); Assert.That(sheet.bounds.size.x, Is.Zero.Within(.0001f));
                Assert.That(sheet.bounds.size.z, Is.EqualTo(3.2f).Within(.0001f));
                Assert.That(sheet.bounds.min.y, Is.Zero.Within(.0001f));
                Assert.That(sheet.bounds.max.y, Is.EqualTo(2.8f).Within(.0001f));
                Assert.That(material.GetColor("_HazeColor").a, Is.EqualTo(.22f));
                Assert.That(material.GetColor("_HazeColor").r, Is.GreaterThan(.5f));
                Assert.That(root.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(driver.UploadRevision, Is.Zero);
                driver.SetEnabled(false); Assert.That(sheet.gameObject.activeInHierarchy, Is.False);
                driver.SetEnabled(true); driver.SetRoomProgress(1, .5f); driver.Flush();
                Assert.That(material.GetColor("_HazeColor").a, Is.EqualTo(.11f));
                Assert.That(sheet.gameObject.activeSelf, Is.False, "The room front, not an early doorway overlay, shows growth.");
                driver.SetRooms(rooms, graph); driver.Flush();
                Assert.That(sheet == null && mesh == null && material == null, Is.True);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true)[0].gameObject.activeSelf, Is.False);
                driver.ResetFloor(); Assert.That(root.transform.childCount, Is.Zero);
            }
            finally { driver.ResetFloor(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        [Test]
        public void MissingDoorwayShaderReportsOnceAndDoesNotInventFallbackFog()
        {
            var root = new GameObject("Missing doorway shader"); var driver = root.AddComponent<FogDoorwayDriver>();
            var config = ScriptableObject.CreateInstance<FogDriverConfig>();
            try
            {
                var bounds = new Bounds(new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f));
                var rooms = new[] { new GeneratedRoomSample(1, bounds, false, false, new[] { new Vector3(6f, 0f, 0f) }) };
                LogAssert.Expect(LogType.Error, "FogDriverConfig requires DoorwayShader (Worsen/Collapse Doorway Haze). Rebuild Fog assets.");
                driver.Build(rooms, config); driver.Build(rooms, config);
                Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
                LogAssert.NoUnexpectedReceived();
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
