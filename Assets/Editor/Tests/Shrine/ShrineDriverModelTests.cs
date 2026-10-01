// ============================================================================
// ShrineDriverModelTests.cs
// ============================================================================
// PURPOSE:
//   Exercises authored-model selection alongside the retained primitive fallback.
//   Private material and hierarchy assertions prevent spent-state or teardown leaks.
//   A separate imported-art check verifies the coordinator's actual setup output.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Shrine.
// KEY RESPONSIBILITIES:
//   - Verify each kind selects its own model, while missing references fall back.
//   - Verify inactive descendants, colliders, shared assets and spent palettes stay isolated.
//   - Verify failed builds, rebuilds and repeated Clear release exact owned resources.
//   - Check real import axes, model references and meshes after coordinator setup.
// DEPENDENCIES:
//   - Core, Domain Shrine, ShrineModelSetup, NUnit, UnityEditor and temporary Unity objects.
// USAGE NOTES:
//   RequiresUnity: native Edit Mode only, not a headless pass. No project asset writes.
//   Every Driver is explicitly cleared in finally before its owner is destroyed.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
using Worsen.Editor.Shrine;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Shrine
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    [Category("RequiresUnity")]
    public sealed class ShrineDriverModelTests
    {
        [TestCase(ShrineKind.Chance)] [TestCase(ShrineKind.Bargain)]
        [TestCase(ShrineKind.Pacification)] [TestCase(ShrineKind.Wick)]
        [TestCase(ShrineKind.Passage)] [TestCase(ShrineKind.Protection)]
        [TestCase(ShrineKind.Echo)] [TestCase(ShrineKind.Purgatory)]
        public void ConfiguredKindSelectsModelAndEveryOtherKindFallsBack(ShrineKind selected)
        {
            WithModel((driver, config, model, body, accent) =>
            {
                SetModel(config, selected, model);
                foreach (ShrineKind kind in Enum.GetValues(typeof(ShrineKind)))
                {
                    Assert.That(config.GetModel(kind), kind == selected ? Is.SameAs(model) : Is.Null);
                    driver.Build(new[] { Placement(1, kind) }, config);
                    var root = State(driver).Objects[1];
                    Assert.That(root.transform.position, Is.EqualTo(Placement(1, kind).Site.Position));
                    var clone = root.transform.Find("Model");
                    if (kind == selected)
                    {
                        Assert.That(clone, Is.Not.Null);
                        Assert.That(clone.gameObject, Is.Not.SameAs(model));
                        Assert.That(clone.Find("Body").GetComponent<MeshFilter>().sharedMesh,
                            Is.SameAs(model.transform.Find("Body").GetComponent<MeshFilter>().sharedMesh));
                        Assert.That(root.transform.Find("Pedestal"), Is.Null);
                        Assert.That(State(driver).Bodies[1].color, Is.EqualTo(body.color));
                        foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) Assert.That(collider.enabled, Is.False);
                        Assert.That(clone.Find("Inactive accent").gameObject.activeSelf, Is.False);
                    }
                    else
                    {
                        Assert.That(clone, Is.Null);
                        Assert.That(root.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(config.Shapes.Get(kind).Parts.Count + 1));
                    }
                    Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
                    Assert.That(root.GetComponentsInChildren<TextMesh>(true), Is.Empty);
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => config.GetModel((ShrineKind)99));
            });
        }
        [Test]
        public void ModelsDimPrivatelyAndRebuildToFallbackReleasesOnlyOwnedObjects()
        {
            WithModel((driver, config, model, body, accent) =>
            {
                SetModel(config, ShrineKind.Wick, model);
                driver.Build(new[] { Placement(1, ShrineKind.Wick), Placement(2, ShrineKind.Wick) }, config);
                var state = State(driver);
                var sharedBody = body.color; var sharedAccent = accent.color;
                var sharedEmission = accent.GetColor("_EmissionColor");
                var otherEmission = state.Accents[2].GetColor("_EmissionColor");
                Assert.That(state.Bodies[1], Is.Not.SameAs(body));
                Assert.That(state.Accents[1], Is.Not.SameAs(accent));
                Assert.That(state.Accents[1], Is.Not.SameAs(state.Accents[2]));
                foreach (var renderer in driver.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        Assert.That(material.globalIlluminationFlags, Is.EqualTo(MaterialGlobalIlluminationFlags.None));
                driver.MarkUsed(1);
                Assert.That(state.Bodies[1].color, Is.EqualTo(config.SpentColor));
                AssertColor(state.Accents[1].color, ShrineDriverPresenter.DimAccent(config.Shapes.Get(ShrineKind.Wick).AccentColor, config.SpentAccentMultiplier));
                var spent = state.Accents[1].GetColor("_EmissionColor");
                AssertColor(spent, ShrineDriverPresenter.Emission(config.Shapes.Get(ShrineKind.Wick).AccentColor,
                    config.AccentEmission, config.SpentAccentMultiplier));
                driver.MarkUsed(1);
                AssertColor(state.Accents[1].GetColor("_EmissionColor"), spent);
                AssertColor(state.Accents[2].GetColor("_EmissionColor"), otherEmission);
                Assert.That(body.color, Is.EqualTo(sharedBody)); Assert.That(accent.color, Is.EqualTo(sharedAccent));
                AssertColor(accent.GetColor("_EmissionColor"), sharedEmission);
                Assert.That(model.GetComponentsInChildren<Collider>(true).All(c => c.enabled), Is.True);
                var children = driver.GetComponentsInChildren<Transform>(true).Where(t => t != driver.transform).ToArray();
                var materials = state.Materials.ToArray();
                SetModel(config, ShrineKind.Wick, null);
                driver.Build(new[] { Placement(1, ShrineKind.Wick) }, config);
                foreach (var child in children) Assert.That(child == null, Is.True, "Old model child survived rebuild.");
                foreach (var mat in materials) Assert.That(mat == null, Is.True, "Private model material survived rebuild.");
                Assert.That(state.Objects[1].transform.Find("Model"), Is.Null);
                driver.Clear(); driver.Clear(); AssertEmpty(driver);
                Assert.That(model != null && body != null && accent != null, Is.True, "Shared art was destroyed.");
            });
        }
        [TestCase(false)] [TestCase(true)]
        public void MalformedConfiguredModelFailsClosedAndReleasesEarlierShrines(bool activeComponent)
        {
            WithModel((driver, config, model, body, accent) =>
            {
                SetModel(config, ShrineKind.Purgatory, model);
                if (activeComponent) model.AddComponent<Light>();
                else model.transform.Find("Inactive accent").GetComponent<Renderer>().sharedMaterial = body;
                Assert.Throws<InvalidOperationException>(() => driver.Build(new[] {
                    Placement(1, ShrineKind.Chance), Placement(2, ShrineKind.Purgatory) }, config));
                AssertEmpty(driver);
                Assert.That(model != null && body != null && accent != null, Is.True);
            });
        }
        [Test]
        public void ModelClearAndDuplicateIdFailureReleaseAllResources()
        {
            WithModel((driver, config, model, body, accent) =>
            {
                SetModel(config, ShrineKind.Chance, model);
                var placement = Placement(1, ShrineKind.Chance);
                driver.Build(new[] { placement }, config);
                var children = driver.GetComponentsInChildren<Transform>(true).Where(t => t != driver.transform).ToArray();
                var materials = State(driver).Materials.ToArray();
                driver.Clear(); driver.Clear(); driver.MarkUsed(1);
                foreach (var child in children) Assert.That(child == null, Is.True);
                foreach (var mat in materials) Assert.That(mat == null, Is.True);
                AssertEmpty(driver);
                Assert.Throws<ArgumentException>(() => driver.Build(new[] { placement, placement }, config));
                AssertEmpty(driver);
            });
        }
        [Test]
        public void ImportedEightModelsAreWiredAtMetreScaleWithTwoSlotsAndCorrectAxes()
        {
            var config = AssetDatabase.LoadAssetAtPath<ShrineDriverConfig>(ShrineModelSetup.ConfigPath);
            Assert.That(config, Is.Not.Null, "Coordinator must run ShrineModelSetup.ImportAndWire first.");
            var root = new GameObject("Imported shrine verification");
            var driver = root.AddComponent<ShrineDriver>();
            try
            {
                var kinds = (ShrineKind[])Enum.GetValues(typeof(ShrineKind));
                foreach (var kind in kinds)
                {
                    string path = ShrineModelSetup.ModelPath(kind);
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    Assert.That(importer.bakeAxisConversion, Is.False);
                    Assert.That(importer.addCollider || importer.importLights || importer.importCameras || importer.importAnimation, Is.False);
                    Assert.That(config.GetModel(kind), Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(path)));
                    Assert.That(config.GetModel(kind), Is.Not.Null);
                }
                driver.Build(kinds.Select((k, i) => Placement(i+1, k)).ToArray(), config);
                foreach (var shrine in State(driver).Objects.Values)
                {
                    var renderers = shrine.GetComponentsInChildren<Renderer>();
                    Assert.That(renderers.Length, Is.EqualTo(1));
                    Assert.That(renderers[0].bounds.size.y, Is.InRange(.8f, 1.6f));
                    Assert.That(renderers[0].sharedMaterials.Length, Is.EqualTo(2));
                    Assert.That(renderers[0].bounds.min.y, Is.EqualTo(shrine.transform.position.y).Within(.001f));
                    var marker = shrine.GetComponentsInChildren<Transform>().Single(t => t.name == "InteractionPoint");
                    var point = shrine.transform.InverseTransformPoint(marker.position);
                    Assert.That(point.y, Is.EqualTo(.30f).Within(.001f));
                    Assert.That(point.z, Is.EqualTo(-.405f).Within(.001f));
                }
            }
            finally { driver.Clear(); Object.DestroyImmediate(root); }
        }
        private static void WithModel(Action<ShrineDriver, ShrineDriverConfig, GameObject, Material, Material> test)
        {
            var root = new GameObject("Model driver fixture");
            var model = new GameObject("Shared static shrine template"); model.SetActive(false);
            var driver = root.AddComponent<ShrineDriver>();
            var config = ScriptableObject.CreateInstance<ShrineDriverConfig>();
            Material body = null, accent = null;
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit"); Assert.That(shader, Is.Not.Null);
                body = new Material(shader) { name = ShrineDriverConfig.BodyMaterialName, color = new Color(.24f, .27f, .29f) };
                accent = new Material(shader) { name = ShrineDriverConfig.AccentMaterialName, color = Color.white };
                accent.SetColor("_EmissionColor", Color.white);
                foreach (bool isAccent in new[] { false, true })
                {
                    var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    child.transform.SetParent(model.transform, false);
                    child.name = isAccent ? "Inactive accent" : "Body";
                    child.GetComponent<Renderer>().sharedMaterial = isAccent ? accent : body;
                    if (isAccent) child.SetActive(false);
                }
                test(driver, config, model, body, accent);
            }
            finally
            {
                driver.Clear(); Object.DestroyImmediate(root); Object.DestroyImmediate(model); Object.DestroyImmediate(config);
                if (body != null) Object.DestroyImmediate(body); if (accent != null) Object.DestroyImmediate(accent);
            }
        }
        private static ShrinePlacement Placement(int id, ShrineKind kind) => new ShrinePlacement(id, kind, new ShrineSite(new Vector3(id*2f, 0f, 0f), 1));
        private static void SetModel(ShrineDriverConfig config, ShrineKind kind, GameObject model)
        {
            string name = kind.ToString();
            typeof(ShrineDriverConfig).GetField("_" + char.ToLowerInvariant(name[0]) + name.Substring(1) + "Model",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, model);
        }
        private static ShrineDriverState State(ShrineDriver driver) => (ShrineDriverState)typeof(ShrineDriver)
            .GetField("state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        private static void AssertEmpty(ShrineDriver driver)
        {
            var state = State(driver);
            Assert.That(driver.transform.childCount, Is.Zero); Assert.That(state.Objects, Is.Empty);
            Assert.That(state.Materials, Is.Empty); Assert.That(state.Bodies, Is.Empty);
            Assert.That(state.Accents, Is.Empty); Assert.That(state.AccentColors, Is.Empty);
        }
        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.00001f)); Assert.That(actual.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.00001f)); Assert.That(actual.a, Is.EqualTo(expected.a).Within(.00001f));
        }
    }
}
