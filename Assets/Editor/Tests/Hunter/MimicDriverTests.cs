// ============================================================================
// MimicDriverTests.cs
// ============================================================================
// PURPOSE:
//   Checks the engine disguise consumer without modifying authored art or assets.
//   Golden colouring and spent visibility must not change a shared material or
//   manufacture a real Floor pickup, and teardown restores the original renderer.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Check golden tint, spent hiding, ordinary restoration and no pickup components.
// DEPENDENCIES:
//   - Mimic Driver, Floor config, Core facts, Unity scene objects and NUnit.
// USAGE NOTES:
//   Native Edit Mode only. Explicit Teardown precedes DestroyImmediate.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter.Archetypes.Mimic;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MimicDriverTests
    {
        [Test] public void GoldenPoseUsesFloorPalette_SpentHidesAndTeardownRestores_NoPickupIsCreated()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var config = ScriptableObject.CreateInstance<FloorDriverConfig>();
            var driver = root.AddComponent<MimicDriver>(); var renderer = root.GetComponent<Renderer>();
            var original = new MaterialPropertyBlock(); original.SetColor("_BaseColor", Color.magenta); renderer.SetPropertyBlock(original);
            var material = renderer.sharedMaterial;
            var hunter = new Worsen.Core.EntityId(-1); var player = new Worsen.Core.EntityId(1);
            try
            {
                driver.Initialize(config);
                driver.Apply(new MimicFact(hunter, player, MimicFactKind.Pose, 1, Vector3.zero, golden: true));
                var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(config.GoldenColor));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material)); Assert.That(renderer.enabled, Is.True);
                driver.Apply(new MimicFact(hunter, player, MimicFactKind.PoseRemoved, 2, Vector3.zero));
                Assert.That(renderer.enabled, Is.False);
                driver.Apply(new MimicFact(hunter, player, MimicFactKind.BiteEnded, 3, Vector3.zero));
                Assert.That(renderer.enabled, Is.False);
                Assert.That(root.GetComponentsInChildren<CakePickup>(true), Is.Empty);
                driver.Teardown(); Assert.That(renderer.enabled, Is.True); renderer.GetPropertyBlock(block);
                Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.magenta));
                driver.Initialize(config); driver.Apply(new MimicFact(hunter, player, MimicFactKind.Pose, 4, Vector3.zero));
                renderer.GetPropertyBlock(block); Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(Color.magenta));
            }
            finally { driver.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }
    }
}
