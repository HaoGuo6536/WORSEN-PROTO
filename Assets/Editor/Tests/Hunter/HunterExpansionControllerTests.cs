// ============================================================================
// HunterExpansionControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies Hunter behavior with explicit reproducible fixtures.
//   Tests exercise observable light, physical attacks, route admission or creature
//   animation contracts without changing authored gameplay assets.
// ARCHITECTURAL ROLE:
//   Editor tool (section 10), test suite (section 11) - Domain - Hunter.
// KEY RESPONSIBILITIES:
//   - Preserve observable sensing, committed attacks and explicit ownership boundaries.
//   - Name the intended archetype for each species-specific curse fixture.
// DEPENDENCIES:
//   - Hunter-owned contracts and Core values; Manager/Controller receive Player and Level views.
//   - Engine operations remain in Drivers; tests use UnityEditor and NUnit fixtures.
// USAGE NOTES:
//   Coordinator runs Unity tests with the exclusive lease. Fixtures clean up their own objects.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Level;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class HunterExpansionControllerTests
    {
        private HunterProfile _profile;
        private HunterBehaviorState _state;
        private HunterController _controller;
        private PlayerBehaviorState _player;
        private sealed class LevelFixture : IReadOnlyLevelState
        {
            public bool IsReady => true;
            public LevelGraph Graph { get; } = new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one * 100f) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        }
        [SetUp] public void SetUp()
        {
            _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Position = Vector3.forward * 10f, Health = 100f, SprintSpeed = 8f };
            _state = new HunterBehaviorState();
            _controller = new HunterController(_state, _profile, new System.Random(11), _player, new LevelFixture());
            _controller.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_profile); }
        private void Integer(string key, int value)
        { var so = new SerializedObject(_profile); so.FindProperty(key).intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private void Archetype(string key)
        { var so = new SerializedObject(_profile); so.FindProperty("_archetypeKey").stringValue = key; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static readonly SightProbe Visible = new SightProbe(true, true, true);
        private void Tick(int tick, bool visible, bool light, float dt = 0.02f)
        { _controller.Tick(visible ? Visible : default, new HunterLightObservation(light, light, Vector3.forward * 5f, tick), dt, tick); }
        private List<HunterFeedbackKind> Feedback()
        { var result = new List<HunterFeedbackKind>(); while (_controller.TryDequeueFeedback(out HunterFeedbackEvent cue)) result.Add(cue.Kind); return result; }
        [Test] public void LightClueTargetsObservedPatchWithoutRevealingHiddenPlayer()
        {
            _player.Position = Vector3.right * 80f;
            var patch = new Vector3(2f, 0f, 6f);
            HunterTickResult result = _controller.Tick(default, new HunterLightObservation(true, false, patch, 0), 0.02f, 0);
            Assert.That(_state.PlayerVisible, Is.False);
            Assert.That(_state.BeliefConfidence, Is.Zero);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.InvestigateLight));
            Assert.That(result.Target, Is.EqualTo(patch));
            for (int i = 1; i < 170; i++) Tick(i, false, false);
            Assert.That(_state.LightMemoryRemaining, Is.Zero);
            Assert.That(_state.CurrentAction, Is.EqualTo(HunterAction.Patrol));
        }
        [Test] public void FutureAndNonfiniteLightCannotBecomeKnowledge()
        {
            _controller.Tick(default, new HunterLightObservation(true, true, Vector3.one, 5), 0.02f, 0);
            Assert.That(_state.LightMemoryRemaining, Is.Zero);
            _controller.Tick(default, new HunterLightObservation(true, true, new Vector3(float.NaN, 0, 0), 4), 0.02f, 4);
            Assert.That(_state.LightMemoryRemaining, Is.Zero);
        }
        [TestCase(HunterLightResponse.Avoid, HunterAction.AvoidLight)]
        [TestCase(HunterLightResponse.Flank, HunterAction.FlankLight)]
        public void LightReactionIsBriefAndCannotPermanentlyPreventPursuit(HunterLightResponse response, HunterAction expected)
        {
            Integer("_lightResponse", (int)response);
            for (int i = 0; i < 12; i++) Tick(i, true, true);
            Assert.That(_state.CurrentAction, Is.EqualTo(expected));
            Assert.That(_state.CurrentTarget, Is.Not.EqualTo(_player.Position));
            for (int i = 12; i < 100; i++) Tick(i, true, true);
            Assert.That(_state.CurrentAction, Is.Not.EqualTo(expected));
            Assert.That(Feedback().FindAll(cue => cue == HunterFeedbackKind.LightReaction).Count, Is.EqualTo(1));
        }
        [Test] public void LightNeverInterruptsCommittedAttackOrEnablesExtraContacts()
        {
            Integer("_lightResponse", (int)HunterLightResponse.Avoid);
            _player.Position = Vector3.forward * 3f;
            Tick(0, true, false);
            for (int i = 1; i <= 14; i++) Tick(i, true, true);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Active));
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.True);
            Assert.That(_controller.TryAcceptContact(_player.Id, out _), Is.False);
            Assert.That(Feedback().Contains(HunterFeedbackKind.LightReaction), Is.False);
        }
        [Test] public void RusherIgnoresBeamAsAnAttackBlockAndItsStrideCurseChangesReach()
        {
            Archetype("rusher");
            _player.Position = Vector3.forward * 5f;
            Tick(0, true, true);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            _controller.SetTraits(ProgressionTraits.RusherLongStride);
            Tick(4, true, true);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.Windup));
        }
        [TestCase(HunterAttackStyle.Projectile, ChaseEndReason.Projectile)]
        [TestCase(HunterAttackStyle.GroundSpikes, ChaseEndReason.GroundSpike)]
        public void RangedContactsRequireFiredIdentityAndApplyOnlyOnce(HunterAttackStyle style, ChaseEndReason reason)
        {
            Integer("_attackStyle", (int)style);
            Tick(0, true, false);
            int serial = _state.AttackSerial;
            Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.False);
            for (int i = 1; i < 18; i++) Tick(i, true, false);
            Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial + 1, out _), Is.False);
            Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out HunterHit hit), Is.True);
            Assert.That(hit.Reason, Is.EqualTo(reason));
            Assert.That(_controller.TryAcceptRangedContact(_player.Id, serial, out _), Is.False);
        }
        [TestCase("hexer")]
        [TestCase("thorncaller")]
        public void CasterAndSpikeCursesChangeActualAttackParametersAndResetPerLife(string archetype)
        {
            Archetype(archetype);
            Integer("_attackStyle", (int)(archetype == "hexer" ? HunterAttackStyle.Projectile : HunterAttackStyle.GroundSpikes));
            float speed = _controller.ProjectileSpeed, radius = _controller.ProjectileRadius, spike = _controller.SpikeRadius;
            _controller.SetTraits(ProgressionTraits.HexerSplitBolt | ProgressionTraits.HexerLingeringHex |
                ProgressionTraits.ThorncallerThornRing | ProgressionTraits.ThorncallerReachingRoots);
            Assert.That(_controller.SplitBolt, Is.EqualTo(archetype == "hexer"));
            Assert.That(_controller.ThornRing, Is.EqualTo(archetype == "thorncaller"));
            if (archetype == "hexer")
            {
                Assert.That(_controller.ProjectileSpeed, Is.LessThan(speed));
                Assert.That(_controller.ProjectileRadius, Is.GreaterThan(radius));
                Assert.That(_controller.SpikeRadius, Is.EqualTo(spike));
            }
            else
            {
                Assert.That(_controller.ProjectileSpeed, Is.EqualTo(speed));
                Assert.That(_controller.ProjectileRadius, Is.EqualTo(radius));
                Assert.That(_controller.SpikeRadius, Is.GreaterThan(spike));
            }
            _controller.Reset(new EntityId(-2), Vector3.zero, Vector3.forward);
            Assert.That(_controller.SplitBolt || _controller.ThornRing, Is.False);
            Assert.That(_controller.ProjectileSpeed, Is.EqualTo(speed));
            Assert.That(_controller.ProjectileRadius, Is.EqualTo(radius));
            Assert.That(_controller.SpikeRadius, Is.EqualTo(spike));
        }
        [Test] public void MeleeMissAndSuccessfulContactHaveDifferentFeedback()
        {
            _player.Position = Vector3.forward * 3f;
            Tick(0, true, false);
            for (int i = 1; i < 30; i++) Tick(i, true, false);
            List<HunterFeedbackKind> cues = Feedback();
            Assert.That(cues, Does.Contain(HunterFeedbackKind.AttackWindup));
            Assert.That(cues, Does.Contain(HunterFeedbackKind.AttackSwing));
            Assert.That(cues, Does.Contain(HunterFeedbackKind.AttackMiss));
            Assert.That(cues.Contains(HunterFeedbackKind.AttackHit), Is.False);
        }
        [Test] public void RepeatedDetectionCannotSpamScreams()
        {
            var so = new SerializedObject(_profile); so.FindProperty("_screamOnDetection").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < 80; i++) Tick(i, (i / 4) % 2 == 0, false);
            Assert.That(Feedback().FindAll(cue => cue == HunterFeedbackKind.Scream).Count, Is.Zero,
                "Sight changes alone must not scream; the player is outside attack range.");
        }
        [Test] public void ExternalNoiseIsMuffledAndCannotRevealItsSourcesCurrentPosition()
        {
            Tick(0, false, false);
            Assert.That(_controller.HearNoise(new NoiseEvent(_player.Id, Vector3.forward * 5f, 1f, 0), 0.35f), Is.True);
            Assert.That(_state.LastKnownPosition, Is.EqualTo(Vector3.forward * 5f));
            Assert.That(_state.PlayerVisible, Is.False);
            Assert.That(_controller.HearNoise(new NoiseEvent(_player.Id, Vector3.forward * 5f, 1f, 100), 1f), Is.False);
        }
        [TestCase(ProgressionTraits.LurkerStolenSilence, "lurker")]
        [TestCase(ProgressionTraits.HexerHastyScript, "hexer")]
        [TestCase(ProgressionTraits.ThorncallerQuickRoots, "thorncaller")]
        public void FasterTelegraphCursesShortenButKeepReactionTime(ProgressionTraits trait, string archetype)
        {
            Archetype(archetype);
            float original = _controller.WindupDuration;
            _controller.SetTraits(trait);
            Assert.That(_controller.WindupDuration, Is.LessThan(original));
            Assert.That(_controller.WindupDuration, Is.GreaterThanOrEqualTo(0.15f));
        }
        [TestCase("lurker")]
        [TestCase("watcher")]
        [TestCase("rusher")]
        public void PerceptionCursesAlterSightAndOldNoiseEligibility(string archetype)
        {
            Archetype(archetype);
            float range = _controller.EffectiveSightRange, cone = _controller.EffectiveSightCone;
            _controller.SetTraits(ProgressionTraits.LurkerDarkAdaptation | ProgressionTraits.WatcherUnquietGaze);
            if (archetype == "watcher") Assert.That(_controller.EffectiveSightRange, Is.GreaterThan(range));
            else Assert.That(_controller.EffectiveSightRange, Is.EqualTo(range));
            if (archetype == "lurker") Assert.That(_controller.EffectiveSightCone, Is.GreaterThan(cone));
            else Assert.That(_controller.EffectiveSightCone, Is.EqualTo(cone));
            Tick(40, false, false);
            var old = new NoiseEvent(_player.Id, Vector3.forward * 5f, 1f, 0);
            Assert.That(_controller.HearNoise(old, 1f), Is.False);
            _controller.SetTraits(ProgressionTraits.RusherBloodScent);
            Assert.That(_controller.HearNoise(old, 1f), Is.EqualTo(archetype == "rusher"));
        }
        [Test] public void LongerMemoryCursePreservesLightAndBeliefBeyondNormalExpiry()
        {
            Archetype("watcher");
            _controller.SetTraits(ProgressionTraits.WatcherLongMemory);
            Tick(0, true, true);
            for (int i = 1; i <= 180; i++) Tick(i, false, false);
            Assert.That(_state.LightMemoryRemaining, Is.GreaterThan(0f));
            for (int i = 181; i <= 420; i++) Tick(i, false, false);
            Assert.That(_state.BeliefConfidence, Is.GreaterThan(0f));
        }
        [Test] public void ShortenedRecoveryReturnsRusherToPursuitEarlier()
        {
            Archetype("rusher");
            _controller.SetTraits(ProgressionTraits.RusherSecondWind);
            _player.Position = Vector3.forward * 3f; Tick(0, true, false);
            for (int i = 1; i <= 30; i++) Tick(i, true, false);
            _player.Position = Vector3.forward * 10f;
            for (int i = 31; i <= 56; i++) Tick(i, true, false);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.None));
        }
        [Test] public void ConsumedRoomCannotBecomeANewAttackTarget()
        {
            _player.Position = Vector3.forward * 3f;
            _controller.SetRoomPhase(new RoomPhaseChangedFact(1, RoomPhase.Closed, 0));
            Tick(0, true, false);
            Assert.That(_state.LungePhase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(_controller.UnavailableRooms.Count, Is.EqualTo(1));
            _controller.SetRoomPhase(new RoomPhaseChangedFact(1, RoomPhase.Closed, 1));
            Assert.That(_controller.UnavailableRooms.Count, Is.EqualTo(1));
        }
    }
}
