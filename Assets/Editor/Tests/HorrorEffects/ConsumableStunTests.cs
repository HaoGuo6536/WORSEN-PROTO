// ============================================================================
// ConsumableStunTests.cs
// ============================================================================
// PURPOSE:
//   Exercises enabled face aim through the shared hunter reaction rules headlessly.
//   It proves one fact, actual motion/contact suppression and charge timing without
//   requiring F to remain held after its toggle, or a native physics scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Verify one applied stun, recharge, Steady Hand and duplicate-tick rejection.
//   - Verify target loss, invalid observations, disabled beam and revival reset aim.
// DEPENDENCIES:
//   Core, HorrorEffects/Hunter/Player logic, managed Hunter test fixtures and NUnit.
// USAGE NOTES:
//   Config shells have explicit managed fields; no native constructors or object APIs.
//   Manager registry/physics/motor routing remains a separately named native test.
// ============================================================================
using System;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Player;
using Worsen.Session.HorrorEffects;
using Worsen.Tests.Hunter;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.HorrorEffects
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableStunTests
    {
        private ConsumableController items;
        private long tick;
        private static readonly EntityId Player = new EntityId(1), Hunter = new EntityId(-1);
        internal static HorrorEffectsConfig Config()
        {
            var c = (HorrorEffectsConfig)FormatterServices.GetUninitializedObject(typeof(HorrorEffectsConfig));
            void Set(string field, float value) => typeof(HorrorEffectsConfig).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(c, value);
            Set("stunAimSeconds", .8f); Set("stunSeconds", 2.5f); Set("stunRechargeSeconds", 25f);
            Set("steadyHandMultiplier", .7f); Set("stunCone", 12f); Set("stunStrength", 1f);
            Set("movingSpeed", .1f);
            return c;
        }
        [SetUp] public void Setup() { items = new ConsumableController(new ConsumableBehaviorState(), Config()); items.BeginFloor(); tick = 0; }
        private void Tick(float dt, bool enabled = true, bool visible = true, int stacks = 0, int id = -1, Vector3? head = null)
        {
            var light = new FlashlightSample(Player, ++tick, enabled, Vector3.zero, Vector3.forward, 18f, 52f);
            items.Tick(dt, tick, light, new[] { new HunterFaceSample(new EntityId(id), head ?? Vector3.forward * 3f, Vector3.zero, visible) },
                Vector3.zero, true, stacks, out _);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void HeldFaceAimProducesOneAppliedStunAndRespectsRecharge(int stacks)
        {
            items.ReceiveInput(default); // F was released after toggling ON.
            Tick(.4f, stacks: stacks); Assert.That(items.AimFraction, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.4f, stacks: stacks);
            var facts = items.DrainStuns(); Assert.That(facts.Length, Is.EqualTo(1));
            Assert.That(facts[0].HunterId, Is.EqualTo(Hunter)); Assert.That(items.DrainStuns(), Is.Empty);
            Assert.That(items.ChargeFraction, Is.Zero); Assert.That(items.AimFraction, Is.Zero);
            var state = new HunterBehaviorState();
            var receiver = new HunterController(state, HunterAttackControllerTests.Profile(), new System.Random(7),
                new PlayerBehaviorState { Id = Player, Health = 100, Position = Vector3.forward * 10, SprintSpeed = 8 }, new EchoControllerTests.World());
            receiver.Reset(Hunter, Vector3.zero, Vector3.forward);
            receiver.CommitPose(Vector3.zero, Vector3.forward * 8, Vector3.forward);
            receiver.ApplyStun(facts[0].Seconds, facts[0].Strength);
            Assert.That(state.Velocity, Is.EqualTo(Vector3.zero));
            for (int i = 0; i < 5; i++)
            {
                var held = receiver.Tick(default, .5f, i + 1);
                Assert.That(held.HoldPosition, Is.True); Assert.That(held.Speed, Is.Zero);
                Assert.That(held.ActiveContact, Is.False); Assert.That(receiver.TryAcceptContact(Player, out _), Is.False);
            }
            // Keep unrelated navigation out of this interruption test. Catch holds motion
            // independently but cannot keep ReactionHeld true after the stun deadline.
            receiver.SetCatchActive(true);
            receiver.Tick(default, .01f, 6); Assert.That(receiver.ReactionHeld, Is.False);
            float recharge = 25f * (float)Math.Pow(.7f, stacks);
            Tick(recharge * .5f, stacks: stacks); Assert.That(items.ChargeFraction, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(items.DrainStuns(), Is.Empty);
            items.BeginFloor(); Assert.That(items.ChargeFraction, Is.EqualTo(.5f).Within(.0001f)); tick = 0;
            Tick(recharge * .5f, stacks: stacks); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.79f, stacks: stacks); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.02f, stacks: stacks); Assert.That(items.DrainStuns().Length, Is.EqualTo(1));
            Assert.That(items.Tick(.8f, tick, default, null, Vector3.zero, true, stacks, out _), Is.False);
            Assert.That(items.DrainStuns(), Is.Empty);
            items.ResetRun(); Assert.That(items.ChargeFraction, Is.EqualTo(1f)); Assert.That(items.AimFraction, Is.Zero);
        }

        [Test]
        public void LosingFaceOrChangingTargetResetsContinuousAim()
        {
            Tick(.6f); Tick(.1f, visible: false); Assert.That(items.AimFraction, Is.Zero);
            Tick(.6f); Tick(.1f, enabled: false); Assert.That(items.AimFraction, Is.Zero);
            Tick(.6f); Tick(.1f, head: Vector3.forward * 19f); Assert.That(items.AimFraction, Is.Zero);
            Tick(.6f); Tick(.1f, head: Vector3.right * 3f); Assert.That(items.AimFraction, Is.Zero);
            Tick(.6f); Tick(.1f, head: new Vector3(float.NaN, 0f, 3f)); Assert.That(items.AimFraction, Is.Zero);
            Tick(.6f); Tick(.1f, id: -2); Assert.That(items.AimFraction, Is.EqualTo(.125f).Within(.0001f));
            Assert.That(items.DrainStuns(), Is.Empty);
            Assert.That(items.BeginRevival(Player), Is.True); Tick(1f);
            Assert.That(items.AimFraction, Is.Zero); Assert.That(items.DrainStuns(), Is.Empty);
        }
    }
}
