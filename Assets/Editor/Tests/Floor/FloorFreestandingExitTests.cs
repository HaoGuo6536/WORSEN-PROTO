// ============================================================================
// FloorFreestandingExitTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that the exit is a grounded freestanding assembly and that rotated
//   imported door art fits its actual moving collider. The gate can open without
//   placing oversized facade geometry into the central room's walking routes.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor visual and physical integration.
// KEY RESPONSIBILITIES:
//   - Compare imported leaf bounds with collision before and after opening.
//   - Check grounded supports, bounded footprint and trigger opening timing.
// DEPENDENCIES:
//   NUnit, UnityEditor AssetDatabase, UnityEngine and Floor-owned door/config types.
// USAGE NOTES:
//   Edit Mode only. Reads the imported castle leaf asset without modifying it.
//   Creates only temporary objects/materials and destroys them after each test.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Floor;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorFreestandingExitTests
    {
        [Test]
        public void ImportedLeafFitsItsColliderAfterYawAndKeepsThatFitWhileOpening()
        {
            GameObject root = null;
            FloorDriverConfig config = null;
            Material material = null;
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/External/Environment/The_Modular_Medieval_Castle/Prefabs/Castle/MC_Castle_Gates_A.prefab");
                Assert.That(source, Is.Not.Null);
                var leaf = source.GetComponentsInChildren<Transform>(true).Single(item => item.name == "MC_Castle_Gates_01").gameObject;
                Assert.That(leaf.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(1), "Use the leaf child, never its surrounding castle facade.");
                config = ScriptableObject.CreateInstance<FloorDriverConfig>();
                typeof(FloorDriverConfig).GetField("_exitDoorPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, leaf);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                root = new GameObject("Freestanding exit test");
                root.transform.position = new Vector3(51000f, 0f, 51000f);
                var door = root.AddComponent<FloorExitDoor>();
                door.Configure(config, material, material, material);
                Assert.That(root.GetComponent<BoxCollider>().enabled, Is.False);
                AssertFitted(root);
                var supports = root.GetComponentsInChildren<BoxCollider>().Where(item => item.name.Contains("Support") || item.name.Contains("Grounded Foot")).ToArray();
                Assert.That(supports.Length, Is.EqualTo(4));
                foreach (var support in supports)
                {
                    var localMin = support.transform.localPosition.y - support.transform.localScale.y * 0.5f;
                    Assert.That(localMin, Is.EqualTo(0f).Within(0.0001f));
                    Assert.That(Mathf.Abs(support.transform.localPosition.x) + support.transform.localScale.x * 0.5f, Is.LessThanOrEqualTo(1.2f));
                    Assert.That(support.transform.localScale.z, Is.LessThanOrEqualTo(0.65f));
                }
                Assert.That(root.GetComponentsInChildren<Transform>().Any(item => item.name.StartsWith("Stone ")), Is.False);
                door.Open(); door.Tick(config.ExitDoorOpeningDuration * 0.5f);
                Assert.That(root.GetComponent<BoxCollider>().enabled, Is.False);
                AssertFitted(root);
                door.Tick(config.ExitDoorOpeningDuration);
                Assert.That(door.FullyOpen, Is.True);
                Assert.That(root.GetComponent<BoxCollider>().enabled, Is.True);
                AssertFitted(root);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                if (config != null) Object.DestroyImmediate(config);
                if (material != null) Object.DestroyImmediate(material);
            }
        }

        private static void AssertFitted(GameObject root)
        {
            var hinges = root.GetComponentsInChildren<Transform>().Where(item => item.name.Contains("Hinged Door")).ToArray();
            Assert.That(hinges.Length, Is.EqualTo(2));
            foreach (var hinge in hinges)
            {
                var collision = hinge.GetComponentsInChildren<BoxCollider>().Single(item => item.name == "Door Leaf Collision");
                var imported = hinge.GetComponentsInChildren<Renderer>().Single(item => item.name != "Door Leaf Collision");
                // Compare bounds expressed in the hinge frame rather than world
                // bounds: opening rotates both pieces and must preserve their fit.
                var local = imported.localBounds;
                var actual = new Bounds();
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = hinge.InverseTransformPoint(imported.transform.TransformPoint(point));
                    if (corner == 0) actual = new Bounds(point, Vector3.zero); else actual.Encapsulate(point);
                }
                Assert.That(Vector3.Distance(actual.center, collision.transform.localPosition), Is.LessThan(0.025f));
                Assert.That(Vector3.Distance(actual.size, collision.transform.localScale), Is.LessThan(0.025f));
                Assert.That(collision.enabled, Is.True);
                Assert.That(collision.isTrigger, Is.False);
            }
        }
    }
}
