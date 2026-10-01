// ============================================================================
// ProceduralPassageNavigationTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Passage support and partition permissions against native Unity
//   navigation. Remote floors and isolated link landings prevent authored scenes
//   or an ordinary bypass from disguising a missing bridge or an unsafe area mask.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Verify tiles, both wall apertures, path opening/closure, facts and teardown.
//   - Probe tile support inside the void, away from intentionally overlapping room seams.
//   - Require mask-9 connectivity and mask-1 isolation for each generated link family.
// DEPENDENCIES:
//   - Domain.Procedural, Core, NUnit, UnityEngine physics and native navigation.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Coordinator-only EditMode execution. Removes only owned objects and navigation.
//   Link-family tests isolate the generated endpoint plan; full-floor detour and
//   Driver-owned link teardown remain covered by ProceduralStoreyNavigationTests.
// ============================================================================
using System;
using System.Collections.Generic;
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
    public sealed class ProceduralPassageNavigationTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        private GameObject _owner;
        private ProceduralManager _manager;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ProceduralConfig>();
            _driverConfig = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<ProceduralDriverConfig>();
            Set(_config, "_origin", new Vector2(30000f, 30000f));
            Set(_config, "_castleModules", false); Set(_config, "_storeyProbability", 0f);
            Set(_config, "_gapProbability", 1f); Set(_config, "_pocketProbability", 1f);
            Set(_config, "_ordinaryDoorFraction", 1f);
            _owner = new GameObject("Passage native verification"); _manager = _owner.AddComponent<ProceduralManager>();
        }
        [TearDown] public void TearDown()
        {
            _manager.Teardown(); UnityEngine.Object.DestroyImmediate(_owner);
            UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_driverConfig);
        }
        private static void Set(object owner, string field, object value)
            => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        private NavMeshQueryFilter Filter(int mask) => new NavMeshQueryFilter { agentTypeID = _driverConfig.NavMeshAgentTypeId, areaMask = mask };
        private bool Connected(Vector3 from, Vector3 to, int mask)
        {
            if (!NavMesh.SamplePosition(from, out var a, _driverConfig.NavSampleRadius, Filter(mask)) ||
                !NavMesh.SamplePosition(to, out var b, _driverConfig.NavSampleRadius, Filter(mask))) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, Filter(mask), path) && path.status == NavMeshPathStatus.PathComplete;
        }

        [Test]
        public void PassageOpensWalkableTilesAndAperturesThenCollapsesAndTearsDownDeterministically()
        {
            _manager.Initialize(_config, _driverConfig, 19, 12);
            var driver = _owner.GetComponent<ProceduralDriver>();
            int index = _manager.ShrineSites.ToList().FindIndex(s => s.GapEdge);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var site = _manager.ShrineSites[index];
            var pocket = _manager.Graph.Rooms.Single(r => r.Id == site.DestinationPocketRoomId);
            var module = _manager.RoomModules.Single(m => m.RoomId == pocket.Id);
            var target = new Vector3(pocket.Cells[0].center.x, _config.SpawnHeight, pocket.Cells[0].center.z) +
                (module.AlongX ? Vector3.forward : Vector3.right) * _config.SpawnSideOffset;
            Assert.That(Connected(site.Position, target, 1), Is.False);
            var collapsed = new List<int>(); IReadOnlyList<Vector3> opened = null; int openedPocket = 0, openedSite = -1;
            Action<int, int, IReadOnlyList<Vector3>> onOpened = (s, p, tiles) => { openedSite = s; openedPocket = p; opened = tiles; };
            Action<int, int, int, Vector3> onCollapsed = (s, p, tile, position) =>
            { Assert.That(s, Is.EqualTo(index)); Assert.That(p, Is.EqualTo(pocket.Id)); Assert.That(position, Is.EqualTo(opened[tile])); collapsed.Add(tile); };
            _manager.PassageOpened += onOpened; _manager.PassageTileCollapsed += onCollapsed;
            try
            {
                Assert.That(_manager.ActivatePassage(-1), Is.False);
                Assert.That(_manager.ActivatePassage(index), Is.True);
                Assert.That(openedSite, Is.EqualTo(index)); Assert.That(openedPocket, Is.EqualTo(pocket.Id));
                Assert.That(opened, Is.Not.Empty); Assert.That(_manager.LinedPocketAnchors, Is.Not.Empty);
                Assert.That(_manager.ActivatePassage(index), Is.False, "A paid crossing cannot reset its timer.");
                Assert.That(Connected(site.Position, target, 1), Is.True);
                var state = (ProceduralDriverState)typeof(ProceduralDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
                var plan = state.Passages.Single(p => p.Plan.SiteIndex == index).Plan;
                var source = _manager.Graph.Rooms.Single(r => r.Id == site.RoomId);
                var sourceCell = source.Cells.Single(c => c.min.x <= site.Position.x && c.max.x >= site.Position.x &&
                    c.min.z <= site.Position.z && c.max.z >= site.Position.z);
                var pocketCell = pocket.Cells.Single(c => c.min.x <= plan.End.x && c.max.x >= plan.End.x &&
                    c.min.z <= plan.End.z && c.max.z >= plan.End.z);
                // PLAN-025 §3 C7 / §8: support must span a real gap and then vanish.
                // Endpoint centres may coincide with room floors at y=0; neither
                // collider tie ordering nor removal of permanent room floor is a contract.
                var probes = plan.Tiles.Select(tile => ProceduralPassageContractTests.GapProbe(tile, site.Facing,
                    new LevelRoom(source.Id, sourceCell.center, sourceCell.size),
                    new LevelRoom(pocket.Id, pocketCell.center, pocketCell.size, pocket: true))).ToArray();
                Assert.That(probes.Length, Is.EqualTo(opened.Count));
                foreach (var point in probes)
                {
                    Assert.That(Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 2f), Is.True);
                    Assert.That(hit.collider.gameObject.name, Does.StartWith("Passage "));
                    Assert.That(hit.point.y, Is.EqualTo(point.y).Within(.01f));
                    Assert.That(Physics.CheckCapsule(point + Vector3.up * .31f, point + Vector3.up * 1.49f, .3f,
                        ~0, QueryTriggerInteraction.Ignore), Is.False, "Both former sealed wall faces must admit a standing actor.");
                }
                driver.TickPassages(3.99f); Assert.That(collapsed, Is.Empty);
                driver.TickPassages(.01f); Assert.That(collapsed, Is.EqualTo(new[] { 0 }));
                Assert.That(Connected(site.Position, target, 1), Is.False);
                driver.TickPassages(.4f); Assert.That(collapsed, Is.EqualTo(new[] { 0, 1 }));
                driver.TickPassages(100f); Assert.That(collapsed, Is.EqualTo(Enumerable.Range(0, opened.Count)));
                foreach (var point in probes)
                    Assert.That(Physics.Raycast(point + Vector3.up * .5f, Vector3.down, 1f), Is.False);
                var positions = opened.ToArray(); var anchors = _manager.LinedPocketAnchors.ToArray();
                var oldLinks = ((ProceduralDriverState)typeof(ProceduralDriver).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(driver)).NavigationLinks.ToArray();
                _manager.Teardown();
                Assert.That(_manager.LinedPocketAnchors, Is.Empty); Assert.That(_owner.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(oldLinks.Any(NavMesh.IsLinkValid), Is.False);
                Assert.That(NavMesh.SamplePosition(positions[0], out _, .2f, Filter(1)), Is.False);
                _manager.Initialize(_config, _driverConfig, 19, 12);
                Assert.That(_manager.ActivatePassage(index), Is.True);
                Assert.That(opened, Is.EqualTo(positions)); Assert.That(_manager.LinedPocketAnchors, Is.EqualTo(anchors));
                _manager.Teardown();
                Assert.That(_owner.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(_manager.LinedPocketAnchors, Is.Empty);
                Assert.That(NavMesh.SamplePosition(positions[0], out _, .2f, Filter(1)), Is.False);
            }
            finally { _manager.PassageOpened -= onOpened; _manager.PassageTileCollapsed -= onCollapsed; }
        }

        [TestCase("optional-door")] [TestCase("vault-window")] [TestCase("thin-partition")]
        public void GeneratedLinkAloneConnectsMaskNineButNeverMaskOne(string family)
        {
            var layout = new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(19, 12))).Generate(19, 12);
            var blocks = new ProceduralGeometryPresenter().Build(layout, _config, _driverConfig);
            var interactables = new ProceduralInteractablePresenter().Build(layout, _config, _driverConfig, blocks, new System.Random(19));
            typeof(ProceduralLayout).GetProperty(nameof(layout.Interactables)).SetValue(layout, interactables);
            var plans = new ProceduralNavigationPresenter().Links(layout, blocks, _driverConfig);
            ProceduralNavigationLink plan;
            if (family == "optional-door")
            {
                var door = layout.Doors.First(d => !d.IsOptional && interactables.Any(p => p.State.Kind == InteractableKind.Door &&
                    p.State.EdgeId == 1001 + layout.Doors.ToList().IndexOf(d)));
                var offset = (door.AlongX ? Vector3.forward : Vector3.right) * _driverConfig.LandingOffset;
                plan = plans.Single(p => p.Start == door.Center - offset && p.End == door.Center + offset);
            }
            else
            {
                var kind = family == "vault-window" ? ProceduralModuleKind.WindowPartition : ProceduralModuleKind.VaultPartition;
                var wall = blocks.First(b => b.TraversalKind == TraversalSurfaceKind.Vault &&
                    layout.Modules.Single(m => m.RoomId == b.RoomId).Kind == kind);
                plan = plans.Single(p => p.Start == wall.EndpointA && p.End == wall.EndpointB);
            }
            Assert.That(plan.Area, Is.EqualTo(3));
            // Bake just these two generated landings, remote from the full floor.
            // No walking bypass exists: the link itself must supply connectivity.
            var shift = Vector3.right * 10000f;
            var start = plan.Start + shift; var end = plan.End + shift;
            var sources = new List<NavMeshBuildSource>();
            foreach (var point in new[] { start, end })
                sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.TRS(point - Vector3.up * .15f, Quaternion.identity, Vector3.one),
                    size = new Vector3(1.6f, .3f, 1.6f), area = 0 });
            var settings = NavMesh.GetSettingsByID(_driverConfig.NavMeshAgentTypeId);
            settings.overrideVoxelSize = true; settings.voxelSize = _driverConfig.NavVoxelSize;
            settings.ledgeDropHeight = 0f; settings.maxJumpAcrossDistance = 0f; settings.minRegionArea = 0f;
            var bounds = new Bounds((start + end) * .5f, new Vector3(8f, 4f, 8f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null);
            var instance = NavMesh.AddNavMeshData(data); NavMeshLinkInstance link = default;
            try
            {
                Assert.That(instance.valid, Is.True); Assert.That(Connected(start, end, 9), Is.False);
                link = NavMesh.AddLink(new NavMeshLinkData { startPosition = start, endPosition = end,
                    agentTypeID = _driverConfig.NavMeshAgentTypeId, area = plan.Area, bidirectional = true, width = 0f, costModifier = -1f });
                Assert.That(NavMesh.IsLinkValid(link), Is.True);
                Assert.That(Connected(start, end, 9), Is.True); Assert.That(Connected(end, start, 9), Is.True);
                Assert.That(Connected(start, end, 1), Is.False); Assert.That(Connected(end, start, 1), Is.False);
                NavMesh.RemoveLink(link); Assert.That(Connected(start, end, 9), Is.False);
            }
            finally
            { if (NavMesh.IsLinkValid(link)) NavMesh.RemoveLink(link); if (instance.valid) instance.Remove(); UnityEngine.Object.DestroyImmediate(data); }
        }
    }
}
