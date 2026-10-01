// ============================================================================
// ShrineDriverTests.cs
// ============================================================================
// PURPOSE:
//   Exercises actual shrine primitives and their private material lifetimes in Edit Mode.
//   All eight kinds must build without labels, remain nonblocking and dim independently.
//   Explicit cleanup covers assertions, rebuilds and failed construction as well as Clear.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Shrine.
// KEY RESPONSIBILITIES:
//   - Verify distinct native silhouettes with no text or lights and disabled colliders.
//   - Verify spent colors and emission, private palette isolation and repeat use.
//   - Verify Clear, rebuild and failure release roots, children, materials and retained state.
// DEPENDENCIES:
//   - Domain Shrine, Core, NUnit, temporary Unity objects and test-only reflection.
// USAGE NOTES:
//   Coordinator-only native Edit Mode fixture; headless environment cases are not passes.
//   Every fixture clears its Driver in finally before destroying its owner.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Shrine
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    [Category("RequiresUnity")]
    public sealed class ShrineDriverTests
    {
        [Test]
        public void AllKindsBuildDistinctSilhouettesWithoutTextLightsOrBlocking()
        {
            WithDriver((driver, config) =>
            {
                driver.Build(Lineup(), config);
                Assert.That(driver.transform.childCount, Is.EqualTo(8));
                Assert.That(driver.GetComponentsInChildren<TextMesh>(true), Is.Empty);
                Assert.That(driver.GetComponentsInChildren<Light>(true), Is.Empty);
                var signatures = new HashSet<string>();
                foreach (var placement in Lineup())
                {
                    var root = State(driver).Objects[placement.Id];
                    Assert.That(root.transform.position, Is.EqualTo(placement.Site.Position));
                    var renderers = root.GetComponentsInChildren<Renderer>();
                    Assert.That(renderers.Length, Is.EqualTo(config.Shapes.Get(placement.Kind).Parts.Count + 1));
                    Assert.That(signatures.Add(string.Join(";", renderers.Select(renderer =>
                        renderer.GetComponent<MeshFilter>().sharedMesh.name + ":" + renderer.transform.localPosition + ":" +
                        renderer.transform.localScale + ":" + renderer.transform.localRotation))), Is.True, placement.Kind.ToString());
                    foreach (var collider in root.GetComponentsInChildren<Collider>()) Assert.That(collider.enabled, Is.False);
                    foreach (var renderer in renderers)
                    {
                        Assert.That(renderer.bounds.size.sqrMagnitude, Is.GreaterThan(0f));
                        Assert.That(renderer.sharedMaterial.globalIlluminationFlags, Is.EqualTo(MaterialGlobalIlluminationFlags.None));
                    }
                    var accent = State(driver).Accents[placement.Id];
                    Assert.That(accent.IsKeywordEnabled("_EMISSION"), Is.True);
                    Assert.That(accent.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(0f));
                }
                Assert.That(State(driver).Materials.Count, Is.EqualTo(16));
            });
        }
        [Test]
        public void MarkUsedGreysBodyDimsAccentOnceAndDoesNotAffectOtherShrines()
        {
            WithDriver((driver, config) =>
            {
                driver.Build(Lineup(), config);
                var state = State(driver);
                var otherEmission = state.Accents[2].GetColor("_EmissionColor");
                driver.MarkUsed(1);
                AssertColor(state.Bodies[1].color, config.SpentColor);
                AssertColor(state.Accents[1].color, ShrineDriverPresenter.DimAccent(state.AccentColors[1], config.SpentAccentMultiplier));
                var spent = state.Accents[1].GetColor("_EmissionColor");
                AssertColor(spent, ShrineDriverPresenter.Emission(state.AccentColors[1], config.AccentEmission, config.SpentAccentMultiplier));
                Assert.That(spent.maxColorComponent, Is.LessThan(otherEmission.maxColorComponent));
                driver.MarkUsed(1); driver.MarkUsed(-1);
                AssertColor(state.Accents[1].GetColor("_EmissionColor"), spent);
                AssertColor(state.Accents[2].GetColor("_EmissionColor"), otherEmission);
                AssertColor(state.Bodies[2].color, config.Color);
                Assert.That(config.Color, Is.EqualTo(new Color(.6f, .5f, .2f)));
                Assert.That(state.Bodies[1], Is.Not.SameAs(state.Bodies[2]));
                Assert.That(state.Accents[1], Is.Not.SameAs(state.Accents[2]));
                for (int id = 2; id <= 8; id++)
                {
                    var before = state.Accents[id].GetColor("_EmissionColor");
                    driver.MarkUsed(id);
                    AssertColor(state.Bodies[id].color, config.SpentColor);
                    Assert.That(state.Accents[id].GetColor("_EmissionColor").maxColorComponent, Is.LessThan(before.maxColorComponent));
                }
            });
        }
        [Test]
        public void ClearReleasesEveryObjectAndMaterialAndIsIdempotent()
        {
            WithDriver((driver, config) =>
            {
                driver.Build(Lineup(), config);
                var objects = driver.GetComponentsInChildren<Transform>().Where(value => value != driver.transform).Select(value => value.gameObject).ToArray();
                var materials = State(driver).Materials.ToArray();
                driver.Clear(); driver.Clear(); driver.MarkUsed(1);
                foreach (var value in objects) Assert.That(value == null, Is.True, "Owned child survived Clear.");
                foreach (var value in materials) Assert.That(value == null, Is.True, "Private material survived Clear.");
                AssertEmpty(driver);
            });
        }
        [Test]
        public void RebuildReleasesOldResourcesAndRestoresActivePalette()
        {
            WithDriver((driver, config) =>
            {
                driver.Build(Lineup(), config); driver.MarkUsed(1);
                var roots = State(driver).Objects.Values.ToArray(); var materials = State(driver).Materials.ToArray();
                driver.Build(Lineup(), config);
                foreach (var value in roots) Assert.That(value == null, Is.True);
                foreach (var value in materials) Assert.That(value == null, Is.True);
                Assert.That(driver.transform.childCount, Is.EqualTo(8));
                AssertColor(State(driver).Bodies[1].color, config.Color);
                AssertColor(State(driver).Accents[1].GetColor("_EmissionColor"),
                    ShrineDriverPresenter.Emission(State(driver).AccentColors[1], config.AccentEmission, 1f));
            });
        }
        [Test]
        public void DuplicateIdsAndInvalidKindsLeaveNoPartialBuild()
        {
            WithDriver((driver, config) =>
            {
                var first = Lineup()[0];
                Assert.Throws<ArgumentException>(() => driver.Build(new[] { first, first }, config));
                AssertEmpty(driver);
                Assert.Throws<ArgumentOutOfRangeException>(() => driver.Build(new[] {
                    first, new ShrinePlacement(2, (ShrineKind)99, first.Site) }, config));
                AssertEmpty(driver);
                driver.Build(Array.Empty<ShrinePlacement>(), config); driver.Clear();
                AssertEmpty(driver);
            });
        }
        private static void WithDriver(Action<ShrineDriver, ShrineDriverConfig> test)
        {
            var root = new GameObject("Shrine Driver fixture");
            var config = ScriptableObject.CreateInstance<ShrineDriverConfig>();
            var driver = root.AddComponent<ShrineDriver>();
            try { test(driver, config); }
            finally { driver.Clear(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        private static ShrinePlacement[] Lineup() => ((ShrineKind[])Enum.GetValues(typeof(ShrineKind)))
            .Select((kind, index) => new ShrinePlacement(index + 1, kind, new ShrineSite(new Vector3(index * 2f, 0f, 0f), 1))).ToArray();
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
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.00001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.00001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.00001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.00001f));
        }
    }
}
