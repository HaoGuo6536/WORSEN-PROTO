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
// DEPENDENCIES:
//   - Core shared floor facts and Unity value types; no higher-layer dependency.
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
namespace Worsen.Tests.Floor
{
    public sealed class FloorHandControllerTests
    {
        private FloorHandController _controller;
        private readonly EntityId _player = new EntityId(1);
        private FloorHandProbe Near => new FloorHandProbe(3, 7, new Vector3(0f, 0f, 2f), 0.5f, true);
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
        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTimeRejected(float value)
        { Assert.Throws<ArgumentOutOfRangeException>(() => _controller.Tick(_player, true, Near, value, 1, out _)); }
    }
}

