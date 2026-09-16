// ============================================================================
// FloorGoldenCakeVisualTests.cs
// ============================================================================
// PURPOSE:
//   Protects the cake model's visual identity when the exit replaces ordinary
//   pickups with golden rewards. Golden tinting must retain palette textures,
//   transparent material state and geometry without changing imported materials.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Domain · Floor visual integration.
// KEY RESPONSIBILITIES:
//   - Exercise initialization and exit opening with a multi-material cake fixture.
//   - Check material ownership, reuse, alpha/texture preservation and teardown.
// DEPENDENCIES:
//   NUnit, UnityEngine, Core graph contracts and FloorDriver public lifecycle.
// USAGE NOTES:
//   Edit Mode only. Creates temporary objects/materials; no asset writes or imports.
//   Explicit teardown preserves the surrounding editor and authored source assets.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Object = UnityEngine.Object;

namespace Worsen.Tests.Floor
{
    public sealed class FloorGoldenCakeVisualTests
    {
        [Test]
        public void GoldenVisualRetainsMeshTextureAlphaAndReusesOwnedTintWithoutChangingOrdinaryCake()
        {
            Assert.That(Application.isPlaying, Is.False);
            GameObject root = null, prefab = null;
            FloorDriver driver = null;
            FloorDriverConfig config = null;
            Material source = null;
            Texture2D palette = null;
            try
            {
                config = ScriptableObject.CreateInstance<FloorDriverConfig>();
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null);
                source = new Material(shader);
                palette = new Texture2D(2, 2);
                source.SetTexture("_BaseMap", palette);
                source.SetColor("_BaseColor", new Color(0.8f, 0.7f, 0.6f, 0.35f));
                source.SetFloat("_AlphaClip", 1f); source.EnableKeyword("_ALPHATEST_ON");
                source.renderQueue = 2450;
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(prefab.GetComponent<Collider>());
                prefab.transform.localScale = new Vector3(0.7f, 0.2f, 0.7f);
                prefab.GetComponent<Renderer>().sharedMaterials = new[] { source, source };
                var mesh = prefab.GetComponent<MeshFilter>().sharedMesh;
                Set(config, "_cakePrefab", prefab);
                root = new GameObject("Golden cake visual fixture");
                driver = root.AddComponent<FloorDriver>(); Set(driver, "_config", config);
                var anchors = new[] { new LevelAnchor(1, 1, CakeAnchorType.Flow, Vector3.left), new LevelAnchor(2, 1, CakeAnchorType.Flow, Vector3.right) };
                var graph = LevelGraphUtility.Build(new[] { new LevelRoom(1, Vector3.up * 2f, new Vector3(12f, 4f, 12f)) },
                    Array.Empty<LevelEdge>(), anchors, 1, Vector3.forward * 3f);
                driver.Initialize(graph, anchors);
                var ordinary = root.GetComponentsInChildren<CakePickup>().Where(item => item.Kind == PickupKind.Cake).ToArray();
                driver.OpenExit(anchors);
                var golden = root.GetComponentsInChildren<CakePickup>().Where(item => item.Kind == PickupKind.GoldenCake).ToArray();
                Assert.That(golden.Length, Is.EqualTo(2));
                Material tint = golden[0].GetComponentInChildren<Renderer>().sharedMaterials[0];
                Assert.That(tint, Is.Not.SameAs(source));
                Assert.That(tint.shader, Is.SameAs(source.shader));
                Assert.That(tint.GetTexture("_BaseMap"), Is.SameAs(palette));
                Assert.That(tint.GetColor("_BaseColor").a, Is.EqualTo(0.35f));
                Assert.That(tint.GetColor("_BaseColor").g, Is.EqualTo(0.7f * config.GoldenColor.g).Within(0.0001f));
                Assert.That(tint.GetFloat("_AlphaClip"), Is.EqualTo(1f));
                Assert.That(tint.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
                Assert.That(tint.renderQueue, Is.EqualTo(2450));
                foreach (var pickup in golden)
                {
                    Assert.That(pickup.GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                    Assert.That(pickup.transform.GetChild(0).localScale, Is.EqualTo(prefab.transform.localScale));
                    Assert.That(pickup.GetComponent<SphereCollider>().radius, Is.EqualTo(config.PickupRadius));
                    Assert.That(pickup.GetComponent<SphereCollider>().isTrigger, Is.True);
                    Assert.That(pickup.GetComponentInChildren<Renderer>().sharedMaterials.All(item => item == tint), Is.True);
                }
                Assert.That(ordinary.All(item => item.GetComponentInChildren<Renderer>().sharedMaterials.All(material => material == source)), Is.True);
                Assert.That(source.GetColor("_BaseColor"), Is.EqualTo(new Color(0.8f, 0.7f, 0.6f, 0.35f)));
                driver.Teardown();
                Assert.That(tint == null, Is.True, "The Floor owns and releases its tinted copy.");
                Assert.That(source != null, Is.True, "Authored source materials remain alive.");
            }
            finally
            {
                if (driver != null) driver.Teardown();
                if (root != null) Object.DestroyImmediate(root);
                if (prefab != null) Object.DestroyImmediate(prefab);
                if (config != null) Object.DestroyImmediate(config);
                if (source != null) Object.DestroyImmediate(source);
                if (palette != null) Object.DestroyImmediate(palette);
            }
        }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
