// ============================================================================
// FloorGuidanceIntegrationTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Floor Manager routing, expiration and lifecycle for Faithless Arrow.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Floor.
// KEY RESPONSIBILITIES:
//   - Check both the active curse and Mimic facts are required for white misdirection.
//   - Check time expiry, reward counters and reinitialization through the real Manager.
// DEPENDENCIES:
//   NUnit, UnityEngine, Core, Floor and the existing Floor cake fixture.
// USAGE NOTES:
//   ShaderReferenceTestSetup explicitly binds shaders for transient generated visuals.
//   Requires Unity: temporary scene objects only, no authored asset changes.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Level;
using EntityId = Worsen.Core.EntityId;
using Object = UnityEngine.Object;
namespace Worsen.Tests.Floor
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class FloorGuidanceIntegrationTests
    {
        [Test]
        public void ManagerRequiresCurseExpiresWindowAndResetsAtNextFloor()
        {
            var root = new GameObject("Floor guidance integration"); root.SetActive(false);
            var config = ScriptableObject.CreateInstance<FloorConfig>();
            var visual = Worsen.Tests.Core.ShaderReferenceTestSetup.Create<FloorDriverConfig>();
            var driver = root.AddComponent<FloorDriver>(); FloorCakeRulesTests.Set(driver, "_config", visual);
            var manager = root.AddComponent<FloorManager>();
            var player = new FloorCakeRulesTests.Player(); var hunter = new EntityId(901);
            IReadOnlyList<GuidanceTarget> targets = null;
            manager.OnGuidanceChanged += value => targets = value;
            try
            {
                FloorCakeRulesTests.Set(config, "_useRoomCakeDensity", false);
                FloorCakeRulesTests.Set(config, "_requiredCakeCount", 6);
                manager.Initialize(config, new Level(), new[] { player }, new System.Random(7));
                var total = manager.Snapshot().TotalCakes;
                var pose = new MimicFact(hunter, player.Id, MimicFactKind.Pose, 1, new Vector3(61f, 0f, 0f));
                var window = new MimicFact(hunter, player.Id, MimicFactKind.FaithlessWindow, 1, pose.Position, 2f);
                manager.ReceiveMimic(pose); manager.ReceiveMimic(window);
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.Not.EqualTo(hunter));
                manager.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(FloorGuidanceController.FaithlessArrow, EffectKind.Curse, 1) }));
                manager.ReceiveMimic(window);
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.EqualTo(hunter));
                Assert.That(manager.Snapshot().TotalCakes, Is.EqualTo(total));
                Assert.That(manager.ReadOnlyState.CakeCount, Is.Zero);
                manager.Tick(2f, 2);
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.Not.EqualTo(hunter));
                manager.ReceiveMimic(new MimicFact(hunter, player.Id, MimicFactKind.FaithlessWindow, 3, pose.Position, 2f));
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.EqualTo(hunter));
                manager.Initialize(config, new Level(), new[] { player }, new System.Random(7));
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.Not.EqualTo(hunter));
                manager.ReceiveMimic(window);
                Assert.That(targets.Single(t => t.Kind == GuidanceKind.WhiteArrow).EntityId, Is.Not.EqualTo(hunter));
            }
            finally { manager.Teardown(); Object.DestroyImmediate(root); Object.DestroyImmediate(config); Object.DestroyImmediate(visual); }
        }
        private sealed class Level : IReadOnlyLevelState
        { public bool IsReady => true; public LevelGraph Graph => FloorCakeRulesTests.Graph(); }

    }
}
