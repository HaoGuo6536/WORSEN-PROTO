// ============================================================================
// ProceduralPassageContractTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the plain-data contracts behind the three baseline Passage failures.
//   Separates room-seam overlap and optional socket admission from native raycast
//   ordering and navigation, which remain coordinator-only measurements.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify seam coverage and gap-interior probes in all cardinal directions.
//   - Verify clearance rejection, explicit destination identity and tile collapse.
//   - Verify exact configured collapse deadlines without native lifecycle callbacks.
// DEPENDENCIES:
//   - NUnit, Core and Domain.Procedural; reflection for managed config fields.
// USAGE NOTES:
//   Config shells have no native identity and are never passed to engine APIs.
//   Reduced layouts are explicit test data, not replacements for seed tests.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralPassageContractTests
    {
        [Test]
        public void EndpointCentresShareRoomSeamsButEveryTileHasAnExclusiveGapProbe(
            [Values(0, 1, 2, 3)] int direction, [Values(1, 2, 3)] int gaps, [Values(0f, 30000f)] float origin)
        {
            var facing = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back }[direction];
            var layout = Layout(facing, gaps, new Vector3(origin, 0f, origin), out var blocks);
            var driver = DriverConfig();
            var plan = new ProceduralPassagePresenter().Build(layout, 0, Config(), driver, blocks);
            var source = layout.Graph.Rooms[0]; var pocket = layout.Graph.Rooms[1];
            // PLAN-025 §3 C7 requires support across the gap, not exclusive support
            // inside either room. The default final tile straddles the pocket seam.
            Assert.That(pocket.ContainsXZ(plan.TilePositions.Last()), Is.True);
            Assert.That(plan.Tiles.Count, Is.EqualTo(Mathf.CeilToInt((12f * gaps + .6f) / 2f)));
            foreach (var tile in plan.Tiles)
            {
                var probe = GapProbe(tile, facing, source, pocket);
                Assert.That(source.ContainsXZ(probe) || pocket.ContainsXZ(probe), Is.False);
                Assert.That(probe.y, Is.Zero.Within(.0001f));
                var local = probe - tile.Center;
                Assert.That(Math.Abs(local.x), Is.LessThanOrEqualTo(tile.Size.x * .5f + .001f));
                Assert.That(Math.Abs(local.z), Is.LessThanOrEqualTo(tile.Size.z * .5f + .001f));
                Assert.That(tile.HasCollision, Is.True);
            }
            Assert.That(plan.Walls.Select(w => w.Original.RoomId).Distinct(), Is.EquivalentTo(new[] { 1, 2 }));
            var state = new ProceduralPassageDriverState { Plan = plan };
            var presenter = new ProceduralPassagePresenter();
            Assert.That(presenter.Advance(state, 3.99f, driver), Is.Empty);
            Assert.That(presenter.Advance(state, .01f, driver), Is.EqualTo(new[] { 0 }));
            Assert.That(presenter.Advance(state, 100f, driver), Is.EqualTo(Enumerable.Range(1, plan.Tiles.Count - 1)));
            Assert.That(presenter.Advance(state, 100f, driver), Is.Empty);
        }

        [TestCase(4f, .4f)] [TestCase(2f, .5f)] [TestCase(0f, .25f)]
        public void ExactConfiguredDeadlinesCollapseOnceForSingleAndSplitTicks(float delay, float interval)
        {
            var layout = Layout(Vector3.right, 1, Vector3.zero, out var blocks);
            var driver = DriverConfig();
            Set(driver, "_passageFirstTileDelay", delay); Set(driver, "_passageTileInterval", interval);
            var presenter = new ProceduralPassagePresenter();
            var plan = presenter.Build(layout, 0, Config(), driver, blocks);
            var single = new ProceduralPassageDriverState { Plan = plan };
            var split = new ProceduralPassageDriverState { Plan = plan };
            // Binary-exact split reaches the deadline exactly, not just within the
            // tolerance exercised by the separate 3.99f + .01f regression above.
            if (delay > 0f) Assert.That(presenter.Advance(split, delay - .125f, driver), Is.Empty);
            Assert.That(presenter.Advance(single, delay, driver), Is.EqualTo(new[] { 0 }));
            Assert.That(presenter.Advance(split, delay > 0f ? .125f : 0f, driver), Is.EqualTo(new[] { 0 }));
            foreach (var state in new[] { single, split })
            {
                Assert.That(state.Elapsed, Is.EqualTo((double)delay));
                Assert.That(state.CollapsedCount, Is.EqualTo(1));
                Assert.That(presenter.Advance(state, 0f, driver), Is.Empty);
                Assert.That(presenter.Advance(state, interval, driver), Is.EqualTo(new[] { 1 }));
                Assert.That(state.Elapsed, Is.EqualTo((double)delay + interval));
                Assert.That(presenter.Advance(state, 0f, driver), Is.Empty);
            }
        }

        [Test]
        public void RaisedAnchorClearanceMayRejectEverySocketWithoutRemovingThePocket()
        {
            var layout = Layout(Vector3.right, 1, new Vector3(0f, 0f, -12f), out var blocks);
            var config = Config();
            var presenter = new ProceduralShrineSitePresenter();
            Assert.That(presenter.Build(layout, config, blocks).Count(s => s.GapEdge), Is.EqualTo(3));
            // Reduced Castle-library geometry: clearance is deliberately horizontal,
            // including upper-deck rewards. PLAN-026 §3 C7 makes pockets optional;
            // PLAN-025 §5 constrains admitted placement, not a socket on every seed.
            var anchors = new[] {
                new LevelAnchor(10408, 1, CakeAnchorType.Precision, new Vector3(3.45f, 2.45f, -13.5f)),
                new LevelAnchor(10410, 1, CakeAnchorType.Risk, new Vector3(3.6f, 2.45f, -8.55f)) };
            Property(layout, "Graph", new LevelGraph(layout.Graph.Rooms, layout.Graph.Edges, anchors, 1, layout.Graph.ExitPosition));
            foreach (float lateral in new[] { 0f, -2.4f, 2.4f })
            {
                var position = new Vector3(4.5f, 0f, -12f + lateral);
                Assert.That(new ProceduralPassagePresenter().Destination(layout, 1, position, Vector3.right), Is.EqualTo(2));
                Assert.That(anchors.Min(a => new Vector2(a.Position.x - position.x, a.Position.z - position.z).magnitude),
                    Is.LessThan(config.ShrineSiteClearance));
            }
            Assert.That(presenter.Build(layout, config, blocks).Any(s => s.GapEdge), Is.False);
            Assert.That(layout.Graph.Rooms[1].Pocket, Is.True);
        }

        [Test]
        public void MissingDestinationCannotAdvertiseAPassage()
        {
            Assert.That(new ProceduralShrineSite(1, Vector3.zero, true, Vector3.forward).GapEdge, Is.False);
            var identified = new ProceduralShrineSite(1, Vector3.zero, true, Vector3.forward, 3);
            Assert.That(identified.GapEdge, Is.True); Assert.That(identified.DestinationPocketRoomId, Is.EqualTo(3));
        }

        internal static Vector3 GapProbe(ProceduralBlock tile, Vector3 facing, LevelRoom source, LevelRoom pocket)
        {
            bool x = facing.x != 0f;
            float start = (x ? source.Size.x : source.Size.z) * .5f;
            float end = Vector3.Dot(pocket.Center - source.Center, facing) - (x ? pocket.Size.x : pocket.Size.z) * .5f;
            float center = Vector3.Dot(tile.Center - source.Center, facing);
            float half = (x ? tile.Size.x : tile.Size.z) * .5f;
            float from = Math.Max(start, center - half), to = Math.Min(end, center + half);
            Assert.That(to, Is.GreaterThan(from), "Each tested tile must cover a nonzero portion of the gap.");
            return tile.Center + Vector3.up * (tile.Size.y * .5f) + facing * ((from + to) * .5f - center);
        }

        private static ProceduralLayout Layout(Vector3 facing, int gaps, Vector3 origin, out IReadOnlyList<ProceduralBlock> blocks)
        {
            var source = new LevelRoom(1, origin + Vector3.up * 2f, new Vector3(12f, 4f, 12f));
            var pocket = new LevelRoom(2, source.Center + facing * (12f * (gaps + 1)), source.Size, pocket: true);
            var site = new ProceduralShrineSite(1, origin + facing * 4.5f, true, facing, 2);
            var edge = origin + facing * 6f; var landing = edge + facing * (12f * gaps);
            var layout = new ProceduralLayout();
            Property(layout, "Graph", new LevelGraph(new[] { source, pocket }, Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, origin));
            Property(layout, "CellSize", 12f); Property(layout, "Origin", new Vector2(origin.x, origin.z));
            Property(layout, "Modules", new[] { new ProceduralRoomModule(1, ProceduralModuleKind.TorchGallery, true),
                new ProceduralRoomModule(2, ProceduralModuleKind.TorchGallery, true, pocketId: 1) });
            Property(layout, "GapCells", Enumerable.Range(1, gaps).Select(i => new Vector2Int((int)facing.x * i, (int)facing.z * i)).ToArray());
            Property(layout, "GapSites", new[] { new ProceduralGapSite(1, 1, edge, landing) });
            Property(layout, "ShrineSites", new[] { site }); Property(layout, "Doors", Array.Empty<ProceduralDoorPlan>());
            Property(layout, "HunterSpawnPositions", Array.Empty<Vector3>()); Property(layout, "PlayerSpawnPosition", origin);
            Property(layout, "PocketAnchors", new[] { new LevelAnchor(1, 2, CakeAnchorType.Detour, pocket.Center - Vector3.up * 2f) });
            var wallSize = facing.x != 0f ? new Vector3(.3f, 4f, 12f) : new Vector3(12f, 4f, .3f);
            blocks = new[] {
                new ProceduralBlock(1, ProceduralSurfaceKind.Wall, edge + Vector3.up * 2f, wallSize),
                new ProceduralBlock(2, ProceduralSurfaceKind.Wall, landing + Vector3.up * 2f, wallSize),
                new ProceduralBlock(1, ProceduralSurfaceKind.Floor, origin - Vector3.up * .15f, new Vector3(12f, .3f, 12f)),
                new ProceduralBlock(2, ProceduralSurfaceKind.Floor, pocket.Center - Vector3.up * 2.15f, new Vector3(12f, .3f, 12f)) };
            return layout;
        }

        private static ProceduralConfig Config()
        {
            var c = (ProceduralConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralConfig));
            Set(c, "_doorHeight", 2.8f); Set(c, "_roomSize", 12f);
            Set(c, "_shrineSiteEnvelope", new Vector3(.6f, 1.8f, .6f)); Set(c, "_shrineSiteInset", 1.5f);
            Set(c, "_shrineSiteClearance", 2f); Set(c, "_shrineSiteLateralFraction", .2f);
            return c;
        }
        private static ProceduralDriverConfig DriverConfig()
        {
            var driver = (ProceduralDriverConfig)FormatterServices.GetUninitializedObject(typeof(ProceduralDriverConfig));
            Set(driver, "_wallThickness", .3f); Set(driver, "_floorThickness", .3f); Set(driver, "_navSampleRadius", .75f);
            Set(driver, "_passageTileLength", 2f); Set(driver, "_passageWidth", 2.4f); Set(driver, "_passageFirstTileDelay", 4f);
            Set(driver, "_passageTileInterval", .4f); Set(driver, "_passageFallAcceleration", 18f); Set(driver, "_passageFallDuration", 1.5f);
            return driver;
        }
        private static void Property(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
