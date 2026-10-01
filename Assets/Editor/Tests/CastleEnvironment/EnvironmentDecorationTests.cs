// ============================================================================
// EnvironmentDecorationTests.cs
// ============================================================================
// PURPOSE:
//   Checks instantiated mesh bounds instead of only testing planned slot centres.
//   Inactive ancestors and off-centre, scaled children reproduce the floating
//   decoration failure without loading a scene or entering Play Mode.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests (§11) · CastleEnvironment.
// KEY RESPONSIBILITIES:
//   - Measure spawned floor and wall contacts after enabling previously inactive roots.
//   - Verify impossible fits are rejected and imported real lights are removed.
//   - Exercise template ownership routing through the real Manager and Driver.
// DEPENDENCIES:
//   - NUnit, Core, Environment and transient Unity objects.
// USAGE NOTES:
//   Native Edit Mode only. No assets are written. Driver.Teardown is explicit.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Category("Native")]
    public sealed class EnvironmentDecorationTests
    {
        [TestCase(true, 0f)] [TestCase(true, 90f)] [TestCase(false, 180f)] [TestCase(false, 270f)]
        public void InactiveMeshHierarchyProducesGroundedOrWallFlushSpawn(bool floor, float yaw)
        {
            var parent = new GameObject("Inactive room"); parent.SetActive(false);
            var owner = new GameObject("Placement"); owner.transform.SetParent(parent.transform, false);
            var prefab = new GameObject("Off-centre prefab"); prefab.SetActive(false);
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(prefab.transform, false);
            child.transform.localPosition = new Vector3(3f, -2f, 4f);
            child.transform.localScale = new Vector3(.6f, .8f, 1.8f);
            child.AddComponent<Light>();
            var config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            try
            {
                var slot = new EnvironmentSlot(new Vector3(10f, 5.2f, -8f), yaw,
                    floor ? EnvironmentDecorationKind.FloorProp : EnvironmentDecorationKind.Banner, new Vector3(2.5f, 2.4f, 1.1f));
                var visual = owner.AddComponent<EnvironmentDecoration>();
                Assert.That(visual.Configure(prefab, slot, config, floor), Is.True);
                Assert.That(parent.activeSelf, Is.False, "Measurement must not activate the room.");
                Assert.That(owner.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(owner.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), Is.True);
                parent.SetActive(true);
                var renderer = owner.GetComponentInChildren<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                if (floor) Assert.That(renderer.bounds.min.y, Is.EqualTo(4f).Within(.001f));
                // Project all mesh vertices into the slot frame, independent of the production bounds solver.
                var filter = renderer.GetComponent<MeshFilter>();
                var inverse = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f));
                var vertices = filter.sharedMesh.vertices.Select(v => inverse * (filter.transform.TransformPoint(v) - slot.Position)).ToArray();
                Assert.That(vertices.Min(v => v.z), Is.EqualTo(-.55f).Within(.001f));
                Assert.That(vertices.Max(v => v.x) - vertices.Min(v => v.x), Is.GreaterThan(1.8f));
            }
            finally { Object.DestroyImmediate(parent); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config); }
        }

        [Test]
        public void SevenMetreArchCannotBecomeATinyDecoration()
        {
            var owner = new GameObject("Rejected arch"); owner.SetActive(false);
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.SetActive(false);
            prefab.transform.localScale = new Vector3(.6f, 2f, 7f);
            var config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            try
            {
                Assert.That(owner.AddComponent<EnvironmentDecoration>().Configure(prefab,
                    new EnvironmentSlot(Vector3.zero, 0f, EnvironmentDecorationKind.Arch, new Vector3(2f, 2f, .6f)), config, false), Is.False);
            }
            finally { Object.DestroyImmediate(owner); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config); }
        }

        [Test]
        public void ManagerTemplateFlagSkipsArtButKeepsAuthoritativeLightBinding()
        {
            var owner = new GameObject("Template manager"); owner.SetActive(false);
            var manager = owner.AddComponent<EnvironmentManager>(); var driver = owner.GetComponent<EnvironmentDriver>();
            var config = ScriptableObject.CreateInstance<EnvironmentDriverConfig>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.SetActive(false);
            try
            {
                Set(manager, "_driver", driver); Set(driver, "_config", config);
                Set(config, "_wallTorchPrefab", prefab); Set(config, "_floorPropPrefabs", new[] { prefab });
                Set(config, "_wallDecorationPrefabs", new[] { prefab });
                var socket = new Vector3(2f, 3f, 4f);
                manager.SetRooms(new[] { new GeneratedRoomSample(7, new Bounds(Vector3.up * 3.5f, new Vector3(12f, 7f, 12f)),
                    false, false, Array.Empty<Vector3>()) }, lightSockets: id => new[] { socket }, authoredFurniture: id => true);
                Assert.That(owner.GetComponentsInChildren<EnvironmentDecoration>(true), Is.Empty);
                var state = (EnvironmentDriverState)typeof(EnvironmentDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                Assert.That(state.Flames, Has.Count.EqualTo(1)); Assert.That(state.Positions[0], Is.EqualTo(socket));
                manager.ApplyLight(new InteractableState(1, InteractableKind.Light, 7, socket, InteractableStateValue.Inactive));
                Assert.That(state.Flames[0].Lit, Is.False);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(owner); Object.DestroyImmediate(prefab); Object.DestroyImmediate(config); }
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
