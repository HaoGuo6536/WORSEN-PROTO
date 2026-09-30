// ============================================================================
// FloorHandControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies hand warnings, agency during grabs, damage cadence and lethal confirmation.
//   Explicit observations and elapsed time keep room hazards reproducible.
//   Room-local ownership prevents effects or contacts leaking across portals.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) Â· Domain Â· Floor.
// KEY RESPONSIBILITIES:
//   - Keep collapse presentation aligned with the staged gameplay hazard.
//   - Preserve one escape opportunity and exactly one hit per committed grab.
//   - Verify normal Player damage, outward throws, depth-scaled springs and one-shot wards.
//   - Verify Low Profile rejects new warnings and releases active contacts without consuming a ward.
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
//   - Player Controller, BehaviorState and Profile only for ordinary damage assertions.
// USAGE NOTES:
//   Scene-owned through FloorManager/FloorDriver. Time is supplied by the owner.
//   No persistent singleton, global settings, or independent update loop.
// ============================================================================
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Domain.Floor;
using Worsen.Domain.Player;
namespace Worsen.Tests.Floor
{
    public sealed class FloorHandControllerTests
    {
        private FloorHandController _controller;
        private readonly EntityId _player = new EntityId(1);
        private FloorHandProbe Near => new FloorHandProbe(3, 7, new Vector3(0f, 0f, 2f), 0.5f, true, Vector3.back);
        private sealed class EffectView : IReadOnlyPlayerEffectState
        { public bool IsUngrabbable { get; set; } }

