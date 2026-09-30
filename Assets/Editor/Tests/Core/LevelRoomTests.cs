// ============================================================================
// LevelRoomTests.cs
// ============================================================================
// PURPOSE:
//   Verifies additive room footprint contracts without constructing scene objects.
//   Legacy rectangles retain their extent while L-shaped cells exclude the notch.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Core · shared Level contracts.
// KEY RESPONSIBILITIES:
//   - Verify rectangle defaults, pocket identity and immutable cell snapshots.
// DEPENDENCIES:
//   - Core definitions, NUnit and Unity value types only.
// USAGE NOTES:
//   Pure Edit Mode tests; vertical position is intentionally ignored by ContainsXZ.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;

namespace Worsen.Tests.Core
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class LevelRoomTests
    {
        [Test]
        public void LegacyRoomsDefaultToOneRectangleAndNoPocket()
        {
            var room = new LevelRoom(1, Vector3.one, Vector3.one * 12f);
            var sample = new GeneratedRoomSample(1, room.Bounds, false, false, null);
            Assert.That(room.Cells, Is.EqualTo(new[] { room.Bounds }));
            Assert.That(sample.Cells, Is.EqualTo(new[] { room.Bounds }));
            Assert.That(room.Pocket, Is.False);
            Assert.That(sample.OptionalRoom, Is.False);
            Assert.That(room.ContainsXZ(room.Bounds.min), Is.True);
            Assert.That(room.ContainsXZ(room.Bounds.max), Is.True);
            Assert.That(room.ContainsXZ(Vector3.up * 100f), Is.True);
        }

        [Test]
        public void LShapeExcludesNotchAndCopiesReadOnlyCells()
        {
            var cells = new[] { new Bounds(new Vector3(-3f, 3f, 0f), new Vector3(6f, 6f, 12f)),
                new Bounds(new Vector3(3f, 3f, -3f), new Vector3(6f, 6f, 6f)) };
            var room = new LevelRoom(2, Vector3.up * 3f, Vector3.one * 12f, cells, true);
            var sample = new GeneratedRoomSample(2, room.Bounds, false, false, null, true, cells);
            cells[0] = room.Bounds;
            Assert.That(room.Pocket, Is.True);
            Assert.That(room.ContainsXZ(new Vector3(3f, 0f, 3f)), Is.False);
            Assert.That(room.ContainsXZ(new Vector3(-3f, 100f, 3f)), Is.True);
            Assert.That(room.ContainsXZ(new Vector3(3f, 0f, -3f)), Is.True);
            Assert.That(sample.Cells, Is.EqualTo(room.Cells));
            Assert.Throws<NotSupportedException>(() => ((IList<Bounds>)room.Cells)[0] = room.Bounds);
            Assert.Throws<NotSupportedException>(() => ((IList<Bounds>)sample.Cells)[0] = room.Bounds);
        }
    }
}
