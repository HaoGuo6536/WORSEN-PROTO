// ============================================================================
// ConsumableControllerTests.cs
// ============================================================================
// PURPOSE:
//   Checks in-motion item rules, explicit deadlines and flashlight face aiming.
//   Tests inject observed heads, doors and impacts instead of querying a scene.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), Tests (§11) · Editor · HorrorEffects.
// KEY RESPONSIBILITIES:
//   - Cover every consumable, invalid targets, paused healing, visible progress and cleanup.
// DEPENDENCIES:
//   - HorrorEffects pure controllers, Core values, NUnit and transient Config creation.
// USAGE NOTES:
//   Edit Mode; no assets are saved and no engine clock is read.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.HorrorEffects;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.HorrorEffects
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ConsumableControllerTests
    {
        private HorrorEffectsConfig config;
        private ConsumableController items;
        private long tick;
        private readonly EntityId player = new EntityId(1), hunter = new EntityId(2);
        private FlashlightSample Aim(bool enabled = true) => new FlashlightSample(player, tick, enabled, Vector3.zero, Vector3.forward, 18f, 52f);
        private HunterFaceSample[] Faces(float distance = 3f, bool visible = true) => new[] { new HunterFaceSample(hunter, Vector3.forward * distance, Vector3.zero, visible) };
        private InputFrame Hold => new InputFrame(Vector2.up, Vector2.zero, InputButtons.UseItem, InputButtons.None, InputButtons.None);
        [SetUp] public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HorrorEffectsConfig>();
            items = new ConsumableController(new ConsumableBehaviorState(), config); items.BeginFloor(); tick = 0;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);
        private float Tick(float dt, Vector3 velocity, bool alive = true, int stacks = 0, bool light = true, HunterFaceSample[] faces = null)
        {
            Assert.That(items.Tick(dt, ++tick, Aim(light), faces ?? Faces(), velocity, alive, stacks, out float seconds), Is.True);
            return seconds;
        }
        private void Use(string id) => items.CommitUse(id, Aim(), Vector3.zero, hunter, 7, Vector3.back, tick);

        [TestCase("firecracker")] [TestCase("gauze")] [TestCase("smelling-salts")]
        [TestCase("wax-ward")] [TestCase("doorstop")] [TestCase("oil-flask")]
        [TestCase("glass-vial")] [TestCase("adrenaline")]
        public void AllItemsAdmitWhileMovingAndNeverRewriteInput(string id)
        {
            var frame = Hold;
            var doors = new[] { new InteractableState(7, InteractableKind.Door, 1, Vector3.back, InteractableStateValue.Open) };
            Tick(.02f, Vector3.forward * 8f);
            Assert.That(items.CanUse(id, 25f, 100f, Aim(), Faces(), doors, Vector3.zero, out _, out _), Is.True);
            Assert.That(frame.Move, Is.EqualTo(Vector2.up)); Assert.That(frame.Held, Is.EqualTo(InputButtons.UseItem));
            Assert.That(items.CanUse(id, 0f, 100f, Aim(), Faces(), doors, Vector3.zero, out _, out _), Is.False);
        }
        [Test]
        public void GauzeHealsExactlyThirtyFiveOverFourMovingSecondsAndPausesWhileStill()
        {
            Use("gauze");
            Assert.That(Tick(10f, Vector3.zero), Is.Zero);
            Assert.That(Tick(2f, Vector3.forward) * items.HealingRate, Is.EqualTo(17.5f));
            Assert.That(Tick(10f, Vector3.zero), Is.Zero);
            Assert.That(Tick(3f, Vector3.forward) * items.HealingRate, Is.EqualTo(17.5f));
            Assert.That(Tick(1f, Vector3.forward), Is.Zero);
        }
        [TestCase(25f, true)] [TestCase(25.01f, false)] [TestCase(1f, true)] [TestCase(float.NaN, false)]
        public void AdrenalineRequiresCriticalHealth(float health, bool accepted)
        {
            Assert.That(items.CanUse("adrenaline", health, 100f, Aim(), Faces(), null, Vector3.zero, out _, out _), Is.EqualTo(accepted));
        }
        [Test]
        public void AdrenalineDoesNotStackAndExpiresOnTheInjectedClock()
        {
            Use("adrenaline"); Assert.That(items.SpeedMultiplier, Is.EqualTo(1.5f));
            Assert.That(items.CanUse("adrenaline", 20f, 100f, Aim(), Faces(), null, Vector3.zero, out _, out _), Is.False);
            Tick(2.99f, Vector3.forward); Assert.That(items.SpeedMultiplier, Is.EqualTo(1.5f));
            Tick(.02f, Vector3.forward); Assert.That(items.SpeedMultiplier, Is.EqualTo(1f));
        }
        [Test]
        public void FirecrackerSweepsAlongViewAndOnlyAnImpactProducesOneNoise()
        {
            Use("firecracker");
            var flight = items.AdvanceThrows(.1f)[0];
            Assert.That(flight.To.z, Is.GreaterThan(flight.From.z));
            Assert.That(flight.To.y, Is.LessThan(flight.From.y));
            Assert.That(items.DrainNoises(), Is.Empty);
            Assert.That(items.Impact(flight.Id, Vector3.forward, 1), Is.True);
            Assert.That(items.Impact(flight.Id, Vector3.forward, 1), Is.False);
            var noise = items.DrainNoises(); Assert.That(noise.Length, Is.EqualTo(1));
            Assert.That(noise[0].Position, Is.EqualTo(Vector3.forward));
            Assert.That(noise[0].Loudness, Is.EqualTo(config.FirecrackerLoudness));
            Assert.That(HorrorNoiseUtility.Origin(noise[0]), Is.EqualTo(HorrorNoiseOrigin.Firecracker));
            Assert.That(noise[0].Origin, Is.EqualTo(NoiseOrigin.Firecracker));
            Assert.That(HunterHearingUtility.Allows(noise[0]), Is.True);
            Assert.That(HorrorNoiseUtility.HunterAudible(noise[0]), Is.True);
            Assert.That(items.AdvanceThrows(.1f), Is.Empty);
        }
        [Test]
        public void DoorstopSelectsNearestIntactDoorBehindAndExpiresOrBreaksOnce()
        {
            var doors = new[] {
                new InteractableState(1, InteractableKind.Door, 1, Vector3.forward, InteractableStateValue.Open),
                new InteractableState(2, InteractableKind.Door, 1, Vector3.back, InteractableStateValue.Broken),
                new InteractableState(7, InteractableKind.Door, 1, Vector3.back * 2, InteractableStateValue.Open),
                new InteractableState(8, InteractableKind.Door, 1, Vector3.back * 3, InteractableStateValue.Inactive) };
            Assert.That(items.CanUse("doorstop", 100f, 100f, Aim(), Faces(), doors, Vector3.zero, out _, out int door), Is.True);
            Assert.That(door, Is.EqualTo(7)); Use("doorstop");
            Assert.That(items.DrainDoors()[0].Seconds, Is.EqualTo(8f));
            Tick(8f, Vector3.forward); Assert.That(items.IsJammed(7), Is.False); Assert.That(items.DrainNoises(), Is.Empty);
            Use("doorstop"); Assert.That(items.EndJam(7, true, tick), Is.True);
            Assert.That(items.EndJam(7, true, tick), Is.False);
            var noises = items.DrainNoises(); Assert.That(noises.Length, Is.EqualTo(1));
            Assert.That(HorrorNoiseUtility.HunterAudible(noises[0]), Is.False);
        }
        [Test]
        public void OilSlipsOnEntryNotEveryTickAndExpires()
        {
            Use("oil-flask"); Tick(.02f, Vector3.forward);
            var slips = items.DrainSlips(); Assert.That(slips.Length, Is.EqualTo(1)); Assert.That(slips[0].Seconds, Is.EqualTo(1f));
            Tick(.02f, Vector3.forward); Assert.That(items.DrainSlips(), Is.Empty);
            Tick(8f, Vector3.forward); Assert.That(items.DrainSlips(), Is.Empty);
        }
        [Test]
        public void VialNeedsVisibleFaceInConeAndPublishesOneWeakerFlinch()
        {
            Assert.That(items.CanUse("glass-vial", 100f, 100f, Aim(), Faces(5f), null, Vector3.zero, out _, out _), Is.False);
            Assert.That(items.CanUse("glass-vial", 100f, 100f, Aim(), Faces(3f, false), null, Vector3.zero, out _, out _), Is.False);
            var side = new[] { new HunterFaceSample(hunter, Vector3.right, Vector3.zero, true) };
            Assert.That(items.CanUse("glass-vial", 100f, 100f, Aim(), side, null, Vector3.zero, out _, out _), Is.False);
            Use("glass-vial"); var facts = items.DrainStuns(); Assert.That(facts.Length, Is.EqualTo(1));
            Assert.That(facts[0].Seconds, Is.EqualTo(.6f)); Assert.That(facts[0].Strength, Is.LessThan(config.StunStrength));
            Assert.That(items.DrainStuns(), Is.Empty);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FlashlightRequiresContinuousAimAndSpendsOneSlowlyRechargingCharge(int stacks)
        {
            items.ReceiveInput(Hold);
            Tick(.79f, Vector3.forward, stacks: stacks); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.01f, Vector3.forward, stacks: stacks); var facts = items.DrainStuns();
            Assert.That(facts.Length, Is.EqualTo(1)); Assert.That(facts[0].HunterId, Is.EqualTo(hunter));
            Assert.That(facts[0].Seconds, Is.EqualTo(2.5f)); Assert.That(items.Charged, Is.False);
            Assert.That(items.Tick(.1f, tick, Aim(), Faces(), Vector3.forward, true, stacks, out _), Is.False);
            Assert.That(items.DrainStuns(), Is.Empty);
            items.ReceiveInput(default);
            float recharge = config.StunRechargeSeconds * Mathf.Pow(config.SteadyHandMultiplier, stacks);
            Tick(recharge - .01f, Vector3.forward, stacks: stacks); Assert.That(items.Charged, Is.False);
            Tick(.02f, Vector3.forward, stacks: stacks); Assert.That(items.Charged, Is.True);
        }
        [Test]
        public void HoldingThroughRechargeDoesNotCountUnchargedTimeTowardAim()
        {
            items.ReceiveInput(Hold); Tick(.8f, Vector3.forward); items.DrainStuns();
            Tick(25f, Vector3.forward); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.79f, Vector3.forward); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.02f, Vector3.forward); Assert.That(items.DrainStuns().Length, Is.EqualTo(1));
        }

        [Test]
        public void ReleasedToggleStillAimsAndReportsAuthoritativeProgress()
        {
            items.ReceiveInput(default);
            Tick(.4f, Vector3.zero); Assert.That(items.AimFraction, Is.EqualTo(.5f).Within(.0001f));
            Tick(.4f, Vector3.zero); Assert.That(items.DrainStuns().Length, Is.EqualTo(1));
            Assert.That(items.ChargeFraction, Is.Zero); Assert.That(items.AimFraction, Is.Zero);
            Tick(12.5f, Vector3.zero); Assert.That(items.ChargeFraction, Is.EqualTo(.5f).Within(.0001f));
        }
        [Test]
        public void LosingFaceVisibilityDistanceOrLightResetsAimAndNewFloorDoesNotRefillCharge()
        {
            items.ReceiveInput(Hold); Tick(.7f, Vector3.forward);
            Tick(.2f, Vector3.forward, faces: Faces(3f, false));
            Tick(.7f, Vector3.forward); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.2f, Vector3.forward, faces: Faces(19f));
            Tick(.7f, Vector3.forward); Tick(.2f, Vector3.forward, light: false);
            Tick(.7f, Vector3.forward); Assert.That(items.DrainStuns(), Is.Empty);
            Tick(.1f, Vector3.forward); Assert.That(items.DrainStuns().Length, Is.EqualTo(1));
            items.BeginFloor(); Assert.That(items.Charged, Is.False);
            items.ResetRun(); Assert.That(items.Charged, Is.True);
        }
        [Test]
        public void TrapCleanseRetainsDeduplicationAndOtherPlayersSlow()
        {
            var curse = new HorrorEffectsController(new HorrorEffectsBehaviorState(), config);
            curse.BeginFloor(1, default);
            Assert.That(curse.StartTrapSlow(player, 1), Is.True); Assert.That(curse.StartTrapSlow(hunter, 2), Is.True);
            curse.ClearTrapSlow(player);
            Assert.That(curse.TrapSpeedMultiplier(player), Is.EqualTo(1f));
            Assert.That(curse.TrapSpeedMultiplier(hunter), Is.LessThan(1f));
            Assert.That(curse.StartTrapSlow(player, 1), Is.False);
        }
        [Test]
        public void RevivalIsAnIdentityGuardedWaitAndFloorCleanupDropsAllTransientEffects()
        {
            Use("gauze"); Use("adrenaline"); Use("firecracker"); Use("oil-flask"); Use("doorstop");
            Assert.That(items.BeginRevival(player), Is.True); Assert.That(items.BeginRevival(player), Is.False);
            Assert.That(items.CompleteRevival(hunter), Is.False); Assert.That(items.RevivalPending, Is.True);
            Assert.That(items.CompleteRevival(player), Is.True); Assert.That(items.SpeedMultiplier, Is.EqualTo(1f));
            items.BeginFloor(); Assert.That(items.AdvanceThrows(.1f), Is.Empty); Assert.That(items.IsJammed(7), Is.False);
            Tick(.1f, Vector3.forward); Assert.That(items.DrainSlips(), Is.Empty);
        }
    }
}
