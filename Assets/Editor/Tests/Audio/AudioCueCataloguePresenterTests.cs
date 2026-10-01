// ============================================================================
// AudioCueCataloguePresenterTests.cs
// ============================================================================
// PURPOSE:
//   Checks cue aliases and voice ownership directly at the catalogue boundary.
//   Invalid cues and interface sounds must not leak into the active-floor budget.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Verify canonical aliases, admission gates, hunter identity and protected tells.
// DEPENDENCIES:
//   - Core cue values, Presentation Audio and NUnit only.
// USAGE NOTES:
//   Pure Edit Mode tests; no audio source, configuration asset or random stream.
// ============================================================================
using NUnit.Framework;
using Worsen.Core;
using Worsen.Presentation.Audio;

namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioCueCataloguePresenterTests
    {
        private readonly AudioCueCataloguePresenter catalogue = new AudioCueCataloguePresenter();
        [TestCase(CueId.GrabWarning)][TestCase(CueId.GrabStart)][TestCase(CueId.GrabHit)][TestCase(CueId.GrabEscape)]
        [TestCase(CueId.RoomTear)][TestCase(CueId.MistAdvance)][TestCase(CueId.RoomConsumed)]
        public void CollapseCuesAdmitWithoutJitterAndDifferentRoomsCanSoundTogether(CueId cue)
        {
            Assert.That(catalogue.Admits(cue, true), Is.True);
            catalogue.TryGet(cue, out var entry);
            Assert.That(entry.TimingIsTell, Is.True);
            Assert.That(catalogue.SameVoice(entry, 1, entry, 2), Is.False);
            var presenter = new AudioSoundscapePresenter(); var state = new AudioSoundscapeDriverState();
            presenter.Reset(state, 8, new System.Random(7)); state.InRun = true;
            var bank = new AudioSoundDefinition { Cue = cue, Gain = .15f, PitchMinimum = 1f, PitchMaximum = 1f, Priority = 50 };
            for (int room = 1; room <= 3; room++)
            {
                Assert.That(presenter.TryPlay(state, bank, room, new[] { 2f }, 1f, out var playback, .3f), Is.True);
                Assert.That(playback.Delay, Is.Zero); Assert.That(playback.Gain, Is.EqualTo(.15f));
            }
            Assert.That(presenter.TryPlay(state, bank, 1, new[] { 2f }, 1f, out var repeated), Is.True);
            Assert.That(repeated.Voice, Is.Zero, "A fresh fact rearticulates its room, not an extra voice.");
        }
        [TestCase(CueId.FootstepWood, CueId.Footstep)]
        [TestCase(CueId.FootstepMetal, CueId.Footstep)]
        [TestCase(CueId.FootstepSoil, CueId.Footstep)]
        [TestCase(CueId.ExitOpen, CueId.DoorOpen)]
        [TestCase(CueId.RoomCrack, CueId.RoomTelegraph)]
        public void AliasesShareCanonicalBudgetEntries(CueId alias, CueId canonical)
        {
            Assert.That(catalogue.Canonical(alias), Is.EqualTo(canonical));
            Assert.That(catalogue.TryGet(alias, out var a), Is.True);
            Assert.That(catalogue.TryGet(canonical, out var b), Is.True);
            Assert.That(a, Is.EqualTo(b));
        }
        [Test]
        public void InterfaceIsOutsideRunAndUnknownOrRemovedCuesNeverAdmit()
        {
            Assert.That(catalogue.Admits(CueId.UiConfirm, false), Is.True);
            Assert.That(catalogue.Admits(CueId.UiConfirm, true), Is.False);
            foreach (var cue in new[] { (CueId)int.MaxValue, CueId.EnemyRecovery })
            {
                Assert.That(catalogue.Admits(cue, false), Is.False);
                Assert.That(catalogue.Admits(cue, true), Is.False);
            }
        }
        [Test]
        public void HunterSlotsArePerOwnerWhileWorldSlotsShareAndAttackTimingIsProtected()
        {
            catalogue.TryGet(CueId.Presence, out var presence);
            catalogue.TryGet(CueId.EnemyFootstep, out var footstep);
            catalogue.TryGet(CueId.EnemyWindup, out var attack);
            catalogue.TryGet(CueId.CakeCollect, out var cake);
            catalogue.TryGet(CueId.GoldenCakeCollect, out var golden);
            Assert.That(catalogue.SameVoice(presence, 1, footstep, 1), Is.True);
            Assert.That(catalogue.SameVoice(presence, 1, footstep, 2), Is.False);
            Assert.That(catalogue.SameVoice(presence, 1, attack, 1), Is.False);
            Assert.That(catalogue.SameVoice(cake, 1, golden, 2), Is.True);
            Assert.That(attack.Protected && attack.TimingIsTell, Is.True);
            Assert.That(attack.Category, Is.EqualTo(CueCategory.Hunter));
            Assert.That(attack.Slot, Is.EqualTo((int)HunterCueSlot.AttackTiming));
        }
    }
}
