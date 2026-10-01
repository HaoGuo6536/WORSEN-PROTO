// ============================================================================
// ShrineControllerPlacementTests.cs
// ============================================================================
// PURPOSE:
//   Proves seeded farthest-site selection cannot cluster multiple shrines in a room.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Tests · Shrine.
// KEY RESPONSIBILITIES:
//   - Retain the existing floor-count curve and deterministic site identities.
//   - Check singular rooms, separation and Passage-only gap eligibility.
// DEPENDENCIES:
//   - NUnit, Core shrine values and own Domain controller/config.
// USAGE NOTES:
//   Managed config fields only; no Unity object or scene allocation.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Shrine;

namespace Worsen.Tests.Shrine
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ShrineControllerPlacementTests
    {
        [TestCase(2, 0)] [TestCase(3, 1)] [TestCase(5, 1)] [TestCase(6, 2)] [TestCase(9, 2)] [TestCase(10, 3)] [TestCase(100, 3)]
        public void ExistingCountCurveIsPreserved(int floor, int expected)
        {
            var c = Controller(1); Assert.That(c.CountForFloor(floor, false), Is.EqualTo(expected));
            Assert.That(c.CountForFloor(floor, true), Is.EqualTo(expected == 0 ? 0 : expected + 1));
        }
        [Test]
        public void DuplicateRoomCandidatesNeverClusterAndReplayExactly()
        {
            var sites = Enumerable.Range(0, 6).SelectMany(room => Enumerable.Range(0, 8)
                .Select(i => new ShrineSite(new Vector3(room * 30f + i * .1f, 0f, 0f), room + 1, room == 5))).ToArray();
            for (int seed = 0; seed < 64; seed++)
            {
                var placements = Controller(seed).Assemble(sites, 10, true);
                Assert.That(placements.Count, Is.EqualTo(4));
                Assert.That(placements.Select(p => p.Site.RoomId).Distinct().Count(), Is.EqualTo(4));
                Assert.That(placements.Select(p => (p.Id, p.Kind)), Is.EqualTo(Controller(seed).Assemble(sites, 10, true).Select(p => (p.Id, p.Kind))));
                Assert.That(placements.Max(p => p.Site.Position.x) - placements.Min(p => p.Site.Position.x), Is.GreaterThan(120f));
                Assert.That(placements.Where(p => p.Kind == ShrineKind.Passage).All(p => p.Site.GapEdge), Is.True);
            }
        }
        private static ShrineController Controller(int seed)
        {
            var c = (ShrineConfig)FormatterServices.GetUninitializedObject(typeof(ShrineConfig));
            Field("_countFloors", new[] { 3, 6, 10 }); Field("_baseCap", 3); Field("_moreShrinesBonus", 1);
            Field("_availability", new[] { new ShrineAvailability(ShrineKind.Chance, 3, FearAxis.Unpredictability), new ShrineAvailability(ShrineKind.Passage, 8, FearAxis.Time) });
            return new ShrineController(new ShrineBehaviorState(), c, new System.Random(seed));
            void Field(string name, object value) => typeof(ShrineConfig).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(c, value);
        }
    }
}
