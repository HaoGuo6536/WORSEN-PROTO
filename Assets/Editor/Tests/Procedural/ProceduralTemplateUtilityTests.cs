// ============================================================================
// ProceduralTemplateUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks corner-origin transforms independently of seeded layout selection.
//   Exact quarter turns and merged cell coverage keep gameplay sockets aligned
//   with the geometry produced later by the driver.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify rotated cell centers, anchor clearance and merged footprint area.
// DEPENDENCIES:
//   - NUnit, own test data and Domain.Procedural.
// USAGE NOTES:
//   Pure value tests; no Unity objects.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralTemplateUtilityTests
    {
        [Test] public void QuarterTurnsKeepCellAndMetreCentersAligned()
        {
            for (int turn = 0; turn < 4; turn++)
            {
                var c = ProceduralTemplateUtility.Cell(new Vector2Int(2, 3), turn);
                Assert.That(new Vector3(c.x * 2 + 1, 0f, c.y * 2 + 1),
                    Is.EqualTo(ProceduralTemplateUtility.Rotate(new Vector3(5, 0, 7), turn)));
            }
        }
        [Test] public void ConcaveNotchesAndInternalSeamsUseActualBoundary()
        {
            var room = ProceduralTemplateTestData.Catalogue().Templates[4];
            Assert.That(ProceduralTemplateUtility.Inside(room, new Vector3(6f, 0f, 6f)), Is.False);
            Assert.That(ProceduralTemplateUtility.Inside(room, new Vector3(2f, 0f, 2f), .6f), Is.True);
            Assert.That(ProceduralTemplateUtility.Volumes(room.Footprint, Vector2.zero, room.Height).Sum(b => b.size.x * b.size.z),
                Is.EqualTo(room.Footprint.Length * 4f));
        }
    }
}
