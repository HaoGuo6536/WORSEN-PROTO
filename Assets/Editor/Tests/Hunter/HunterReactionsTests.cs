// ============================================================================
// HunterReactionsTests.cs
// ============================================================================
// PURPOSE:
//   Exercises shared interruptions without running a scene or a physics clock.
//   Explicit route evidence distinguishes a jam on the path from a nearby door.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify belief retention, momentum removal, break acknowledgement and Wick.
// DEPENDENCIES:
//   - HunterController, Core snapshots, existing world fixture and NUnit.
// USAGE NOTES:
//   Coordinator executes Edit Mode tests; all time and randomness are injected.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class HunterReactionsTests
    {
        public sealed class World : IReadOnlyHunterWorldView
        {
            public bool Lit, Known = true;
            public List<HunterDoorJam> Jams = new List<HunterDoorJam>();
            public bool TryGetRoomLit(int room, out bool lit) { lit = Lit; return Known; }
            public IReadOnlyList<HunterDoorJam> JammedDoors => Jams;
        }
        private HunterProfile _profile;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private World _world;
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            EchoControllerTests.Tune(_profile, "_sensorIntervalTicks", 1);
            _state = new HunterBehaviorState(); _world = new World();
            _controller = new HunterController(_state, _profile, new System.Random(7),
                new PlayerBehaviorState { Id = new EntityId(1), Position = Vector3.forward * 10, Health = 100, SprintSpeed = 8 },
                new EchoControllerTests.World());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward); _controller.SetWorldView(_world);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_profile);
        [Test] public void StunHoldsForFullDurationWithoutForgettingAndWeakHitIsShorter()
        {
            _controller.Tick(new SightProbe(true, true, true), .1f, 1);
            Vector3 belief = _state.LastKnownPosition; float confidence = _state.BeliefConfidence;
            _controller.ApplyStun(1f, 1f);
            for (int i = 2; i <= 5; i++)
            {
                var result = _controller.Tick(default, .25f, i);
                Assert.That(result.HoldPosition, Is.True); Assert.That(result.ActiveContact, Is.False);
                Assert.That(_state.LastKnownPosition, Is.EqualTo(belief)); Assert.That(_state.BeliefConfidence, Is.EqualTo(confidence));
            }
            _controller.Tick(default, .1f, 6); Assert.That(_controller.ReactionHeld, Is.False);
            _controller.ApplyStun(1f, .25f); _controller.Tick(default, .25f, 7);
            _controller.Tick(default, .1f, 8); Assert.That(_controller.ReactionHeld, Is.False);
        }
        [Test] public void SlipRemovesMomentumAndBlocksContactAndResetClearsReactions()
        {
            _controller.CommitPose(Vector3.zero, Vector3.forward * 8, Vector3.forward);
            _controller.ApplySlip(.5f); Assert.That(_state.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(_controller.Tick(default, .5f, 1).HoldPosition, Is.True);
            Assert.That(_controller.TryAcceptContact(new EntityId(1), out _), Is.False);
            _controller.Reset(new EntityId(-2), Vector3.zero, Vector3.forward);
            _controller.Tick(default, .1f, 2); Assert.That(_controller.ReactionHeld, Is.False);
        }
        [Test] public void OnlyCrossingNearbyJamDelaysAndCompletesOnceUntilWorldAcknowledges()
        {
            _world.Jams.Add(new HunterDoorJam(3, 9, new Bounds(Vector3.forward, Vector3.one), 1f));
            Assert.That(_controller.BlockJammedPath(new[] { Vector3.zero, Vector3.right * 5 }, .5f), Is.False);
            var path = new[] { Vector3.zero, Vector3.forward * 5 };
            Assert.That(_controller.BlockJammedPath(path, .5f), Is.True); Assert.That(_controller.TryTakeDoorBreak(out _), Is.False);
            Assert.That(_controller.BlockJammedPath(path, .5f), Is.True); Assert.That(_controller.TryTakeDoorBreak(out var fact), Is.True);
            Assert.That(fact.DoorId, Is.EqualTo(3)); Assert.That(fact.Revision, Is.EqualTo(9));
            Assert.That(_controller.BlockJammedPath(path, 10f), Is.True); Assert.That(_controller.TryTakeDoorBreak(out _), Is.False);
            _world.Jams.Clear(); Assert.That(_controller.BlockJammedPath(path, .1f), Is.False);
        }
        [Test] public void WickRaisesSightWithoutMutatingProfileAndInvalidReactionsAreIgnored()
        {
            _controller.SetWickActive(true); Assert.That(_controller.EffectiveSightRange, Is.EqualTo(_profile.SightRange * 1.3f));
            _controller.SetWickActive(false); Assert.That(_controller.EffectiveSightRange, Is.EqualTo(_profile.SightRange));
            _controller.ApplyStun(float.NaN, 1); _controller.ApplySlip(float.PositiveInfinity);
            _controller.Tick(default, .1f, 1); Assert.That(_controller.ReactionHeld, Is.False);
        }
        [Test] public void ReplacementJamRestartsWorkAndPartialApproachCanBreakWithoutCrossing()
        {
            var path = new[] { Vector3.zero, Vector3.forward * .25f };
            var bounds = new Bounds(Vector3.forward, Vector3.one);
            _world.Jams.Add(new HunterDoorJam(3, 1, bounds, 1f));
            Assert.That(_controller.BlockJammedPath(path, .75f, Vector3.forward * 5), Is.True);
            _world.Jams[0] = new HunterDoorJam(3, 2, bounds, 1f);
            Assert.That(_controller.BlockJammedPath(path, .5f, Vector3.forward * 5), Is.True);
            Assert.That(_controller.TryTakeDoorBreak(out _), Is.False);
            _controller.ApplyStun(1f, 1f); _controller.Tick(default, 1f, 1);
            _controller.BlockJammedPath(path, 1f); Assert.That(_controller.TryTakeDoorBreak(out _), Is.False);
            _controller.Tick(default, .1f, 2); _controller.BlockJammedPath(path, .5f);
            Assert.That(_controller.TryTakeDoorBreak(out var fact), Is.True); Assert.That(fact.Revision, Is.EqualTo(2));
        }
        private sealed class RecordingRules : Worsen.Domain.Hunter.Archetypes.Default.DefaultHunterController
        {
            public int Samples;
            public bool CanReplay;
            public override void Tick(HunterArchetypeContext context) { Samples++; CanReplay = context.CanReplay; }
        }
        [Test] public void StunContinuesArchetypeObservationButCannotAuthorizeReplay()
        {
            var module = new RecordingRules();
            var shared = new HunterController(new HunterBehaviorState(), _profile, new System.Random(8),
                new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 }, new EchoControllerTests.World(), module);
            shared.Reset(new EntityId(-2), Vector3.zero, Vector3.forward); shared.ApplyStun(1f, 1f);
            Assert.That(shared.Tick(default, .5f, 1).HoldPosition, Is.True);
            Assert.That(module.Samples, Is.EqualTo(1)); Assert.That(module.CanReplay, Is.False);
        }
    }
}
