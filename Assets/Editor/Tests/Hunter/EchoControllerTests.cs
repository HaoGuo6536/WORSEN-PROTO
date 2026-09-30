// ============================================================================
// EchoControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises recording order, replay pace, counterplay and facts with pure inputs.
//   The fixture acknowledges commanded points explicitly, separating game rules
//   from the coordinator's native navigation and physics integration checks.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Reproduce delayed corners, blocking, truncation, rejoining and effect caps.
//   - Verify bounded state, deterministic reset and the existing lunge/hit path.
// DEPENDENCIES:
//   - Hunter controllers, Core contracts, Player/Level/Floor views and NUnit.
// USAGE NOTES:
//   No scene simulation. Coordinator runs these Edit Mode tests after import.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Echo;
using Worsen.Domain.Player;
using Worsen.Domain.Level;
using Worsen.Domain.Floor;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class EchoControllerTests
    {
        private sealed class HunterView : IReadOnlyHunterState
        {
            public EntityId Id => new EntityId(-1);
            public EntityId TargetId => new EntityId(1);
            public Vector3 Position { get; set; }
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public bool PlayerVisible => false;
            public Vector3 LastKnownPosition => Vector3.zero;
            public long LastKnownTick => 0;
            public float BeliefConfidence => 1f;
            public long Tick => 0;
            public bool IsActive => true;
        }
        public sealed class World : IReadOnlyLevelState, IReadOnlyFloorState, IReadOnlyInteractableSet
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; set; } = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 1000f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            public int CakeCount => 0;
            public int RequiredCakeCount => 1;
            public int GoldenCakeCount => 0;
            public ExitState ExitState => ExitState.Open;
            public Dictionary<int, RoomPhase> Phases { get; } = new Dictionary<int, RoomPhase>();
            public IReadOnlyDictionary<int, RoomPhase> RoomPhases => Phases;
            public IReadOnlyList<LevelAnchor> ActiveCakeAnchors { get; set; } = Array.Empty<LevelAnchor>();
            public Dictionary<int, bool> Doors { get; } = new Dictionary<int, bool>();
            public InteractableState Door { get; set; }
            public bool TryGet(int id, out InteractableState state) { state = Door; return Door.Id == id; }
            public IReadOnlyList<InteractableState> InRoom(int room) => Door.Id > 0 && Door.RoomId == room ? new[] { Door } : Array.Empty<InteractableState>();
            public void TwoRooms(TraversalAccess access = TraversalAccess.All, bool bidirectional = true)
            {
                Graph = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, new Vector3(4, 10, 10)),
                    new LevelRoom(2, Vector3.right * 4, new Vector3(4, 10, 10)) },
                    new[] { new LevelEdge(7, 1, 2, bidirectional, access) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
            }
        }
        private EchoConfig _config;
        private EchoBehaviorState _state;
        private EchoController _echo;
        private HunterView _hunter;
        private PlayerBehaviorState _player;
        private World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(bool moving = true) => new HunterArchetypeContext(_hunter, _player, _world,
            _world, _world.Doors, _world, _effects, 1f, _tick, moving, 1f);
        public static void Tune(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EchoConfig>(); _state = new EchoBehaviorState();
            _hunter = new HunterView(); _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 };
            _world = new World(); _effects = default(ActiveEffects); _tick = 0;
            _echo = new EchoController(_state, _config); _echo.Reset(Context());
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_config); }
        private Vector3[] Advance(Vector3 player, bool acknowledge = true)
        {
            _player.Position = player; _tick++; _echo.Tick(Context());
            var points = new List<Vector3>(_echo.ReplayPath).ToArray();
            if (acknowledge)
            {
                if (points.Length > 0) _hunter.Position = points[points.Length - 1];
                _echo.CommitReplay(points.Length);
            }
            return points;
        }
        private List<HunterArchetypeFact> Facts()
        { var facts = new List<HunterArchetypeFact>(); while (_echo.TryTakeFact(out var fact)) facts.Add(fact); return facts; }
        private static ActiveEffects Effect(EffectId id, int stacks = 1, EffectKind kind = EffectKind.Curse)
            => new ActiveEffects(new[] { new ActiveEffect(id, kind, stacks) });
        [Test] public void FixedDelayReplaysEveryCornerAtRecordedPaceAndCursorNeverRewinds()
        {
            Vector3[] path = { Vector3.zero, Vector3.right, Vector3.right + Vector3.forward,
                Vector3.forward, Vector3.zero, Vector3.left };
            long cursor = 0;
            for (int i = 1; i <= 9; i++)
            {
                Vector3[] motion = Advance(path[Math.Min(i, path.Length - 1)]);
                if (i <= 4) Assert.That(motion, Is.Empty);
                else Assert.That(_hunter.Position, Is.EqualTo(path[i - 4]));
                Assert.That(_state.ConsumedSequence, Is.GreaterThanOrEqualTo(cursor)); cursor = _state.ConsumedSequence;
            }
            Assert.That(_echo.NeverLoses, Is.True);
        }
        [Test] public void ClosedDoorWaitsWithoutConsumingThenResumesRecordedSegment()
        {
            _world.TwoRooms(); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            _world.Doors[7] = true;
            Advance(Vector3.right * 4); // Last sample on the near side.
            long cursor = _state.ConsumedSequence;
            Assert.That(Advance(Vector3.right * 4), Is.Empty);
            Assert.That(_hunter.Position.x, Is.EqualTo(1)); Assert.That(_state.ConsumedSequence, Is.EqualTo(cursor));
            _world.Doors[7] = false;
            Advance(Vector3.right * 4); Assert.That(_hunter.Position.x, Is.EqualTo(3));
        }
        [Test] public void PlayerOnlyDropTruncatesAndLaterTrailThroughHeldPositionResumesWithoutDetour()
        {
            _world.TwoRooms(TraversalAccess.Player, false); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            Assert.That(_hunter.Position.x, Is.EqualTo(1));
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.ReplayTruncated), Is.True);
            Advance(Vector3.zero); Advance(Vector3.right); Advance(Vector3.zero);
            for (int i = 0; i < 7; i++) Advance(Vector3.zero);
            Assert.That(_hunter.Position, Is.EqualTo(Vector3.zero));
            Assert.That(_state.ConsumedSequence, Is.GreaterThan(7));
        }
        [Test] public void CollapsedRoomStopsAtLastRecordedPointBeforeBoundary()
        {
            _world.TwoRooms(); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            _world.Phases[2] = RoomPhase.Closed;
            for (int i = 0; i < 4; i++) Advance(Vector3.right * 4);
            Assert.That(_hunter.Position.x, Is.EqualTo(1));
            Assert.That(_state.ConsumedSequence, Is.EqualTo(1));
        }
        [Test] public void PhysicalRejectionDoesNotConsumeOrEmitAndRetryKeepsCorner()
        {
            for (int i = 1; i <= 4; i++) Advance(Vector3.right * i);
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.right * 5, 1, 5, NoiseSourceKind.Footstep) };
            Advance(Vector3.right * 5, false); _echo.CommitReplay(0);
            Assert.That(_state.ConsumedSequence, Is.Zero); Assert.That(Facts(), Is.Empty);
            Vector3[] retry = Advance(Vector3.right * 6);
            Assert.That(retry[0], Is.EqualTo(Vector3.right));
        }
        [Test] public void ShorterDelayAndFasterPlaybackUseCappedEffectsAndNeverSkipCorners()
        {
            _effects = Effect(EchoController.ShorterDelay, 100);
            _echo.Tick(Context());
            Assert.That(_echo.EffectiveDelay, Is.EqualTo(4f * Mathf.Pow(.75f, 3)).Within(.00001f));
            _effects = Effect(EchoController.FasterPlayback);
            _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right + Vector3.forward); Advance(Vector3.forward); Advance(Vector3.zero);
            Vector3[] points = Advance(Vector3.left);
            Assert.That(_echo.EffectivePlayback, Is.EqualTo(1.25f));
            Assert.That(points.Length, Is.EqualTo(2)); Assert.That(points[0], Is.EqualTo(Vector3.right));
            Assert.That(points[1], Is.EqualTo(Vector3.right + Vector3.forward * .25f));
            _effects = default(ActiveEffects); Advance(Vector3.left * 2);
            Assert.That(_echo.EffectiveDelay, Is.EqualTo(4f)); Assert.That(_echo.EffectivePlayback, Is.EqualTo(1f));
        }
        [Test] public void FactsDelayDetuneAndSilenceStepsAndReplayDoorHabitExactlyOnce()
        {
            _world.TwoRooms(); _world.Door = new InteractableState(9, InteractableKind.Door, 1, Vector3.right * 2, InteractableStateValue.Open, 7);
            _effects = Effect(EchoController.SilentSteps); _echo.Reset(Context());
            Advance(Vector3.right);
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.right * 3, 1, 2, NoiseSourceKind.Footstep) };
            Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            Assert.That(Facts(), Is.Empty);
            Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            var facts = Facts();
            var step = facts.Find(f => f.Kind == HunterArchetypeFactKind.ReplayedFootstep);
            Assert.That(step.RecordedTick, Is.EqualTo(2)); Assert.That(step.Tick, Is.EqualTo(6));
            Assert.That(step.Gain, Is.EqualTo(.5f)); Assert.That(step.Pitch, Is.EqualTo(.94f));
            Assert.That(facts.Find(f => f.Kind == HunterArchetypeFactKind.ReplayedDoorPassage).ObjectId, Is.EqualTo(9));
            Advance(Vector3.right * 4); Assert.That(Facts(), Is.Empty);
        }
        [Test] public void TrailReaderIsLookBackEdgeTriggeredAndSnapshotSurvivesFutureTicks()
        {
            _effects = Effect(EchoController.TrailReader, 1, EffectKind.Upgrade);
            Advance(Vector3.right); _player.LookBack = true; Advance(Vector3.right * 2);
            var trail = Facts().Find(f => f.Kind == HunterArchetypeFactKind.TrailRevealed);
            Assert.That(trail.Path.Count, Is.EqualTo(3)); Assert.That(trail.Duration, Is.EqualTo(2));
            Advance(Vector3.right * 3); Assert.That(Facts(), Is.Empty);
            Assert.That(trail.Path.Count, Is.EqualTo(3)); Assert.That(trail.Path[2], Is.EqualTo(Vector3.right * 2));
        }
        [Test] public void OverrunIsBoundedDoesNotTeleportAndResetIsDeterministic()
        {
            Tune(_config, "_sampleCapacity", 8); _echo.Reset(Context());
            for (int i = 1; i <= 24; i++) Advance(Vector3.right * i, false);
            Assert.That(_state.RecordedCount, Is.EqualTo(8)); Assert.That(_hunter.Position, Is.EqualTo(Vector3.zero));
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.RecordingOverrun), Is.True);
            Vector3 Run()
            {
                _tick = 0; _player.Position = _hunter.Position = Vector3.zero; _echo.Reset(Context());
                for (int i = 1; i <= 6; i++) Advance(Vector3.right * i);
                return _hunter.Position;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
        [Test] public void BacktrackingIntoEchoUsesSharedCommittedLungeAndOneAcceptedHit()
        {
            var profile = ScriptableObject.CreateInstance<HunterProfile>();
            try
            {
                Tune(profile, "_chaseSpeedMultiplier", 1f); Tune(profile, "_sensorIntervalTicks", 1);
                var state = new HunterBehaviorState();
                var shared = new HunterController(state, profile, new System.Random(19), _player, _world, _echo);
                shared.Reset(new EntityId(-3), Vector3.zero, Vector3.forward);
                for (int i = 1; i <= 40; i++) { _player.Position = Vector3.right * i; shared.Tick(default, 1f, i); }
                Assert.That(state.BeliefConfidence, Is.EqualTo(1f)); Assert.That(state.IsDeliberating, Is.False);
                _player.Position = Vector3.right * .5f;
                var result = shared.Tick(new SightProbe(true, true, true), .1f, 41);
                Assert.That(result.BeginLunge, Is.True); Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.Windup));
                shared.Tick(new SightProbe(true, true, true), profile.LungeWindupSeconds, 42);
                Assert.That(shared.TryAcceptContact(_player.Id, out HunterHit hit), Is.True);
                Assert.That(hit.Hunter, Is.EqualTo(state.Id)); Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
                Assert.That(state.LossSeconds, Is.EqualTo(float.PositiveInfinity));
                Assert.That(shared.ApplyMutation(new HunterMutation(HunterTunable.LossSeconds, 1f, "bad-loss"), out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        [Test] public void MidReplayShorterDelayConsumesEveryInterveningSample()
        {
            for (int i = 1; i <= 6; i++) Advance(Vector3.right * i);
            _effects = Effect(EchoController.ShorterDelay);
            Vector3[] points = Advance(Vector3.right * 7);
            Assert.That(points, Is.EqualTo(new[] { Vector3.right * 3, Vector3.right * 4 }));
            Assert.That(_state.ReplayedTime, Is.EqualTo(_state.RecordedTime - 3));
        }
        [Test] public void EngineRejectedTraversalTruncatesButLaterReachableRecordingCanRejoin()
        {
            for (int i = 1; i <= 4; i++) Advance(Vector3.right * i);
            Advance(Vector3.right * 5, false); _echo.CommitReplay(0, true);
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.ReplayTruncated), Is.True);
            Advance(Vector3.zero); Advance(Vector3.right);
            for (int i = 0; i < 6; i++) Advance(Vector3.right);
            Assert.That(_hunter.Position, Is.EqualTo(Vector3.right));
            Assert.That(_state.ConsumedSequence, Is.GreaterThanOrEqualTo(7));
        }
        [Test] public void DuplicateInjectedTickDoesNotRecordOrAdvanceTimeTwice()
        {
            Advance(Vector3.right);
            int count = _state.RecordedCount; double time = _state.RecordedTime;
            _echo.Tick(Context());
            Assert.That(_state.RecordedCount, Is.EqualTo(count)); Assert.That(_state.RecordedTime, Is.EqualTo(time));
        }
    }
}
