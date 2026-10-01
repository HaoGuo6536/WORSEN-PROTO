// ============================================================================
// AudioWorldMixPresenterTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the silence-first catalogue, acoustics, variation and embodiment contracts.
//   These tests use supplied topology, time and random streams, not scene queries.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
// KEY RESPONSIBILITIES:
//   - Fail on unclassified cues, missing environmental noise kinds and budget leaks.
//   - Cover protected voices, false-positive limits, targeted effects and transaction identity.
// DEPENDENCIES:
//   - Core contracts, Presentation Audio, NUnit and transient config fixtures.
// USAGE NOTES:
//   Coordinator executes in Edit Mode. Mapping parity is not proof that every Session
//   producer publishes its required NoiseEvent; the integration hand-off lists missing producers.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;

namespace Worsen.Tests.Audio
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioWorldMixPresenterTests
    {
        private AudioSoundscapeDriverConfig _config;
        private readonly AudioWorldMixPresenter _mix = new AudioWorldMixPresenter();
        private readonly AudioCueCataloguePresenter _catalogue = new AudioCueCataloguePresenter();
        [SetUp] public void SetUp() => _config = ScriptableObject.CreateInstance<AudioSoundscapeDriverConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_config);
        public static LevelGraph Graph() => new LevelGraph(new[] {
            new LevelRoom(1, Vector3.zero, new Vector3(8, 8, 8)),
            new LevelRoom(2, Vector3.right * 8, new Vector3(8, 8, 8)),
            new LevelRoom(3, Vector3.right * 16, new Vector3(8, 8, 8)) },
            new[] { new LevelEdge(1, 1, 2, true), new LevelEdge(2, 2, 3, true) }, Array.Empty<LevelAnchor>(), 3, Vector3.right * 16);
        private AudioCueCatalogueEntry Entry(CueId cue) { Assert.That(_catalogue.TryGet(cue, out var entry), Is.True); return entry; }
        private AudioWorldMixDriverState State() { var state = new AudioWorldMixDriverState(); _mix.SetWorld(state, Graph(), null); return state; }
        [Test] public void EveryCueIsExplicitlyBudgetedOrExplicitlyRemovedAndWorldCuesHaveNoiseKinds()
        {
            var removed = new HashSet<CueId> { CueId.Lose, CueId.Jump, CueId.SlideStart, CueId.FlashlightOn, CueId.FlashlightOff,
                CueId.PlayerHit, CueId.GrabWarning, CueId.GrabStart, CueId.GrabEscape, CueId.GrabHit, CueId.Consumed,
                CueId.CakeChain, CueId.EnemyAttack, CueId.EnemyMiss, CueId.EnemyHit, CueId.EnemyRecovery, CueId.EnemyLost,
                CueId.RoomTear, CueId.MistAdvance, CueId.RoomConsumed, CueId.WindLoop, CueId.Drip, CueId.Heal,
                CueId.RoundStart, CueId.ProjectileLaunch, CueId.ProjectileTravel, CueId.ProjectileImpact,
                CueId.TraversalMiss, CueId.PostureRustle, CueId.SprintExertion };
            foreach (CueId cue in Enum.GetValues(typeof(CueId)))
            {
                bool accepted = _catalogue.TryGet(cue, out var entry);
                Assert.That(accepted ^ removed.Contains(cue), Is.True, "Unclassified or contradictory budget entry: " + cue);
                Assert.That(_catalogue.Admits(cue, false), Is.EqualTo(accepted));
                if (!accepted) continue;
                if (entry.Category == CueCategory.World) Assert.That(entry.Noise.HasValue, Is.True, cue.ToString());
                if (entry.Noise.HasValue) Assert.That(Enum.IsDefined(typeof(NoiseSourceKind), entry.Noise.Value), Is.True);
                Assert.That(_catalogue.Admits(cue, true), Is.EqualTo(entry.Category != CueCategory.Interface));
            }
            Assert.That(_catalogue.TryGet((CueId)int.MaxValue, out _), Is.False);
            Assert.That(_catalogue.FalsePositiveExempt(CueId.Footstep), Is.True);
            Assert.That(_catalogue.FalsePositiveExempt(CueId.ChainCreak), Is.True);
            foreach (CueId cue in Enum.GetValues(typeof(CueId)))
                if (cue != CueId.Footstep && cue != CueId.ChainCreak) Assert.That(_catalogue.FalsePositiveExempt(cue), Is.False);
        }
        [Test] public void OcclusionIsMonotonicWithPortalsAndClosedDoorsAndFailsClosedWithoutTopology()
        {
            var state = State(); var entry = Entry(CueId.DoorOpen);
            float local = _mix.Gain(state, entry, Vector3.zero, _config);
            float one = _mix.Gain(state, entry, Vector3.right * 8, _config);
            float two = _mix.Gain(state, entry, Vector3.right * 16, _config);
            state.ClosedDoors[1] = true; float closed = _mix.Gain(state, entry, Vector3.right * 8, _config);
            Assert.That(local, Is.GreaterThan(one)); Assert.That(one, Is.GreaterThanOrEqualTo(two)); Assert.That(closed, Is.LessThan(one));
            var expected = AcousticOcclusionUtility.Sample(state.Graph, 2, Vector3.right * 8, 1, Vector3.zero, 1f, _config.Hearing, state.ClosedDoors);
            Assert.That(closed, Is.EqualTo(expected.Audible ? expected.PerceivedLoudness : 0f));
            _mix.SetWorld(state, null, null); Assert.That(_mix.Gain(state, entry, Vector3.zero, _config), Is.Zero);
        }
        [Test] public void NativeRolloffRemovesOnlyDistanceLossAndRetainsPortalDoorAndMaskLoss()
        {
            var state = State(); var entry = Entry(CueId.DoorOpen);
            Assert.That(_mix.Gain(state, entry, Vector3.right * 3, _config, true), Is.EqualTo(1f));
            float one = _mix.Gain(state, entry, Vector3.right * 8, _config, true);
            Assert.That(one, Is.EqualTo(_config.Hearing.PerPortalAttenuation).Within(.000001f));
            Assert.That(_mix.Gain(state, entry, Vector3.right * 8, _config), Is.LessThan(one));
            Assert.That(_mix.Gain(state, entry, Vector3.right * 16, _config, true), Is.LessThan(one));
            state.ClosedDoors[1] = true; state.MaskGain = .5f;
            Assert.That(_mix.Gain(state, entry, Vector3.right * 8, _config, true),
                Is.EqualTo(one * _config.Hearing.ClosedDoorAttenuation * .5f).Within(.000001f));
            _mix.SetWorld(state, null, null);
            Assert.That(_mix.Gain(state, entry, Vector3.zero, _config, true), Is.Zero);
        }
        [Test] public void SilentPresenceAndKeenEarsOnlyChangePresenceWhileProtectedTellsIgnoreMasking()
        {
            var state = State(); var presence = Entry(CueId.Presence); var attack = Entry(CueId.EnemyWindup); var door = Entry(CueId.DoorOpen);
            Vector3 point = Vector3.right * 8;
            float original = _mix.Gain(state, presence, point, _config); float world = _mix.Gain(state, door, point, _config);
            _mix.SetEffects(state, new ActiveEffects(new[] { new ActiveEffect(new EffectId("keen-ears"), EffectKind.Upgrade, 1) }));
            Assert.That(_mix.Gain(state, presence, point, _config), Is.GreaterThan(original));
            Assert.That(_mix.Gain(state, door, point, _config), Is.EqualTo(world));
            _mix.SetEffects(state, new ActiveEffects(new[] { new ActiveEffect(new EffectId("silent-presence"), EffectKind.Curse, 1) }));
            Assert.That(_mix.Gain(state, presence, point, _config), Is.Zero);
            Assert.That(_mix.Gain(state, attack, point, _config), Is.EqualTo(original));
            _mix.SetEffects(state, null); state.MaskGain = .5f; _mix.Deafening(state, 1f); _mix.MuffledDark(state, 1f);
            Assert.That(_mix.Gain(state, presence, point, _config), Is.EqualTo(original));
            Assert.That(_mix.Gain(state, attack, point, _config), Is.EqualTo(original));
            Assert.That(_mix.Gain(state, door, point, _config), Is.EqualTo(world * .5f * _config.DeafenedGain));
            Assert.That(_mix.Cutoff(state, true, _config), Is.EqualTo(22000f));
            Assert.That(_mix.Cutoff(state, false, _config), Is.EqualTo(_config.MuffledCutoff));
            _mix.Tick(state, _config, true, true, 0f, 2f);
            Assert.That(_mix.Gain(state, door, point, _config), Is.EqualTo(world));
            Assert.That(_mix.Cutoff(state, false, _config), Is.EqualTo(22000f));
        }
        [Test] public void TimedHooksAreIndependentAndNonPositiveDurationsDoNotActivate()
        {
            var state = State(); _mix.Deafening(state, float.NaN); _mix.MuffledDark(state, -1f);
            Assert.That(state.DeafenedRemaining + state.MuffledRemaining, Is.Zero);
            _mix.Deafening(state, 2f); Assert.That(_mix.Cutoff(state, false, _config), Is.EqualTo(22000f));
            _mix.Tick(state, _config, true, true, 0f, 2f); _mix.MuffledDark(state, 1f);
            Assert.That(_mix.Mask(state, false, _config), Is.EqualTo(1f));
        }
        [Test] public void FalsePositivesAreDeterministicRateLimitedAndNeverDuringChase()
        {
            var a = State(); var b = State(); var ra = new System.Random(17); var rb = new System.Random(17); float last = -90f; int heard = 0;
            for (int second = 0; second < 1200; second++)
            {
                a.InChase = b.InChase = second >= 300 && second < 600;
                _mix.Tick(a, _config, true, true, 0f, 1f); _mix.Tick(b, _config, true, true, 0f, 1f);
                bool first = _mix.FalsePositive(a, _config, true, true, ra, out var x);
                Assert.That(_mix.FalsePositive(b, _config, true, true, rb, out var y), Is.EqualTo(first));
                if (!first) continue;
                heard++; Assert.That(a.Time - last, Is.GreaterThanOrEqualTo(90f)); last = a.Time;
                Assert.That(a.InChase, Is.False); Assert.That(x.Cue, Is.EqualTo(y.Cue)); Assert.That(x.Position, Is.EqualTo(y.Position));
                Assert.That(x.Emitter, Is.EqualTo(int.MinValue)); Assert.That(_catalogue.FalsePositiveExempt(x.Cue), Is.True);
                Assert.That(_mix.Gain(a, Entry(x.Cue), x.Position, _config), Is.GreaterThan(0f));
            }
            Assert.That(heard, Is.GreaterThan(0)); a.Time += 1000;
            Assert.That(_mix.FalsePositive(a, _config, false, true, ra, out _), Is.False);
            Assert.That(_mix.FalsePositive(a, _config, true, false, ra, out _), Is.False);
        }
        [Test] public void HeartbeatScalesWithUncensoredClosenessAndGraceSpikesAndCollapseAccelerates()
        {
            var state = State(); state.Closeness = .2f;
            _mix.Tick(state, _config, true, true, 0f, .01f); float far = state.HeartbeatStrength;
            state.Closeness = .8f; state.InChase = true; _mix.Tick(state, _config, true, true, 0f, .01f);
            Assert.That(state.HeartbeatStrength, Is.GreaterThan(far)); Assert.That(state.BreathGain, Is.EqualTo(_config.PursuitBreathGain));
            state.InChase = false; state.Closeness = 0; _mix.Grace(state, _config); _mix.Tick(state, _config, true, true, 0f, .01f);
            Assert.That(state.Heartbeat, Is.True); Assert.That(state.HeartbeatStrength, Is.EqualTo(1f)); Assert.That(state.MaskGain, Is.LessThan(1f));
            Assert.That(state.BreathGain, Is.Zero);
            state.Rooms[1] = new RoomDestructionSample(1, RoomPhase.Telegraph, .1f);
            _mix.Tick(state, _config, true, true, 0f, 1f); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.RoomTelegraph));
            Assert.That(_mix.CollapseInterval(.9f, _config), Is.LessThan(_mix.CollapseInterval(.1f, _config)));
            state.Rooms[1] = new RoomDestructionSample(1, RoomPhase.Closed, 1f);
            _mix.Tick(state, _config, true, true, 0f, 10f); Assert.That(state.Commands, Is.Empty);
            _mix.Tick(state, _config, true, false, 1f, 1f); Assert.That(state.HeartbeatStrength, Is.Zero); Assert.That(state.BreathGain, Is.Zero);
        }
        [Test] public void VoiceSlotsRespectPriorityAndNeverStealProtectedHuntersAndTellsHaveNoDelay()
        {
            var presenter = new AudioSoundscapePresenter(); var state = new AudioSoundscapeDriverState(); presenter.Reset(state, 8, new System.Random(2));
            for (int i = 0; i < 8; i++) Assert.That(presenter.TryPlay(state, Bank(CueId.Presence, 10), i, new[] { 2f }, 1f, out _), Is.True);
            Assert.That(presenter.TryPlay(state, Bank(CueId.DoorOpen, 100), 0, new[] { 2f }, 1f, out _), Is.False);
            Assert.That(presenter.TryPlay(state, Bank(CueId.EnemyFootstep, 9), 0, new[] { 2f }, 1f, out _), Is.False);
            Assert.That(presenter.TryPlay(state, Bank(CueId.EnemyFootstep, 11), 0, new[] { 2f }, 1f, out _), Is.True);
            presenter.Reset(state, 8, new System.Random(2));
            Assert.That(presenter.TryPlay(state, Bank(CueId.EnemyWindup, 80), 1, new[] { 2f }, 1f, out var tell, 1f), Is.True);
            Assert.That(tell.Delay, Is.Zero);
            Assert.That(presenter.TryPlay(state, Bank(CueId.CakeCollect, 35), 0, new[] { 2f }, 1f, out _), Is.True);
            Assert.That(presenter.TryPlay(state, Bank(CueId.GoldenCakeCollect, 36), 99, new[] { 2f }, 1f, out _), Is.True);
            int pickups = 0; foreach (var voice in state.Voices) if (voice.Remaining > 0 && voice.Catalogue.Category == CueCategory.World) pickups++;
            Assert.That(pickups, Is.EqualTo(1));
        }
        private AudioSoundDefinition Bank(CueId cue, int priority) => new AudioSoundDefinition { Cue = cue, Gain = .5f, Priority = priority, PitchMinimum = .98f, PitchMaximum = 1.02f, GainVariation = .03f };
        [Test] public void DoorOpenFiresOnceAtPhysicalOpeningNotLogicalUnlock()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState();
            presenter.Exit(state, new FloorDisplaySnapshot(1, 1, 0, ExitState.Open, false, Vector3.zero, 0f), Vector3.right, .01f);
            Assert.That(state.Commands, Is.Empty);
            presenter.Exit(state, new FloorDisplaySnapshot(1, 1, 0, ExitState.Open, false, Vector3.zero, .02f), Vector3.right, .01f);
            Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.DoorOpen)); Assert.That(state.Commands[0].Position, Is.EqualTo(Vector3.right));
            presenter.Exit(state, new FloorDisplaySnapshot(1, 1, 0, ExitState.Open, false, Vector3.zero, 1f), Vector3.right, .01f);
            Assert.That(state.Commands, Is.Empty);
        }
        [Test] public void ShopUsesCommitReasonNotWalletAndReservationIsSilent()
        {
            var presenter = new AudioFeedbackPresenter(); var state = new AudioFeedbackDriverState { Phase = ProgressionPhase.Shop };
            presenter.Progression(state, Shop(1)); presenter.Progression(state, Shop(2, wallet: 1)); Assert.That(state.Commands, Is.Empty);
            presenter.Transaction(state, Shop(2), Shop(3, "pending"), ProgressionOperation.ReservePurchase); Assert.That(state.Commands, Is.Empty);
            presenter.Transaction(state, Shop(3, "pending"), Shop(4), ProgressionOperation.Purchase); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.ShopBuy));
            presenter.Transaction(state, Shop(3), Shop(4), ProgressionOperation.Purchase); Assert.That(state.Commands, Is.Empty);
            presenter.Transaction(state, Shop(4), Shop(5), ProgressionOperation.RerollShop); Assert.That(state.Commands[0].Cue, Is.EqualTo(CueId.UiMove));
            presenter.Transaction(state, Shop(5), Shop(6), ProgressionOperation.CancelReplacement); Assert.That(state.Commands, Is.Empty);
        }
        private ProgressionSnapshot Shop(int revision, string pending = "", int wallet = 10) => new ProgressionSnapshot(revision, 1, 1, 0, wallet, 0, 0,
            ProgressionPhase.Shop, 100, 100, null, null, null, default, "", false, false, pendingOfferId: pending);
    }
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class AudioWorldMixDistanceTests
    {
        [TestCase(false)] [TestCase(true)]
        public void DistanceIsAppliedOnceWhilePortalAndDoorRetentionRemain(bool nativeRolloff)
        {
            var presenter = new AudioWorldMixPresenter();
            var state = new AudioWorldMixDriverState { Graph = AudioWorldMixPresenterTests.Graph() };
            var hearing = new HearingModelSettings(2f, 1f, .7f, .35f, .001f);
            float distance = nativeRolloff ? 1f : .25f;
            Assert.That(presenter.AcousticGain(state, Vector3.right * 8f, hearing, nativeRolloff),
                Is.EqualTo(distance * .7f).Within(.000001f));
            state.ClosedDoors[1] = true;
            Assert.That(presenter.AcousticGain(state, Vector3.right * 8f, hearing, nativeRolloff),
                Is.EqualTo(distance * .7f * .35f).Within(.000001f));
            Assert.That(presenter.AcousticGain(state, Vector3.right * 3f, hearing, nativeRolloff),
                Is.EqualTo(nativeRolloff ? 1f : 2f / 3f).Within(.000001f));
        }
        [TestCase(false)] [TestCase(true)]
        public void MissingTopologyAndUnknownRoomStaySilentWithEitherDistanceOwner(bool nativeRolloff)
        {
            var presenter = new AudioWorldMixPresenter(); var state = new AudioWorldMixDriverState();
            var hearing = new HearingModelSettings(2f, 1f, .7f, .35f, .001f);
            Assert.That(presenter.AcousticGain(state, Vector3.zero, hearing, nativeRolloff), Is.Zero);
            state.Graph = AudioWorldMixPresenterTests.Graph();
            Assert.That(presenter.AcousticGain(state, Vector3.right * 1000f, hearing, nativeRolloff), Is.Zero);
        }
    }
}
