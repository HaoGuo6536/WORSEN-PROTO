// ============================================================================
// ProceduralGimmickUtilityTests.cs
// ============================================================================
// PURPOSE:
//   Verifies shared late-room pacing instead of independent obstacle quotas.
//   A fully wired config exercises traversal, optional apertures, freeze rooms
//   and puzzle selection together without requiring native navigation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check curve endpoints, saturation, determinism and combined room counts.
// DEPENDENCIES:
//   - NUnit, UnityEditor serialization, Core and Domain.Procedural.
// USAGE NOTES:
//   Native traversal and final puzzle admission remain coordinator-owned gates.
// ============================================================================
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralGimmickUtilityTests
    {
        [Test]
        public void BasicVaultsAndStoreysDoNotSpendZeroEarlyGimmickBudget()
        {
            var c = ProceduralTemplateSeamPresenterTests.Empty<ProceduralChallengeConfig>();
            ProceduralTemplateSeamPresenterTests.Field(c, "_gimmickFirstRound", 3);
            ProceduralTemplateSeamPresenterTests.Field(c, "_gimmickFullRound", 8);
            ProceduralTemplateSeamPresenterTests.Field(c, "_gimmickInitialBudget", 1);
            ProceduralTemplateSeamPresenterTests.Field(c, "_gimmickMaximumBudget", 3);
            var modules = new[] { new ProceduralRoomModule(1, ProceduralModuleKind.VaultPartition, true, new[] { Vector2Int.zero }),
                new ProceduralRoomModule(2, ProceduralModuleKind.BrokenGallery, true, new[] { Vector2Int.right }) };
            var reserved = ProceduralGimmickUtility.Reserve(modules, c, 1, new System.Random(1));
            Assert.That(reserved[0].TraversalObstacles, Is.True); Assert.That(reserved[1].TraversalObstacles, Is.False);
            var layout = new ProceduralLayout();
            ProceduralTemplateSeamPresenterTests.Set(layout, "Modules", reserved);
            ProceduralTemplateSeamPresenterTests.Set(layout, "Storeys", new[] { new ProceduralStoreyPlan(2, Vector3.zero, 3.2f, ProceduralVerticalKind.Balcony) });
            ProceduralTemplateSeamPresenterTests.Set(layout, "Doors", new[] { new ProceduralDoorPlan(1, 2, Vector3.zero, true, TraversalSurfaceKind.Vault) });
            Assert.That(ProceduralGimmickUtility.Rooms(layout), Is.Empty);
        }
        [Test]
        public void LinearCurveStartsAtThreeAndSaturatesAtEight()
        {
            var c = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            try
            {
                Assert.That(Enumerable.Range(1, 10).Select(r => ProceduralGimmickUtility.Budget(c, r)),
                    Is.EqualTo(new[] { 0, 0, 1, 1, 1, 2, 2, 3, 3, 3 }));
                Assert.That(ProceduralGimmickUtility.Budget(c, int.MaxValue), Is.EqualTo(3));
                Assert.Throws<ArgumentOutOfRangeException>(() => ProceduralGimmickUtility.Budget(c, 0));
                var edit = new SerializedObject(c); edit.FindProperty("_gimmickFirstRound").intValue = 2;
                edit.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentException>(() => ProceduralGimmickUtility.Budget(c, 1));
            }
            finally { UnityEngine.Object.DestroyImmediate(c); }
        }

        [Test]
        public void SeedSweepSharesTheBudgetAcrossEveryGimmickFamily()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var p = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            var d = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var t = ScriptableObject.CreateInstance<ProceduralThemeConfig>();
            try
            {
                var edit = new SerializedObject(c); edit.FindProperty("_challenges").objectReferenceValue = p;
                edit.FindProperty("_themes").objectReferenceValue = t; edit.ApplyModifiedPropertiesWithoutUndo();
                int lateMaximum = 0;
                for (int seed = 0; seed < 32; seed++)
                foreach (int round in new[] { 1, 2, 3, 6, 8, 12 })
                {
                    var layout = new ProceduralController(new ProceduralBehaviorState(), c,
                        new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
                    var blocks = new ProceduralGeometryPresenter().Build(layout, c, d);
                    var puzzles = new ProceduralPuzzleLayoutPresenter().Build(layout, p, blocks, new System.Random(seed));
                    var occupied = ProceduralGimmickUtility.Rooms(layout).Concat(puzzles.Select(v => v.RoomId)).Distinct().ToArray();
                    Assert.That(occupied.Length, Is.LessThanOrEqualTo(ProceduralGimmickUtility.Budget(p, round)));
                    Assert.That(occupied, Has.No.Member(layout.Graph.ExitRoomId));
                    if (round <= 2)
                    {
                        Assert.That(occupied, Is.Empty); Assert.That(layout.Storeys, Is.Empty);
                        Assert.That(blocks.Where(b => b.TraversalKind != TraversalSurfaceKind.None)
                            .All(b => b.TraversalKind == TraversalSurfaceKind.Vault), Is.True,
                            "Basic vaults are allowed before gimmicks.");
                        Assert.That(layout.Doors.Where(v => v.IsOptional).All(v => v.TraversalKind == TraversalSurfaceKind.Vault), Is.True);
                    }
                    if (round >= 8) lateMaximum = Math.Max(lateMaximum, occupied.Length);
                    ProceduralFootprintUtility.Validate(layout); ProceduralStoreyUtility.Validate(layout, c);
                }
                Assert.That(lateMaximum, Is.EqualTo(3), "The late sample must exercise the full budget, not just the bound.");
            }
            finally
            { UnityEngine.Object.DestroyImmediate(c); UnityEngine.Object.DestroyImmediate(p);
                UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(t); }
        }
    }
}