        [TestCase(false)] [TestCase(true)]
        public void LowProfileReleasesWarningOrGrabExactlyOnce(bool grabbed)
        {
            Step(0f, 1);
            if (grabbed) Step(0.7f, 2);
            var effects = new EffectView { IsUngrabbable = true };
            Assert.That(_controller.Tick(_player, true, Near, 20f, 3, out var fact, effects), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(CollapseHandEventKind.Released));
            Assert.That(fact.SlowMultiplier, Is.EqualTo(1f));
            Assert.That(fact.Damage, Is.Zero);
            Assert.That(_controller.Target(_player, out _, out _), Is.False);
            Assert.That(_controller.Tick(_player, true, Near, 20f, 4, out _, effects), Is.False);
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 3, out _), Is.False);
        }

        [Test]
        public void LowProfileRejectsNewContactAndDoesNotConsumeWaxWard()
        {
            var effects = new EffectView { IsUngrabbable = true };
            _controller.ArmWaxWard(_player);
            Assert.That(_controller.Tick(_player, true, Near, 20f, 1, out _, effects), Is.False);
            Assert.That(_controller.Target(_player, out _, out _), Is.False);
            effects.IsUngrabbable = false;
            Assert.That(_controller.Tick(_player, true, Near, 0f, 2, out var fact, effects), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(CollapseHandEventKind.Warning));
            Assert.That(Step(0.7f, 3).Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
        }
        [SetUp] public void Setup()
        {
            var config = (FloorConfig)FormatterServices.GetUninitializedObject(typeof(FloorConfig));
            _controller = new FloorHandController(new FloorHandBehaviorState(), config);
        }
        private CollapseHandFact Step(float dt, long tick, FloorHandProbe? probe = null, bool alive = true)
        { Assert.That(_controller.Tick(_player, alive, probe ?? Near, dt, tick, out var fact), Is.True); return fact; }
        [Test] public void LargeFirstObservationOnlyWarnsWithoutImmediateSlowOrDamage()
        {
            var warning = Step(30f, 1);
            Assert.That(warning.Kind, Is.EqualTo(CollapseHandEventKind.Warning));
            Assert.That(warning.Damage, Is.Zero); Assert.That(warning.SlowMultiplier, Is.EqualTo(1f));
            Assert.That(_controller.Tick(_player, true, Near, 0.69f, 2, out _), Is.False);
        }
        [Test] public void WarningThenGrabGrantsFullGraceBeforeNormalDamage()
        {
            Step(0f, 1);
            var grab = Step(0.7f, 2);
            Assert.That(grab.Kind, Is.EqualTo(CollapseHandEventKind.Grabbed));
            Assert.That(grab.SlowMultiplier, Is.InRange(0.15f, 0.9f));
            Assert.That(_controller.Tick(_player, true, Near, 1.39f, 3, out _), Is.False);
            var hit = Step(0.02f, 4);
            Assert.That(hit.Kind, Is.EqualTo(CollapseHandEventKind.Hit));
            Assert.That(hit.Damage, Is.EqualTo(25f)); Assert.That(hit.SlowMultiplier, Is.EqualTo(1f));
            Assert.That(hit.ThrowVelocity, Is.EqualTo(Vector3.back * 8f));
            Assert.That(hit.Severity, Is.EqualTo(HitSeverity.Light)); Assert.That(hit.Source, Is.EqualTo(HitSource.Hand));
        }
        [Test] public void MovingAwayDuringGrabEscapesAndDoesNotDamage()
        {
            Step(0f, 1); Step(0.7f, 2);
            var escape = Step(1.4f, 3, new FloorHandProbe(3, 7, Vector3.zero, 2.2f, true));
            Assert.That(escape.Kind, Is.EqualTo(CollapseHandEventKind.Escaped)); Assert.That(escape.Damage, Is.Zero);
            Assert.That(_controller.Tick(_player, true, Near, 0.1f, 4, out _), Is.False);
        }
        [Test] public void ObstructionOrLeavingRoomReleasesGrabInsteadOfGrabbingThroughWalls()
        {
            Step(0f, 1); Step(0.7f, 2);
            Assert.That(Step(0.1f, 3, default(FloorHandProbe)).Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
        }
        [Test] public void TargetHandCannotJumpToAnotherNearbyHandDuringGrace()
        {
            Step(0f, 1); Step(0.7f, 2);
            Assert.That(_controller.Target(_player, out var room, out var hand), Is.True);
            Assert.That(room, Is.EqualTo(3)); Assert.That(hand, Is.EqualTo(7));
            Assert.That(Step(0.1f, 3, new FloorHandProbe(3, 8, Vector3.zero, 0.1f, true)).Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
        }
        [Test] public void HitHasCooldownAndMustWarnAgainBeforeAnotherHit()
        {
            Step(0f, 1); Step(0.7f, 2); Step(1.4f, 3);
            Assert.That(_controller.Tick(_player, true, Near, 2f, 4, out _), Is.False);
            Assert.That(Step(0f, 5).Kind, Is.EqualTo(CollapseHandEventKind.Warning));
        }
        [Test] public void OnlySameTickLethalHitCanBeConfirmedAndOnlyOnce()
        {
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 1, out _), Is.False);
            Step(0f, 1); Step(0.7f, 2); Step(1.4f, 3);
            Assert.That(_controller.ConfirmDeath(_player, 3, true, 3, out _), Is.False);
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 4, out _), Is.False);
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 3, out var fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(CollapseHandEventKind.Consumed));
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 3, out _), Is.False);
        }
        [Test] public void WaxWardCancelReleasesAndEnforcesCooldown()
        {
            Step(0f, 1); Step(0.7f, 2);
            Assert.That(_controller.Cancel(_player, 2, out var fact), Is.True);
            Assert.That(fact.Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
            Assert.That(_controller.Cancel(_player, 2, out _), Is.False);
            Assert.That(_controller.Tick(_player, true, Near, 0.5f, 3, out _), Is.False);
        }
        [Test] public void OtherPlayersHaveIndependentSingleGrabAndResetForgetsAll()
        {
            Step(0f, 1);
            Assert.That(_controller.Tick(new EntityId(2), true, Near, 0f, 1, out var other), Is.True);
            Assert.That(other.Kind, Is.EqualTo(CollapseHandEventKind.Warning));
            _controller.Reset();
            Assert.That(_controller.Target(_player, out _, out _), Is.False);
            Assert.That(Step(0f, 2).Kind, Is.EqualTo(CollapseHandEventKind.Warning));
        }
        [Test] public void DeathDuringUnrelatedCombatClearsSlowWithoutFogConsumption()
        {
            Step(0f, 1); Step(0.7f, 2);
            Assert.That(Step(0f, 3, alive:false).Kind, Is.EqualTo(CollapseHandEventKind.Released));
            Assert.That(_controller.ConfirmDeath(_player, 3, false, 3, out _), Is.False);
        }
        [Test]
        public void ArmedWaxWardBreaksExactlyOneGrabAndDefaultsOff()
        {
            Assert.That(_controller.ArmWaxWard(_player), Is.True);
            Assert.That(_controller.ArmWaxWard(_player), Is.False, "Arming twice does not stack wards.");
            Step(0f, 1);
            var escaped = Step(0.7f, 2);
            Assert.That(escaped.Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
            Assert.That(escaped.Damage, Is.Zero); Assert.That(escaped.ThrowVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_controller.Tick(_player, true, Near, 2f, 3, out _), Is.False);
            Step(0f, 4);
            Assert.That(Step(0.7f, 5).Kind, Is.EqualTo(CollapseHandEventKind.Grabbed));
            Assert.That(Step(1.4f, 6).Kind, Is.EqualTo(CollapseHandEventKind.Hit));
        }
        [Test]
        public void BoundarySpringGrowsWithDepthAndContactStartsWarning()
        {
            var shallow = new FloorHandProbe(3, 1, Vector3.zero, 0f, true, Vector3.right, 0.2f, true);
            var deep = new FloorHandProbe(3, 1, Vector3.zero, 0f, true, Vector3.right, 1f, true);
            Assert.That(_controller.BoundaryAcceleration(shallow).x, Is.GreaterThan(0f));
            Assert.That(_controller.BoundaryAcceleration(deep).x, Is.GreaterThan(_controller.BoundaryAcceleration(shallow).x));
            Assert.That(_controller.BoundaryAcceleration(Near), Is.EqualTo(Vector3.zero));
            Assert.That(Step(0f, 1, shallow).Kind, Is.EqualTo(CollapseHandEventKind.Warning));
        }
        [Test]
        public void MovingAlongBoundaryEscapesTheOriginalGrabRadius()
        {
            var contact = new FloorHandProbe(3, 1, Vector3.zero, 0f, true, Vector3.right, playerPosition: Vector3.zero);
            Step(0f, 1, contact); Step(0.7f, 2, contact);
            var moved = new FloorHandProbe(3, 1, Vector3.forward * 3f, 0f, true, Vector3.right,
                playerPosition: Vector3.forward * 3f);
            Assert.That(Step(0.1f, 3, moved).Kind, Is.EqualTo(CollapseHandEventKind.Escaped));
        }
        [TestCase(26f, false)] [TestCase(25f, true)] [TestCase(10f, true)]
        public void HandKillsOnlyThroughOrdinaryDamage(float health, bool dies)
        {
            Step(0f, 1); Step(0.7f, 2); var hit = Step(1.4f, 3);
            var state = new PlayerBehaviorState { Health = health, MaxHealth = 100f };
            var profile = (PlayerProfile)FormatterServices.GetUninitializedObject(typeof(PlayerProfile));
            var player = new PlayerController(state, profile, new System.Random(1));
            Assert.That(player.ApplyHit(hit.Damage).Died, Is.EqualTo(dies));
            Assert.That(_controller.ConfirmDeath(_player, 3, state.IsAlive, 3, out _), Is.EqualTo(dies));
        }
        [Test]
        public void RoomDebugViewAndNoiseReflectTheSameGrab()
        {
            var phases = new System.Collections.Generic.Dictionary<int, FloorHandPhase> { [3] = FloorHandPhase.Idle };
            var warning = Step(0f, 11);
            _controller.CopyRoomPhases(phases);
            Assert.That(phases[3], Is.EqualTo(FloorHandPhase.Warning));
            var noise = _controller.GrabNoise(warning);
            Assert.That(noise.Tick, Is.EqualTo(11)); Assert.That(noise.SourceKind, Is.EqualTo(NoiseSourceKind.Other));
            Step(0.7f, 12); _controller.CopyRoomPhases(phases);
            Assert.That(phases[3], Is.EqualTo(FloorHandPhase.Grabbed));
            Step(1.4f, 13); _controller.CopyRoomPhases(phases);
            Assert.That(phases[3], Is.EqualTo(FloorHandPhase.Cooldown));
            _controller.Reset(); _controller.CopyRoomPhases(phases);
            Assert.That(phases[3], Is.EqualTo(FloorHandPhase.Idle));
        }
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTimeRejected(float value)
        { Assert.Throws<ArgumentOutOfRangeException>(() => _controller.Tick(_player, true, Near, value, 1, out _)); }
    }
}

