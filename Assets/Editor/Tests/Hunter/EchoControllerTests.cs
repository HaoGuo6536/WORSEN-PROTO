// ============================================================================
// EchoControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises exact delayed poses, activation and contact with managed inputs.
//   Explicit config values avoid native object construction; the integration
//   fixture separately verifies that the real body applies these commands.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Reproduce delayed corners/heights/facing, ghost traversal and effect caps.
//   - Verify fixed-rate/ring-wrap playback, reset and ordinary Player hit grace without lunges.
// DEPENDENCIES:
//   - Hunter controllers, Core contracts, Player/Level/Floor views and NUnit.
// USAGE NOTES:
//   No scene simulation. Coordinator runs these Edit Mode tests after import.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
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
            _config = (EchoConfig)FormatterServices.GetUninitializedObject(typeof(EchoConfig));
            Tune(_config, "_delaySeconds", 4f); Tune(_config, "_sampleCapacity", 4096);
            Tune(_config, "_footstepGain", 1f); Tune(_config, "_footstepPitch", .94f);
            Tune(_config, "_trailSeconds", 2f); Tune(_config, "_trailPointLimit", 256);
            Tune(_config, "_shorterDelayMultiplier", .75f); Tune(_config, "_fasterPlaybackMultiplier", 1.25f);
            Tune(_config, "_silentStepsMultiplier", .5f); Tune(_config, "_curseStackCap", 3);
            _state = new EchoBehaviorState();
            _hunter = new HunterView(); _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 };
            _world = new World(); _effects = default(ActiveEffects); _tick = 0;
            _echo = new EchoController(_state, _config); _echo.Reset(Context());
        }
        private Vector3[] Advance(Vector3 player)
        {
            _player.Position = player; _tick++; _echo.Tick(Context());
            var points = new List<Vector3>();
            foreach (var pose in _echo.ReplayPoses) points.Add(pose.Position);
            if (_echo.ReplayActive) _hunter.Position = _echo.ReplayPose.Position;
            return points.ToArray();
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
                if (i < 4) Assert.That(motion, Is.Empty);
                else Assert.That(_hunter.Position, Is.EqualTo(path[i - 4]));
                Assert.That(_state.ConsumedSequence, Is.GreaterThanOrEqualTo(cursor)); cursor = _state.ConsumedSequence;
            }
            Assert.That(_echo.NeverLoses, Is.True);
        }
        [Test] public void ClosedDoorNeverHoldsOrDivertsReplay()
        {
            _world.TwoRooms(); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            _world.Doors[7] = true;
            Advance(Vector3.right * 4); // Last sample on the near side.
            Assert.That(Advance(Vector3.right * 4), Is.EqualTo(new[] { Vector3.right * 3 }));
            Assert.That(_state.ConsumedSequence, Is.EqualTo(2));
            Advance(Vector3.right * 4); Assert.That(_hunter.Position.x, Is.EqualTo(4));
        }
        [Test] public void PlayerOnlyOneWayDropIsMirroredWithoutTruncation()
        {
            _world.TwoRooms(TraversalAccess.Player, false); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            Assert.That(_hunter.Position.x, Is.EqualTo(3));
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.ReplayTruncated), Is.False);
            Advance(Vector3.zero); Advance(Vector3.right); Advance(Vector3.zero);
            for (int i = 0; i < 7; i++) Advance(Vector3.zero);
            Assert.That(_hunter.Position, Is.EqualTo(Vector3.zero));
            Assert.That(_state.ConsumedSequence, Is.GreaterThan(7));
        }
        [Test] public void CollapsedRoomDoesNotAlterRecordedPoses()
        {
            _world.TwoRooms(); _echo.Reset(Context());
            Advance(Vector3.right); Advance(Vector3.right * 3); Advance(Vector3.right * 4); Advance(Vector3.right * 4);
            _world.Phases[2] = RoomPhase.Closed;
            for (int i = 0; i < 4; i++) Advance(Vector3.right * 4);
            Assert.That(_hunter.Position.x, Is.EqualTo(4));
            Assert.That(_state.ConsumedSequence, Is.EqualTo(4));
        }
        [Test] public void OffTrailSpawnIsAbsentUntilDelayThenAppearsAtPlayerSpawn()
        {
            _hunter.Position = Vector3.one * 100; _player.Position = new Vector3(2, 3, 4);
            _player.HeadingDegrees = 123f; _echo.Reset(Context());
            Assert.That(_echo.ReplayActive || _echo.ContactReady, Is.False);
            for (int i = 1; i < 4; i++)
            {
                Assert.That(Advance(Vector3.right * i), Is.Empty);
                Assert.That(_echo.ReplayActive || _echo.ContactReady, Is.False);
                Assert.That(_hunter.Position, Is.EqualTo(Vector3.one * 100));
            }
            Advance(Vector3.right * 4);
            Assert.That(_echo.ReplayActive && _echo.ContactReady, Is.True);
            Assert.That(_hunter.Position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(_echo.ReplayPose.HeadingDegrees, Is.EqualTo(123f));
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
        [Test] public void FactsDelayDetuneAndSilenceStepsExactlyOnceWithoutDoorHabits()
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
            Assert.That(facts.Exists(f => f.Kind == HunterArchetypeFactKind.ReplayedDoorPassage), Is.False);
            Advance(Vector3.right * 4); Assert.That(Facts(), Is.Empty);
        }
        [Test] public void TrailReaderIsLookBackEdgeTriggeredAndSnapshotSurvivesFutureTicks()
        {
            _effects = Effect(EchoController.TrailReader, 1, EffectKind.Upgrade);
            for (int i = 1; i <= 4; i++) Advance(Vector3.right * i);
            _player.LookBack = true; Advance(Vector3.right * 5);
            var trail = Facts().Find(f => f.Kind == HunterArchetypeFactKind.TrailRevealed);
            Assert.That(trail.Path.Count, Is.EqualTo(5)); Assert.That(trail.Duration, Is.EqualTo(2));
            Advance(Vector3.right * 6); Assert.That(Facts(), Is.Empty);
            Assert.That(trail.Path.Count, Is.EqualTo(5)); Assert.That(trail.Path[4], Is.EqualTo(Vector3.right * 5));
        }
        [Test] public void UndersizedRingHidesRatherThanSubstitutingAnIncorrectTimestamp()
        {
            Tune(_config, "_sampleCapacity", 4); _echo.Reset(Context());
            for (int i = 1; i <= 24; i++) Advance(Vector3.right * i);
            Assert.That(_state.RecordedCount, Is.EqualTo(4)); Assert.That(_hunter.Position, Is.EqualTo(Vector3.zero));
            Assert.That(_echo.ReplayActive || _echo.ContactReady, Is.False);
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.RecordingOverrun), Is.True);
            Tune(_config, "_sampleCapacity", 8);
            Vector3 Run()
            {
                _tick = 0; _player.Position = _hunter.Position = Vector3.zero; _echo.Reset(Context());
                for (int i = 1; i <= 6; i++) Advance(Vector3.right * i);
                return _hunter.Position;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
        [Test] public void ContactUsesPlayerGraceWithoutLungeAndReplayContinuesAfterHit()
        {
            var profile = HunterAttackControllerTests.Profile(); Tune(profile, "_lungeDamage", 10);
            var playerProfile = (PlayerProfile)FormatterServices.GetUninitializedObject(typeof(PlayerProfile));
            Tune(playerProfile, "_hitGraceSeconds", 2f);
            _player.MaxHealth = 100; _player.RecoveryTickSeconds = 1f;
            var player = new PlayerController(_player, playerProfile, new System.Random(17));
            var state = new HunterBehaviorState();
            var shared = new HunterController(state, profile, new System.Random(19), _player, _world, _echo);
            shared.Reset(new EntityId(-3), Vector3.one * 100, Vector3.forward);
            for (int i = 1; i <= 4; i++)
            {
                _player.Tick = i;
                _player.Position = i == 4 ? Vector3.zero : Vector3.right * i;
                var result = shared.Tick(new SightProbe(true, true, true), 1f, i);
                Assert.That(result.BeginLunge || result.ActiveContact, Is.False);
                Assert.That(state.IsActive, Is.EqualTo(i == 4));
                if (i < 4) Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
            }
            shared.CommitPose(_echo.ReplayPose.Position, Vector3.zero, Vector3.forward);
            Assert.That(shared.TryAcceptContact(new EntityId(99), out _), Is.False);
            Assert.That(shared.TryAcceptContact(_player.Id, out HunterHit hit), Is.True);
            Assert.That(hit.Source, Is.EqualTo(HitSource.Other)); Assert.That(hit.Damage, Is.EqualTo(10));
            Assert.That(player.ApplyHit(hit.Damage, hit.Severity).Changed, Is.True);
            Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False, "Duplicate collider in the same tick.");
            shared.SetCatchActive(true); shared.ApplyStun(10f, 1f); shared.ApplySlip(10f);
            _player.Position = Vector3.right; player.AdvanceRecovery(5);
            shared.Tick(default, 1f, 5); shared.CommitPose(_echo.ReplayPose.Position, Vector3.zero, Vector3.forward);
            Assert.That(state.Position, Is.EqualTo(Vector3.right));
            Assert.That(state.CatchActive || shared.ReactionHeld, Is.False);
            Assert.That(shared.TryAcceptContact(_player.Id, out hit), Is.True);
            Assert.That(player.ApplyHit(hit.Damage, hit.Severity).AbsorbedByGrace, Is.True);
            Assert.That(_player.Health, Is.EqualTo(90f));
            player.AdvanceRecovery(_player.GraceWindow.EndTick);
            _player.Position = Vector3.right * 2;
            shared.Tick(default, 1f, 6); shared.CommitPose(_echo.ReplayPose.Position, Vector3.zero, Vector3.forward);
            Assert.That(state.Position, Is.EqualTo(Vector3.right * 2));
            Assert.That(shared.TryAcceptContact(_player.Id, out hit), Is.True);
            Assert.That(player.ApplyHit(hit.Damage, hit.Severity).Changed, Is.True);
            Assert.That(_player.Health, Is.EqualTo(80f));
            Assert.That(state.BeliefConfidence, Is.Zero); Assert.That(state.PlayerVisible || state.IsDeliberating, Is.False);
            Assert.That(HunterAttackControllerTests.Get<int>(state, "ReplanCount"), Is.Zero);
            Assert.That(state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(shared.ShouldProbe(6), Is.False); Assert.That(shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void MidReplayShorterDelayConsumesEveryInterveningSample()
        {
            for (int i = 1; i <= 6; i++) Advance(Vector3.right * i);
            _effects = Effect(EchoController.ShorterDelay);
            Vector3[] points = Advance(Vector3.right * 7);
            Assert.That(points, Is.EqualTo(new[] { Vector3.right * 3, Vector3.right * 4 }));
            Assert.That(_state.ReplayedTime, Is.EqualTo(_state.RecordedTime - 3));
        }
        [Test] public void VaultStairsAndDropPreserveEveryHeightAndFacing()
        {
            Vector3[] poses = { Vector3.zero, new Vector3(1, 2, 0), new Vector3(2, 1, 0),
                new Vector3(3, 1.5f, 0), new Vector3(3, -2, 0), new Vector3(1, -2, 0) };
            float[] headings = { 0, 90, 180, 270, 20, 350 };
            for (int i = 1; i <= 9; i++)
            {
                int record = Math.Min(i, poses.Length - 1);
                _player.HeadingDegrees = headings[record]; Advance(poses[record]);
                if (i < 4) continue;
                Assert.That(_echo.ReplayPose.Position, Is.EqualTo(poses[i - 4]));
                Assert.That(_echo.ReplayPose.HeadingDegrees, Is.EqualTo(headings[i - 4]));
            }
        }
        [Test] public void DuplicateInjectedTickDoesNotRecordOrAdvanceTimeTwice()
        {
            Advance(Vector3.right);
            int count = _state.RecordedCount; double time = _state.RecordedTime;
            _echo.Tick(Context());
            Assert.That(_state.RecordedCount, Is.EqualTo(count)); Assert.That(_state.RecordedTime, Is.EqualTo(time));
        }
        [TestCase(1f / 60f, 240)]
        [TestCase(.02f, 200)]
        [TestCase(.125f, 32)]
        public void FixedRateRecordingKeepsFourSecondDelayAcrossRingWraps(float dt, int delayTicks)
        {
            Tune(_config, "_sampleCapacity", 256); _echo.Reset(Context());
            var poses = new List<Vector3> { Vector3.zero };
            var headings = new List<float> { 0f };
            for (int i = 1; i <= 1200; i++)
            {
                _player.Position = new Vector3(i * .01f, (i % 120) * .01f, (i % 80) * .01f);
                _player.HeadingDegrees = i % 360;
                poses.Add(_player.Position); headings.Add(_player.HeadingDegrees);
                _echo.Tick(new HunterArchetypeContext(_hunter, _player, _world, _world,
                    _world.Doors, _world, _effects, dt, i, true, 1f));
                Assert.That(_echo.ReplayActive, Is.EqualTo(i >= delayTicks));
                if (i < delayTicks) continue;
                Assert.That(Vector3.Distance(_echo.ReplayPose.Position, poses[i - delayTicks]), Is.LessThan(.00001f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(_echo.ReplayPose.HeadingDegrees, headings[i - delayTicks])), Is.LessThan(.0001f));
                Assert.That(_state.RecordedTime - _state.ReplayedTime, Is.EqualTo(4).Within(.0000001));
            }
            Assert.That(_state.RecordedCount, Is.EqualTo(256));
            Assert.That(Facts().Exists(f => f.Kind == HunterArchetypeFactKind.RecordingOverrun), Is.False);
        }
        [Test] public void JammedDoorCannotHoldSharedReplayOrPublishABreak()
        {
            var shared = new HunterController(new HunterBehaviorState(), HunterAttackControllerTests.Profile(),
                new System.Random(1), _player, _world, _echo);
            shared.Reset(new EntityId(-2), Vector3.one * 100, Vector3.forward);
            var world = new HunterReactionsTests.World();
            world.Jams.Add(new HunterDoorJam(7, 1, new Bounds(Vector3.right * 2, Vector3.one), 10f));
            shared.SetWorldView(world); shared.SetClosedDoors(new Dictionary<int, bool> { [7] = true });
            for (int i = 1; i <= 7; i++)
            {
                _player.Position = Vector3.right * i;
                shared.Tick(new SightProbe(true, true, true), 1f, i);
                Assert.That(shared.BlockJammedPath(new[] { Vector3.zero, Vector3.right * 5 }, 1f), Is.False);
                Assert.That(shared.TryTakeDoorBreak(out _), Is.False);
                if (i >= 4) Assert.That(_echo.ReplayPose.Position, Is.EqualTo(Vector3.right * (i - 4)));
            }
        }
        [Test] public void FractionalDelayInterpolatesHeightAndHeadingAcrossWrap()
        {
            Tune(_config, "_delaySeconds", 1.5f); _echo.Reset(Context());
            _player.HeadingDegrees = 350; Advance(new Vector3(1, 2, 0));
            _player.HeadingDegrees = 10; Advance(new Vector3(2, 0, 0));
            Advance(new Vector3(30, 0, 0));
            Assert.That(_echo.ReplayPose.Position, Is.EqualTo(new Vector3(1.5f, 1, 0)));
            Assert.That(Mathf.DeltaAngle(_echo.ReplayPose.HeadingDegrees, 0), Is.Zero.Within(.0001f));
            Assert.That(_state.ReplayedTime, Is.EqualTo(1.5));
        }
        [Test] public void FasterPlaybackConsumesOnlyRecordedSamplesAndRemovalNeverRewinds()
        {
            _effects = Effect(EchoController.FasterPlayback, 100); _echo.Reset(Context());
            Assert.That(_echo.EffectivePlayback, Is.EqualTo(Mathf.Pow(1.25f, 3)));
            for (int i = 1; i <= 40; i++)
            {
                _player.HeadingDegrees = i * 7; Advance(new Vector3(i, i % 3, 0));
                Assert.That(_state.ReplayedTime, Is.LessThanOrEqualTo(_state.RecordedTime));
                foreach (var pose in _echo.ReplayPoses) Assert.That(pose.Position.x, Is.LessThanOrEqualTo(i));
            }
            Assert.That(_state.ReplayedTime, Is.EqualTo(_state.RecordedTime));
            Assert.That(_echo.ReplayPose.Position, Is.EqualTo(_player.Position));
            Assert.That(_echo.ReplayPose.HeadingDegrees, Is.EqualTo(_player.HeadingDegrees));
            _effects = default(ActiveEffects); double previous = _state.ReplayedTime;
            for (int i = 41; i <= 46; i++)
            {
                Advance(Vector3.right * i);
                Assert.That(_state.ReplayedTime, Is.GreaterThanOrEqualTo(previous)); previous = _state.ReplayedTime;
            }
            Assert.That(_state.ReplayedTime, Is.EqualTo(_state.RecordedTime - 4));
        }
        [Test] public void ShorterDelaySpawnsEarlierAndSilentStepsCapsGain()
        {
            _effects = new ActiveEffects(new[] { new ActiveEffect(EchoController.ShorterDelay, EffectKind.Curse, 1),
                new ActiveEffect(EchoController.SilentSteps, EffectKind.Curse, 100) });
            _echo.Reset(Context());
            _player.RecentNoises = new[] { new NoiseEvent(_player.Id, Vector3.right, 1, 1, NoiseSourceKind.Footstep) };
            Advance(Vector3.right); Advance(Vector3.right * 2); Assert.That(_echo.ReplayActive, Is.False);
            Advance(Vector3.right * 3); Assert.That(_echo.ReplayActive, Is.True);
            Assert.That(_echo.ReplayPose.Position, Is.EqualTo(Vector3.zero));
            Advance(Vector3.right * 4);
            Assert.That(Facts().Find(f => f.Kind == HunterArchetypeFactKind.ReplayedFootstep).Gain, Is.EqualTo(.125f));
        }
        [Test] public void NeutralReplayIgnoresSharedSpeedAndReactionPermission()
        {
            for (int i = 1; i <= 6; i++)
            {
                _player.Position = Vector3.right * i;
                _echo.Tick(new HunterArchetypeContext(_hunter, _player, null, null, null, null, _effects,
                    1f, i, false, 3f, new[] { new Bounds(Vector3.zero, Vector3.one * 100) }));
            }
            Assert.That(_echo.EffectivePlayback, Is.EqualTo(1));
            Assert.That(_echo.ReplayPose.Position, Is.EqualTo(Vector3.right * 2));
        }
        [Test] public void ZeroDelayUsesLatestRecordedPoseAndDeadPlayerCannotContact()
        {
            Tune(_config, "_delaySeconds", 0f); _echo.Reset(Context());
            Assert.That(_echo.ReplayActive, Is.True);
            Advance(Vector3.up * 2); Assert.That(_echo.ReplayPose.Position, Is.EqualTo(Vector3.up * 2));
            _player.Health = 0; Advance(Vector3.one);
            Assert.That(_echo.ReplayActive || _echo.ContactReady, Is.False);
        }
        [Test] public void ReplayRejectsRevivalContactsWithoutSpendingAdmission()
        {
            var shared = new HunterController(new HunterBehaviorState(), HunterAttackControllerTests.Profile(),
                new System.Random(1), _player, _world, _echo);
            shared.Reset(new EntityId(-2), Vector3.one * 100, Vector3.forward);
            shared.Tick(default, 4f, 4);
            HunterAttackControllerTests.Set(_player, "RevivalCollisionEndTick", 1L);
            Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
            Assert.That(_echo.ContactReady, Is.True);
            _player.Tick = 1; Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.True);
        }
    }
}
