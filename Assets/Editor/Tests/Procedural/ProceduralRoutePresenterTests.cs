// ============================================================================
// ProceduralRoutePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that movement modules create actual alternate routes through
//   purposeful partitions. Cakes and required progress remain on ordinary floor
//   while the base movement kit gains explicit optional shortcut opportunities.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Procedural.
// KEY RESPONSIBILITIES:
//   - Validate all module kinds, explicit landings and useful physical clearances.
// DEPENDENCIES:
//   - Domain.Procedural, Core traversal metadata, NUnit and UnityEngine values.
// USAGE NOTES:
//   Native navigation and Player traversal acceptance complement these pure checks.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Procedural;

namespace Worsen.Tests.Procedural
{
    public sealed class ProceduralRoutePresenterTests
    {
        private ProceduralConfig _config;
        private ProceduralDriverConfig _driverConfig;
        [SetUp] public void SetUp()
        { _config = ScriptableObject.CreateInstance<ProceduralConfig>(); _driverConfig = ScriptableObject.CreateInstance<ProceduralDriverConfig>(); var settings = new SerializedObject(_config); settings.FindProperty("_castleModules").boolValue = false; settings.FindProperty("_initialRoomCount").intValue = 5; settings.ApplyModifiedPropertiesWithoutUndo(); }
        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_config); Object.DestroyImmediate(_driverConfig); }

        [Test]
        public void InitialFloorIncludesVaultWindowSlideAndReboundAlternatives()
        {
            var layout = Generate(); var presenter = new ProceduralRoutePresenter();
            var blocks = presenter.Build(layout, _config, _driverConfig);
            Assert.That(layout.Modules.Select(module => module.Kind).Distinct().Count(), Is.EqualTo(3));
            foreach (var module in layout.Modules)
            {
                var owned = blocks.Where(block => block.RoomId == module.RoomId).ToArray();
                Assert.That(owned.Count(block => block.TraversalKind == TraversalSurfaceKind.Rebound), Is.EqualTo(2));
                var shortcut = owned.Single(block => block.TraversalKind == TraversalSurfaceKind.Vault || block.TraversalKind == TraversalSurfaceKind.SlideGate);
                Assert.That(Vector3.Distance(shortcut.EndpointA, shortcut.EndpointB), Is.EqualTo(2.1f).Within(0.0001f));
                Assert.That(shortcut.EndpointA.y, Is.Zero); Assert.That(shortcut.EndpointB.y, Is.Zero);
                foreach (var point in new[] { shortcut.EndpointA, shortcut.EndpointB })
                    Assert.That(owned.Any(block => new Bounds(block.Center, block.Size).Contains(point + Vector3.up)), Is.False);
            }
            Assert.That(presenter.DescribeMarkers(blocks).All(marker => marker.Access == TraversalAccess.Player), Is.True);
            Assert.That(presenter.DescribeMarkers(blocks).Select(marker => marker.Id).Distinct().Count(), Is.EqualTo(15));
        }

        [Test]
        public void PartitionsPreserveWideWalkingBypassesAndCapsuleAppropriateApertures()
        {
            float bypass = (_config.RoomSize - _driverConfig.PartitionLength - _driverConfig.WallThickness) * 0.5f;
            Assert.That(bypass, Is.GreaterThan(2f));
            Assert.That(_driverConfig.SlideClearance, Is.GreaterThan(0.9f + 0.02f));
            Assert.That(_driverConfig.SlideClearance, Is.LessThan(1.8f));
            Assert.That(_driverConfig.WindowTopHeight - _driverConfig.VaultHeight, Is.GreaterThan(1.8f + 0.08f));
            Assert.That(_driverConfig.LandingOffset - _driverConfig.PartitionThickness * 0.5f, Is.GreaterThan(0.3f + 0.02f));
            var layout = Generate();
            var blocks = new ProceduralRoutePresenter().Build(layout, _config, _driverConfig);
            foreach (var module in layout.Modules)
            {
                var room = layout.Graph.Rooms[module.RoomId - 1];
                var along = module.AlongX ? Vector3.right : Vector3.forward;
                var bypassMidpoint = new Vector3(room.Center.x, 0.95f, room.Center.z) + along * 4.75f;
                var pass = new Bounds(bypassMidpoint, module.AlongX ? new Vector3(1f, 1.8f, 5f) : new Vector3(5f, 1.8f, 1f));
                Assert.That(blocks.Where(block => block.RoomId == room.Id).Any(block => new Bounds(block.Center, block.Size).Intersects(pass)), Is.False);
            }
        }
        private ProceduralLayout Generate()
            => new ProceduralController(new ProceduralBehaviorState(), _config,
                new System.Random(ProceduralController.LayoutSeed(8, 1))).Generate(8, 1);
    }
}
