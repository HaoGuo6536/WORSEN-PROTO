// ============================================================================
// TickingControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the spring, key, guidance and catalogue rules without a running scene.
//   Shared-controller cases prove dormancy blocks committed attacks, not just sight.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise injected time/randomness, per-parameter effects and winding transitions.
// DEPENDENCIES:
//   - Hunter rules, Core contracts, existing read-only world fixture and NUnit.
// USAGE NOTES:
//   Coordinator runs in Edit Mode. Navigation evidence is injected here; separate
//   integration tests cover native reachable-ground admission and trigger routing.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class TickingControllerTests
    {
        private TickingConfig _config;
        private HunterProfile _profile;
        private TickingController _clock;
        private HunterBehaviorState _hunter;
        private PlayerBehaviorState _player;
        private EchoControllerTests.World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(float dt) => new HunterArchetypeContext(_hunter, _player, _world, _world,
            _world.Doors, _world, _effects, dt, _tick, true, _profile.ChaseSpeedMultiplier);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<TickingConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _hunter = new HunterBehaviorState();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8f };
            _world = new EchoControllerTests.World(); _effects = default(ActiveEffects); _tick = 0;
            _clock = new TickingController(_config, new System.Random(19));
            new HunterController(_hunter, _profile, new System.Random(7), _player, _world, _clock)
                .Reset(new EntityId(-1), Vector3.back * 15f, Vector3.forward);
        }
        [TearDown] public void TearDown()
        { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_profile); }
        private void Advance(float dt) { _tick++; _clock.Tick(Context(dt)); }
        private List<TickingSoundFact> Sounds()
        { var facts = new List<TickingSoundFact>(); while (_clock.TakeSound(out var fact)) facts.Add(fact); return facts; }
        private void Effect(EffectId id, int count = 1)
        { _effects = new ActiveEffects(new[] { new ActiveEffect(id, EffectKind.Curse, count) }); Advance(.001f); }
        private void Place()
        { Assert.That(_clock.PlaceKey(_player.Position + Vector3.right * _clock.EffectiveKeyDistance.x, true), Is.True); }
        [Test] public void TickSlowsThenStopsOnceAtZeroAndNeverRestartsWithoutAKey()
        {
            Advance(44f); Assert.That(_clock.Dormant, Is.True);
            var facts = Sounds(); float previous = 0;
            foreach (var fact in facts)
            { Assert.That(fact.Sound, Is.EqualTo(TickingSound.Tick)); Assert.That(fact.Interval, Is.GreaterThan(previous)); previous = fact.Interval; }
            Assert.That(facts.Count, Is.GreaterThan(1));
            Advance(1f); Assert.That(_clock.Dormant, Is.False);
            facts = Sounds(); Assert.That(facts.FindAll(f => f.Sound == TickingSound.Stop).Count, Is.EqualTo(1));
            Assert.That(facts.FindAll(f => f.Sound == TickingSound.Wake).Count, Is.EqualTo(1));
            Advance(90f); Assert.That(Sounds(), Is.Empty);
        }
        [Test] public void DormantTargetStaysInRearBandAndSharedPursuitNeverAttacks()
        {
            var shared = new HunterController(_hunter, _profile, new System.Random(7), _player, _world, _clock);
            shared.Reset(new EntityId(-1), Vector3.forward, Vector3.back);
            for (int i = 1; i <= 44; i++)
            {
                _player.HeadingDegrees = i * 7;
                var result = shared.Tick(new SightProbe(true, true, true), 1f, i);
                Assert.That(Vector3.Distance(result.Target, _player.Position), Is.InRange(12f, 18f));
                Vector3 forward = Quaternion.Euler(0, _player.HeadingDegrees, 0) * Vector3.forward;
                Assert.That(Vector3.Dot(result.Target - _player.Position, forward), Is.LessThan(0f));
                Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None)); Assert.That(result.ActiveContact, Is.False);
                Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
                Assert.That(_hunter.PursuitSuppressed, Is.True); Assert.That(_hunter.PlayerVisible, Is.False);
            }
        }
        [Test] public void WakeUsesSharedLungeAndKeyImmediatelyCancelsActiveContactAndPursuit()
        {
            EchoControllerTests.Tune(_profile, "_sensorIntervalTicks", 1);
            var shared = new HunterController(_hunter, _profile, new System.Random(7), _player, _world, _clock);
            shared.Reset(new EntityId(-1), Vector3.back, Vector3.forward);
            var result = shared.Tick(new SightProbe(true, true, true), 45f, 1);
            Assert.That(result.BeginLunge, Is.True); Assert.That(_hunter.PursuitSuppressed, Is.False);
            shared.Tick(new SightProbe(true, true, true), _profile.LungeWindupSeconds, 2);
            Assert.That(_hunter.LungePhase, Is.EqualTo(HunterLungePhase.Active));
            Place(); Assert.That(_clock.TakeKey(_player.Id, _clock.KeySerial), Is.True);
            // Contact gate must be safe even before the Manager's refresh command.
            Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
            shared.RefreshDormancy(); Assert.That(_hunter.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(_hunter.PursuitSuppressed, Is.True); Assert.That(_clock.SpringFraction, Is.EqualTo(1f));
            Assert.That(_hunter.LossSeconds, Is.EqualTo(_profile.LossSeconds)); Assert.That(_hunter.LossDistance, Is.EqualTo(_profile.LossDistance));
            Assert.That(shared.Effective(HunterTunable.ChaseSpeedMultiplier), Is.EqualTo(_profile.ChaseSpeedMultiplier));
        }
        [Test] public void TimedKeyIsOneAtATimeAndGuidanceNeverUsesWhiteArrow()
        {
            Advance(29f); Assert.That(_clock.KeyDue, Is.False); Advance(1f); Assert.That(_clock.KeyDue, Is.True);
            Assert.That(_clock.PlaceKey(Vector3.right * 8, false), Is.False);
            Assert.That(_clock.PlaceKey(Vector3.right * 5, true), Is.False);
            Assert.That(_clock.PlaceKey(Vector3.right * 11, true), Is.False);
            Place(); int serial = _clock.KeySerial;
            Assert.That(_clock.Guidance.Kind, Is.EqualTo(GuidanceKind.ThreatArrow));
            Assert.That(_clock.Guidance.EntityId, Is.EqualTo(_hunter.Id)); Assert.That(_clock.Guidance.IsFallback, Is.True);
            Assert.That(_clock.Guidance.TargetPosition, Is.EqualTo(_clock.KeyPosition));
            Advance(60); Assert.That(_clock.KeyDue, Is.False); Assert.That(_clock.PlaceKey(Vector3.right * 8, true), Is.False);
            Assert.That(_clock.TakeKey(new EntityId(99), serial), Is.False); Assert.That(_clock.TakeKey(_player.Id, serial + 1), Is.False);
            Assert.That(_clock.TakeKey(_player.Id, serial), Is.True); Assert.That(_clock.TakeKey(_player.Id, serial), Is.False);
            Advance(29); Assert.That(_clock.KeyDue, Is.False); Advance(1); Assert.That(_clock.KeyDue, Is.True);
        }
        [TestCase("ticking-runs-faster", 23.04f, 6f, 10f)]
        [TestCase("ticking-farther-keys", 45f, 11.71875f, 19.53125f)]
        [TestCase("spare-key", 45f, 3.9f, 6.5f)]
        [TestCase("ticking-loud-keys", 45f, 6f, 10f)]
        [TestCase("ticking-double-spring", 45f, 6f, 10f)]
        public void EachHookChangesOnlyItsOwnParametersAndCapsStacks(string id, float seconds, float minimum, float maximum)
        {
            Effect(new EffectId(id), 99);
            Assert.That(_clock.EffectiveSpringSeconds, Is.EqualTo(seconds).Within(.0001f));
            Assert.That(_clock.EffectiveKeyDistance.x, Is.EqualTo(minimum).Within(.0001f));
            Assert.That(_clock.EffectiveKeyDistance.y, Is.EqualTo(maximum).Within(.0001f));
            Assert.That(_config.KeySeconds, Is.EqualTo(30f)); Assert.That(_profile.ChaseSpeedMultiplier, Is.EqualTo(1.12f));
            Assert.That(_config.SpringSeconds, Is.EqualTo(45f)); Assert.That(_config.KeyDistance, Is.EqualTo(new Vector2(6, 10)));
        }
        [Test] public void DoubleSpringRequiresTwoKeysEvenWhenFirstHalfRunsOut()
        {
            Effect(TickingController.DoubleSpring); Advance(45); Place();
            Assert.That(_clock.TakeKey(_player.Id, _clock.KeySerial), Is.True);
            Assert.That(_clock.SpringFraction, Is.EqualTo(.5f)); Assert.That(_clock.Dormant, Is.True);
            Advance(30); Assert.That(_clock.Dormant, Is.False); Place();
            Assert.That(_clock.TakeKey(_player.Id, _clock.KeySerial), Is.True); Assert.That(_clock.SpringFraction, Is.EqualTo(1f));
        }
        [Test] public void LoudKeysPublishesOneNoiseAtCollectionAndNeutralKeysDoNot()
        {
            Advance(30); Place(); _clock.TakeKey(_player.Id, _clock.KeySerial); Assert.That(_clock.TakeNoise(out _), Is.False);
            Effect(TickingController.LoudKeys); Advance(30); Place(); Vector3 position = _clock.KeyPosition;
            Assert.That(_clock.TakeKey(_player.Id, _clock.KeySerial), Is.True);
            Assert.That(_clock.TakeNoise(out NoiseEvent noise), Is.True); Assert.That(noise.Source, Is.EqualTo(_player.Id));
            Assert.That(noise.Position, Is.EqualTo(position)); Assert.That(noise.Tick, Is.EqualTo(_tick));
            Assert.That(noise.Loudness, Is.EqualTo(_config.LoudKeyLoudness)); Assert.That(_clock.TakeNoise(out _), Is.False);
        }
        [Test] public void IdenticalInjectedInputsProduceIdenticalCandidatesMotionAndIntervals()
        {
            var other = new TickingController(_config, new System.Random(19)); other.Reset(Context(0));
            for (int i = 0; i < 100; i++)
            {
                _tick++; var context = Context(.2f); _clock.Tick(context); other.Tick(context);
                Vector3 candidate = _clock.KeyCandidate(); Assert.That(other.KeyCandidate(), Is.EqualTo(candidate));
                Assert.That(Vector3.Distance(candidate, _player.Position), Is.InRange(6f, 10f));
                _clock.TryMovement(out Vector3 a, out float sa); other.TryMovement(out Vector3 b, out float sb);
                Assert.That(a, Is.EqualTo(b)); Assert.That(sa, Is.EqualTo(sb)); Assert.That(_clock.TickInterval, Is.EqualTo(other.TickInterval));
            }
            float before = _clock.SpringFraction; _clock.Tick(Context(10)); Assert.That(_clock.SpringFraction, Is.EqualTo(before));
            _tick++; _clock.Tick(Context(float.NaN)); Assert.That(_clock.SpringFraction, Is.EqualTo(before));
        }
        [Test] public void ClosedRoomInvalidatesKeyAndFailedPlacementHasBoundedRetryDelay()
        {
            Advance(30); Place(); _world.Phases[1] = RoomPhase.Closed; Advance(.1f);
            Assert.That(_clock.HasKey, Is.False); Assert.That(_clock.KeyDue, Is.False);
            Advance(1f); Assert.That(_clock.PlaceKey(Vector3.right * 8, true), Is.False);
            _clock.DeferPlacement(); Assert.That(_clock.KeyDue, Is.False);
        }
        [Test] public void DifferentInstancesDoNotShareSpringOrKeys()
        {
            var other = new TickingController(_config, new System.Random(20)); other.Reset(Context(0));
            Advance(30); Place(); Assert.That(other.HasKey, Is.False); Assert.That(other.SpringFraction, Is.EqualTo(1f));
        }
        [Test] public void TickIntervalsAreIndependentOfFramePartition()
        {
            Advance(20f); var coarse = Sounds();
            var fine = new TickingController(_config, new System.Random(19)); fine.Reset(Context(0));
            for (int i = 0; i < 200; i++) { _tick++; fine.Tick(Context(.1f)); }
            var intervals = new List<float>(); while (fine.TakeSound(out var fact)) intervals.Add(fact.Interval);
            Assert.That(intervals.Count, Is.EqualTo(coarse.Count));
            for (int i = 0; i < coarse.Count; i++) Assert.That(intervals[i], Is.EqualTo(coarse[i].Interval).Within(.00001f));
            Assert.That(fine.SpringFraction, Is.EqualTo(_clock.SpringFraction).Within(.00001f));
        }
        [Test] public void FollowProbeRejectsFrontOrUnreachableTargetsAndHoldsInsteadOfTeleporting()
        {
            Assert.That(_clock.SetFollowTarget(Vector3.forward * 15, true), Is.False);
            Assert.That(_clock.SetFollowTarget(Vector3.back * 15, false), Is.False);
            _clock.TryMovement(out var held, out var speed);
            Assert.That(held, Is.EqualTo(_hunter.Position)); Assert.That(speed, Is.Zero);
            Assert.That(_clock.SetFollowTarget(Vector3.back * 16, true), Is.True);
            _clock.TryMovement(out var rear, out _); Assert.That(rear, Is.EqualTo(Vector3.back * 16));
        }
        [Test] public void DormantThresholdHabitPausesOnceThenResumesFollowing()
        {
            _world.TwoRooms();
            EchoControllerTests.Tune(_profile, "_habits", new[] { new HunterHabitData(HunterHabitKind.ThresholdPause) });
            var shared = new HunterController(_hunter, _profile, new System.Random(7), _player, _world, _clock);
            shared.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
            shared.Tick(default, .1f, 1);
            shared.CommitPose(Vector3.right * 4, Vector3.zero, Vector3.forward);
            var paused = shared.Tick(default, .1f, 2);
            Assert.That(paused.HoldPosition, Is.True); Assert.That(shared.TryTakeHabit(out var habit), Is.True);
            Assert.That(habit.Kind, Is.EqualTo(HunterHabitKind.ThresholdPause));
            var resumed = shared.Tick(default, .5f, 3);
            Assert.That(resumed.HoldPosition, Is.False); Assert.That(shared.TryTakeHabit(out _), Is.False);
        }
    }
}
