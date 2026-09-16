// ============================================================================
// AudioSoundscapePresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Exercises the soundscape behavior independently of Unity playback.
//   Tests prove multi-enemy hold/release and variant/priority safety without claiming perceptual sound quality.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Verify variation, cooldown, bounded polyphony and music lifecycle.
//   - Cover missing clip slots and invalid timing data.
//
// DEPENDENCIES:
//   - Core cue identities and value data; own Audio presentation stack only.
//
// USAGE NOTES:
//   Uses explicit time and seeded cosmetic randomness; no imported assets or scene mutations.
//
// ============================================================================

using NUnit.Framework;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
using Worsen.Presentation.Audio;

namespace Worsen.Tests.Audio
{
    public sealed class AudioSoundscapePresenterTests
    {
        [Test]
        public void LoopGainRefreshPreservesVariationVoiceAndClipSelectionAcrossRepeatedUpdates()
        {
            var presenter = new AudioSoundscapePresenter(); var state = State(); var bank = Bank(CueId.SlideLoop); bank.Loop = true;
            Assert.That(presenter.TryPlay(state, bank, 12, new[] { 2f, 2f }, .5f, out var first), Is.True);
            Assert.That(presenter.TryPlay(state, bank, 12, new[] { 2f, 2f }, 1f, out var turn), Is.True);
            Assert.That(turn.ReuseLoop, Is.True); Assert.That(turn.Voice, Is.EqualTo(first.Voice));
            Assert.That(turn.Gain, Is.EqualTo(first.Gain * 2f).Within(.000001f));
            for (int i = 0; i < 100; i++)
            {
                Assert.That(presenter.TryPlay(state, bank, 12, new[] { 2f, 2f }, .5f, out var straight), Is.True);
                Assert.That(straight.Gain, Is.EqualTo(first.Gain));
                Assert.That(state.LastClips[(int)CueId.SlideLoop], Is.EqualTo(first.Clip));
            }
        }
        [TestCase("Old Timber Plank", CueId.FootstepWood)]
        [TestCase("Iron Grate", CueId.FootstepMetal)]
        [TestCase("Courtyard grass", CueId.FootstepSoil)]
        [TestCase("Castle masonry", CueId.Footstep)]
        [TestCase(null, CueId.Footstep)]
        public void FootstepMaterialClassificationIsCaseInsensitiveAndHasStoneFallback(string description, CueId expected)
        { Assert.That(new AudioSoundscapePresenter().FootstepSurface(description), Is.EqualTo(expected)); }
        private AudioSoundscapeDriverState State()
        {
            var state = new AudioSoundscapeDriverState();
            new AudioSoundscapePresenter().Reset(state, 8, new System.Random(13)); return state;
        }
        private AudioSoundDefinition Bank(CueId cue, int priority = 40) => new AudioSoundDefinition
        { Cue = cue, Gain = .8f, GainVariation = .1f, PitchMinimum = .94f, PitchMaximum = 1.06f, MaxConcurrent = 16, Priority = priority };
        [Test]
        public void VariantsNeverRepeatImmediatelyAndPitchIsBounded()
        {
            var p = new AudioSoundscapePresenter(); var state = State(); int prior = -1; bool variedPitch = false; float first = 0f;
            for (int i = 0; i < 60; i++)
            {
                Assert.That(p.TryPlay(state, Bank(CueId.CakeCollect), 0, new[] { .1f, .1f, .1f }, 1f, out var play), Is.True);
                Assert.That(play.Clip, Is.Not.EqualTo(prior)); Assert.That(play.Pitch, Is.InRange(.94f, 1.06f)); Assert.That(play.Gain, Is.InRange(.72f, .8f));
                if (i == 0) first = play.Pitch; else variedPitch |= play.Pitch != first;
                prior = play.Clip; p.Tick(state, .2f, 1f, 1f, 1f);
            }
            Assert.That(variedPitch, Is.True);
        }
        [Test]
        public void OneClipAndNullSlotsRemainPlayableWithoutSelectionFailure()
        {
            var p = new AudioSoundscapePresenter(); var s = State();
            Assert.That(p.TryPlay(s, Bank(CueId.CakeCollect), 0, new[] { 0f, .1f, float.NaN }, 1, out var a), Is.True);
            p.Tick(s, 1, 1, 1, 1);
            Assert.That(p.TryPlay(s, Bank(CueId.CakeCollect), 0, new[] { 0f, .1f, float.NaN }, 1, out var b), Is.True);
            Assert.That(a.Clip, Is.EqualTo(1)); Assert.That(b.Clip, Is.EqualTo(1));
            Assert.That(p.TryPlay(s, Bank(CueId.EnemyHit), 0, new[] { 0f, float.NaN }, 1, out _), Is.False);
        }
        [Test]
        public void CooldownIsPerEmitterAndLoopRefreshDoesNotRestart()
        {
            var p = new AudioSoundscapePresenter(); var s = State(); var bank = Bank(CueId.TorchLoop); bank.Loop = true; bank.Cooldown = 4;
            Assert.That(p.TryPlay(s, bank, 7, new[] { 2f, 2f }, 1, out var first), Is.True);
            Assert.That(p.TryPlay(s, bank, 7, new[] { 2f, 2f }, 1, out var repeat), Is.True);
            Assert.That(repeat.ReuseLoop, Is.True); Assert.That(repeat.Voice, Is.EqualTo(first.Voice));
            bank.Loop = false;
            Assert.That(p.TryPlay(s, bank, 7, new[] { 2f, 2f }, 1, out _), Is.False);
            Assert.That(p.TryPlay(s, bank, 8, new[] { 2f, 2f }, 1, out _), Is.True);
        }
        [Test]
        public void ImportantWarningsStealOnlyLowerPriorityVoices()
        {
            var p = new AudioSoundscapePresenter(); var s = State();
            for (int i = 0; i < 8; i++) Assert.That(p.TryPlay(s, Bank(CueId.ChainCreak, 15), i, new[] { 3f }, 1, out _), Is.True);
            Assert.That(p.TryPlay(s, Bank(CueId.ChainCreak, 15), 9, new[] { 3f }, 1, out _), Is.False);
            Assert.That(p.TryPlay(s, Bank(CueId.EnemyWindup, 80), 9, new[] { 3f }, 1, out var warning), Is.True);
            Assert.That(s.Voices[warning.Voice].Priority, Is.EqualTo(80)); Assert.That(s.Duck, Is.GreaterThan(0));
        }
        [Test]
        public void MultipleHuntersKeepMusicUpUntilAllLoseThenHoldAndRelease()
        {
            var p = new AudioSoundscapePresenter(); var s = State();
            s.Threats[1] = new AudioThreatSample { Chasing = true, Closeness = .8f };
            s.Threats[2] = new AudioThreatSample { Chasing = true, Closeness = .3f };
            p.Tick(s, 1, .5f, 2, 2); Assert.That(s.ChaseGain, Is.EqualTo(1));
            s.Threats.Remove(1); p.Tick(s, 1, .5f, 2, 2); Assert.That(s.ChaseGain, Is.EqualTo(1));
            s.Threats.Clear(); p.Tick(s, 1, .5f, 2, 2); Assert.That(s.ChaseGain, Is.EqualTo(1));
            p.Tick(s, 1.1f, .5f, 2, 2); Assert.That(s.ChaseGain, Is.LessThan(1).And.GreaterThan(0));
            p.Tick(s, 2, .5f, 2, 2); Assert.That(s.ChaseGain, Is.Zero);
        }
        [Test]
        public void DeathAndResetClearThreatMixAndVariationLeases()
        {
            var p = new AudioSoundscapePresenter(); var s = State();
            s.Threats[1] = new AudioThreatSample { Chasing = true, Closeness = 1 };
            p.Tick(s, 1, .5f, 1, 2); s.Alive = false; p.Tick(s, 2, .5f, 1, 2);
            Assert.That(s.ChaseGain, Is.Zero); Assert.That(s.DangerGain, Is.Zero);
            p.Reset(s, 8, new System.Random(1)); Assert.That(s.Threats, Is.Empty); Assert.That(s.Cooldowns, Is.Empty); Assert.That(s.LastClips, Is.Empty);
        }
        [Test]
        public void OpennessCrossfadesBedsAndInvalidDeltaDoesNotAdvance()
        {
            var p = new AudioSoundscapePresenter(); var s = State(); s.Openness = 1;
            p.Tick(s, 2, 1, 1, 1); Assert.That(s.ExteriorGain, Is.EqualTo(1)); Assert.That(s.InteriorGain, Is.Zero);
            float time = s.Time; p.Tick(s, float.NaN, 1, 1, 1); Assert.That(s.Time, Is.EqualTo(time));
        }
    }
}
