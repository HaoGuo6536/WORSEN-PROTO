// ============================================================================
// HunterIntelligenceTests.cs
// ============================================================================
// PURPOSE:
//   Freezes retreat, stumble, goal, deliberation, prediction and memory contracts.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise controller decisions with explicit topology, observations and time.
//   - Keep seeded direction choices and repeated loss-search sequences reproducible.
// DEPENDENCIES:
//   - Hunter logic, Core values, Player/Level/Floor views, reflection and NUnit.
// USAGE NOTES:
//   No scene, physics, navigation bake or Unity tick. Coordinator runs Edit Mode.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterIntelligenceTests
    {
        private HunterProfile _profile;
        private HunterController _controller;
        private HunterBehaviorState _state;
        private PlayerBehaviorState _player;
        private FloorFixture _floor;
        private static readonly SightProbe Visible = new SightProbe(true, false, false);
        private sealed class LevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(new[] {
                new LevelRoom(1, new Vector3(0, 2, 0), new Vector3(8, 4, 8)),
                new LevelRoom(2, new Vector3(0, 2, 10), new Vector3(8, 4, 8)),
                new LevelRoom(3, new Vector3(10, 2, 0), new Vector3(8, 4, 8)),
                new LevelRoom(4, new Vector3(10, 2, 10), new Vector3(8, 4, 8)),
                new LevelRoom(5, new Vector3(0, 2, -20), new Vector3(8, 4, 8)) },
                new[] { new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 4, true),
                    new LevelEdge(3, 1, 3, true), new LevelEdge(4, 3, 4, true), new LevelEdge(5, 1, 5, true) },
                Array.Empty<LevelAnchor>(), 4, new Vector3(10, 0, 10));
        }
        private sealed class FloorFixture : IReadOnlyFloorState
        {
            public bool IsReady => true;
            public int CakeCount => 0;
            public int RequiredCakeCount => 2;
            public int GoldenCakeCount => 0;
            public ExitState ExitState { get; set; }
            public IReadOnlyDictionary<int, RoomPhase> RoomPhases { get; } = new Dictionary<int, RoomPhase>();
            public IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; set; } = Array.Empty<LevelAnchor>();
        }
        private void Tune(string field, object value)
            => typeof(HunterProfile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_profile, value);
        private T Memory<T>(string field)
            => (T)typeof(HunterBehaviorState).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_state);
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            Tune("_sensorIntervalTicks", 1);
            _state = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100f, SprintSpeed = 8f, Position = Vector3.forward * 10f };
            _floor = new FloorFixture();
            _controller = new HunterController(_state, _profile, new System.Random(11), _player, new LevelFixture());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
            _controller.SetFloorView(_floor);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        private static LevelAnchor Cake(int id, int room, Vector3 point) => new LevelAnchor(id, room, default, point);

        [Test] public void RetreatIsAppendedAndRequiresAnOccludedReachableRoomFartherFromObservedPrey()
        {
            Assert.That((int)HunterAction.Stalk, Is.EqualTo(9));
            Assert.That((int)HunterAction.Retreat, Is.EqualTo(10));
            _controller.Tick(Visible, 0.1f, 0);
            Assert.That(_state.CurrentAction, Is.Not.EqualTo(HunterAction.Retreat));
            Assert.That(_controller.RequestRetreat(Array.Empty<int>()), Is.False);
            Assert.That(_controller.RequestRetreat(new[] { 1, 2 }), Is.False);
            Assert.That(_controller.RequestRetreat(new[] { 5 }), Is.True);
            var result = _controller.Tick(Visible, 0.1f, 1);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Retreat));
            Assert.That(result.Target, Is.EqualTo(Vector3.back * 20f));
            Assert.That(result.Speed, Is.GreaterThan(0f));
            Assert.That(_state.PursuitSuppressed, Is.True);
            Assert.That(_state.PlayerVisible, Is.False);
            Assert.That(_state.BeliefConfidence, Is.Zero);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 1, 1, Vector3.one, 0f, 0f)), Is.False);
            _controller.CommitPose(result.Target, Vector3.zero, Vector3.back);
            _controller.Tick(default, 0.1f, 2);
            Assert.That(_state.PursuitSuppressed, Is.False);
            Assert.That(_state.CurrentAction, Is.Not.EqualTo(HunterAction.Retreat));
        }
        [TestCase(false)] [TestCase(true)]
        public void OnlyMissedLungeStumblesForwardForConfiguredInterval(bool hit)
        {
            _player.Position = Vector3.forward * 3f;
            _controller.Tick(Visible, 0.1f, 0);
            _controller.Tick(Visible, _profile.LungeWindupSeconds, 1);
            if (hit) Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.True);
            _controller.Tick(Visible, _profile.LungeActiveSeconds, 2);
            Vector3 total = Vector3.zero;
            _player.Position = Vector3.right * 10f;
            for (int i = 3; i <= 5; i++)
            {
                var result = _controller.Tick(default, 0.2f, i);
                total += result.StumbleDisplacement;
                Assert.That(result.ActiveContact, Is.False);
                Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.Recovery));
                Assert.That(result.StumbleDisplacement.magnitude, Is.EqualTo(hit ? 0f : _profile.MissStumbleMeters / 3f).Within(0.00001f));
            }
            Assert.That(total.z, Is.EqualTo(hit ? 0f : 0.8f).Within(0.00001f));
            Assert.That(total.x, Is.Zero);
            Assert.That(_controller.Tick(default, 0.1f, 6).StumbleDisplacement, Is.EqualTo(Vector3.zero));
        }
        [Test] public void OversizedAttackTickStillEmitsExactlyOneMissDisplacementBudget()
        {
            _player.Position = Vector3.forward * 3f; _controller.Tick(Visible, 0.1f, 0);
            var result = _controller.Tick(default, 2f, 1);
            Assert.That(result.StumbleDisplacement.magnitude, Is.EqualTo(0.8f).Within(0.00001f));
            Assert.That(result.ActiveContact, Is.False);
            Assert.That(_controller.Tick(default, 0.1f, 2).StumbleDisplacement, Is.EqualTo(Vector3.zero));
        }
        [Test] public void PatrolPrefersCakeThenInferredPickupThenOpenExit()
        {
            Tune("_cakeGoalUtility", 0f); Tune("_exitGoalUtility", 0f);
            _floor.ActiveCakeAnchors = new[] { Cake(10, 2, Vector3.forward * 10f) };
            Assert.That(_controller.Tick(default, 0.5f, 0).Target, Is.EqualTo(Vector3.forward * 10f));
            _floor.ActiveCakeAnchors = new[] { Cake(11, 3, Vector3.right * 10f) };
            Assert.That(_controller.Tick(default, 0.5f, 1).Target, Is.EqualTo(Vector3.forward * 10f));
            Assert.That(_state.LastPickupRoom, Is.EqualTo(2));
            _floor.ExitState = ExitState.Open;
            Assert.That(_controller.Tick(default, 0.5f, 2).Target, Is.EqualTo(new Vector3(10, 0, 10)));
        }
        [Test] public void UtilitySwitchWaitsForCommitmentThenChangesDirection()
        {
            _floor.ActiveCakeAnchors = new[] { Cake(10, 2, Vector3.forward * 10f) };
            var first = _controller.Tick(default, 0.1f, 0);
            Assert.That(_state.CurrentGoal, Is.EqualTo(HunterGoal.DenyCake));
            int replans = Memory<int>("ReplanCount");
            _floor.ExitState = ExitState.Open;
            Assert.That(_controller.Tick(default, 0.1f, 1).Target, Is.EqualTo(first.Target));
            Assert.That(Memory<int>("ReplanCount"), Is.EqualTo(replans));
            var switched = _controller.Tick(default, 0.4f, 2);
            Assert.That(_state.CurrentGoal, Is.EqualTo(HunterGoal.ProtectExit));
            Assert.That(switched.Target, Is.Not.EqualTo(first.Target));
            Assert.That(Memory<int>("ReplanCount"), Is.EqualTo(replans + 1));
        }
        [Test] public void HintPublishesOneDeliberationAndTurnsBeforeMovement()
        {
            _controller.Tick(default, 0.1f, 0);
            Assert.That(_controller.ReceiveHint(new HintPayload(_state.Id, _player.Id, 0, 0, Vector3.right * 3f, 0f, 0f)), Is.True);
            Assert.That(_controller.TryTakeDeliberation(out Vector3 candidate), Is.True);
            Assert.That(candidate, Is.EqualTo(Vector3.right * 3f));
            Assert.That(_controller.TryTakeDeliberation(out _), Is.False);
            for (int i = 1; i <= 5; i++)
            {
                var result = _controller.Tick(default, 0.1f, i);
                Assert.That(result.HoldPosition, Is.True); Assert.That(result.Speed, Is.Zero);
                Assert.That(result.DeliberationFacing.x, Is.GreaterThan(0f));
                _controller.CommitPose(Vector3.zero, Vector3.zero, result.DeliberationFacing);
            }
            Assert.That(_controller.Tick(default, 0.1f, 6).Speed, Is.GreaterThan(0f));
        }
        [Test] public void SeedElevenChoosesParallelCorridorInsteadOfDirectDoor()
        {
            _player.Position = new Vector3(10, 0, 10);
            var result = _controller.Tick(Visible, 0.1f, 0);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Chase));
            Assert.That(result.Target, Is.EqualTo(Vector3.right * 10f));
            Assert.That(Memory<List<int>>("PredictionRoute"), Is.EqualTo(new[] { 1, 3, 4 }));
        }
        [Test] public void TwoLossesRepeatExpansionDoorwayBeyondAndSingleReturnInOrder()
        {
            Tune("_beliefFreshSeconds", 0f); Tune("_deliberationSeconds", 0f); Tune("_lungeDistance", 1f);
            List<Vector3> first = null;
            for (int loss = 0; loss < 2; loss++)
            {
                long tick = loss * 100;
                _controller.CommitPose(Vector3.zero, Vector3.zero, Vector3.forward);
                _player.Position = Vector3.forward * 3f; _player.Velocity = Vector3.forward;
                _controller.Tick(Visible, 0.1f, tick);
                _player.Position = Vector3.forward * 10f; _controller.Tick(Visible, 0.1f, tick + 1);
                _controller.Tick(default, 0.1f, tick + 2);
                var route = new List<Vector3>(Memory<List<Vector3>>("SearchRoute"));
                Assert.That(route, Is.EqualTo(new[] { new Vector3(0, 0, 10), new Vector3(2, 0, 10),
                    new Vector3(-4, 0, 10), new Vector3(0, 0, 4), new Vector3(0, 0, 10), new Vector3(0, 0, 10) }));
                if (first != null) Assert.That(route, Is.EqualTo(first)); else first = route;
                foreach (var point in route)
                {
                    _controller.CommitPose(Vector3.back * 100f, Vector3.zero, Vector3.forward);
                    _controller.Tick(default, 0.1f, ++tick + 2);
                    Assert.That(_state.CurrentTarget, Is.EqualTo(point));
                    _controller.CommitPose(point, Vector3.zero, Vector3.forward);
                    _controller.Tick(default, 0.1f, ++tick + 2);
                }
                Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
                Assert.That(Memory<List<Vector3>>("SearchRoute"), Is.Empty);
            }
        }
        [TestCase(false, 0.079f, false, false)]
        [TestCase(false, 0.09f, true, false)]
        [TestCase(false, 0.1f, true, true)]
        [TestCase(true, 0.39f, true, false)]
        [TestCase(true, 0.9f, true, true)]
        public void SoundFacesBeforeThresholdDecisionAndExitIgnoresQuietSounds(bool exit, float loudness, bool audible, bool investigate)
        {
            if (exit) _floor.ExitState = ExitState.Open;
            _controller.Tick(default, 0.1f, 0);
            var noise = new NoiseEvent(_player.Id, Vector3.right * 2f, loudness, 0);
            Assert.That(_controller.HearNoise(noise, 1f), Is.EqualTo(audible));
            Assert.That(_state.BeliefConfidence, Is.Zero);
            if (audible) Assert.That(_controller.Tick(default, 0.1f, 1).HoldPosition, Is.True);
            for (int i = 2; i <= 7; i++) _controller.Tick(default, 0.1f, i);
            Assert.That(_state.BeliefConfidence > 0f, Is.EqualTo(investigate));
            if (exit && !investigate) Assert.That(_state.CurrentGoal, Is.EqualTo(HunterGoal.ProtectExit));
            Assert.That(_controller.HearNoise(noise, 1f), Is.False, "A delivered event is not replayed.");
        }
        [Test] public void ClosedDoorAttenuationChangesSoundAdmission()
        {
            _controller.Tick(default, 0.1f, 0);
            _controller.SetClosedDoors(new Dictionary<int, bool> { [1] = true });
            Assert.That(_controller.HearNoise(new NoiseEvent(_player.Id, Vector3.forward * 10f, 1f, 0), 1f), Is.False);
            _controller.SetClosedDoors(null);
            Assert.That(_controller.HearNoise(new NoiseEvent(_player.Id, Vector3.forward * 10f, 1f, 0), 1f), Is.True);
        }
    }
}
