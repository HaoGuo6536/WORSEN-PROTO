// ============================================================================
// FloorFreestandingExitTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the project-made door's imported axes, hinge and fixed aperture.
//   The single moving leaf must fit its collision throughout opening. This native
//   fixture also checks visibility reset and explicit teardown without saving assets.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor visual and physical integration.
// KEY RESPONSIBILITIES:
//   - Compare imported leaf width/height with collision across its swing.
//   - Check fixed aperture, grounded frame and repeatable configuration teardown.
// DEPENDENCIES:
//   NUnit, UnityEditor AssetDatabase, UnityEngine and Floor-owned door/config types.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Native Edit Mode only; coordinator runs after FBX/shader import.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard, Timeout(300000)]
    public sealed class FloorFreestandingExitTests
    {
        [Test]
        public void ImportedSingleLeafKeepsItsHingeFitAndRevealsAFixedAperture()
        {
            GameObject root = null, template = null;
            FloorExitDoor door = null;
            FloorDriverConfig config = null;
            Material material = null, escapeMaterial = null;
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Exit/WeatheredDoor/WORSEN_WeatheredExitDoor.fbx");
                Assert.That(source, Is.Not.Null);
                template = Object.Instantiate(source); template.SetActive(false);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/ExitPortal.shader");
                Assert.That(shader, Is.Not.Null); Assert.That(shader.isSupported, Is.True);
                escapeMaterial = new Material(shader);
                template.GetComponentsInChildren<Renderer>(true).Single(item => item.name == "EscapeSurface").sharedMaterial = escapeMaterial;
                config = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
                typeof(FloorDriverConfig).GetField("_exitDoorPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, template);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                root = new GameObject("Freestanding exit test");
                root.transform.position = new Vector3(51000f, 0f, 51000f);
                door = root.AddComponent<FloorExitDoor>();
                door.Configure(config, material, material, material);
                Assert.That(root.GetComponent<BoxCollider>().enabled, Is.True);
                AssertFitted(root);
                var escape = root.GetComponentsInChildren<Renderer>(true).Single(item => item.name == "EscapeSurface");
                var plane = escape.transform.localToWorldMatrix;
                Assert.That(escape.enabled, Is.False);
                var supports = root.GetComponentsInChildren<BoxCollider>(true).Where(item => item.name.Contains("Support")).ToArray();
                Assert.That(supports.Length, Is.EqualTo(2));
                foreach (var support in supports)
                {
                    var localMin = support.center.y - support.size.y * 0.5f;
                    Assert.That(localMin, Is.EqualTo(0f).Within(0.0001f));
                    Assert.That(Mathf.Abs(support.center.x) + support.size.x * 0.5f, Is.LessThanOrEqualTo(1.2f));
                }
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(item => item.name.Contains("Medieval") || item.name == "Exit Seal"), Is.False);
                door.Open(); door.Tick(config.ExitDoorOpeningDuration * 0.5f);
                Assert.That(escape.enabled, Is.True);
                Assert.That(escape.transform.localToWorldMatrix, Is.EqualTo(plane));
                AssertFitted(root);
                door.Tick(config.ExitDoorOpeningDuration);
                Assert.That(door.FullyOpen, Is.True);
                Assert.That(root.GetComponent<BoxCollider>().enabled, Is.True);
                AssertFitted(root);
                door.Configure(config, material, material, material);
                Assert.That(escape == null, Is.True, "Reconfiguration releases the previous hierarchy.");
                Assert.That(root.GetComponentsInChildren<FloorExitDoorVisual>(true).Length, Is.EqualTo(1));
                Assert.That(door.OpeningProgress, Is.Zero);
            }
            finally
            {
                if (door != null) door.Teardown();
                if (root != null) Object.DestroyImmediate(root);
                if (template != null) Object.DestroyImmediate(template);
                if (config != null) Object.DestroyImmediate(config);
                if (material != null) Object.DestroyImmediate(material);
                if (escapeMaterial != null) Object.DestroyImmediate(escapeMaterial);
            }
        }

        private static void AssertFitted(GameObject root)
        {
            var hinges = root.GetComponentsInChildren<Transform>(true).Where(item => item.name == "DoorLeaf").ToArray();
            Assert.That(hinges.Length, Is.EqualTo(1));
            foreach (var hinge in hinges)
            {
                var collision = hinge.GetComponentsInChildren<BoxCollider>(true).Single(item => item.name == "Door Leaf Collision");
                var imported = hinge.GetComponent<Renderer>();
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
                Assert.That(Vector3.Distance(hinge.localPosition, Vector3.left), Is.LessThan(.001f), "Hinge origin must survive FBX conversion.");
                Assert.That(actual.center.x, Is.EqualTo(collision.center.x).Within(.025f));
                Assert.That(actual.center.y, Is.EqualTo(collision.center.y).Within(.025f));
                Assert.That(actual.size.x, Is.EqualTo(collision.size.x).Within(.025f));
                Assert.That(actual.size.y, Is.EqualTo(collision.size.y).Within(.025f));
                Assert.That(actual.size.z, Is.LessThan(.34f), "Handles decorate the outside of the .16 m physical slab.");
                Assert.That(collision.enabled, Is.True);
                Assert.That(collision.isTrigger, Is.False);
            }
        }
    }
}
