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
//   - Keep lethal health silent and admit one sting only at catch hold start, reset per run.
//   - Verify ordinary pickups never become combo stings, and removed cues stay silent.
//   - Verify per-action mapping and duplicate suppression.
//   - Cover every typed progression operation, including intentionally silent transactions.
//   - Verify silent initialization/death, admitted injury and bounded collapse transitions.
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
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioFeedbackPresenterTests
    {
        [Test]
        public void HealthOwnsInjuryWhileHandImpactIsDistinctAndDeathWaitsForCatch()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var player = new EntityId(1);
            presenter.Health(state, player, 100, 100);
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.AttackHit, Vector3.zero, 10)); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, player, 90, 100); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            presenter.Health(state, player, 80, 100); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.AttackHit, Vector3.zero, 11)); Assert.That(state.Commands, Is.Empty);
            presenter.Hand(state, new CollapseHandFact(player, 3, CollapseHandEventKind.Hit, Vector3.zero, 1, 10, 12)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.GrabHit));
            presenter.Health(state, player, 70, 100); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            presenter.Hand(state, new CollapseHandFact(player, 3, CollapseHandEventKind.Consumed, Vector3.zero, 1, 100, 13)); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, player, 0, 100); Assert.That(state.Commands, Is.Empty);
            presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(2), "rusher", HunterFeedbackKind.Scream, Vector3.zero, 14)); Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void CatchStingAdmitsOnlyOneValidHoldStartUntilRunStateResets()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var player = new EntityId(1);
            presenter.Health(state, player, 100, 100);
            presenter.Health(state, player, 0, 100);
            Assert.That(state.Commands, Is.Empty);
            Assert.That(state.CatchStingIssued, Is.False);
            Assert.That(presenter.TryCatchSting(state, default), Is.False);
            Assert.That(presenter.TryCatchSting(state, player), Is.True);
            Assert.That(presenter.TryCatchSting(state, player), Is.False);
            Assert.That(presenter.TryCatchSting(state, new EntityId(2)), Is.False);
            // AudioDriver.ResetRun replaces this state rather than resetting unrelated observations piecemeal.
            state = new AudioFeedbackDriverState();
            Assert.That(presenter.TryCatchSting(state, player), Is.True);
        }

        [Test]
        public void SwingOwnsOneWhooshAndSeparateHuntersStillSoundIndependently()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            for (int hunter = 1; hunter <= 2; hunter++)
            {
                presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(hunter), "rusher", HunterFeedbackKind.AttackWindup, Vector3.zero, 10));
                Assert.That(state.Commands.Count, Is.EqualTo(1)); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.EnemyWindup)); Assert.That(state.Commands[0].Emitter, Is.EqualTo(hunter));
                presenter.Hunter(state, new HunterFeedbackEvent(new EntityId(hunter), "rusher", HunterFeedbackKind.AttackMiss, Vector3.zero, 11)); Assert.That(state.Commands, Is.Empty);
            }
        }
        [Test]
        public void RapidPickupsKeepTheirSingleCueWithoutAChainSting()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            for (int i = 1; i <= 6; i++)
            {
                var kind = i == 6 ? PickupKind.GoldenCake : PickupKind.Cake;
                var fact = new PickupCollectedFact(new EntityId(1), i, kind, 0, 1, i);
                presenter.Pickup(state, fact, Vector3.zero); Assert.That(state.Commands.Count, Is.EqualTo(1));
                Assert.That(state.Commands[0].Cue, Is.EqualTo(i == 6 ? CueId.GoldenCakeCollect : CueId.CakeCollect));
                Assert.That(state.Commands.Exists(command => command.Cue == CueId.CakeChain), Is.False);
                presenter.Pickup(state, fact, Vector3.zero); Assert.That(state.Commands, Is.Empty);
            }
        }
        [Test]
        public void PostureSeedsSilentlyAndOnlyCommittedTransitionsRustleOutsideSlides()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            presenter.Movement(state, PostureSample(1, true, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(2, false, false));
            Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(2, true, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(3, false, false)); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(4, true, false, MovementState.Slide));
            Assert.That(state.Commands.Exists(command => command.Cue == CueId.PostureRustle), Is.False);
            presenter.Movement(state, PostureSample(5, false, false));
            Assert.That(state.Commands.Exists(command => command.Cue == CueId.PostureRustle), Is.False);
            presenter.Movement(state, PostureSample(6, true, false));
            Assert.That(state.Commands, Is.Empty);
        }
        [Test]
        public void ExertionRampsOneEmitterReleasesAndDoesNotInferSprintFromVelocity()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            presenter.Movement(state, PostureSample(1, false, false));
            presenter.TickExertion(state, .2f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
            presenter.Movement(state, PostureSample(2, false, true)); Assert.That(state.Commands, Is.Empty);
            presenter.TickExertion(state, .2f, .8f, 1f, 0);
            Assert.That(state.Commands, Is.Empty, "Pursuit breathing replaces the extra sprint envelope.");
            Assert.That(state.ExertionGain, Is.Zero);
        }
        [Test]
        public void CriticalHealthSuppressesExertionAndDeathClearsItsEnvelope()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState(); var id = new EntityId(1);
            presenter.Health(state, id, 20, 100);
            presenter.Movement(state, PostureSample(1, false, true));
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, id, 100, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.ExertionGain, Is.Zero);
            presenter.Health(state, id, 20, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.Commands, Is.Empty);
            presenter.Health(state, id, 100, 100);
            presenter.TickExertion(state, 1f, .8f, 1f, 0);
            presenter.Health(state, id, 0, 100);
            presenter.TickExertion(state, .001f, .8f, 1f, 0);
            Assert.That(state.ExertionGain, Is.Zero); Assert.That(state.IsSprinting, Is.False); Assert.That(state.Commands, Is.Empty);
        }
        private static PlayerMovementSample PostureSample(long tick, bool crouched, bool sprinting, MovementState movement = MovementState.Ground) =>
            new PlayerMovementSample(new EntityId(1), tick, Vector3.forward * tick, Vector3.forward * 12f, Vector3.up, 0, Vector2.zero, false, movement, 0, 0, crouched, sprinting);
        [Test]
        public void NewFloorReseedsPostureAndClearsExertionIntentAndEnvelope()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState { Generation = 5, Revision = 2 };
            presenter.Movement(state, PostureSample(99, false, true));
            presenter.TickExertion(state, 1f, .8f, 1f, 0); Assert.That(state.ExertionActive, Is.False);
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
            p.Health(s, id, 40, 100); Assert.That(s.Commands, Is.Empty); Assert.That(s.IsCritical, Is.False);
        }
        [Test]
        public void FlashlightInitializationAndRepeatedSamplesAreSilent()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 1, true, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands, Is.Empty);
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 2, false, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands, Is.Empty);
            p.Flashlight(s, new FlashlightSample(new EntityId(1), 3, false, Vector3.zero, Vector3.forward, 20, 40)); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void SlideStartsAndStopsOneLoopAndTraversalDoesNotDuplicateIt()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState(); var id = new EntityId(1);
            p.Movement(s, new PlayerMovementSample(id, 1, Vector3.zero, Vector3.forward, Vector3.up, 0, Vector2.zero, false, MovementState.Slide, 0));
            Assert.That(s.Commands.Count, Is.EqualTo(1)); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.SlideLoop));
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
            p.Hunter(s, new HunterFeedbackEvent(id, "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.zero, 16, 1, 7)); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void SplitProjectilesAndDifferentHuntersOwnIndependentMovingLoops()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.zero, 1, 2, 1));
            Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(2), "hexer", HunterFeedbackKind.ProjectileLaunched, Vector3.one, 1, 2, 1));
            Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileMoved, Vector3.right, 2, 2, 1));
            Assert.That(s.Commands, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileImpact, Vector3.right, 3, 2, 1));
            Assert.That(s.Commands, Is.Empty); Assert.That(s.FlyingProjectiles, Is.Empty);
            p.Hunter(s, new HunterFeedbackEvent(new EntityId(1), "hexer", HunterFeedbackKind.ProjectileMoved, Vector3.right * 2, 4, 2, 1)); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void CollapsePhasesEmitOncePerRoomWithoutAnotherPulseOrLoopClock()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, 0), Vector3.zero); Assert.That(s.Commands, Is.Empty);
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, .01f), Vector3.zero); Assert.That(s.Commands, Is.Empty);
            p.Room(s, new RoomDestructionSample(3, RoomPhase.Telegraph, .21f), Vector3.zero); Assert.That(s.Commands, Is.Empty);
            foreach (var pair in new[] { (RoomPhase.Tearing, CueId.RoomTear), (RoomPhase.Encroaching, CueId.MistAdvance), (RoomPhase.Closed, CueId.RoomConsumed) })
            foreach (int room in new[] { 3, 4 })
            {
                var fact = new RoomDestructionSample(room, pair.Item1, .5f);
                p.Room(s, fact, Vector3.right * room);
                Assert.That(s.Commands.Count, Is.EqualTo(1)); Assert.That(s.Commands[0].Cue, Is.EqualTo(pair.Item2));
                Assert.That(s.Commands[0].Emitter, Is.EqualTo(room)); Assert.That(s.Commands[0].Position, Is.EqualTo(Vector3.right * room));
                p.Room(s, fact, Vector3.right * room); Assert.That(s.Commands, Is.Empty);
            }
            Assert.That(s.Rooms[3].Phase, Is.EqualTo(RoomPhase.Closed));
        }
        [TestCase(CollapseHandEventKind.Warning, CueId.GrabWarning)]
        [TestCase(CollapseHandEventKind.Grabbed, CueId.GrabStart)]
        [TestCase(CollapseHandEventKind.Hit, CueId.GrabHit)]
        [TestCase(CollapseHandEventKind.Escaped, CueId.GrabEscape)]
        public void HandTransitionsRetainRoomPositionAndDeduplicate(CollapseHandEventKind kind, CueId cue)
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState();
            foreach (int room in new[] { 3, 4 })
            {
                var fact = new CollapseHandFact(new EntityId(1), room, kind, Vector3.right * room, 1, 10, 7);
                p.Hand(s, fact); Assert.That(s.Commands.Count, Is.EqualTo(1));
                Assert.That(s.Commands[0].Cue, Is.EqualTo(cue)); Assert.That(s.Commands[0].Emitter, Is.EqualTo(room));
                Assert.That(s.Commands[0].Position, Is.EqualTo(fact.Position));
                p.Hand(s, fact); Assert.That(s.Commands, Is.Empty);
            }
        }
        [Test] public void InvalidAndRepeatedHealthCannotProduceHitOrPoisonNextObservation()
        {
            var p = new AudioFeedbackPresenter(); var s = new AudioFeedbackDriverState(); var id = new EntityId(1);
            p.Health(s, id, 100, 100);
            p.Health(s, id, float.NaN, 100); Assert.That(s.Commands, Is.Empty);
            p.Health(s, id, 90, 100); Assert.That(s.Commands[0].Cue, Is.EqualTo(CueId.PlayerHit));
            p.Health(s, id, 90, 100); Assert.That(s.Commands, Is.Empty);
            p.Health(s, default, 80, 100); Assert.That(s.Commands, Is.Empty);
            p.Health(s, id, 0, 100); Assert.That(s.Commands, Is.Empty);
        }
        [Test]
        public void EveryOperationPreservesShopCueAndDuplicateSuppression([Values] ProgressionOperation operation)
        {
            var presenter = new AudioFeedbackPresenter();
            var state = new AudioFeedbackDriverState { Phase = ProgressionPhase.Shop };
            var before = TransactionSnapshot(1, ProgressionPhase.Shop);
            var after = TransactionSnapshot(2, ProgressionPhase.Shop);
            presenter.Transaction(state, before, after, operation);
            if (operation == ProgressionOperation.Purchase || operation == ProgressionOperation.RerollShop)
            {
                Assert.That(state.Commands.Count, Is.EqualTo(1));
                Assert.That(state.Commands[0].Cue, Is.EqualTo(operation == ProgressionOperation.Purchase ? CueId.ShopBuy : CueId.UiMove));
            }
            else Assert.That(state.Commands, Is.Empty);
            presenter.Transaction(state, before, after, operation);
            Assert.That(state.Commands, Is.Empty, "A replayed revision never sounds twice.");
        }

        [Test]
        public void EveryOperationStaysSilentOutsideShop([Values] ProgressionOperation operation)
        {
            var state = new AudioFeedbackDriverState();
            new AudioFeedbackPresenter().Transaction(state, TransactionSnapshot(1, ProgressionPhase.Exploring),
                TransactionSnapshot(2, ProgressionPhase.Exploring), operation);
            Assert.That(state.Commands, Is.Empty);
        }

        [Test]
        public void PendingPurchaseAndUncommittedRevisionStaySilent()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            var before = TransactionSnapshot(1, ProgressionPhase.Shop);
            presenter.Transaction(state, before, TransactionSnapshot(2, ProgressionPhase.Shop, "wax-ward"), ProgressionOperation.Purchase);
            Assert.That(state.Commands, Is.Empty);
            presenter.Transaction(state, before, before, ProgressionOperation.RerollShop);
            Assert.That(state.Commands, Is.Empty);
        }

        private static ProgressionSnapshot TransactionSnapshot(int revision, ProgressionPhase phase, string pending = null) =>
            new ProgressionSnapshot(revision, 1, 1, 0, 10, 0, 0, phase, 100, 100,
                null, null, null, default, "", false, false, pendingOfferId: pending);

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
