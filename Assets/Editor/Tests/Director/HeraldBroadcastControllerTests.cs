// ============================================================================
// HeraldBroadcastControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies typed Herald admission without changing ordinary hearing provenance.
//   Checks observation age and clue identity independently of native registries.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Director.
// KEY RESPONSIBILITIES:
//   - Verify queued broadcasts are deduplicated, validated and floor-reset scoped.
//   - Verify eligible recipients preserve the clue and exclude the emitting hunter.
// DEPENDENCIES:
//   Core, Director/Hunter/Player/Floor/Chase pure state and NUnit.
// USAGE NOTES:
//   Uninitialized configs keep this fixture independent of native Unity allocation.
// ============================================================================
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Director;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;

using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Director
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HeraldBroadcastControllerTests
    {
        private static HeraldScreamFact Fact(int source = 7, long emitted = 20, long observed = 9) =>
            new HeraldScreamFact(new EntityId(source), HeraldSound.ChaseOne, "herald.chase", 1f,
                new NoiseEvent(new EntityId(source), Vector3.zero, 1f, emitted, NoiseSourceKind.Scream),
                new NoiseEvent(new EntityId(source), Vector3.one * 12f, 1f, emitted, NoiseSourceKind.Scream), observed, false);
        [Test] public void TypedQueueRejectsDuplicateInvalidAgeAndFutureDeliveryAndResetsPerFloor()
        {
            var state = new DirectorBehaviorState();
            var config = Blank<DirectorConfig>();
            foreach (string field in new[] { "_evaluationIntervalSeconds", "_hintAgeSeconds", "_hintCadenceSeconds", "_exitOpenHintCadenceSeconds", "_intrusionDurationSeconds", "_retreatCooldownSeconds" })
                typeof(DirectorConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, 1f);
            typeof(DirectorConfig).GetField("_historyCapacity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(config, 8);
            var controller = new DirectorController(state, config, new System.Random(7));
            var fact = Fact();
            Assert.That(HunterHearingUtility.Allows(fact.FloorWideHint), Is.False);
            Assert.That(controller.HearHeraldBroadcast(fact), Is.True);
            Assert.That(controller.HearHeraldBroadcast(fact), Is.False);
            Assert.That(controller.HearHeraldBroadcast(Fact(observed: 21)), Is.False);
            Assert.That(controller.HearHeraldBroadcast(Fact(source: 0)), Is.False);
            Assert.That(controller.DrainHeraldBroadcasts(20), Is.EqualTo(new[] { fact }));
            Assert.That(controller.DrainHeraldBroadcasts(20), Is.Empty);
            Assert.That(controller.HearHeraldBroadcast(Fact(emitted: 22)), Is.True);
            Assert.That(controller.DrainHeraldBroadcasts(21), Is.Empty, "Future facts cannot be delivered.");
            controller.Reset(); Assert.That(controller.HearHeraldBroadcast(fact), Is.True);
            Assert.That(state.Noises, Is.Empty, "Typed broadcasts never enter ordinary hearing.");
        }
        [Test] public void HunterReceivesAgedPlayerClueNotTheSoundPositionAndEmitterIsExcluded()
        {
            var state = new HunterBehaviorState();
            var player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, Position = Vector3.forward * 99f };
            var profile = Blank<HunterProfile>();
            typeof(HunterProfile).GetField("_memoryDecaySeconds", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, 10f);
            var controller = new HunterController(state, profile, new System.Random(7), player, new LevelView());
            controller.Reset(new EntityId(8), Vector3.zero, Vector3.forward);
            typeof(HunterBehaviorState).GetProperty("Tick").SetValue(state, 20L);
            typeof(HunterBehaviorState).GetField("DeltaTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, .02f);
            Assert.That(controller.HearHeraldBroadcast(Fact(), out var hint), Is.True);
            Assert.That(hint.Position, Is.EqualTo(Vector3.one * 12f));
            Assert.That(hint.ObservedTick, Is.EqualTo(9)); Assert.That(hint.DeliveredTick, Is.EqualTo(20));
            Assert.That(hint.AgeSeconds, Is.EqualTo(.22f).Within(.00001f));
            Assert.That(hint.Player, Is.EqualTo(player.Id)); Assert.That(state.LastKnownPosition, Is.EqualTo(hint.Position));
            Assert.That(controller.HearHeraldBroadcast(Fact(source: 8), out _), Is.False);
            typeof(HunterBehaviorState).GetProperty("IsActive").SetValue(state, false);
            Assert.That(controller.HearHeraldBroadcast(Fact(), out _), Is.False);
        }
        private static T Blank<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private sealed class LevelView : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 80f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        }
    }
}
