// ============================================================================
// ProceduralPassagePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Passage identity, tile coverage, wall subtraction and collapse timing
//   independently of the engine boundary. Seeded generated floors exercise all
//   cardinal directions and ensure a crossing cannot skip a missing pocket.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Check deterministic contiguous tiles and both collision/visual apertures.
//   - Check pocket anchor lining, invalid destinations and ordered collapse deadlines.
// DEPENDENCIES:
//   - Domain.Procedural, Core, NUnit and temporary Unity configuration instances.
// USAGE NOTES:
//   Pure calculations only after fixture construction; native behavior has a separate fixture.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralPassagePresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        private readonly ProceduralPassagePresenter _presenter = new ProceduralPassagePresenter();
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            Set(_config, "_gapProbability", 1f); Set(_config, "_pocketProbability", 1f);
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driver); }
        private static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Property(object target, string property, object value)
            => target.GetType().GetProperty(property).SetValue(target, value);
        private ProceduralLayout Layout(int seed, out System.Collections.Generic.IReadOnlyList<ProceduralBlock> blocks)
        {
            var layout = new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(seed, 12))).Generate(seed, 12);
            blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
            Property(layout, nameof(layout.ShrineSites), new ProceduralShrineSitePresenter().Build(layout, _config, blocks));
            return layout;
        }
        private ProceduralPassagePlan Plan()
        {
            var layout = Layout(19, out var blocks);
            int index = layout.ShrineSites.ToList().FindIndex(s => s.GapEdge);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            return _presenter.Build(layout, index, _config, _driver, blocks);
        }

        [TestCase(false)] [TestCase(true)]
        public void EveryGapCrossingHasDeterministicContiguousTilesAndTwoOpenApertures(bool castle)
        {
            Set(_config, "_castleModules", castle);
            for (int seed = 0; seed < 8; seed++)
            {
                var layout = Layout(seed, out var blocks);
                Assert.That(layout.ShrineSites.Any(s => s.GapEdge), Is.True);
                for (int index = 0; index < layout.ShrineSites.Count; index++)
                {
                    var site = layout.ShrineSites[index]; if (!site.GapEdge) continue;
                    var plan = _presenter.Build(layout, index, _config, _driver, blocks);
                    var again = _presenter.Build(layout, index, _config, _driver, blocks);
                    Assert.That(plan.PocketRoomId, Is.EqualTo(site.DestinationPocketRoomId));
                    Assert.That(plan.Tiles, Is.EqualTo(again.Tiles)); Assert.That(plan.TilePositions, Is.EqualTo(again.TilePositions));
                    Assert.That(plan.Tiles, Is.Not.Empty); Assert.That(plan.LinedAnchors, Is.Not.Empty);
                    int pocket = layout.Modules.Single(m => m.RoomId == plan.PocketRoomId).PocketId;
                    Assert.That(plan.LinedAnchors, Is.EqualTo(layout.PocketAnchors.Where(a =>
                        layout.Modules.Single(m => m.RoomId == a.RoomId).PocketId == pocket).OrderBy(a => a.Id)));
                    Assert.That(plan.Walls.Any(w => w.Original.RoomId == site.RoomId && w.Original.HasCollision), Is.True);
                    Assert.That(plan.Walls.Any(w => w.Original.RoomId == plan.PocketRoomId && w.Original.HasCollision), Is.True);
                    for (int tile = 0; tile < plan.Tiles.Count; tile++)
                    {
                        var b = plan.Tiles[tile];
                        Assert.That(b.HasCollision, Is.True); Assert.That(new ProceduralNavigationPresenter().Area(b), Is.Zero);
                        Assert.That(plan.TilePositions[tile].y, Is.Zero.Within(.0001f));
                        float length = site.Facing.x == 0f ? b.Size.z : b.Size.x;
                        Assert.That(length, Is.InRange(.0001f, _driver.PassageTileLength));
                        if (tile == 0) continue;
                        var previous = plan.Tiles[tile - 1];
                        float priorLength = site.Facing.x == 0f ? previous.Size.z : previous.Size.x;
                        Assert.That(Vector3.Dot(b.Center - previous.Center, site.Facing), Is.EqualTo((length + priorLength) * .5f).Within(.001f));
                    }
                    foreach (var wall in plan.Walls)
                    {
                        var point = wall.Original.Center;
                        float along = Vector3.Dot(point - site.Position, site.Facing);
                        var opening = site.Position + site.Facing * along + Vector3.up * (_config.DoorHeight * .5f);
                        foreach (var piece in wall.Pieces)
                            Assert.That(new Bounds(piece.Center, piece.Size).Contains(opening), Is.False);
                        foreach (var piece in wall.Pieces)
                        { Assert.That(piece.Role, Is.EqualTo(wall.Original.Role)); Assert.That(piece.Size.x * piece.Size.y * piece.Size.z, Is.GreaterThan(0f)); }
                    }
                }
            }
        }

        [Test]
        public void MissingPocketAndStaleDestinationCannotBecomeGapSites()
        {
            var layout = Layout(19, out var blocks);
            var site = layout.ShrineSites.First(s => s.GapEdge);
            Assert.That(new ProceduralShrineSite(site.RoomId, site.Position, true, site.Facing).GapEdge, Is.False);
            Property(layout, nameof(layout.ShrineSites), new[] {
                new ProceduralShrineSite(site.RoomId, site.Position, true, site.Facing, layout.Graph.ExitRoomId) });
            Assert.Throws<ArgumentException>(() => _presenter.Build(layout, 0, _config, _driver, blocks));
            Property(layout, nameof(layout.GapCells), Array.Empty<Vector2Int>());
            Assert.That(_presenter.Destination(layout, site.RoomId, site.Position, site.Facing), Is.Zero);
            Assert.That(new ProceduralShrineSitePresenter().Build(layout, _config, blocks).Any(s => s.GapEdge), Is.False);
        }

        [Test]
        public void FutureGoldenCountDeduplicatesAllSitesLeadingToTheSamePocket()
        {
            var layout = Layout(19, out var blocks);
            var site = layout.ShrineSites.First(s => s.GapEdge);
            Property(layout, nameof(layout.ShrineSites), new[] { site, site });
            var anchors = _presenter.Build(layout, 0, _config, _driver, blocks).LinedAnchors;
            Assert.That(_presenter.FutureGoldenAnchorCount(layout, _config, _driver, blocks), Is.EqualTo(anchors.Select(a => a.Id).Distinct().Count()));
            Assert.That(anchors, Is.Not.Empty);
        }

        [Test]
        public void CollapseStartsAtFourSecondsThenMovesOutwardEveryPointFourSecondsWithoutRepeats()
        {
            var state = new ProceduralPassageDriverState { Plan = Plan() };
            Assert.That(_presenter.Advance(state, 3.99f, _driver), Is.Empty);
            Assert.That(_presenter.Advance(state, .01f, _driver), Is.EqualTo(new[] { 0 }));
            Assert.That(_presenter.Advance(state, .399f, _driver), Is.Empty);
            Assert.That(_presenter.Advance(state, .001f, _driver), Is.EqualTo(new[] { 1 }));
            Assert.That(_presenter.Advance(state, .8f, _driver), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(_presenter.FallOffset(state.Elapsed, 0, _driver).y, Is.LessThan(0f));
            Assert.That(_presenter.Advance(state, 100f, _driver), Is.EqualTo(Enumerable.Range(4, state.Plan.Tiles.Count - 4)));
            Assert.That(_presenter.Advance(state, 100f, _driver), Is.Empty);
            var largeTick = new ProceduralPassageDriverState { Plan = state.Plan };
            Assert.That(_presenter.Advance(largeTick, 200f, _driver), Is.EqualTo(Enumerable.Range(0, state.Plan.Tiles.Count)));
            Assert.That(largeTick.CollapsedCount, Is.EqualTo(state.CollapsedCount));
        }

        [TestCase("_passageTileLength", 0f)] [TestCase("_passageWidth", float.NaN)]
        [TestCase("_passageFirstTileDelay", -1f)] [TestCase("_passageTileInterval", 0f)]
        [TestCase("_passageFallAcceleration", float.PositiveInfinity)] [TestCase("_passageFallDuration", 0f)]
        public void InvalidDesignerValuesFailClosed(string field, float value)
        { Set(_driver, field, value); Assert.Throws<ArgumentException>(() => Plan()); }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaTimeDoesNotAdvance(float delta)
        {
            var state = new ProceduralPassageDriverState { Plan = Plan() };
            Assert.Throws<ArgumentException>(() => _presenter.Advance(state, delta, _driver));
            Assert.That(state.Elapsed, Is.Zero); Assert.That(state.CollapsedCount, Is.Zero);
        }
    }
}
