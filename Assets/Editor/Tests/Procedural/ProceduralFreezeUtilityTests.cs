// ============================================================================
// ProceduralFreezeUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Checks threshold staging against the same graph and first-contact policy used
//   by admission. This prevents an authored scare from bypassing initial safety
//   or placing its next cake inside the optional puzzle reward set.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check round gating, manifest identity, door sockets and validated spawn bias.
// DEPENDENCIES:
//   - NUnit, UnityEditor, Core and Domain.Procedural.
// USAGE NOTES:
//   Audio occlusion and native doorway navigation still require live verification.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralFreezeUtilityTests
    {
        [Test]
        public void FreezeRoomsRespectRoundAndFirstContactAcrossThirtyTwoSeeds()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var f = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            try
            {
                var settings = new SerializedObject(c); settings.FindProperty("_challenges").objectReferenceValue = f;
                settings.ApplyModifiedPropertiesWithoutUndo(); int count = 0;
                for (int seed = 0; seed < 32; seed++)
                {
                    Assert.That(Generate(2).FreezeRooms, Is.Empty);
                    var layout = Generate(4);
                    ProceduralFootprintUtility.Validate(layout);
                    foreach (var plan in layout.FreezeRooms)
                    {
                        count++;
                        Assert.That(layout.Manifest, Does.Contain("|Freeze:"));
                        Assert.That(plan.BehindRoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                        Assert.That(ProceduralSpawnUtility.Validate(layout, plan.Hunter, c.DoorWidth, layout.MinimumHunterSpawnRooms, out _), Is.True);
                        Assert.That(layout.HunterSpawnPositions[0], Is.EqualTo(plan.Hunter));
                        var anchor = layout.Graph.Anchors.Single(a => a.Id == plan.AnchorId);
                        Assert.That(Vector3.Distance(anchor.Position, layout.Doors[plan.DoorIndex].Center), Is.LessThan(c.CandidatePerimeterInset + c.AnchorHeight));
                    }
                    Assert.That(layout.Manifest, Is.EqualTo(Generate(4).Manifest));
                    ProceduralLayout Generate(int round) => new ProceduralController(new ProceduralBehaviorState(), c,
                        new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
                }
                Assert.That(count, Is.GreaterThan(0), "The sample must actually exercise authored freezes.");
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(f); }
        }
    }
}
