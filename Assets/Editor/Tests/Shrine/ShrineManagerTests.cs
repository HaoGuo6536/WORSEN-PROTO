// ============================================================================
// ShrineManagerTests.cs
// ============================================================================
// PURPOSE:
//   Checks that the real shrine endpoint builds nonblocking placeholders and publishes use.
//   Fixture sites keep this test independent of generated room assembly.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Shrine.
// KEY RESPONSIBILITIES:
//   - Verify one object per admitted site, no blocking collider and one activation event.
// DEPENDENCIES:
//   - Domain Shrine, Core, NUnit and temporary Unity objects.
// USAGE NOTES:
//   Edit Mode engine test; does not prove the Run owner calls Sample in a live floor.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
namespace Worsen.Tests.Shrine
{
    public sealed class ShrineManagerTests
    {
        [TestCase(false)] [TestCase(true)]
        public void MovingActivationPublishesExactlyOnceWithoutBlocking(bool interact)
        {
            var root = new GameObject("Shrine fixture");
            var config = ScriptableObject.CreateInstance<ShrineConfig>();
            var visual = ScriptableObject.CreateInstance<ShrineDriverConfig>();
            var facts = new List<ShrineActivatedFact>();
            var manager = root.AddComponent<ShrineManager>();
            manager.Activated += facts.Add;
            try
            {
                var placements = manager.Assemble(new[] { new ShrineSite(Vector3.zero, 7) }, 3, config, visual, new System.Random(3));
                Assert.That(placements.Count, Is.EqualTo(1));
                Assert.That(root.GetComponentsInChildren<TextMesh>().Length, Is.EqualTo(1));
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Assert.That(collider.enabled, Is.False);
                var position = interact ? Vector3.right * 1.5f : Vector3.zero;
                var pressed = interact ? InputButtons.Interact : InputButtons.None;
                Assert.That(manager.Sample(position, Vector3.zero, pressed, 1), Is.False);
                Assert.That(manager.Sample(position, Vector3.forward, pressed, 2), Is.True);
                Assert.That(manager.Sample(position, Vector3.forward, pressed, 3), Is.False);
                Assert.That(facts.Count, Is.EqualTo(1)); Assert.That(facts[0].RoomId, Is.EqualTo(7));
                Assert.That(facts[0].Kind, Is.EqualTo(placements[0].Kind)); Assert.That(facts[0].Tick, Is.EqualTo(2));
                manager.Teardown(); Assert.That(root.GetComponentsInChildren<TextMesh>(), Is.Empty);
            }
            finally
            {
                manager.Activated -= facts.Add;
                Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(visual);
            }
        }
    }
}
