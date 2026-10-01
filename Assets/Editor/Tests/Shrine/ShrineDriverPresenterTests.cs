// ============================================================================
// ShrineDriverPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Freezes the eight provisional recipe silhouettes without constructing Unity objects.
//   Distinct geometry must not rely on kind names, color differences or floating text.
//   Separate assertions freeze sizing and spent-state math for headless execution.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Shrine.
// KEY RESPONSIBILITIES:
//   - Verify nonempty, distinct, volumetric recipes with body and accent parts per kind.
//   - Verify normalized positioning, cylinder sizing and clamped dimming/emission.
// DEPENDENCIES:
//   - Shrine DriverConfig data and Presenter, Core, NUnit and Unity value types.
// USAGE NOTES:
//   Pure fixture; nested recipe data is constructed directly, never as a ScriptableObject.
//   Native objects, materials, colliders and visible results require ShrineDriverTests.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
namespace Worsen.Tests.Shrine
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShrineDriverPresenterTests
    {
        [Test]
        public void AllKindsHaveDistinctNonemptyVolumetricRecipes()
        {
            var shapes = new ShrineDriverConfig.Silhouettes();
            var signatures = new HashSet<string>();
            foreach (ShrineKind kind in Enum.GetValues(typeof(ShrineKind)))
            {
                var shape = shapes.Get(kind);
                Assert.That(shape.Parts.Count, Is.InRange(2, 8), kind.ToString());
                Assert.That(shape.Parts.Any(part => part.Accent), Is.True, kind.ToString());
                Assert.That(shape.Parts.Any(part => !part.Accent), Is.True, kind.ToString());
                foreach (var part in shape.Parts.Append(shapes.Pedestal))
                {
                    Assert.That(new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cylinder }, Does.Contain(part.Primitive));
                    var scale = ShrineDriverPresenter.Scale(part, Vector3.one);
                    Assert.That(scale.x, Is.GreaterThan(0f)); Assert.That(scale.y, Is.GreaterThan(0f)); Assert.That(scale.z, Is.GreaterThan(0f));
                    Assert.That(part.Position.y, Is.GreaterThanOrEqualTo(0f));
                }
                // Ignore accent flags and colors; sort to avoid order-only differences.
                string signature = string.Join(";", shape.Parts.Select(part => part.Primitive + ":" +
                    Signature(part.Position) + ":" + Signature(part.Dimensions) + ":" + Signature(part.Euler)).OrderBy(value => value));
                Assert.That(signatures.Add(signature), Is.True, "Geometry duplicated for " + kind);
            }
            Assert.That(signatures.Count, Is.EqualTo(8));
            Assert.That(shapes.Pedestal.Accent, Is.False);
            Assert.That(shapes.Pedestal.Position.y - shapes.Pedestal.Dimensions.y * .5f, Is.EqualTo(0f));
        }
        [TestCase(PrimitiveType.Cube, 1f)]
        [TestCase(PrimitiveType.Sphere, 1f)]
        [TestCase(PrimitiveType.Cylinder, .5f)]
        public void RecipeDimensionsAndPositionScaleWithConfiguredEnvelope(PrimitiveType primitive, float heightScale)
        {
            var part = new ShrineDriverConfig.Part(primitive, new Vector3(.2f, .5f, -.3f), new Vector3(.4f, .6f, .8f));
            var size = new Vector3(2f, 3f, 4f);
            Assert.That(ShrineDriverPresenter.Position(part, size), Is.EqualTo(new Vector3(.4f, 1.5f, -1.2f)));
            var scale = ShrineDriverPresenter.Scale(part, size);
            Assert.That(scale.x, Is.EqualTo(.8f).Within(.00001f));
            Assert.That(scale.y, Is.EqualTo(1.8f * heightScale).Within(.00001f));
            Assert.That(scale.z, Is.EqualTo(3.2f).Within(.00001f));
        }
        [Test]
        public void EveryKindsSpentAccentDimsWithoutChangingHueOrAlpha()
        {
            var shapes = new ShrineDriverConfig.Silhouettes();
            foreach (ShrineKind kind in Enum.GetValues(typeof(ShrineKind)))
            {
                var color = shapes.Get(kind).AccentColor;
                var active = ShrineDriverPresenter.Emission(color, .65f, 1f);
                var spent = ShrineDriverPresenter.Emission(color, .65f, .12f);
                var body = ShrineDriverPresenter.DimAccent(color, .12f);
                Assert.That(spent.r, Is.EqualTo(active.r * .12f).Within(.00001f));
                Assert.That(spent.g, Is.EqualTo(active.g * .12f).Within(.00001f));
                Assert.That(spent.b, Is.EqualTo(active.b * .12f).Within(.00001f));
                Assert.That(spent.r + spent.g + spent.b, Is.LessThan(active.r + active.g + active.b));
                Assert.That(body.a, Is.EqualTo(color.a));
            }
        }
        [TestCase(-1f, 0f)] [TestCase(0f, 0f)] [TestCase(.12f, .12f)] [TestCase(1f, 1f)] [TestCase(2f, 1f)]
        public void AccentMultiplierClampsAndPreservesAlpha(float multiplier, float expected)
        {
            var color = ShrineDriverPresenter.DimAccent(new Color(1f, .5f, .25f, .4f), multiplier);
            Assert.That(color, Is.EqualTo(new Color(expected, .5f * expected, .25f * expected, .4f)));
            Assert.That(ShrineDriverPresenter.Emission(color, -1f, 1f), Is.EqualTo(new Color(0f, 0f, 0f, 1f)));
        }
        [Test]
        public void InvalidKindAndUnsupportedPrimitiveFailExplicitly()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ShrineDriverConfig.Silhouettes().Get((ShrineKind)99));
            var part = new ShrineDriverConfig.Part(PrimitiveType.Quad, Vector3.zero, Vector3.one);
            Assert.Throws<ArgumentOutOfRangeException>(() => ShrineDriverPresenter.Scale(part, Vector3.one));
        }
        private static string Signature(Vector3 value) => string.Join(",", new[] { value.x, value.y, value.z }
            .Select(component => component.ToString("R", CultureInfo.InvariantCulture)));
    }
}
