// ============================================================================
// ShrineControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests shrine cadence, placement admission and movement-only single-use activation.
//   Fixture sites avoid depending on the separately owned procedural generator.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Shrine.
// KEY RESPONSIBILITIES:
//   - Cover contact, Interact, thresholds, nearest selection and activation payloads.
//   - Cover every availability floor, gap edges and event-axis exclusions over seeds.
// DEPENDENCIES:
//   - Domain Shrine, Core, NUnit and temporary Unity config allocation.
// USAGE NOTES:
//   Edit Mode tests; no scene assembly, engine clock or unseeded randomness.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;
namespace Worsen.Tests.Shrine
{
    public sealed class ShrineControllerTests
    {
        private ShrineConfig config;
        private ShrineController controller;
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ShrineConfig>();
            controller = new ShrineController(new ShrineBehaviorState(), config, new System.Random(9));
            controller.Reset(new[] { new ShrinePlacement(4, ShrineKind.Chance, new ShrineSite(Vector3.zero, 7)) });
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);
        [TestCase(0f, InputButtons.None, true)]
        [TestCase(1.5f, InputButtons.Interact, true)]
        [TestCase(1.5f, InputButtons.None, false)]
        [TestCase(3f, InputButtons.Interact, false)]
        public void MovingContactAndInteractRespectTheirRanges(float distance, InputButtons pressed, bool expected)
        {
            Assert.That(controller.TryActivate(Vector3.right * distance, Vector3.forward, pressed, 12, out var fact), Is.EqualTo(expected));
            if (!expected) return;
            Assert.That(fact.ShrineId, Is.EqualTo(4)); Assert.That(fact.Kind, Is.EqualTo(ShrineKind.Chance));
            Assert.That(fact.RoomId, Is.EqualTo(7)); Assert.That(fact.Position, Is.EqualTo(Vector3.zero));
            Assert.That(fact.Tick, Is.EqualTo(12));
            Assert.That(controller.TryActivate(Vector3.zero, Vector3.forward, InputButtons.Interact, 13, out _), Is.False);
        }
        [TestCase(InputButtons.None)] [TestCase(InputButtons.Interact)]
        public void StationaryVerticalAndThresholdMotionDoNotActivate(InputButtons pressed)
        {
            foreach (var velocity in new[] { Vector3.zero, Vector3.up * 10f, Vector3.right * config.MinimumSpeed })
                Assert.That(controller.TryActivate(Vector3.zero, velocity, pressed, 1, out _), Is.False);
            Assert.That(controller.TryActivate(Vector3.zero, Vector3.forward, pressed, 2, out _), Is.True);
        }
        [Test]
        public void InteractSelectsNearestAndCannotReachAcrossStoreys()
        {
            controller.Reset(new[] {
                new ShrinePlacement(1, ShrineKind.Bargain, new ShrineSite(Vector3.right * 1.8f, 1)),
                new ShrinePlacement(2, ShrineKind.Chance, new ShrineSite(Vector3.right, 1)),
                new ShrinePlacement(3, ShrineKind.Chance, new ShrineSite(Vector3.up * 4f, 2)) });
            Assert.That(controller.TryActivate(Vector3.zero, Vector3.forward, InputButtons.Interact, 1, out var fact), Is.True);
            Assert.That(fact.ShrineId, Is.EqualTo(2));
        }
        [TestCase(0, 0)] [TestCase(2, 0)] [TestCase(3, 1)] [TestCase(5, 1)]
        [TestCase(6, 2)] [TestCase(9, 2)] [TestCase(10, 3)] [TestCase(1000, 3)]
        public void DepthCurveCapsBaseCountAndAddsMoreShrinesAfterUnlock(int floor, int count)
        {
            Assert.That(controller.CountForFloor(floor, false), Is.EqualTo(count));
            Assert.That(controller.CountForFloor(floor, true), Is.EqualTo(count == 0 ? 0 : count + 1));
        }
        [Test]
        public void SeededPlacementHonoursFloorsGapEdgesAndExcludedAxes()
        {
            var sites = Enumerable.Range(0, 8).Select(i => new ShrineSite(Vector3.right * i * 5, i, i % 2 == 0)).ToArray();
            foreach (int floor in new[] { 3, 5, 6, 8, 10 })
                for (int seed = 0; seed < 40; seed++)
                {
                    var a = new ShrineController(new ShrineBehaviorState(), config, new System.Random(seed));
                    var b = new ShrineController(new ShrineBehaviorState(), config, new System.Random(seed));
                    var placed = a.Assemble(sites, floor, false, new[] { FearAxis.Stakes });
                    Assert.That(placed.Select(p => p.Kind), Is.EqualTo(b.Assemble(sites, floor, false, new[] { FearAxis.Stakes }).Select(p => p.Kind)));
                    Assert.That(placed.Count, Is.EqualTo(a.CountForFloor(floor, false)));
                    foreach (var placement in placed)
                    {
                        var entry = config.Availability.Single(e => e.Kind == placement.Kind);
                        Assert.That(entry.Floor, Is.LessThanOrEqualTo(floor));
                        Assert.That(entry.Axis, Is.Not.EqualTo(FearAxis.Stakes));
                        if (placement.Kind == ShrineKind.Passage) Assert.That(placement.Site.GapEdge, Is.True);
                    }
                }
        }
        [Test]
        public void AllBlockedOrAbsentSitesProduceNoPlacements()
        {
            Assert.That(controller.Assemble(Array.Empty<ShrineSite>(), 10), Is.Empty);
            Assert.That(controller.Assemble(new[] { new ShrineSite(Vector3.zero, 1) }, 10, true,
                new[] { FearAxis.Information, FearAxis.Unpredictability, FearAxis.Stakes, FearAxis.Agency, FearAxis.Time }), Is.Empty);
        }
    }
}
