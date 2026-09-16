// ============================================================================
// AudioFeedbackPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Checks that committed actions receive the intended sounds exactly once.
//   It separates initialization from damage and confirms slide, pickup, attack and collapse transition semantics.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Verify one pickup/outcome sound and health-owned damage without suppressing different actions.
//   - Verify per-action mapping and duplicate suppression.
//   - Verify silent posture seeding, slide suppression, exertion fades and critical/death priority.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Pure tests use Core facts without playing sources or changing a scene.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Audio;
namespace Worsen.Tests.Audio
{
    public sealed class AudioFeedbackPresenterTests
    {
        [Test]
        public void HealthOwnsOneHitOrDeathRegardlessOfHunterAndHandFeedbackOrder()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var player = new EntityId(1);
            presenter.Health(state, player, 100, 100);
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.AttackHit, Vector3.zero, 10)); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, player, 90, 100); Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            presenter.Health(state, player, 80, 100); Assert.That(state.Commands.Count, Is.EqualTo(1), "A separate accepted injury still produces its own cue.");
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.AttackHit, Vector3.zero, 11)); Assert.That(state.Commands, Is.Empty);
            presenter.Hand(state, new CollapseHandFact(player, 3, CollapseHandEventKind.Hit, Vector3.zero, 1, 10, 12)); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, player, 70, 100); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            presenter.Hand(state, new CollapseHandFact(player, 3, CollapseHandEventKind.Consumed, Vector3.zero, 1, 100, 13)); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, player, 0, 100); Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.Death));
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.Scream, Vector3.zero, 14)); Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void SwingOwnsOneWhooshAndSeparateHuntersStillSoundIndependently()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            for (int hunter = 1; hunter <= 2; hunter++)
            {
                presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(hunter), "rusher", HunterFeedbackKind.AttackSwing, Vector3.zero, 10));
                Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.EnemyAttack)); Assert.That(state.Commands[0].Emitter, Is.EqualTo(hunter));
                presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(hunter), "rusher", HunterFeedbackKind.AttackMiss, Vector3.zero, 11)); Assert.That(state.Commands, Is.Empty);
            }
        }
        [Test]
        public void EachPickupSelectsOneCueEvenWhenGoldenCoincidesWithChainMilestone()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            for (int i = 1; i <= 6; i++)
            {
                var kind = i == 6 ? PickupKind.GoldenCake : PickupKind.Cake;
                var fact = new PickupCollectedFact(new EntityId(1), i, kind, 0, 1, i);
                presenter.Pickup(state, fact, Vector3.zero); Assert.That(state.Commands.Count, Is.EqualTo(1));
                Assert.That(state.Commands[0].Cue, Is.EqualTo(i == 6 ? CueId.GoldenCakeCollect : i == 3 ? CueId.CakeChain : CueId.CakeCollect));
                presenter.Pickup(state, fact, Vector3.zero); Assert.That(state.Commands, Is.Empty);
            }
        }
        [Test]
        public void PostureSeedsSilentlyAndOnlyCommittedTransitionsRustleOutsideSlides()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            presenter.Movement(state, PostureSample(1, true, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(2, false, false));
            Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PostureRustle));
            presenter.Movement(state, PostureSample(2, true, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(3, false, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(4, true, false, MovementState.Slide));
            Assert.That(state.Commands.Exists(command => command.Cue == CueId.PostureRustle), Is.False);
            presenter.Movement(state, PostureSample(5, false, false));
            Assert.That(state.Commands.Exists(command => command.Cue == CueId.PostureRustle), Is.False);
            presenter.Movement(state, PostureSample(6, true, false));
            Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PostureRustle));
        }
        [Test]
        public void ExertionRampsOneEmitterReleasesAndDoesNotInferSprintFromVelocity()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            presenter.Movement(state, PostureSample(1, false, false));
            presenter.TickExertion(state, .2f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(2, false, true)); Assert.That(state.Commands, Is.Empty);
            presenter.TickExertion(state, .2f, .8f, 1f, 0);
            Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.SprintExertion));
            Assert.That(state.Commands[0].Gain, Is.EqualTo(.25f).Within(.00001f)); int emitter = state.Commands[0].Emitter;
            presenter.TickExertion(state, .2f, .8f, 1f, 0);
            Assert.That(state.Commands[0].Emitter, Is.EqualTo(emitter)); Assert.That(state.Commands[0].Gain, Is.EqualTo(.5f).Within(.00001f));
            presenter.Movement(state, PostureSample(3, false, false));
            presenter.TickExertion(state, .2f, .8f, 1f, 0);
            Assert.That(state.Commands[0].Gain, Is.EqualTo(.3f).Within(.00001f));
            presenter.TickExertion(state, .4f, .8f, 1f, 0);
            Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].StopEmitter, Is.True); Assert.That(state.Commands[0].Emitter, Is.EqualTo(emitter));
            presenter.TickExertion(state, .2f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void CriticalHealthSuppressesExertionAndDeathClearsItsEnvelope()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var id = new EntityId(1);
            presenter.Health(state, id, 20, 100);
            presenter.Movement(state, PostureSample(1, false, true));
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, id, 100, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.ExertionGain, Is.EqualTo(1f));
            presenter.Health(state, id, 20, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.Commands[0].StopEmitter, Is.True);
            presenter.Health(state, id, 100, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0);
            presenter.Health(state, id, 0, 100);
            presenter.TickExertion(state, .001f, .8f, 1f, 0);
            Assert.That(state.ExertionGain, Is.Zero); Assert.That(state.IsSprinting, Is.False); Assert.That(state.Commands[0].StopEmitter, Is.True);
        }
        private static PlayerMovementSample PostureSample(long tick, bool crouched, bool sprinting, MovementState movement = MovementState.Ground) =>
            new PlayerMovementSample(new EntityId(1), tick, Vector3.forward * tick, Vector3.forward * 12f, Vector3.up, 0, Vector2.zero, false, movement, 0, 0, crouched, sprinting);
        [Test]
        public void NewFloorReseedsPostureAndClearsExertionIntentAndEnvelope()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState { Generation = 5, Revision = 2 };
            presenter.Movement(state, PostureSample(99, false, true));
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.ExertionActive, Is.True);
            var effects = new ProgressionEffects(1, 1, 1, 1, 100, 100, 1);
            presenter.Progression(state, new ProgressionSnapshot(3, 6, 1, 0, 0, 0, 0, ProgressionPhase.ChooseThreat, 100, 100, null, null, null, effects, "", false, false));
            Assert.That(state.ExertionActive, Is.False); Assert.That(state.ExertionGain, Is.Zero); Assert.That(state.IsSprinting, Is.False);
            presenter.Movement(state, PostureSample(1, true, false)); Assert.That(state.Commands, Is.Empty);
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void CommittedLandingSeverityChangesImpactGainAndStillDeduplicates()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var id = new EntityId(1);
            presenter.Traversal(state, new PlayerTraversalFact(id, 1, TraversalKind.Land, true, Vector3.down, 0f));
            float soft = state.Commands[0].Gain;
            presenter.Traversal(state, new PlayerTraversalFact(id, 2, TraversalKind.Land, true, Vector3.down, .2f));
            float medium = state.Commands[0].Gain;
            presenter.Traversal(state, new PlayerTraversalFact(id, 3, TraversalKind.Land, true, Vector3.down, .5f));
            float hard = state.Commands[0].Gain;
            Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.Land));
            Assert.That(soft, Is.GreaterThan(0f)); Assert.That(medium, Is.GreaterThan(soft)); Assert.That(hard, Is.GreaterThan(medium));
            Assert.That(hard, Is.LessThanOrEqualTo(1f));
            presenter.Traversal(state, new PlayerTraversalFact(id, 3, TraversalKind.Land, true, Vector3.down, .5f));
            Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void SlideFrictionRespondsToActualTurnSymmetricallyAndRemainsBounded()
        {
            var presenter = new AudioFeedbackPresenter();
            float straight = presenter.SlideFrictionGain(0), turning = presenter.SlideFrictionGain(20);
            Assert.That(turning, Is.GreaterThan(straight));
            Assert.That(presenter.SlideFrictionGain(-20), Is.EqualTo(turning));
            Assert.That(presenter.SlideFrictionGain(10000), Is.EqualTo(1f));
            Assert.That(presenter.SlideFrictionGain(float.NaN), Is.EqualTo(straight));
        }
        [Test]
        public void InitialHealthIsSilentDamageIsDistinctAndHealingEscapesCriticalLoop()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState(); var id = new EntityId(1);
            p.Health(s, id, 100, 100); Assert.That(s.Commands, Is.Empty);
            p.Health(s, id, 20, 100); Assert.That(s.Commands.Count, Is.EqualTo(1));
            Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit)); Assert.That(s.IsCritical, Is.True);
            p.Health(s, id, 20, 100); Assert.That(s.Commands, Is.Empty);
            p.Health(s, id, 40, 100); Assert.That(s.Commands.Count, Is.EqualTo(1)); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.Heal)); Assert.That(s.IsCritical, Is.False);
        }
        [Test]
        public void FlashlightInitializationAndRepeatedSamplesAreSilent()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 1, true, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands, Is.Empty);
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 2, false, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.FlashlightOff));
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 3, false, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void SlideStartsAndStopsOneLoopAndTraversalDoesNotDuplicateIt()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState(); var id = new EntityId(1);
            p.Movement(s, new PlayerMovementSample(id, 1, Vector3.zero, Vector3.forward, Vector3.up, 0, Vector2.zero, false, MovementState.Slide, 0));
            Assert.That(s.Commands.Count, Is.EqualTo(2)); Assert.That(s.Commands[1].Cue, Is.EqualTo(CueId.SlideLoop));
            p.Traversal(s, new PlayerTraversalFact(id, 1, TraversalKind.Slide, true, Vector3.forward, 1)); Assert.That(s.Commands, Is.Empty);
            p.Movement(s, new PlayerMovementSample(id, 2, Vector3.forward, Vector3.forward, Vector3.up, 0, Vector2.zero, false, MovementState.Ground, 0));
            Assert.That(s.Commands[0].StopEmitter, Is.True); Assert.That(s.Commands[1].Cue, Is.EqualTo(CueId.SlideEnd));
        }
        [Test]
        public void GoldenPickupAndAttackOutcomesAreDistinctAndDeduplicated()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState(); var id = new EntityId(1);
            var fact = new PickupCollectedFact(id, 7, PickupKind.GoldenCake, 0, 1, 10);
            p.Pickup(s, fact, Vector3.zero); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.GoldenCakeCollect));
            p.Pickup(s, fact, Vector3.zero); Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(id, "rusher", HunterFeedbackKind.AttackMiss, Vector3.zero, 15)); Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(id, "rusher", HunterFeedbackKind.AttackMiss, Vector3.zero, 15)); Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(id, "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.zero, 16, 1, 7)); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.ProjectileLaunch));
        }
        [Test]
        public void SplitProjectilesAndDifferentHuntersOwnIndependentMovingLoops()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.zero, 1, 2, 1));
            int first = s.Commands[1].Emitter;
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(2), "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.one, 1, 2, 1));
            int second = s.Commands[1].Emitter; Assert.That(second, Is.Not.EqualTo(first));
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileMoved, Vector3.right, 2, 2, 1));
            Assert.That(s.Commands.Count, Is.EqualTo(1)); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.ProjectileTravel)); Assert.That(s.Commands[0].Emitter, Is.EqualTo(first));
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileImpact, Vector3.right, 3, 2, 1));
            Assert.That(s.Commands[0].StopEmitter, Is.True); Assert.That(s.Commands[0].Emitter, Is.EqualTo(first));
            Assert.That(s.FlyingProjectiles.Count, Is.EqualTo(1));
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileMoved, Vector3.right * 2, 4, 2, 1)); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void CollapseRepeatsCracksOnlyAtProgressIntervalsAndReleasesMistOnConsumption()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, 0), Vector3.zero); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.RoomCrack));
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, .01f), Vector3.zero); Assert.That(s.Commands, Is.Empty);
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, .21f), Vector3.zero); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.RoomCrack));
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Encroaching, .5f), Vector3.zero); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.MistAdvance));
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Closed, 1), Vector3.zero); Assert.That(s.Commands[0].StopEmitter, Is.True); Assert.That(s.Commands[1].Cue, Is.EqualTo(CueId.RoomConsumed));
        }
        [Test]
        public void RestartDoesNotPretendThatAStoredWardBroke()
        {
            var p = new AudioFeedbackPresenter();
            var s = new AudioFeedbackDriverState { Generation = 5, Revision = 2, Phase = ProgressionPhase.Exploring, WardCharges = 1 };
            var effects = new ProgressionEffects(1, 1, 1, 1, 100, 100, 1);
            p.Progression(s, new ProgressionSnapshot(3, 6, 1, 0, 0, 0, 0, ProgressionPhase.ChooseThreat, 100, 100, null, null, null, effects, "", false, false));
            Assert.That(s.Commands.Exists(command => command.Cue == CueId.WardBreak), Is.False);
        }
    }
}
