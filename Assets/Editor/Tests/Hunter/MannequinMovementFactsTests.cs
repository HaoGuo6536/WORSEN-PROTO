// ============================================================================
// MannequinMovementFactsTests.cs
// ============================================================================
// PURPOSE:
//   Distinguishes actual committed Mannequin movement from permission to pursue.
//   Audio can start on the first unseen displacement and stop on the exact hold
//   tick without assuming that a path request moved a blocked hunter.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover movement edges, blocked paths, observation, Wick, stale cameras and teardown.
// DEPENDENCIES:
//   - Mannequin rules, Core facts, managed fixture views and NUnit.
// USAGE NOTES:
//   No native assets or scene; committed positions and camera evidence are injected.
// ============================================================================
using System;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MannequinMovementFactsTests
    {
        private MannequinController rules;
        private RosterBTestHunter hunter;
        private PlayerBehaviorState player;
        private HunterArchetypeContext Context(long tick) => new HunterArchetypeContext(hunter, player,
            null, null, null, null, null, .1f, tick, true, 1f);
        [SetUp] public void Setup()
        {
            var config = (MannequinConfig)FormatterServices.GetUninitializedObject(typeof(MannequinConfig));
            EchoControllerTests.Tune(config, "_observationHeight", 1f);
            EchoControllerTests.Tune(config, "_longerStridesMultiplier", 1.2f);
            hunter = new RosterBTestHunter { Id = new EntityId(-1), IsActive = true };
            player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100 };
            rules = new MannequinController(config, new System.Random(2)); rules.Reset(Context(0));
            Assert.That(rules.TakeFact(out var fact), Is.True); Assert.That(fact.Kind, Is.EqualTo(MannequinFactKind.SilentSoundSet));
        }
        private void Step(long tick, Vector3 position, bool observed = false, bool wick = false, bool fresh = true)
        {
            rules.Observe(fresh ? new HunterPlayerView(new Vector3(0, 1, 10),
                observed ? new Quaternion(0, 1, 0, 0) : Quaternion.identity, 90, 60, tick) : default,
                observed, false, null, wick);
            rules.Tick(Context(tick)); hunter.Position = position;
            rules.CommitMovement(hunter.Id, position, tick);
        }
        [Test] public void IntentDoesNotStartCreaks_ActualDisplacementStartsOnce_StationaryStopsOnce()
        {
            Step(1, Vector3.zero); Assert.That(rules.Hold, Is.False); Assert.That(rules.TakeFact(out _), Is.False);
            Step(2, Vector3.forward);
            Assert.That(rules.TakeFact(out var start), Is.True); Assert.That(start.Kind, Is.EqualTo(MannequinFactKind.MovementStarted));
            Assert.That(start.Position, Is.EqualTo(Vector3.forward)); Assert.That(start.Tick, Is.EqualTo(2));
            Step(3, Vector3.forward * 2); Assert.That(rules.TakeFact(out _), Is.False);
            Step(4, Vector3.forward * 2);
            Assert.That(rules.TakeFact(out var stop), Is.True); Assert.That(stop.Kind, Is.EqualTo(MannequinFactKind.MovementHeld));
            Assert.That(stop.Position, Is.EqualTo(Vector3.forward * 2)); Assert.That(stop.Hunter, Is.EqualTo(hunter.Id));
            Step(5, Vector3.forward * 2); Assert.That(rules.TakeFact(out _), Is.False);
        }
        [TestCase("view")] [TestCase("wick")] [TestCase("stale")]
        public void FreezePublishesHoldOnTheSameTick(string reason)
        {
            Step(1, Vector3.forward); rules.TakeFact(out _);
            Step(2, Vector3.forward, reason == "view", reason == "wick", reason != "stale");
            Assert.That(rules.Hold, Is.True);
            Assert.That(rules.TakeFact(out var stop), Is.True); Assert.That(stop.Kind, Is.EqualTo(MannequinFactKind.MovementHeld));
            Assert.That(stop.Tick, Is.EqualTo(2)); Assert.That(stop.Position, Is.EqualTo(hunter.Position));
        }
        [Test] public void CatchOrTeardownStopsMovementOnceAndResetDoesNotLeakMotion()
        {
            Step(1, Vector3.forward); rules.TakeFact(out _);
            rules.CommitMovement(hunter.Id, hunter.Position, 1, false);
            Assert.That(rules.TakeFact(out var stop), Is.True); Assert.That(stop.Kind, Is.EqualTo(MannequinFactKind.MovementHeld));
            rules.CommitMovement(hunter.Id, hunter.Position, 1, false); Assert.That(rules.TakeFact(out _), Is.False);
            rules.Reset(Context(0)); rules.TakeFact(out _); Step(1, hunter.Position); Assert.That(rules.TakeFact(out _), Is.False);
        }
    }
}
