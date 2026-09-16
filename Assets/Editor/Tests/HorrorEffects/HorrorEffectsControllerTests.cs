// ============================================================================
// HorrorEffectsControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies that flashlight authority and concrete curse schedules use real
//   captured positions and stop at generation boundaries. These tests exercise
//   gameplay outcomes without any scene, rendering, hearing or engine clock.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Session · HorrorEffects pure controller tests (§11).
// KEY RESPONSIBILITIES:
//   Cover light ownership, bounded delayed effects, perks and cleanup.
// DEPENDENCIES:
//   HorrorEffects pure types, Core records, NUnit and test-only config creation.
// USAGE NOTES:
//   Tests drive explicit time and ticks. Config assets are transient and destroyed.
// ============================================================================
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.HorrorEffects;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Tests.HorrorEffects
{
    public sealed class HorrorEffectsControllerTests
    {
        private HorrorEffectsConfig config;
        private HorrorEffectsBehaviorState state;
        private HorrorEffectsController controller;
        private readonly EntityId player = new EntityId(7);

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<HorrorEffectsConfig>();
            state = new HorrorEffectsBehaviorState();
            controller = new HorrorEffectsController(state, config);
            controller.BeginFloor(1, Effects());
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(config);

        [Test]
        public void AimCannotOverridePlayerToggleAndDuplicateTickCannotToggleTwice()
        {
            controller.ObserveAim(Aim(1, Vector3.zero));
            controller.Tick(Toggle(), 0.02f, 1);
            controller.Tick(Toggle(), 0.02f, 1);
            controller.ObserveAim(Aim(2, Vector3.right));
            FlashlightSample light = controller.DrainFacts().Last(f => f.Kind == HorrorEffectKind.Flashlight).Light;
            Assert.That(light.Enabled, Is.False);
            Assert.That(light.Origin, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void BeamUsesUnshakenAimAndLensConeWithRangeModifier()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.ShutteredLens, 0.7f));
            controller.ObserveAim(Aim(1, new Vector3(2f, 3f, 4f), Vector3.left));
            FlashlightSample light = controller.DrainFacts().Last().Light;
            Assert.That(light.Direction, Is.EqualTo(Vector3.left));
            Assert.That(light.Range, Is.EqualTo(config.FlashlightRange * 0.7f).Within(0.001f));
            Assert.That(light.ConeDegrees, Is.EqualTo(config.FlashlightCone * config.ShutteredConeMultiplier).Within(0.001f));
        }

        [Test]
        public void InvalidOrStaleAimCannotReplaceLastValidPose()
        {
            controller.ObserveAim(Aim(3, Vector3.right));
            controller.DrainFacts();
            controller.ObserveAim(Aim(2, Vector3.left));
            controller.ObserveAim(Aim(4, new Vector3(float.NaN, 0f, 0f)));
            controller.ObserveAim(Aim(4, Vector3.left, Vector3.zero));
            Assert.That(controller.DrainFacts(), Is.Empty);
        }

        [Test]
        public void AfterimageStaysAtSwitchOffLocationThenExpiresOnce()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.Afterimage));
            controller.ObserveAim(Aim(1, Vector3.right));
            controller.Tick(Toggle(), 0.01f, 1);
            var afterimage = controller.DrainFacts().Single(f => f.Kind == HorrorEffectKind.Afterimage);
            Assert.That(afterimage.Light.Enabled, Is.True);
            controller.ObserveAim(Aim(2, Vector3.left));
            controller.Tick(config.AfterimageLifetime + 0.1f, 2);
            var expired = controller.DrainFacts().Single(f => f.Kind == HorrorEffectKind.Afterimage);
            Assert.That(expired.Light.Enabled, Is.False);
            Assert.That(expired.Light.Origin, Is.EqualTo(Vector3.right));
            controller.Tick(1f, 3);
            Assert.That(controller.DrainFacts().Any(f => f.Kind == HorrorEffectKind.Afterimage), Is.False);
        }

        [Test]
        public void EchoDebtUsesOriginalHardLandingPositionAndNotSoftLandings()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.EchoDebt));
            controller.ObserveMovement(Movement(1, Vector3.right));
            controller.ObserveTraversal(new PlayerTraversalFact(player, 1, TraversalKind.Land, true, Vector3.down, 0.2f));
            Assert.That(state.PendingNoises.Count, Is.Zero);
            var hard = new PlayerTraversalFact(player, 2, TraversalKind.Land, true, Vector3.down, 0.5f);
            controller.ObserveTraversal(hard);
            controller.ObserveTraversal(hard);
            controller.ObserveMovement(Movement(3, Vector3.left));
            controller.Tick(config.EchoDelay - 0.1f, 1);
            Assert.That(controller.DrainFacts().Any(f => f.Kind == HorrorEffectKind.Noise), Is.False);
            controller.Tick(0.2f, 2);
            NoiseEvent noise = controller.DrainFacts().Single(f => f.Kind == HorrorEffectKind.Noise).Noise;
            Assert.That(noise.Position, Is.EqualTo(Vector3.right));
            Assert.That(noise.Tick, Is.EqualTo(2));
        }

        [Test]
        public void BorrowedStepsFollowActualRouteAndAreDistanceAndTimeLimited()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.BorrowedFootsteps));
            controller.ObserveMovement(Movement(1, Vector3.zero));
            controller.ObserveMovement(Movement(2, Vector3.right * 10f));
            Assert.That(state.PendingNoises.Count, Is.EqualTo(1));
            controller.Tick(config.BorrowedStepInterval + 0.1f, 1);
            controller.ObserveMovement(Movement(3, Vector3.right * 0.1f));
            Assert.That(state.PendingNoises.Count, Is.EqualTo(1));
            controller.ObserveMovement(Movement(4, Vector3.right * 5f));
            Assert.That(state.PendingNoises.Count, Is.EqualTo(2));
            controller.Tick(config.BorrowedStepDelay + 0.1f, 2);
            var noises = controller.DrainFacts().Where(f => f.Kind == HorrorEffectKind.Noise).ToArray();
            Assert.That(noises.Length, Is.EqualTo(2));
            Assert.That(noises[0].Noise.Position, Is.EqualTo(Vector3.zero));
            Assert.That(noises[1].Noise.Position, Is.EqualTo(Vector3.right * 5f));
        }

        [Test]
        public void SchedulesAreBoundedAndTraitRemovalCancelsPendingFacts()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.EchoDebt));
            controller.ObserveMovement(Movement(1, Vector3.zero));
            for (int i = 1; i <= config.MaximumPendingNoises * 3; i++)
                controller.ObserveTraversal(new PlayerTraversalFact(player, i, TraversalKind.Land, true, Vector3.down, 0.5f));
            Assert.That(state.PendingNoises.Count, Is.EqualTo(config.MaximumPendingNoises));
            controller.UpdateEffects(Effects());
            controller.Tick(10f, 1);
            Assert.That(controller.DrainFacts().Any(f => f.Kind == HorrorEffectKind.Noise), Is.False);
        }

        [Test]
        public void GildedHungerAttractsOnlyWhenOwnedAndUsesCollectionPosition()
        {
            controller.RecordGoldenCollected(player, Vector3.right, 1);
            Assert.That(controller.DrainFacts(), Is.Empty);
            controller.UpdateEffects(Effects(ProgressionTraits.GildedHunger));
            controller.RecordGoldenCollected(player, Vector3.left, 2);
            NoiseEvent noise = controller.DrainFacts().Single().Noise;
            Assert.That(noise.Position, Is.EqualTo(Vector3.left));
            Assert.That(noise.Source, Is.EqualTo(player));
        }

        [Test]
        public void RestlessMasonryOnlyEmitsSuppliedOptionalRoomsOnce()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.RestlessMasonry));
            controller.SetOptionalRooms(new[] { 4, -1, 4, 9 });
            controller.Tick(config.OptionalRoomInterval + 0.1f, 1);
            Assert.That(controller.DrainFacts().Single().RoomId, Is.EqualTo(4));
            controller.Tick(config.OptionalRoomInterval + 0.1f, 2);
            Assert.That(controller.DrainFacts().Single().RoomId, Is.EqualTo(9));
            controller.Tick(config.OptionalRoomInterval + 0.1f, 3);
            Assert.That(controller.DrainFacts(), Is.Empty);
        }

        [Test]
        public void UnquietFlameDimsWhileMovingAndRestoresOnStop()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.UnquietFlame));
            controller.ObserveMovement(Movement(1, Vector3.right));
            controller.Tick(Sprint(), 0.02f, 1);
            HorrorEffectFact dim = controller.DrainFacts().Single();
            Assert.That(dim.Kind, Is.EqualTo(HorrorEffectKind.FlameDim));
            Assert.That(dim.Value, Is.EqualTo(config.FlameDimMultiplier));
            Assert.That(dim.Value, Is.GreaterThan(0f));
            controller.Tick(default, 0.02f, 2);
            Assert.That(controller.DrainFacts().Single().Value, Is.EqualTo(1f));
        }

        [Test]
        public void PilgrimChalkOnlyMarksCrossedDoorsOncePerFloor()
        {
            ProgressionEffects effects = Effects(ProgressionTraits.PilgrimChalk);
            controller.UpdateEffects(effects);
            controller.RecordDoorCrossed(3, Vector3.right);
            controller.RecordDoorCrossed(3, Vector3.right);
            Assert.That(controller.DrainFacts().Count(f => f.Kind == HorrorEffectKind.DoorMark), Is.EqualTo(1));
            controller.BeginFloor(2, effects);
            controller.RecordDoorCrossed(3, Vector3.right);
            Assert.That(controller.DrainFacts().Count(f => f.Kind == HorrorEffectKind.DoorMark), Is.EqualTo(1));
        }

        [Test]
        public void PerksExposeIndependentFootstepRecoveryAndOptionalWindowMultipliers()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.FeltSoles | ProgressionTraits.ClimberWraps | ProgressionTraits.SealedSills));
            Assert.That(controller.FootstepLoudnessMultiplier, Is.EqualTo(config.FeltSolesMultiplier));
            Assert.That(controller.ReboundCooldownMultiplier, Is.EqualTo(config.ClimberRecoveryMultiplier));
            Assert.That(controller.OptionalWindowMultiplier, Is.EqualTo(config.SealedWindowMultiplier));
        }

        [Test]
        public void SuspendCancelsQueuedNoiseRestoresVisualsAndPreventsInput()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.EchoDebt | ProgressionTraits.Afterimage | ProgressionTraits.UnquietFlame));
            controller.ObserveAim(Aim(1, Vector3.right));
            controller.ObserveMovement(Movement(1, Vector3.right));
            controller.ObserveTraversal(new PlayerTraversalFact(player, 1, TraversalKind.Land, true, Vector3.down, 0.5f));
            controller.Tick(new InputFrame(Vector2.zero, Vector2.zero, InputButtons.Sprint, InputButtons.UseItem, InputButtons.None), 0.02f, 1);
            controller.DrainFacts();
            controller.Suspend();
            var cleanup = controller.DrainFacts();
            Assert.That(cleanup.Single(f => f.Kind == HorrorEffectKind.Afterimage).Light.Enabled, Is.False);
            Assert.That(cleanup.Single(f => f.Kind == HorrorEffectKind.FlameDim).Value, Is.EqualTo(1f));
            controller.Tick(Toggle(), 10f, 2);
            Assert.That(controller.DrainFacts(), Is.Empty);
            Assert.That(controller.FlashlightEnabled, Is.False);
            controller.BeginFloor(2, Effects());
            controller.Tick(10f, 1);
            Assert.That(controller.DrainFacts(), Is.Empty);
            Assert.That(controller.FlashlightEnabled, Is.True);
        }

        [Test]
        public void InvalidDeltaTimeDoesNotConsumeInputOrScheduleTime()
        {
            controller.Tick(Toggle(), float.NaN, 1);
            controller.Tick(Toggle(), -1f, 1);
            Assert.That(controller.FlashlightEnabled, Is.True);
            controller.Tick(Toggle(), 0.02f, 1);
            Assert.That(controller.FlashlightEnabled, Is.False);
        }

        [Test]
        public void GrabAllowsWardAttemptAndNestedEscapeRestoresSpeed()
        {
            var grabbed = new CollapseHandFact(player, 3, CollapseHandEventKind.Grabbed, Vector3.zero, 0.4f, 0f, 5);
            HorrorHazardResolution grab = controller.ResolveHand(grabbed, true);
            Assert.That(grab.TryWard, Is.True);
            Assert.That(grab.SpeedMultiplier, Is.EqualTo(0.4f));
            var escape = controller.ResolveHand(new CollapseHandFact(player, 3, CollapseHandEventKind.Escaped,
                Vector3.zero, 1f, 0f, 5), true);
            Assert.That(escape.Accepted, Is.True);
            Assert.That(escape.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(controller.ResolveHand(grabbed, true).Accepted, Is.False);
        }

        [Test]
        public void HandHitAppliesOneFiniteDamageAndReleasesSlow()
        {
            var hit = new CollapseHandFact(player, 3, CollapseHandEventKind.Hit, Vector3.zero, 0.2f, 12f, 5);
            HorrorHazardResolution result = controller.ResolveHand(hit, true);
            Assert.That(result.Damage, Is.EqualTo(12f));
            Assert.That(result.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(controller.ResolveHand(hit, true).Accepted, Is.False);
            Assert.That(controller.ResolveHand(new CollapseHandFact(player, 3, CollapseHandEventKind.Hit,
                Vector3.zero, 0.2f, 12f, 6), false).Accepted, Is.False);
            Assert.That(controller.ResolveHand(new CollapseHandFact(player, 3, CollapseHandEventKind.Hit,
                Vector3.zero, 0.2f, float.NaN, 7), true).Damage, Is.Zero);
        }

        [Test]
        public void LateSpawnReceivesPerksExactlyOnceAndHealthSnapshotsDoNotReapply()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.FeltSoles));
            Assert.That(controller.TryGetActorEffects(player, out float footsteps, out _, out float grab), Is.True);
            Assert.That(footsteps, Is.EqualTo(config.FeltSolesMultiplier));
            Assert.That(grab, Is.EqualTo(1f));
            controller.UpdateEffects(new ProgressionEffects(1f, 1f, 1f, 1f, 100f, 50f, 3, ProgressionTraits.FeltSoles));
            Assert.That(controller.TryGetActorEffects(player, out _, out _, out _), Is.False);
            Assert.That(controller.TryGetActorEffects(new EntityId(8), out _, out _, out _), Is.True);
        }

        [Test]
        public void PerkChangeDuringGrabPreservesSlowAndWardReleaseRestoresIt()
        {
            controller.TryGetActorEffects(player, out _, out _, out _);
            controller.ResolveHand(new CollapseHandFact(player, 1, CollapseHandEventKind.Grabbed,
                Vector3.zero, 0.35f, 0f, 2), true);
            controller.UpdateEffects(Effects(ProgressionTraits.ClimberWraps));
            Assert.That(controller.TryGetActorEffects(player, out _, out float rebound, out float grab), Is.True);
            Assert.That(rebound, Is.EqualTo(config.ClimberRecoveryMultiplier));
            Assert.That(grab, Is.EqualTo(0.35f));
            controller.ReleaseGrab(player);
            controller.UpdateEffects(Effects(ProgressionTraits.ClimberWraps | ProgressionTraits.FeltSoles));
            controller.TryGetActorEffects(player, out _, out _, out grab);
            Assert.That(grab, Is.EqualTo(1f));
        }

        [Test]
        public void NewGenerationReappliesPerksWithoutCarryingOldGrab()
        {
            controller.TryGetActorEffects(player, out _, out _, out _);
            controller.ResolveHand(new CollapseHandFact(player, 1, CollapseHandEventKind.Grabbed,
                Vector3.zero, 0.35f, 0f, 2), true);
            controller.BeginFloor(2, Effects(ProgressionTraits.FeltSoles));
            Assert.That(controller.TryGetActorEffects(player, out _, out _, out float grab), Is.True);
            Assert.That(grab, Is.EqualTo(1f));
            controller.Suspend();
            Assert.That(controller.TryGetActorEffects(new EntityId(9), out _, out _, out _), Is.False);
        }

        [Test]
        public void LateHunterReceivesFrozenAfterimageWithRemainingLifetimeOnce()
        {
            controller.UpdateEffects(Effects(ProgressionTraits.Afterimage));
            controller.ObserveAim(Aim(1, Vector3.right));
            controller.Tick(Toggle(), 0.2f, 1);
            controller.ObserveAim(Aim(2, Vector3.left));
            Assert.That(controller.TryGetHunterEffects(new EntityId(20), out FlashlightSample light,
                out FlashlightSample trace, out float lifetime), Is.True);
            Assert.That(light.Enabled, Is.False);
            Assert.That(light.Origin, Is.EqualTo(Vector3.left));
            Assert.That(trace.Origin, Is.EqualTo(Vector3.right));
            Assert.That(lifetime, Is.EqualTo(config.AfterimageLifetime - 0.2f).Within(0.001f));
            Assert.That(controller.TryGetHunterEffects(new EntityId(20), out _, out _, out _), Is.False);
        }

        private ProgressionEffects Effects(ProgressionTraits traits = ProgressionTraits.None, float range = 1f)
            => new ProgressionEffects(1f, 1f, 1f, range, 100f, 100f, 3, traits);
        private FlashlightSample Aim(long tick, Vector3 position, Vector3? direction = null)
            => new FlashlightSample(player, tick, true, position, direction ?? Vector3.forward, 99f, 99f);
        private PlayerMovementSample Movement(long tick, Vector3 position)
            => new PlayerMovementSample(player, tick, position, Vector3.forward * 6f, position + Vector3.up,
                0f, Vector2.zero, false, MovementState.Ground, 0f);
        private InputFrame Toggle() => new InputFrame(Vector2.zero, Vector2.zero, InputButtons.None, InputButtons.UseItem, InputButtons.None);
        private InputFrame Sprint() => new InputFrame(Vector2.zero, Vector2.zero, InputButtons.Sprint, InputButtons.None, InputButtons.None);
    }
}
