// ============================================================================
// ProceduralStoreyNavigationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies multi-storey admission against Unity's native navigation builder.
//   Removing the only ramp must disconnect the upper floor even though ledges,
//   balcony vaults and drop geometry remain physically present.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Require a hunter path to cross the ramp, then reject a bake without that ramp.
//   - Verify partition area masks and owned-link teardown in the actual Driver.
// DEPENDENCIES:
//   - Core, Domain.Procedural, NUnit and UnityEngine.AI native navigation.
// USAGE NOTES:
//   Coordinator-only EditMode execution under the Unity lease. All generated
//   geometry is remote from authored scenes; teardown never clears global navigation.
// ============================================================================
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ProceduralStoreyNavigationTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        private GameObject _owner;
        private ProceduralDriver _driver;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            _driverConfig = ScriptableObject.CreateInstance<ProceduralDriverConfig>();
            Set(_config, "_origin", new Vector2(20000f, 20000f));
            Set(_config, "_oneCellWeight", 0f); Set(_config, "_twoCellWeight", 1f); Set(_config, "_threeCellWeight", 0f);
            Set(_config, "_storeyProbability", 1f); Set(_config, "_gapProbability", 0f);
            _owner = new GameObject("Storey native navigation verification");
            _driver = _owner.AddComponent<ProceduralDriver>();
        }
        [TearDown] public void TearDown()
        {
            if (_driver != null) _driver.Teardown();
            Object.DestroyImmediate(_owner); Object.DestroyImmediate(_config); Object.DestroyImmediate(_driverConfig);
        }
        private static void Set(object owner, string field, object value)
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private ProceduralLayout Generate() => new ProceduralController(new ProceduralBehaviorState(), _config,
            new System.Random(ProceduralController.LayoutSeed(19, 3))).Generate(19, 3);

        [TestCase(ProceduralVerticalKind.FloorHole)] [TestCase(ProceduralVerticalKind.Shaft)]
        [TestCase(ProceduralVerticalKind.Balcony)] [TestCase(ProceduralVerticalKind.CollapsedRamp)]
        public void HuntersReachUpperFloorOnlyByRampNotByPlayerShortcuts(ProceduralVerticalKind drop)
        {
            var layout = Generate();
            var storeys = layout.Storeys.Select(s => new ProceduralStoreyPlan(s.RoomId, s.Origin, s.Height, drop)).ToArray();
            typeof(ProceduralLayout).GetProperty(nameof(layout.Storeys)).SetValue(layout, storeys);
            typeof(ProceduralLayout).GetProperty(nameof(layout.VerticalRoutes)).SetValue(layout,
                storeys.SelectMany(s => ProceduralStoreyUtility.Routes(s, _config)).ToArray());
            _driver.Build(layout, _config, _driverConfig);
            Assert.That(_driver.IsReady, Is.True);
            var s0 = storeys[0];
            var climb = layout.VerticalRoutes.First(r => r.RoomId == s0.RoomId && r.Kind == ProceduralVerticalKind.LedgeClimb);
            var filter = new NavMeshQueryFilter { agentTypeID = _driverConfig.NavMeshAgentTypeId, areaMask = _driverConfig.HunterAreaMask };
            Assert.That(NavMesh.SamplePosition(climb.Points[0], out var start, _driverConfig.NavSampleRadius, filter), Is.True);
            Assert.That(NavMesh.SamplePosition(climb.Points.Last(), out var end, _driverConfig.NavSampleRadius, filter), Is.True);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(start.position, end.position, filter, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            bool crossesRamp = false;
            for (int i = 1; i < path.corners.Length; i++)
            {
                var a = path.corners[i - 1] - s0.Origin; var b = path.corners[i] - s0.Origin;
                if (Mathf.Abs(b.y - a.y) < 0.001f) continue;
                float fraction = (s0.Height * 0.5f - a.y) / (b.y - a.y);
                if (fraction < 0f || fraction > 1f) continue;
                var crossing = Vector3.Lerp(a, b, fraction);
                crossesRamp |= Mathf.Abs(crossing.x + 3f) < 1.1f && crossing.z > -3.8f && crossing.z < 1.4f;
            }
            Assert.That(crossesRamp, Is.True, "A complete path must physically cross the hunter ramp.");
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driverConfig);
            _driver.Teardown();
            var navigation = new ProceduralNavigationPresenter();
            var sources = blocks.Where(b => b.HasCollision && !(b.RoomId == s0.RoomId && b.Role == ProceduralBlockRole.StairRamp))
                .Select(b => new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = b.Size,
                    transform = Matrix4x4.TRS(b.Center, b.Rotation, Vector3.one), area = navigation.Area(b) }).ToList();
            var settings = NavMesh.GetSettingsByID(_driverConfig.NavMeshAgentTypeId);
            settings.overrideVoxelSize = true; settings.voxelSize = _driverConfig.NavVoxelSize;
            settings.ledgeDropHeight = 0f; settings.maxJumpAcrossDistance = 0f;
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources,
                new ProceduralGeometryPresenter().NavigationBounds(blocks, _driverConfig.NavBoundsPadding), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            var instance = NavMesh.AddNavMeshData(data);
            try
            {
                Assert.That(instance.valid, Is.True);
                Assert.That(NavMesh.SamplePosition(climb.Points[0], out start, _driverConfig.NavSampleRadius, filter), Is.True);
                Assert.That(NavMesh.SamplePosition(climb.Points.Last(), out end, _driverConfig.NavSampleRadius, filter), Is.True);
                Assert.That(NavMesh.CalculatePath(start.position, end.position, filter, path) && path.status == NavMeshPathStatus.PathComplete, Is.False,
                    "Climbs, drops and collapsed ramps must not repair the missing hunter ramp.");
            }
            finally { if (instance.valid) instance.Remove(); Object.DestroyImmediate(data); }
        }

        [Test]
        public void PartitionLinksAreMaskRestrictedAndRemovedOnTeardown()
        {
            Set(_driverConfig, "_enablePartitionIgnoringLinks", true);
            var layout = Generate();
            _driver.Build(layout, _config, _driverConfig);
            var state = (ProceduralDriverState)typeof(ProceduralDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_driver);
            var owned = state.NavigationLinks.ToArray();
            Assert.That(owned, Is.Not.Empty);
            Assert.That(owned.All(NavMesh.IsLinkValid), Is.True);
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driverConfig);
            var window = blocks.First(b => b.TraversalKind == TraversalSurfaceKind.Vault && b.EndpointA.y == b.EndpointB.y);
            var hunter = new NavMeshQueryFilter { agentTypeID = _driverConfig.NavMeshAgentTypeId, areaMask = _driverConfig.HunterAreaMask };
            var weaver = new NavMeshQueryFilter { agentTypeID = _driverConfig.NavMeshAgentTypeId, areaMask = _driverConfig.PartitionIgnoringAreaMask };
            Assert.That(NavMesh.SamplePosition(window.EndpointA, out var a, _driverConfig.NavSampleRadius, hunter), Is.True);
            Assert.That(NavMesh.SamplePosition(window.EndpointB, out var b, _driverConfig.NavSampleRadius, hunter), Is.True);
            var ordinary = new NavMeshPath(); var through = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(a.position, b.position, hunter, ordinary), Is.True);
            Assert.That(NavMesh.CalculatePath(a.position, b.position, weaver, through), Is.True);
            Assert.That(ordinary.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(through.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            Assert.That(Length(through), Is.LessThan(Length(ordinary)));
            _driver.Teardown();
            Assert.That(owned.Any(NavMesh.IsLinkValid), Is.False);
        }
        private static float Length(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }
    }
}
