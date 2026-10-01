// ============================================================================
// AudioRosterPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies expansion cue mapping with value facts only, without native sources.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Cover fixed Herald pitch, emission-clock deduplication and origin identity.
//   - Preserve silent hunters, five slots, committed tells and confirmed catch gating.
//   - Cover all currently authored mutation identifiers across floor/run resets.
// DEPENDENCIES:
//   - Core facts, Audio presenter/state and NUnit.
// USAGE NOTES:
//   Pure decision coverage; does not establish audibility or Session subscriptions.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioRosterPresenterTests
    {
        private AudioRosterPresenter presenter;
        private AudioRosterDriverState state;
        private readonly EntityId hunter = new EntityId(7);
        [SetUp] public void Setup() { presenter = new AudioRosterPresenter(); state = new AudioRosterDriverState(); }
        [TestCase(HeraldSound.Discovery, HunterCueSlot.Detection)]
        [TestCase(HeraldSound.ChaseOne, HunterCueSlot.ChaseLayer)]
        [TestCase(HeraldSound.ChaseTwo, HunterCueSlot.ChaseLayer)]
        [TestCase(HeraldSound.Attack, HunterCueSlot.AttackTiming)]
        public void HeraldUsesEmissionTickAndAcousticOriginNotOldPlayerClue(HeraldSound sound, HunterCueSlot slot)
        {
            var origin = new Vector3(2, 3, 4); var clue = new Vector3(9, 8, 7);
            var fact = new HeraldScreamFact(hunter, sound, "fixed-id", .94f,
                new NoiseEvent(hunter, origin, 1f, 10), new NoiseEvent(hunter, clue, 1f, 10), 2, false);
            Assert.That(presenter.Herald(state, fact, out var command), Is.True);
            Assert.That(command.Id, Is.EqualTo("fixed-id")); Assert.That(command.Slot, Is.EqualTo(slot));
            Assert.That(command.Position, Is.EqualTo(origin)); Assert.That(command.Exact, Is.True);
            Assert.That(command.Pitch, Is.EqualTo(sound == HeraldSound.Attack ? 1f : .94f));
            Assert.That(presenter.Herald(state, fact, out _), Is.False);
            var next = new HeraldScreamFact(hunter, sound, "fixed-id", .94f,
                new NoiseEvent(hunter, origin, 1f, 11), new NoiseEvent(hunter, clue, 1f, 11), 2, false);
            Assert.That(presenter.Herald(state, next, out _), Is.True, "Repeated last-known clue must not suppress a new scream.");
        }
        [Test] public void BreathUsesProtectedAttackSlotAndSuppliedDuration()
        {
            var fact = new HeraldBreathFact(hunter, Vector3.zero, 1, .8f);
            Assert.That(presenter.HeraldBreath(state, fact, out var command), Is.True);
            Assert.That(command.Id, Is.EqualTo("herald.breath")); Assert.That(command.Slot, Is.EqualTo(HunterCueSlot.AttackTiming));
            Assert.That(command.Interval, Is.EqualTo(.8f)); Assert.That(command.Exact, Is.True);
            Assert.That(presenter.HeraldBreath(state, fact, out _), Is.False);
        }
        [TestCase("mannequin")][TestCase("mimic")][TestCase("herald")][TestCase("blinder")][TestCase("stare")]
        public void GenericFeedbackCannotDuplicateDedicatedSoundsOrBreakSilence(string key)
        {
            foreach (HunterFeedbackKind kind in System.Enum.GetValues(typeof(HunterFeedbackKind)))
                Assert.That(presenter.Feedback(state, new HunterFeedbackEvent(hunter, key, kind, Vector3.zero, 1), out _), Is.False);
            Assert.That(state.Archetypes[hunter], Is.EqualTo(key));
        }
        [TestCase(BlinderSound.Presence, HunterCueSlot.Presence)]
        [TestCase(BlinderSound.Detection, HunterCueSlot.Detection)]
        [TestCase(BlinderSound.Chase, HunterCueSlot.ChaseLayer)]
        [TestCase(BlinderSound.ThrowHiss, HunterCueSlot.AttackTiming)]
        [TestCase(BlinderSound.TrapTick, HunterCueSlot.AttackTiming)]
        public void BlinderSoundsUseExistingSlotsAndDeduplicate(BlinderSound sound, HunterCueSlot slot)
        {
            var fact = new BlinderSoundFact(hunter, sound, Vector3.zero, 1, 1, default);
            Assert.That(presenter.Blinder(state, fact, out var command), Is.True);
            Assert.That(command.Slot, Is.EqualTo(slot)); Assert.That(command.Exact, Is.EqualTo(slot == HunterCueSlot.AttackTiming));
            Assert.That(presenter.Blinder(state, fact, out _), Is.False);
        }
        [TestCase(RamFactKind.Stamp, HunterCueSlot.AttackTiming)]
        [TestCase(RamFactKind.Bellow, HunterCueSlot.AttackTiming)]
        [TestCase(RamFactKind.Stride, HunterCueSlot.Presence)]
        [TestCase(RamFactKind.WallStagger, HunterCueSlot.Presence)]
        [TestCase(RamFactKind.PartitionImpact, HunterCueSlot.Presence)]
        public void RamTellsAndImpactsDoNotAddBudgetSlots(RamFactKind kind, HunterCueSlot slot)
        {
            var fact = new RamFact(hunter, kind, 1, Vector3.zero, Vector3.forward, "ram-test");
            Assert.That(presenter.Ram(state, fact, out var command), Is.True); Assert.That(command.Slot, Is.EqualTo(slot));
            Assert.That(command.Id, Is.EqualTo("ram-test")); Assert.That(presenter.Ram(state, fact, out _), Is.False);
        }
        [Test] public void UnconfirmedWinsAndCatchSoundsDoNotPlayDeath()
        {
            Assert.That(presenter.Ram(state, new RamFact(hunter, RamFactKind.Won, 1, default, default, "ram-win"), out _), Is.False);
            Assert.That(presenter.Blinder(state, new BlinderSoundFact(hunter, BlinderSound.Catch, default, 1, 1, default), out _), Is.False);
            Assert.That(presenter.Mimic(state, new MimicFact(hunter, default, MimicFactKind.Won, 1, default), out _), Is.False);
            Assert.That(presenter.Stare(state, new StareFact(hunter, StareFactKind.Sound, default, 1, "stare.catch", slot: HunterCueSlot.DeathSting), out _), Is.False);
        }
        [Test] public void MimicBiteAndStareCallRetainIdentityAndCurseVolume()
        {
            var bite = new MimicFact(hunter, new EntityId(2), MimicFactKind.BiteStarted, 1, default, soundId: "mimic-wrong-bite");
            Assert.That(presenter.Mimic(state, bite, out var command), Is.True); Assert.That(command.Exact, Is.True);
            Assert.That(command.Slot, Is.EqualTo(HunterCueSlot.AttackTiming)); Assert.That(presenter.Mimic(state, bite, out _), Is.False);
            var call = new StareFact(hunter, StareFactKind.Sound, default, 2, "stare.find-me", .3f);
            Assert.That(presenter.Stare(state, call, out command), Is.True); Assert.That(command.Gain, Is.EqualTo(.3f));
            Assert.That(command.Id, Is.EqualTo("stare.find-me")); Assert.That(presenter.Stare(state, call, out _), Is.False);
        }
        [Test] public void MannequinSilenceFactRetainsIdentityForConfirmedSnap()
        {
            presenter.Mannequin(state, new MannequinFact(hunter, MannequinFactKind.SilentSoundSet, 1));
            Assert.That(state.Archetypes[hunter], Is.EqualTo("mannequin"));
            Assert.That(presenter.Habit(state, new HunterHabitFact(hunter, HunterHabitKind.TurnToFace, default, 2), out _), Is.False);
        }
        [TestCase("echo.quickened-recording")][TestCase("weaver-quickened-skitter")]
        [TestCase("blinder-quickened-approach")][TestCase("herald-quickened-approach")]
        [TestCase("mannequin.long-step")][TestCase("stare.quickened-gaze")]
        public void MutationTellQueuesOnceAndSurvivesOnlyFloorReset(string tell)
        {
            var fact = new ProgressionEventFact(1, 3, ProgressionEventKind.HiddenMutation, ProgressionEventKind.HiddenMutation, FearAxis.None, "effect", "hunter", tell);
            presenter.Mutation(state, fact); presenter.Mutation(state, fact);
            Assert.That(state.PendingTells.Count, Is.EqualTo(1)); Assert.That(state.PendingTells[0].Id, Is.EqualTo(tell));
            Assert.That(state.PendingTells[0].Exact, Is.True);
            presenter.Reset(state, true); presenter.Mutation(state, fact); Assert.That(state.PendingTells.Count, Is.EqualTo(1));
            presenter.Reset(state, false); Assert.That(state.PendingTells, Is.Empty);
        }
        [TestCase(false)][TestCase(true)]
        public void RawSensoryFactsReadUpgradesOnceAndIgnoreDuplicates(bool upgraded)
        {
            var mix = new AudioWorldMixPresenter(); var world = new AudioWorldMixDriverState(); var player = new EntityId(2);
            mix.SetEffects(world, upgraded ? new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("ear-plugs"), EffectKind.Upgrade, 3),
                new ActiveEffect(new EffectId("mirror-skin"), EffectKind.Upgrade, 3) }) : null);
            var deafen = new HeraldDeafenFact(hunter, player, 1, default, 5, 1, 4, false);
            var hit = new BlinderHitFact(hunter, player, 1, 1, 6, true);
            Assert.That(presenter.AcceptHeraldDeafen(state, deafen), Is.True);
            Assert.That(presenter.AcceptBlinderHit(state, hit), Is.True);
            mix.HeraldDeafen(world, deafen, .5f); mix.BlinderHit(world, hit, .5f);
            Assert.That(world.DeafenedRemaining, Is.EqualTo(upgraded ? 2f : 4f));
            Assert.That(world.MuffledRemaining, Is.EqualTo(upgraded ? 3f : 6f));
            Assert.That(presenter.AcceptHeraldDeafen(state, deafen), Is.False);
            Assert.That(presenter.AcceptBlinderHit(state, hit), Is.False);
            mix.SetEffects(world, null); Assert.That(world.EarPlugs || world.MirrorSkin, Is.False);
            world.MuffledRemaining = 0f;
            mix.BlinderHit(world, new BlinderHitFact(hunter, player, 2, 2, 6, false), .5f);
            Assert.That(world.MuffledRemaining, Is.Zero);
        }
        [TestCase(float.NaN)][TestCase(float.PositiveInfinity)][TestCase(-1f)][TestCase(0f)]
        public void InvalidSensoryDurationsDoNotActivate(float seconds)
        {
            var mix = new AudioWorldMixPresenter(); var world = new AudioWorldMixDriverState();
            mix.HeraldDeafen(world, new HeraldDeafenFact(hunter, new EntityId(2), 1, default, 5, 1, seconds, false), .5f);
            mix.BlinderHit(world, new BlinderHitFact(hunter, new EntityId(2), 1, 1, seconds, true), .5f);
            Assert.That(world.DeafenedRemaining + world.MuffledRemaining, Is.Zero);
        }
    }
}
