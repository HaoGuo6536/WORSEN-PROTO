// ============================================================================
// EnvironmentTemplateSocketTests.cs
// ============================================================================
// PURPOSE:
//   Checks template lighting integration and low-ceiling hallway compatibility.
//   Pure slot comparisons prevent mirrored procedural identities from drifting.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · CastleEnvironment.
// KEY RESPONSIBILITIES:
//   - Compare low corridor socket positions across Domain and Presentation.
//   - Check curved decoration admission and exact authored-light override.
//   - Verify real school/basement kit fixtures retain light state without primitive panels.
// DEPENDENCIES:
//   - NUnit, Core, Domain.Procedural and Presentation.Environment.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Transient fixtures only; live Lumen rendering remains a coordinator check.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;
using Worsen.Presentation.Environment;
using Object = UnityEngine.Object;

namespace Worsen.Tests.CastleEnvironment
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EnvironmentTemplateSocketTests
    {
        [TestCase(3.2f)] [TestCase(3.8f)]
        public void FourMetreHallwayPreservesMirroredLightPositions(float height)
        {
            var bounds = new Bounds(Vector3.up * (height * .5f), new Vector3(4f, height, 12f));
            var room = new LevelRoom(7, bounds.center, bounds.size);
            var layout = new ProceduralLayout();
            Property(layout, "Graph", LevelGraphUtility.Build(new[] { room }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 7, Vector3.zero));
            Property(layout, "Doors", Array.Empty<ProceduralDoorPlan>()); Property(layout, "Modules", Array.Empty<ProceduralRoomModule>());
            var config = ScriptableObject.CreateInstance<ProceduralConfig>(); var driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            Field(config, "_knockablePropsPerRoom").SetValue(config, 0);
            try
            {
                var lights = new ProceduralInteractablePresenter().Build(layout, config, driver, Array.Empty<ProceduralBlock>(), new System.Random(1))
                    .Where(p => p.State.Kind == InteractableKind.Light).Select(p => p.State.Position).ToArray();
                var slots = EnvironmentPresenter.BuildSlots(7, bounds, null).Where(s => s.Torch).ToArray();
                Assert.That(lights, Is.Not.Empty); Assert.That(slots.Select(s => s.Position), Is.EquivalentTo(lights));
                Assert.That(slots.All(s => s.Position.y + s.Envelope.y * .5f < bounds.max.y), Is.True);
            }
            finally { Object.DestroyImmediate(config); Object.DestroyImmediate(driver); }
        }
        [Test]
        public void CurvedShellRejectsRectangleCornersButPreservesExplicitLightSockets()
        {
            var boundary = Enumerable.Range(0, 32).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI / 16f) * 6f, 0f, Mathf.Sin(i * Mathf.PI / 16f) * 6f)).ToArray();
            var corner = new EnvironmentSlot(new Vector3(5f, 2f, 5f), 0f, EnvironmentDecorationKind.Column, Vector3.one);
            Assert.That(EnvironmentPresenter.FitsBoundary(corner, boundary), Is.False);
            var authored = new[] { new Vector3(2f, 2.6f, 1f), new Vector3(-2f, 2.6f, 1f) };
            var slots = EnvironmentPresenter.BuildDressing(3, new Bounds(Vector3.up * 3.5f, new Vector3(12f, 7f, 12f)), false, false,
                null, boundary: boundary, lightSockets: authored);
            Assert.That(slots.Where(s => s.Torch).Select(s => s.Position), Is.EqualTo(authored));
            Assert.That(slots.Where(s => !s.Torch).All(s => EnvironmentPresenter.FitsBoundary(s, boundary)), Is.True);
        }
        [TestCase("school", true)] [TestCase("basement", false)] [Category("Native")]
        public void ThemeFixturesUseKitMeshesWithoutRealLights(string theme, bool fluorescent)
        {
            var root = new GameObject("Theme socket fixture"); var config = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<EnvironmentDriverConfig>();
            var driver = root.AddComponent<EnvironmentDriver>(); Field(driver, "_config").SetValue(driver, config);
            try
            {
                Assert.That(Resources.Load<GameObject>(fluorescent ? config.SchoolFixture : config.BasementFixture), Is.Not.Null,
                    "Coordinator must run ProceduralKitAssetSetup before native fixture checks.");
                var socket = new Vector3(1f, 2.6f, 1f);
                driver.SetTheme(theme, theme == "school" ? "fluorescent" : "cage-lamp");
                driver.AddRoom(1, new Bounds(Vector3.up * 1.6f, new Vector3(4f, 3.2f, 12f)), false, false, null, lightSockets: new[] { socket });
                var state = (EnvironmentDriverState)Field(driver, "_state").GetValue(driver);
                Assert.That(state.Flames, Has.Count.EqualTo(1)); Assert.That(state.Flames[0].Fluorescent, Is.EqualTo(fluorescent));
                Assert.That(state.Flames[0].SocketPosition, Is.EqualTo(socket)); Assert.That(state.Flames[0].Panel, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty);
                Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Flat fluorescent diffuser" || t.name == "Cage bar"), Is.False);
                var fixture = root.GetComponentsInChildren<EnvironmentDecoration>(true)
                    .Single(d => d.GetComponent<EnvironmentFluorescentFixture>() != null);
                Assert.That(state.Positions[0], Is.EqualTo(fixture.LightPosition));
                Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
                driver.SetOwnerEnabled(true); driver.SetObserver(state.Positions[0]); driver.Tick(0f);
                var materials = fixture.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
                var emitters = materials.Where(m => m.name == "Owned fixture emitter").ToArray();
                Assert.That(emitters, Is.Not.Empty, "Only the kit's tube/sodium material should emit.");
                Assert.That(emitters.All(m => m.GetColor("_EmissionColor").maxColorComponent > 0f), Is.True);
                Assert.That(materials.Any(m => m.name != "Owned fixture emitter"), Is.True, "Housing must retain lit kit materials.");
                Assert.That(state.Flames.Count(f => f.EffectRoot.activeSelf), Is.LessThanOrEqualTo(config.MaximumLumenEffects));
                driver.ApplyLight(new InteractableState(1, InteractableKind.Light, 1, socket, InteractableStateValue.Inactive));
                Assert.That(state.Flames[0].Lit, Is.False);
                Assert.That(emitters.All(m => m.GetColor("_EmissionColor").maxColorComponent == 0f), Is.True);
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    }
}
