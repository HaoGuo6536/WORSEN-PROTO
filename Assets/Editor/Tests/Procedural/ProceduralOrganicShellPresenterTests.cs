// ============================================================================
// ProceduralOrganicShellPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Tests shell tiling independently from weighted room selection.
//   A forced bent hallway sample checks that primitive floors and roofs match
//   occupied tiles and do not reintroduce the old twelve-metre bounding square.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check two-metre floor/roof coverage, ownership and omitted interior seams.
// DEPENDENCIES:
//   - NUnit, UnityEditor serialization and Domain.Procedural.
// USAGE NOTES:
//   This verifies primitive fallback geometry only; no imported art is required.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralOrganicShellPresenterTests
    {
        [Test]
        public void RoundLeafRoomsHaveCurvedCollisionAndAnOpenFourMetreVestibule()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var o = ScriptableObject.CreateInstance<ProceduralOrganicConfig>();
            var p = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            var d = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var edit = new SerializedObject(c); edit.FindProperty("_organic").objectReferenceValue = o;
                edit.FindProperty("_challenges").objectReferenceValue = p; edit.ApplyModifiedPropertiesWithoutUndo();
                edit = new SerializedObject(o); edit.FindProperty("_hallwayProbability").floatValue = 0f;
                edit.FindProperty("_roundProbability").floatValue = 1f; edit.ApplyModifiedPropertiesWithoutUndo();
                int sampled = 0;
                for (int seed = 0; seed < 16; seed++)
                {
                    var layout = new ProceduralController(new ProceduralBehaviorState(), c,
                        new System.Random(ProceduralController.LayoutSeed(seed, 1))).Generate(seed, 1);
                    var blocks = new ProceduralOrganicShellPresenter().Build(layout, c, d);
                    foreach (var room in layout.OrganicRooms.Where(r => r.Shape == ProceduralRoomShape.Round))
                    {
                        sampled++;
                        Assert.That(blocks.Count(b => b.RoomId == room.RoomId && b.Rotation != Quaternion.identity), Is.GreaterThanOrEqualTo(28));
                        for (int step = 0; step <= 8; step++)
                        {
                            var point = room.RoundCenter + room.RoundFacing * step + Vector3.up;
                            Assert.That(blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Wall &&
                                new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) * (point - b.Center))), Is.False);
                        }
                        var side = new Vector3(room.RoundFacing.z, 0f, -room.RoundFacing.x);
                        Assert.That(ProceduralOrganicUtility.Clear(room, room.RoundCenter + side * 3f - room.RoundFacing * 3f), Is.False);
                        Assert.That(ProceduralSpawnUtility.Validate(layout, room.RoundCenter + side * 3f - room.RoundFacing * 3f,
                            c.DoorWidth, 1, out string reason), Is.False);
                        Assert.That(reason, Is.EqualTo("outside-round-enclosure"));
                    }
                }
                Assert.That(sampled, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(o); Object.DestroyImmediate(p); Object.DestroyImmediate(d); }
        }

        [Test]
        public void ForcedHallwaysTileExactlyAndDoNotWallOffTheirInternalSeams()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var o = ScriptableObject.CreateInstance<ProceduralOrganicConfig>();
            var p = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            var d = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            try
            {
                var edit = new SerializedObject(c); edit.FindProperty("_organic").objectReferenceValue = o;
                edit.FindProperty("_challenges").objectReferenceValue = p; edit.ApplyModifiedPropertiesWithoutUndo();
                edit = new SerializedObject(o); edit.FindProperty("_hallwayProbability").floatValue = 1f; edit.ApplyModifiedPropertiesWithoutUndo();
                var layout = new ProceduralController(new ProceduralBehaviorState(), c,
                    new System.Random(ProceduralController.LayoutSeed(19, 1))).Generate(19, 1);
                var blocks = new ProceduralOrganicShellPresenter().Build(layout, c, d);
                Assert.That(layout.OrganicRooms, Is.Not.Empty);
                foreach (var room in layout.OrganicRooms)
                {
                    Assert.That(room.Shape, Is.EqualTo(ProceduralRoomShape.Hallway));
                    foreach (var kind in new[] { ProceduralSurfaceKind.Floor, ProceduralSurfaceKind.Ceiling })
                    {
                        var pieces = blocks.Where(b => b.RoomId == room.RoomId && b.Kind == kind).ToArray();
                        Assert.That(pieces.Length, Is.EqualTo(room.Tiles.Count));
                        Assert.That(pieces.All(b => b.Size.x == 2f && b.Size.z == 2f), Is.True);
                    }
                    foreach (var tile in room.Tiles)
                    foreach (var step in new[] { Vector2Int.right, Vector2Int.up })
                    {
                        if (!room.Tiles.Contains(tile + step)) continue;
                        var point = ProceduralOrganicUtility.Center(layout, tile, 1f) + new Vector3(step.x, 0f, step.y);
                        Assert.That(blocks.Any(b => b.Kind == ProceduralSurfaceKind.Wall && new Bounds(b.Center, b.Size).Contains(point)), Is.False);
                    }
                }
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(o); Object.DestroyImmediate(p); Object.DestroyImmediate(d); }
        }
    }
}
