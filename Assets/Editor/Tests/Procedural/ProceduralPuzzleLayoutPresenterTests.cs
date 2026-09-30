// ============================================================================
// ProceduralPuzzleLayoutPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies cage plans are optional additions rather than graph gates.
//   The seed sample exercises every module on existing multi-cell, gapped and
//   multi-storey geometry while retaining the required sweep unchanged.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check round gates, deterministic placement and reward/required separation.
// DEPENDENCIES:
//   - NUnit, Core and Domain.Procedural; temporary Unity configs.
// USAGE NOTES:
//   Native navigation and trigger callbacks are coordinator-owned Unity gates.
// ============================================================================
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralPuzzleLayoutPresenterTests
    {
        [Test]
        public void SeedSampleHasFourOptionalModulesWithoutChangingRequiredGraph()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var d = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var p = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            try
            {
                var presenter = new ProceduralPuzzleLayoutPresenter(); var kinds = new HashSet<ProceduralPuzzleKind>();
                for (int seed = 0; seed < 32; seed++)
                {
                    foreach (int round in new[] { 3, 4, 8 })
                    {
                        var layout = new ProceduralController(new ProceduralBehaviorState(), c,
                            new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
                        var blocks = new ProceduralGeometryPresenter().Build(layout, c, d);
                        var original = layout.Graph;
                        var plans = presenter.Build(layout, p, blocks, new System.Random(seed));
                        Assert.That(layout.Graph, Is.SameAs(original));
                        if (round == 3) { Assert.That(plans, Is.Empty); continue; }
                        Assert.That(plans.Count, Is.EqualTo(1));
                        Assert.That(presenter.Manifest(plans), Is.EqualTo(presenter.Manifest(presenter.Build(layout, p, blocks, new System.Random(seed)))));
                        var plan = plans[0]; kinds.Add(plan.Kind);
                        Assert.That(layout.Graph.Anchors.Any(a => a.Id == plan.Reward.Id), Is.False);
                        Assert.That(plan.RoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                        Assert.That(presenter.Tile(plan, p, plan.Reward.Position), Is.EqualTo(3));
                        Assert.That(presenter.Blocks(plan, p).Count, Is.EqualTo(plan.Kind == ProceduralPuzzleKind.TimedVaults ? 8 : 5));
                        ProceduralFootprintUtility.Validate(layout); ProceduralStoreyUtility.Validate(layout, c);
                    }
                }
                Assert.That(kinds.Count, Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(d); Object.DestroyImmediate(p); }
        }
    }
}
