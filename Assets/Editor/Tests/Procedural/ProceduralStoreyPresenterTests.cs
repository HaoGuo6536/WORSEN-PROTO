// ============================================================================
// ProceduralStoreyPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks physical support and open drop shafts using pure box descriptions.
//   The tests inspect geometry assembled by the real shell presenter, so a valid
//   route record cannot hide an intact floor over its supposed downward opening.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Cover every drop, staging ledge, ramp endpoint and moved upper objective.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and temporary Unity configuration instances.
// USAGE NOTES:
//   These conservative geometry checks do not substitute for native movement tests.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralStoreyPresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driver;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driver = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            Set("_oneCellWeight", 0f); Set("_twoCellWeight", 1f); Set("_threeCellWeight", 0f); Set("_storeyProbability", 1f);
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driver); }
        private void Set(string name, object value) => typeof(ProceduralConfig).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_config, value);

        [Test]
        public void ThirtyTwoSeedsHaveRealOpeningsAndSupportedClearLandings()
        {
            var kinds = new System.Collections.Generic.HashSet<ProceduralVerticalKind>();
            for (int seed = 0; seed < 32; seed++)
            {
                var layout = new ProceduralController(new ProceduralBehaviorState(), _config,
                    new System.Random(ProceduralController.LayoutSeed(seed, 3))).Generate(seed, 3);
                var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driver);
                Assert.DoesNotThrow(() => new ProceduralStoreyPresenter().ValidateLandings(layout, blocks), "seed=" + seed);
                foreach (var s in layout.Storeys)
                {
                    kinds.Add(s.Drop);
                    var ledge = layout.VerticalRoutes.Single(r => r.RoomId == s.RoomId && r.Kind == ProceduralVerticalKind.LedgeClimb);
                    foreach (var p in ledge.Points)
                        Assert.That(blocks.Any(b => b.HasCollision && Contains(b, p - Vector3.up * 0.01f)), Is.True);
                    foreach (var b in blocks.Where(b => b.RoomId == s.RoomId && b.Role == ProceduralBlockRole.PlayerOnly))
                    { Assert.That(b.HasCollision && b.HasRenderer, Is.True); Assert.That(new ProceduralNavigationPresenter().Area(b), Is.EqualTo(1)); }
                    var opening = s.Origin + new Vector3(0f, s.Height - 0.01f,
                        s.Drop == ProceduralVerticalKind.FloorHole || s.Drop == ProceduralVerticalKind.Shaft ? 2f : -2f);
                    Assert.That(blocks.Any(b => b.HasCollision && Contains(b, opening)), Is.False, "No slab may cover the drop.");
                    var anchor = layout.Graph.Anchors.First(a => ProceduralStoreyUtility.Region(layout, a.RoomId, a.Position) == s.UpperRegionId);
                    Assert.That(blocks.Any(b => b.HasCollision && Contains(b, anchor.Position - Vector3.up * (_config.AnchorHeight + 0.01f))), Is.True);
                    var clear = new Bounds(anchor.Position + Vector3.up * 0.91f, new Vector3(0.6f, 1.8f, 0.6f));
                    Assert.That(blocks.Where(b => b.HasCollision && b.Rotation == Quaternion.identity)
                        .Any(b => new Bounds(b.Center, b.Size).Intersects(clear)), Is.False);
                }
                var removedFloors = blocks.Where(b => b.Kind != ProceduralSurfaceKind.Floor).ToArray();
                Assert.Throws<InvalidOperationException>(() => new ProceduralStoreyPresenter().ValidateLandings(layout, removedFloors));
            }
            Assert.That(kinds.Count, Is.EqualTo(4));
        }
        private static bool Contains(ProceduralBlock b, Vector3 p)
            => new Bounds(Vector3.zero, b.Size).Contains(Quaternion.Inverse(b.Rotation) * (p - b.Center));
    }
}
