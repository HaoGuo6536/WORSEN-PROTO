// ============================================================================
// MannequinControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies the light-independent camera rule through the shared Hunter controller.
//   Observation and Wick cancel motion and attacks immediately; lighting and the
//   retired lamp curses cannot interfere with pursuit or change the environment.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover lit/dark pursuit, immediate observation holds, silence and Wick.
//   - Guard retired light effects, retained curses and per-life catch admission.
// DEPENDENCIES:
//   - Hunter/Mannequin controllers, Core values and NUnit fixtures.
// USAGE NOTES:
//   Coordinator executes Edit Mode. Time, world lighting and camera are injected.
// ============================================================================
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mannequin;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MannequinControllerTests
    {
        private MannequinConfig _config;
        private HunterProfile _profile;
        private HunterController _shared;
        private MannequinController _module;
        private HunterBehaviorState _hunter;
        private HunterReactionsTests.World _world;
        private long _tick;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<MannequinConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _world = new HunterReactionsTests.World(); _hunter = new HunterBehaviorState(); _tick = 0;
            _module = new MannequinController(_config, new System.Random(23));
            _shared = new HunterController(_hunter, _profile, new System.Random(7),
                new PlayerBehaviorState { Id = new EntityId(1), Health = 100, Position = Vector3.forward * 10, SprintSpeed = 8 },
                new EchoControllerTests.World(), _module);
            _shared.Reset(new EntityId(-1), Vector3.zero, Vector3.forward); _shared.SetWorldView(_world);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_config); Object.DestroyImmediate(_profile); }
        private HunterTickResult Step(bool looking = false, bool clear = true, float dt = .1f, bool illuminated = false)
        {
            _tick++; _shared.SetPlayerView(new HunterPlayerView(new Vector3(0, 1, 10),
                Quaternion.Euler(0, looking ? 180 : 0, 0), 90, 60, _tick));
            _shared.ObservePlayerView(clear);
            return _shared.Tick(new SightProbe(true, true, true), new HunterLightObservation(illuminated, illuminated, Vector3.forward * 10, _tick), dt, _tick);
        }
        [TestCase(false)] [TestCase(true)]
        public void UnseenAdvancesAndSeenImmediatelyHoldsSilentlyRegardlessOfLight(bool lit)
        {
            _world.Lit = lit;
            var moving = Step();
            Assert.That(moving.HoldPosition, Is.False); Assert.That(moving.Speed, Is.GreaterThan(0));
            Assert.That(moving.Target, Is.EqualTo(Vector3.forward * 10));
            _shared.CommitPose(Vector3.forward, Vector3.forward * moving.Speed, Vector3.forward);
            var stopped = Step(true);
            Assert.That(stopped.HoldPosition, Is.True); Assert.That(stopped.Speed, Is.Zero);
            Assert.That(_hunter.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(_shared.TryAcceptContact(new EntityId(1), out _), Is.False);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
            Assert.That(Step(illuminated: true).Speed, Is.GreaterThan(0));
            Assert.That(Step(true, false).Speed, Is.GreaterThan(0));
            _shared.SetWickActive(true); Assert.That(Step().HoldPosition, Is.True);
            Assert.That(_shared.TryAcceptContact(new EntityId(1), out _), Is.False);
            _shared.SetWickActive(false); Assert.That(Step().Speed, Is.GreaterThan(0));
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test]
        public void AfterglowNoLongerCreatesALightSafetyWindow()
        {
            var effects = new ActiveEffects(new[] { new ActiveEffect(new EffectId("afterglow"), EffectKind.Upgrade, 1) });
            _shared.SetActiveEffects(effects); _module.SetEffects(effects);
            Assert.That(_module.BeginAfterglow(99), Is.Zero);
            Assert.That(_module.BeginAfterglow(1), Is.Zero);
            Assert.That(_config.AfterglowSeconds, Is.Zero);
            Assert.That(Step().Speed, Is.GreaterThan(0));
            Assert.That(Step(true).HoldPosition, Is.True);
        }
        [Test] public void MissingLightingDoesNotHoldButMissingCameraStillDoes()
        {
            _world.Known = false; Assert.That(Step().Speed, Is.GreaterThan(0));
            _shared.SetWorldView(null); Assert.That(Step().Speed, Is.GreaterThan(0));
            _tick++; Assert.That(_shared.Tick(default, .1f, _tick).HoldPosition, Is.True);
            _shared.SetPlayerView(default);
            _tick++; Assert.That(_shared.Tick(default, .1f, _tick).HoldPosition, Is.True);
        }
        [Test] public void RetiredLightCursesNeverPublishLampBudgetsOrRoomOverrides()
        {
            _shared.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("mannequin-fewer-lamps"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-broken-lights"), EffectKind.Curse, 99) }));
            _world.Lit = true;
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(Step(dt: 15f).HoldPosition, Is.False);
                while (_module.TakeFact(out var fact)) Assert.That(fact.Kind, Is.EqualTo(MannequinFactKind.SilentSoundSet));
            }
            Assert.That(_world.Lit, Is.True); Assert.That(_module.SpeedMultiplier, Is.EqualTo(1f));
        }
        [TestCase(false)] [TestCase(true)]
        public void ObservationCancelsAnActiveLungeBeforeContactOrFeedback(bool lit)
        {
            _world.Lit = lit;
            _shared.CommitPose(Vector3.forward * 7, Vector3.zero, Vector3.forward);
            Assert.That(Step().Phase, Is.EqualTo(HunterLungePhase.Windup));
            Assert.That(Step(dt: _profile.LungeWindupSeconds).Phase, Is.EqualTo(HunterLungePhase.Active));
            var held = Step(true);
            Assert.That(held.HoldPosition, Is.True); Assert.That(held.Phase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(held.Speed, Is.Zero); Assert.That(held.ActiveContact, Is.False);
            Assert.That(_shared.TryAcceptContact(new EntityId(1), out _), Is.False);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void OcclusionAndHunterFacingCannotSubstituteABodyHeadingHoldOrLightReaction()
        {
            EchoControllerTests.Tune(_profile, "_lightResponse", HunterLightResponse.Avoid);
            _shared.CommitPose(Vector3.zero, Vector3.zero, Vector3.back);
            var result = Step(looking: true, clear: false, illuminated: true);
            Assert.That(result.HoldPosition, Is.False); Assert.That(result.Speed, Is.GreaterThan(0));
            Assert.That(result.Target, Is.EqualTo(Vector3.forward * 10));
            Assert.That(_hunter.CurrentAction, Is.EqualTo(HunterAction.Chase));
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void CatchAdmissionResetsPerLifeAndOrdinaryMovementIsSilent()
        {
            Assert.That(Step(illuminated: true).Speed, Is.GreaterThan(0));
            _shared.CommitPose(Vector3.forward * 3, Vector3.forward, Vector3.forward);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
            Assert.That(_module.TryCatch(), Is.True); Assert.That(_module.TryCatch(), Is.False);
            _shared.Reset(new EntityId(-2), Vector3.zero, Vector3.forward);
            Assert.That(_module.TryCatch(), Is.True);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void LongerStridesUsesExactIdCapsAndLeavesConfigUnchanged()
        {
            _shared.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("mannequin-fewer-lamps"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-longer-strides"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-broken-lights"), EffectKind.Curse, 1) }));
            _world.Lit = true; Assert.That(Step().HoldPosition, Is.False);
            Assert.That(_module.SpeedMultiplier, Is.EqualTo(Mathf.Pow(1.2f, 3)).Within(.0001f));
            while (_module.TakeFact(out var fact)) Assert.That(fact.Kind, Is.EqualTo(MannequinFactKind.SilentSoundSet));
            Assert.That(_config.LongerStridesMultiplier, Is.EqualTo(1.2f));
            _shared.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("longer-strides"), EffectKind.Curse, 3) }));
            Step(); Assert.That(_module.SpeedMultiplier, Is.EqualTo(1f));
        }
        [TestCase(false)] [TestCase(true)]
        public void PeripheralCreepNarrowsObservationInBothLitAndDarkRooms(bool lit)
        {
            _world.Lit = lit;
            _tick++; var view = new HunterPlayerView(new Vector3(0, 1, 10), Quaternion.Euler(0, 150, 0), 90, 60, _tick);
            _shared.SetPlayerView(view); _shared.ObservePlayerView(true);
            Assert.That(_shared.Tick(default, .1f, _tick).HoldPosition, Is.True);
            _shared.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("mannequin-peripheral-creep"), EffectKind.Curse, 1) }));
            _tick++; _shared.SetPlayerView(new HunterPlayerView(view.Origin, view.Rotation, 90, 60, _tick));
            _shared.Tick(new SightProbe(true, true, true), .1f, _tick); Assert.That(_module.Hold, Is.False);
            Assert.That(Step(true).HoldPosition, Is.True);
            Assert.That(Step().Speed, Is.GreaterThan(0));
        }
    }
}
