// ============================================================================
// ProceduralShrineSitePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks shrine candidate production against the generated collision shell.
//   Seed samples cover flat/castle floors, pockets, storeys and reproducible
//   Passage sockets without selecting a shrine kind. A separate admission case
//   verifies the Manager's published pool against native navigation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify deterministic candidates, support, separation and marked gap edges.
//   - Reject unsupported/blocked sockets and invalid designer dimensions.
//   - Keep the candidate manifest culture-independent and manager admission gated.
// DEPENDENCIES:
//   - Domain.Procedural, Core, NUnit and temporary Unity configuration objects.
// USAGE NOTES:
//   Pure seed samples do not establish a bake. The admission case requires Unity
//   and is run by the coordinator; this worker only compiles the entire fixture.
// ============================================================================
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralShrineSitePresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driver); }
        private void Set(string field, object value) => typeof(ProceduralConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value);
        private ProceduralLayout Generate(int seed, int round) => new ProceduralController(new ProceduralBehaviorState(), _config,
            new System.Random(ProceduralController.LayoutSeed(seed, round))).Generate(seed, round);

        [TestCase(false)] [TestCase(true)]
        public void SeedSampleProducesEnoughSeparatedSupportedDeterministicSites(bool castle)
        {
            Set("_castleModules", castle); Set("_gapProbability", 1f); Set("_pocketProbability", 1f);
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 0f); Set("_threeCellWeight", 1f);
            Set("_lShapeWeight", 1f); Set("_storeyProbability", 1f);
            var presenter = new ProceduralShrineSitePresenter();
            foreach (int round in new[] { 1, 3, 12 })
            for (int seed = 0; seed < 12; seed++)
            {
                var layout = Generate(seed, round);
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                var plans = new ProceduralInteractablePresenter().Build(layout, _config, _driver, blocks, new System.Random(seed));
                typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.Interactables)).SetValue(layout, plans);
                var sites = presenter.Build(layout, _config, blocks);
                var repeated = Generate(seed, round);
                typeof(ProceduralLayout).GetProperty(nameof(ProceduralLayout.Interactables)).SetValue(repeated, plans);
                Assert.That(sites, Is.EqualTo(presenter.Build(repeated, _config, blocks)));
                Assert.That(sites.Count(s => !s.GapEdge), Is.GreaterThanOrEqualTo(4));
                Assert.That(sites.Select(s => s.Position).Distinct().Count(), Is.EqualTo(sites.Count));
                if (round >= _config.GapStartRound) Assert.That(sites.Any(s => s.GapEdge), Is.True, "seed " + seed);
                var reachable = LevelGraphUtility.DistancesTo(layout.Graph, layout.Graph.ExitRoomId, TraversalAccess.Player);
                foreach (var site in sites)
                {
                    var room = layout.Graph.Rooms.Single(r => r.Id == site.RoomId);
                    Assert.That(room.Pocket, Is.False); Assert.That(room.ContainsXZ(site.Position), Is.True);
                    Assert.That(reachable[site.RoomId], Is.GreaterThanOrEqualTo(0));
                    Assert.That(site.Position.y, Is.Zero); Assert.That(site.Facing.y, Is.Zero);
                    Assert.That(site.Facing.magnitude, Is.EqualTo(1f).Within(.001f));
                    foreach (var point in layout.Graph.Anchors.Select(a => a.Position).Concat(layout.HunterSpawnPositions)
                        .Concat(new[] { layout.PlayerSpawnPosition, layout.Graph.ExitPosition }))
                        Assert.That(DistanceXZ(point, site.Position), Is.GreaterThanOrEqualTo(_config.ShrineSiteClearance));
                    foreach (var other in sites.Where(s => !s.Position.Equals(site.Position)))
                        Assert.That(DistanceXZ(other.Position, site.Position), Is.GreaterThanOrEqualTo(_config.ShrineSiteClearance));
                    var body = new Bounds(site.Position + Vector3.up * (_config.ShrineSiteEnvelope.y * .5f), _config.ShrineSiteEnvelope);
                    Assert.That(blocks.Where(b => b.HasCollision).Any(b => Overlaps(WorldBounds(b), body)), Is.False);
                    foreach (float x in new[] { -body.extents.x, body.extents.x })
                    foreach (float z in new[] { -body.extents.z, body.extents.z })
                    {
                        var foot = site.Position + new Vector3(x, -.01f, z);
                        Assert.That(room.ContainsXZ(foot), Is.True);
                        Assert.That(blocks.Any(b => b.HasCollision && b.Kind == ProceduralSurfaceKind.Floor &&
                            new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) * (foot - b.Center))), Is.True);
                    }
                    if (site.GapEdge)
                        Assert.That(layout.GapSites.Any(g => g.RoomId == site.RoomId &&
                            Vector3.Dot(g.Edge - site.Position, site.Facing) > 0f &&
                            Mathf.Abs(Vector3.Dot(g.Edge - site.Position, site.Facing) - _config.ShrineSiteInset) < .001f &&
                            Vector3.Dot((g.Landing - g.Edge).normalized, site.Facing) > .99f), Is.True);
                    else Assert.That(site.RoomId, Is.Not.EqualTo(layout.Graph.ExitRoomId));
                }
                var culture = CultureInfo.CurrentCulture;
                string manifest = presenter.Manifest(sites, _config);
                try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); Assert.That(presenter.Manifest(sites, _config), Is.EqualTo(manifest)); }
                finally { CultureInfo.CurrentCulture = culture; }
                Assert.That(manifest.Split(new[] { "|ShrineSite:" }, StringSplitOptions.None).Length - 1, Is.EqualTo(sites.Count));
            }
        }

        [Test]
        public void ManagerPublishesOnlyAdmittedReachableSitesAndClearsThemOnTeardown()
        {
            Set("_origin", new Vector2(10000f, 10000f));
            var owner = new GameObject("Shrine producer admission test");
            try
            {
                var manager = owner.AddComponent<ProceduralManager>();
                Assert.That(manager.ShrineSites, Is.Empty);
                manager.Initialize(_config, _driver, 19, 3);
                Assert.That(manager.IsReady, Is.True);
                Assert.That(manager.ShrineSites.Count(s => !s.GapEdge), Is.GreaterThanOrEqualTo(4));
                var filter = new NavMeshQueryFilter { agentTypeID = _driver.NavMeshAgentTypeId, areaMask = _driver.HunterAreaMask };
                Assert.That(NavMesh.SamplePosition(manager.PlayerSpawnPosition, out var start, _driver.NavSampleRadius, filter), Is.True);
                foreach (var site in manager.ShrineSites)
                {
                    Assert.That(manager.LayoutManifest, Does.Contain("|ShrineSite:" + site.RoomId + "," + (site.GapEdge ? 1 : 0)));
                    Assert.That(NavMesh.SamplePosition(site.Position, out var end, _driver.NavSampleRadius, filter), Is.True);
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(start.position, end.position, filter, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                }
                var manifest = manager.LayoutManifest; var sites = manager.ShrineSites.ToArray();
                manager.Teardown(); Assert.That(manager.ShrineSites, Is.Empty);
                manager.Initialize(_config, _driver, 19, 3);
                Assert.That(manager.LayoutManifest, Is.EqualTo(manifest));
                Assert.That(manager.ShrineSites, Is.EqualTo(sites));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }

        [Test]
        public void MissingFloorAndObstructedSocketsAreNotPublished()
        {
            var layout = Generate(19, 3);
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
            var presenter = new ProceduralShrineSitePresenter();
            Assert.That(presenter.Build(layout, _config, Array.Empty<ProceduralBlock>()), Is.Empty);
            var first = presenter.Build(layout, _config, blocks).First();
            var obstruction = new ProceduralBlock(first.RoomId, ProceduralSurfaceKind.Wall,
                first.Position + Vector3.up, Vector3.one * 2f);
            Assert.That(presenter.Build(layout, _config, blocks.Concat(new[] { obstruction }).ToArray())
                .Any(s => s.Position.Equals(first.Position)), Is.False);
        }

        [TestCase("_shrineSiteInset", 0f)] [TestCase("_shrineSiteInset", 6f)]
        [TestCase("_shrineSiteClearance", float.NaN)] [TestCase("_shrineSiteClearance", float.PositiveInfinity)]
        [TestCase("_shrineSiteLateralFraction", float.NaN)] [TestCase("_shrineSiteLateralFraction", .6f)]
        public void InvalidSiteSettingsFailClosed(string field, float value)
        {
            Set(field, value); var layout = Generate(7, 1);
            Assert.Throws<ArgumentException>(() => new ProceduralShrineSitePresenter().Build(layout, _config,
                new ProceduralGeometryPresenter().Build(layout, _config, _driver)));
        }

        private static float DistanceXZ(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
        private static bool Overlaps(Bounds a, Bounds b) => a.min.x < b.max.x && a.max.x > b.min.x &&
            a.min.y < b.max.y && a.max.y > b.min.y && a.min.z < b.max.z && a.max.z > b.min.z;
        private static Bounds WorldBounds(ProceduralBlock block)
        {
            var bounds = new Bounds(block.Center, Vector3.zero);
            for (int corner = 0; corner < 8; corner++) bounds.Encapsulate(block.Center + block.Rotation * new Vector3(
                (corner & 1) == 0 ? -block.Size.x : block.Size.x, (corner & 2) == 0 ? -block.Size.y : block.Size.y,
                (corner & 4) == 0 ? -block.Size.z : block.Size.z) * .5f);
            return bounds;
        }
    }
}
