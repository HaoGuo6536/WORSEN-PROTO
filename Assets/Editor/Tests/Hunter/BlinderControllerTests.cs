// ============================================================================
// BlinderControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises Blinder sweeps, contact admission and Floor/audio facts from values.
//   Native casts remain a coordinator gate; these tests cannot manufacture a clear
//   shot from sight alone or mutate the shared config while applying a curse.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Prove fresh fixed-line warnings, one-time duration hits and neutral trap hooks.
// DEPENDENCIES:
//   - Hunter rules, Core values, existing world fixture, NUnit and Unity test assets.
// USAGE NOTES:
//   Explicit time; shared pursuit receives seeded randomness. No scene simulation.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Blinder;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class BlinderControllerTests
    {
        public sealed class HunterView : IReadOnlyHunterPursuitState
        {
            public EntityId Id { get; set; } = new EntityId(-1);
            public EntityId TargetId => new EntityId(1);
            public Vector3 Position { get; set; }
            public Vector3 Velocity => Vector3.zero;
            public Vector3 Forward => Vector3.forward;
            public bool PlayerVisible { get; set; } = true;
            public Vector3 LastKnownPosition { get; set; } = Vector3.forward * 8f;
            public long LastKnownTick => 0;
            public float BeliefConfidence => 1f;
            public long Tick => 0;
            public bool IsActive { get; set; } = true;
            public float LossSeconds => 2.5f;
            public float LossDistance => 14f;
            public bool PursuitSuppressed { get; set; }
        }
        private BlinderConfig config;
        private HunterProfile profile;
        private BlinderController module;
        private HunterView hunter;
        private PlayerBehaviorState player;
        private EchoControllerTests.World world;
        private IReadOnlyActiveEffects effects;
        private long tick;
        private HunterArchetypeContext Context(float dt = .1f, bool allowed = true) => new HunterArchetypeContext(
            hunter, player, world, world, world.Doors, world, effects, dt, tick, allowed, 1.05f);
        [SetUp] public void Setup()
        {
            config = ScriptableObject.CreateInstance<BlinderConfig>(); profile = ScriptableObject.CreateInstance<HunterProfile>();
            hunter = new HunterView(); player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, Position = Vector3.forward * 8f, SprintSpeed = 8f };
            world = new EchoControllerTests.World(); effects = default(ActiveEffects); tick = 0;
            module = new BlinderController(new BlinderBehaviorState(), config, profile); module.Reset(Context());
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(config); Object.DestroyImmediate(profile); }
        private void Step(float dt = .1f, bool clear = true, long? observed = null, float? radius = null, WeaverShotSpot[] spots = null, bool allowed = true)
        {
            tick++; module.Observe(new WeaverObservation(observed ?? tick, hunter.Position + Vector3.up, module.Aim(1f),
                radius ?? module.Radius, clear, true, spots)); module.Tick(Context(dt, allowed));
        }
        private List<BlinderSoundFact> Sounds()
        { var result = new List<BlinderSoundFact>(); while (module.TakeSound(out var fact)) result.Add(fact); return result; }
        [Test] public void ClearReachableRepositionIsRequiredBeforeFreshWarning()
        {
            Step(clear: false, spots: new[] { new WeaverShotSpot(Vector3.right, false, true),
                new WeaverShotSpot(Vector3.left, true, false), new WeaverShotSpot(Vector3.right * 2f, true, true) });
            Assert.That(module.Action, Is.EqualTo(BlinderAction.Reposition)); Assert.That(module.Fire || module.Warning, Is.False);
            Assert.That(module.TryMovement(out var target, out var speed), Is.True); Assert.That(speed, Is.EqualTo(profile.InvestigateSpeed));
            Assert.That(target, Is.EqualTo(Vector3.right * 2f)); hunter.Position = target;
            Step(); Assert.That(module.Warning, Is.True); Assert.That(module.AllowSharedAttack, Is.False);
            Assert.That(Sounds().Find(f => f.Sound == BlinderSound.ThrowHiss).Duration, Is.EqualTo(config.WarningSeconds));
        }
        [TestCase(false, 1, .06f)] [TestCase(true, 0, .06f)] [TestCase(true, 1, .09f)]
        public void BlockedStaleOrWrongRadiusSweepCannotThrow(bool clear, long observed, float radius)
        { Step(2f, clear, observed, radius); Assert.That(module.Warning || module.Fire, Is.False); }
        [Test] public void WarningCancelsOnObstructionOrMovedMuzzleAndDoesNotHome()
        {
            Step(); Vector3 target = module.Target; player.Position = Vector3.right * 10f;
            Step(.69f); Assert.That(module.Target, Is.EqualTo(target)); Assert.That(module.Fire, Is.False);
            Step(.01f, clear: false); Assert.That(module.Fire || module.Warning, Is.False);
            Step(); hunter.Position = Vector3.right; Step(1f); Assert.That(module.Fire || module.Warning, Is.False);
        }
        [Test] public void ConfirmedProjectileHitsOnceWithDurationAndRejectsForeignAndExpiredContacts()
        {
            Step(); Step(config.WarningSeconds); Assert.That(module.Fire, Is.True); module.CommitLaunch(true);
            Assert.That(module.TakeThrow(out var shot), Is.True); Assert.That(shot.Serial, Is.EqualTo(1));
            Assert.That(module.TryHit(new EntityId(99), shot.Serial, tick, out _), Is.False);
            Assert.That(module.TryHit(player.Id, shot.Serial, tick, out var hit), Is.True);
            Assert.That(hit.Duration, Is.EqualTo(3f)); Assert.That(hit.MuffledDark || hit.Trap, Is.False);
            Assert.That(module.TryHit(player.Id, shot.Serial, tick, out _), Is.False);
            Step(4f); Step(config.WarningSeconds); module.CommitLaunch(true); Step(2f, clear: false);
            Assert.That(module.TryHit(player.Id, module.Serial, tick, out _), Is.False);
        }
        [Test] public void FailedLaunchDuplicateTickAndResetCannotInventHits()
        {
            Step(); module.Tick(Context(10f)); Assert.That(module.Fire, Is.False);
            Step(config.WarningSeconds); module.CommitLaunch(false);
            Assert.That(module.TakeThrow(out _), Is.False); Assert.That(module.TryHit(player.Id, 1, tick, out _), Is.False);
            Step(4f); Step(config.WarningSeconds); module.CommitLaunch(true); module.Reset(Context());
            Assert.That(module.TryHit(player.Id, 1, tick, out _), Is.False); Assert.That(module.TakeThrow(out _), Is.False);
        }
        [Test] public void TrapPolicyCursesAndRealTrapTickAreNeutralThenCappedAndSilent()
        {
            Step(clear: false); Assert.That(module.TakeTrapPolicy(out var neutral), Is.True);
            Assert.That(neutral.MoreTrapsStacks, Is.Zero); Assert.That(neutral.SilentTraps || neutral.MuffledDark, Is.False);
            Sounds(); module.ReportTrapTick(Vector3.left, tick); var sound = Sounds()[0];
            Assert.That(sound.Sound, Is.EqualTo(BlinderSound.TrapTick)); Assert.That(sound.Position, Is.EqualTo(Vector3.left));
            Assert.That(sound.Noise.SourceKind, Is.EqualTo(NoiseSourceKind.Trap));
            effects = new ActiveEffects(new[] { new ActiveEffect(BlinderController.MoreTraps, EffectKind.Curse, 99),
                new ActiveEffect(BlinderController.LongerDark, EffectKind.Curse, 99), new ActiveEffect(BlinderController.MuffledDark, EffectKind.Curse, 1),
                new ActiveEffect(BlinderController.SilentTraps, EffectKind.Curse, 1) });
            module.SetEffects(effects); Assert.That(module.TrapPolicy.MoreTrapsStacks, Is.EqualTo(3), "Pre-tick floor assembly snapshot");
            Step(); Assert.That(module.TakeTrapPolicy(out var policy), Is.True);
            Assert.That(policy.Duration, Is.EqualTo(3f * Mathf.Pow(1.5f, 3))); Assert.That(policy.SilentTraps && policy.MuffledDark, Is.True);
            Assert.That(Sounds().Exists(f => f.Sound == BlinderSound.ThrowHiss), Is.True);
            module.ReportTrapTick(Vector3.left, tick); Assert.That(Sounds(), Is.Empty);
            Step(config.WarningSeconds); module.CommitLaunch(true); Assert.That(module.TryHit(player.Id, module.Serial, tick, out var hit), Is.True);
            Assert.That(hit.Duration, Is.EqualTo(policy.Duration)); Assert.That(hit.MuffledDark, Is.True);
            Assert.That(config.BlindSeconds, Is.EqualTo(3f)); Step(); Assert.That(module.TakeTrapPolicy(out _), Is.False);
        }
        [Test] public void CatchIsOnceOnlyAndRetreatCancelsTheThrow()
        {
            Step(); hunter.PursuitSuppressed = true; Step(1f); Assert.That(module.Fire || module.Warning, Is.False);
            Sounds(); module.BeginCatch(); module.BeginCatch(); Assert.That(Sounds().FindAll(f => f.Sound == BlinderSound.Catch).Count, Is.EqualTo(1));
        }
        [Test] public void IndependentWeaponSeamPreservesSharedDefaultLunge()
        {
            player.Position = Vector3.forward; var state = new HunterBehaviorState();
            var shared = new HunterController(state, profile, new System.Random(7), player, world);
            shared.Reset(hunter.Id, Vector3.zero, Vector3.forward);
            Assert.That(shared.Tick(new SightProbe(true, true, true), .1f, 1).BeginLunge, Is.True);
        }
    }
}
