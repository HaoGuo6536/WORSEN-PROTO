// ============================================================================
// ProceduralPuzzleLayoutPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies all four puzzle modules use dedicated authored rooms, while legacy
//   organic floors no longer invent a primitive lane or golden-cake reward.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check authored lanes, round gates, determinism and goal/required separation.
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
        public void OrganicFloorsDoNotInventPuzzleRoomsOrRewards()
        {
            var c = ScriptableObject.CreateInstance<ProceduralConfig>();
            var d = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            var p = ScriptableObject.CreateInstance<ProceduralChallengeConfig>();
            try
            {
                var presenter = new ProceduralPuzzleLayoutPresenter();
                for (int seed = 0; seed < 32; seed++)
                {
                    foreach (int round in new[] { 2, 3, 8 })
                    {
                        var layout = new ProceduralController(new ProceduralBehaviorState(), c,
                            new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);
                        var blocks = new ProceduralGeometryPresenter().Build(layout, c, d);
                        var original = layout.Graph;
                        var plans = presenter.Build(layout, p, blocks, new System.Random(seed));
                        Assert.That(layout.Graph, Is.SameAs(original));
                        Assert.That(plans, Is.Empty);
                        Assert.That(presenter.Manifest(plans), Is.EqualTo(presenter.Manifest(presenter.Build(layout, p, blocks, new System.Random(seed)))));
                        ProceduralFootprintUtility.Validate(layout); ProceduralStoreyUtility.Validate(layout, c);
                    }
                }

            }
            finally { Object.DestroyImmediate(c); Object.DestroyImmediate(d); Object.DestroyImmediate(p); }
        }

        [Test]
        public void AuthoredRoomSupportsAllFourModulesWithoutAddingGoldenCake()
        {
            var c = ProceduralBiomeLayoutTests.Config(); var p = c.Challenges;
            var controller = new ProceduralTemplateController(c, new System.Random(ProceduralController.LayoutSeed(0, 8)));
            Assert.That(controller.TryGenerate(0, 8, ProceduralBiomeUtilityTests.Theme("castle"), false, 1,
                out var layout, out string reason, 0, 2), Is.True, reason);
            var blocks = new ProceduralTemplateGeometryPresenter().Build(layout, c, ProceduralTemplateSeamPresenterTests.Driver());
            var presenter = new ProceduralPuzzleLayoutPresenter(); var kinds = new HashSet<ProceduralPuzzleKind>();
            var original = layout.Graph;
            for (int seed = 0; seed < 32; seed++)
            {
                var plans = presenter.Build(layout, p, blocks, new System.Random(seed));
                Assert.That(plans, Is.Not.Empty);
                Assert.That(presenter.Manifest(plans), Is.EqualTo(presenter.Manifest(presenter.Build(layout, p, blocks, new System.Random(seed)))));
                Assert.That(layout.Graph, Is.SameAs(original));
                foreach (var plan in plans)
                {
                    kinds.Add(plan.Kind);
                    var room = layout.TemplateRooms.Single(r => r.RoomId == plan.RoomId);
                    Assert.That(room.Template.Kind, Is.EqualTo("puzzle"));
                    Assert.That(plan.Origin, Is.EqualTo(ProceduralTemplateUtility.Point(room, room.Template.PuzzleSockets.Origin, layout.Origin)));
                    Assert.That(layout.Graph.Anchors.Any(a => a.Id == plan.Reward.Id), Is.False);
                    Assert.That(plan.RoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                    Assert.That(presenter.Tile(plan, p, plan.Reward.Position), Is.EqualTo(3));
                    Assert.That(presenter.Blocks(plan, p).Count, Is.EqualTo(plan.Kind == ProceduralPuzzleKind.TimedVaults ? 8 : 5));
                }
            }
            Assert.That(kinds.Count, Is.EqualTo(4));
            ProceduralTemplateSeamPresenterTests.Set(layout, nameof(ProceduralLayout.RoundIndex), 2);
            Assert.That(presenter.Build(layout, p, blocks, new System.Random(0)), Is.Empty);
        }
    }
}
