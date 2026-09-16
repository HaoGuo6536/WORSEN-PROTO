// ============================================================================
// ExpeditionSessionControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that replacement floors cannot reuse stale generation callbacks or
//   become ready with a missing actor. These pure tests cover the admission
//   protocol independently of runtime geometry and the Unity coroutine scheduler.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Expedition.
// KEY RESPONSIBILITIES:
//   - Exercise duplicate/stale events, cleanup admission and failed generation.
//   - Verify roster identity and portal crossing admission for real retained effects.
//   - Check shop safety, exact threat budgets and invalid loadout rejection.
// DEPENDENCIES:
//   - Session Expedition pure Controller/State, Core definitions, NUnit.
// USAGE NOTES:
//   No scenes, engine objects or global registries are touched. Separate live
//   integration checks must verify deferred destruction, baking and run routing.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.Expedition
{
    public sealed class ExpeditionSessionControllerTests
    {
        private ExpeditionSessionBehaviorState _state;
        private ExpeditionSessionController _controller;

        [SetUp]
        public void SetUp()
        {
            _state = new ExpeditionSessionBehaviorState();
            _controller = new ExpeditionSessionController(_state);
            _controller.Bind(SceneKey.FloorLoop);
        }

        [Test]
        public void SelectedHunterIdentitiesSurviveSpawnPlanning()
        {
            var effects = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 2,
                ProgressionTraits.SealedSills, 0, new[] { "hexer", "thorncaller" });
            _controller.Queue(new ProgressionGenerationRequest(1, 17, 1, false, effects));
            _controller.Begin(1);
            var spawns = _controller.HunterSpawns("fallback", new[] { Vector3.zero, Vector3.one });
            Assert.That(spawns[0].ArchetypeKey, Is.EqualTo("hexer"));
            Assert.That(spawns[1].ArchetypeKey, Is.EqualTo("thorncaller"));
            Assert.That(_controller.OptionalWindowMultiplier(), Is.EqualTo(0.4f));
        }

        [Test]
        public void RosterBudgetMismatchRejectsRatherThanSpawningWrongModel()
        {
            var effects = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 100f, 2,
                activeThreatIds: new[] { "rusher" });
            _controller.Queue(new ProgressionGenerationRequest(1, 17, 1, false, effects));
            _controller.Begin(1);
            Assert.Throws<InvalidOperationException>(() => _controller.HunterSpawns("fallback", new[] { Vector3.zero, Vector3.one }));
        }

        [Test]
        public void MarksRequireAdjacentRoomCrossingNearASharedPortal()
        {
            _controller.Queue(Request(threats: 0)); _controller.Begin(1);
            _controller.RecordRooms(new[] {
                new GeneratedRoomSample(1, new Bounds(new Vector3(0,3,0),new Vector3(12,6,12)),false,false,new[]{new Vector3(6,0,0)}),
                new GeneratedRoomSample(2, new Bounds(new Vector3(12,3,0),new Vector3(12,6,12)),false,false,new[]{new Vector3(6,0,0)},true)});
            var id = new EntityId(1); _controller.RecordPlayer(id); _controller.Ready();
            PlayerMovementSample Sample(float x, long tick) => new PlayerMovementSample(id,tick,new Vector3(x,1,0),Vector3.zero,Vector3.zero,0,Vector2.zero,false,MovementState.Ground,0);
            Assert.That(_controller.ObserveCrossing(Sample(5.5f,1),out _,out _),Is.False);
            Assert.That(_controller.ObserveCrossing(Sample(6.5f,2),out int door,out Vector3 position),Is.True);
            Assert.That(position,Is.EqualTo(new Vector3(6,0,0)));
            Assert.That(door,Is.GreaterThan(0));
            Assert.That(_controller.ObserveCrossing(Sample(0,3),out _,out _),Is.False);
            Assert.That(_controller.OptionalRooms(),Is.EqualTo(new[]{2}));
        }

        [Test]
        public void UnboundGenerationIsRejected()
        {
            _controller.ClearScene();
            Assert.Throws<InvalidOperationException>(() => _controller.Queue(Request()));
            Assert.That(_state.Phase, Is.EqualTo(ExpeditionAssemblyPhase.Unbound));
        }

        [Test]
        public void DuplicateAndOlderRequestsCannotReplaceThePendingFloor()
        {
            Assert.That(_controller.Queue(Request(3)), Is.True);
            Assert.That(_controller.Queue(Request(3)), Is.False);
            Assert.That(_controller.Queue(Request(2)), Is.False);
            Assert.That(_state.Request.GenerationId, Is.EqualTo(3));
            Assert.That(_state.Phase, Is.EqualTo(ExpeditionAssemblyPhase.Queued));
        }

        [Test]
        public void OverlappingNewerRequestIsRejectedBeforeExistingRequestChanges()
        {
            _controller.Queue(Request(1));
            Assert.Throws<InvalidOperationException>(() => _controller.Queue(Request(2)));
            Assert.That(_state.Request.GenerationId, Is.EqualTo(1));
            Assert.That(_controller.Begin(1), Is.True);
            Assert.Throws<InvalidOperationException>(() => _controller.Queue(Request(2)));
        }

        [Test]
        public void DeferredAdmissionRequiresMatchingIdAndReleasedOldActors()
        {
            ReadyFloor();
            _controller.Queue(Request(2));
            Assert.That(_controller.Begin(1), Is.False);
            Assert.Throws<InvalidOperationException>(() => _controller.Begin(2));
            _controller.ReleaseActors();
            Assert.That(_controller.Begin(2), Is.True);
            Assert.That(_state.Player.IsValid, Is.False);
        }

        [Test]
        public void MissingHunterPreventsReadiness()
        {
            _controller.Queue(Request(threats: 2));
            _controller.Begin(1);
            _controller.RecordPlayer(new EntityId(1));
            _controller.RecordHunter(new EntityId(-1));
            Assert.Throws<InvalidOperationException>(_controller.Ready);
            _controller.RecordHunter(new EntityId(-2));
            Assert.DoesNotThrow(_controller.Ready);
        }

        [Test]
        public void MissingPlayerPreventsReadiness()
        {
            _controller.Queue(Request(threats: 0));
            _controller.Begin(1);
            Assert.Throws<InvalidOperationException>(_controller.Ready);
        }

        [Test]
        public void ActorIdentitiesMustBeValidAndUnique()
        {
            _controller.Queue(Request()); _controller.Begin(1);
            Assert.Throws<ArgumentException>(() => _controller.RecordPlayer(EntityId.None));
            _controller.RecordPlayer(new EntityId(1));
            Assert.Throws<ArgumentException>(() => _controller.RecordPlayer(new EntityId(2)));
            Assert.Throws<ArgumentException>(() => _controller.RecordHunter(new EntityId(1)));
            _controller.RecordHunter(new EntityId(-1));
            Assert.Throws<ArgumentException>(() => _controller.RecordHunter(new EntityId(-1)));
        }

        [Test]
        public void ShopCreatesNoHunterRequestsAndDoesNotAcceptFloorCompletion()
        {
            _controller.Queue(Request(shop: true, threats: 3)); _controller.Begin(1);
            Assert.That(_controller.HunterSpawns("Hunter", Array.Empty<Vector3>()), Is.Empty);
            _controller.RecordPlayer(new EntityId(1)); _controller.Ready();
            Assert.That(_controller.AcceptsGameplay(new EntityId(1)), Is.False);
            Assert.That(_controller.Resolve(SceneKey.FloorLoop), Is.False);
        }

        [Test]
        public void HunterBudgetCannotSilentlyShrinkToAvailableSpawns()
        {
            _controller.Queue(Request(threats: 2)); _controller.Begin(1);
            Assert.Throws<InvalidOperationException>(() => _controller.HunterSpawns("Hunter", new[] { Vector3.zero }));
            var spawns = _controller.HunterSpawns("Hunter", new[] { Vector3.zero, Vector3.one, Vector3.up });
            Assert.That(spawns.Count, Is.EqualTo(2));
            Assert.That(spawns[1].Position, Is.EqualTo(Vector3.one));
            Assert.That(spawns[1].ArchetypeKey, Is.EqualTo("Hunter"));
        }

        [Test]
        public void OnlyCurrentPlayerAndOneMatchingTerminalFactAreAccepted()
        {
            ReadyFloor();
            Assert.That(_controller.AcceptsGameplay(new EntityId(1)), Is.True);
            Assert.That(_controller.AcceptsGameplay(new EntityId(2)), Is.False);
            Assert.That(_controller.Resolve(SceneKey.TagArena), Is.False);
            Assert.That(_controller.Resolve(SceneKey.FloorLoop), Is.True);
            Assert.That(_controller.Resolve(SceneKey.FloorLoop), Is.False);
            Assert.That(_controller.AcceptsGameplay(new EntityId(1)), Is.False);
        }

        [Test]
        public void ClearSceneInvalidatesQueuedCallbackAndKeepsGenerationMonotonic()
        {
            _controller.Queue(Request(4)); _controller.ClearScene();
            Assert.That(_controller.Begin(4), Is.False);
            Assert.That(_state.Scene, Is.EqualTo(SceneKey.None));
            _controller.Bind(SceneKey.FloorLoop);
            Assert.That(_controller.Queue(Request(4)), Is.False);
            Assert.That(_controller.Queue(Request(5)), Is.True);
        }

        [Test]
        public void FailureCannotBecomeReadyAndNewGenerationCanRecover()
        {
            _controller.Queue(Request()); _controller.Begin(1);
            _controller.Fail("seed 17: blocked exit");
            Assert.That(_state.Failure, Does.Contain("blocked exit"));
            Assert.Throws<InvalidOperationException>(_controller.Ready);
            Assert.That(_controller.Queue(Request(2)), Is.True);
            Assert.That(_state.Failure, Is.Empty);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidEffectsAreRejectedWithoutAdvancingIdentity(float health)
        {
            var effects = new ProgressionEffects(1f, 1f, 1f, 1f, 100f, health, 1);
            Assert.Throws<ArgumentException>(() => _controller.Queue(new ProgressionGenerationRequest(1, 17, 1, false, effects)));
            Assert.That(_state.LastGenerationId, Is.Zero);
            Assert.That(_state.Phase, Is.EqualTo(ExpeditionAssemblyPhase.Waiting));
        }

        [Test]
        public void InvalidSpawnCoordinatesAreRejected()
        {
            _controller.Queue(Request()); _controller.Begin(1);
            Assert.Throws<ArgumentException>(() => _controller.PlayerSpawn("Player", new Vector3(float.NaN, 0f, 0f), Quaternion.identity));
            Assert.Throws<ArgumentException>(() => _controller.HunterSpawns("Hunter", new[] { new Vector3(0f, float.PositiveInfinity, 0f) }));
        }

        private void ReadyFloor()
        {
            _controller.Queue(Request()); _controller.Begin(1);
            _controller.RecordPlayer(new EntityId(1)); _controller.RecordHunter(new EntityId(-1)); _controller.Ready();
        }

        private static ProgressionGenerationRequest Request(int generationId = 1, bool shop = false, int threats = 1) =>
            new ProgressionGenerationRequest(generationId, 17, generationId, shop,
                new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 75f, threats));
    }
}
