// ============================================================================
// MannequinControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies darkness, observation and Wick as hard movement/attack gates.
//   Seeded light-failure trials and capped effects remain independent of Unity
//   lighting so the coordinator can distinguish rules from scene wiring.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover darkness-only rules, catch admission, silence, rarity and curse isolation.
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
        [Test] public void LitObservedOrWickAlwaysHoldAndUnseenDarknessMovesSilently()
        {
            _world.Lit = true; Assert.That(Step().HoldPosition, Is.True);
            _world.Lit = false; Assert.That(Step(true).HoldPosition, Is.True);
            Assert.That(Step().Speed, Is.GreaterThan(0));
            Assert.That(Step(illuminated: true).HoldPosition, Is.True);
            Assert.That(Step(true, false).Speed, Is.GreaterThan(0));
            _shared.SetWickActive(true); Assert.That(Step().HoldPosition, Is.True);
            Assert.That(_shared.TryAcceptContact(new EntityId(1), out _), Is.False);
            _shared.CommitPose(Vector3.forward * 3, Vector3.forward, Vector3.forward);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void DarknessPolicyCannotInvertAndWickAndMissingWorldStillHold()
        {
            Assert.That(_config.MovesInDarkness, Is.True);
            Assert.That(Step().Speed, Is.GreaterThan(0)); _world.Lit = true; Assert.That(Step().HoldPosition, Is.True);
            Assert.That(Step(true).HoldPosition, Is.True);
            _shared.SetWickActive(true); Assert.That(Step().HoldPosition, Is.True);
            _shared.SetWickActive(false); _world.Known = false; Assert.That(Step().HoldPosition, Is.True);
        }
        [Test] public void SeededSubversionIsRareAndReproducibleAtConfiguredCheckCadence()
        {
            _world.Lit = true; int failures = 0;
            var expected = new System.Random(23);
            for (int i = 0; i < 1000; i++)
            {
                bool occurs = expected.NextDouble() < _config.FailureChance;
                Step(dt: _config.FailureCheckSeconds);
                bool actual = false;
                while (_module.TakeFact(out var fact)) if (fact.Kind == MannequinFactKind.RoomLightOverride) actual = true;
                Assert.That(actual, Is.EqualTo(occurs)); if (actual) failures++;
            }
            Assert.That(failures, Is.InRange(1, 50));
        }
        [Test] public void TemporaryFailureExpiresAndObservationCannotBeSubverted()
        {
            EchoControllerTests.Tune(_config, "_failureChance", 1f); _world.Lit = true;
            Assert.That(Step(dt: 15f).HoldPosition, Is.False);
            Assert.That(Step(true).HoldPosition, Is.True);
            EchoControllerTests.Tune(_config, "_failureChance", 0f);
            Assert.That(Step(dt: 2f).HoldPosition, Is.True);
        }
        [Test] public void SubversionOnlySwitchesRoomLightOffAndStaleViewHolds()
        {
            _world.Lit = true;
            EchoControllerTests.Tune(_config, "_failureChance", 1f);
            Assert.That(Step(dt: 15f).HoldPosition, Is.False);
            bool switchedOff = false;
            while (_module.TakeFact(out var fact)) if (fact.Kind == MannequinFactKind.RoomLightOverride) switchedOff = !fact.Lit;
            Assert.That(switchedOff, Is.True);
            _tick++; Assert.That(_shared.Tick(default, .1f, _tick).HoldPosition, Is.True);
        }
        [Test] public void DirectIlluminationCannotTriggerRoomFailureAndCatchAdmissionResetsPerLife()
        {
            _world.Lit = true; EchoControllerTests.Tune(_config, "_failureChance", 1f);
            Assert.That(Step(dt: 15f, illuminated: true).HoldPosition, Is.True);
            while (_module.TakeFact(out var fact)) Assert.That(fact.Kind, Is.Not.EqualTo(MannequinFactKind.RoomLightOverride));
            Assert.That(_module.TryCatch(), Is.True); Assert.That(_module.TryCatch(), Is.False);
            _shared.Reset(new EntityId(-2), Vector3.zero, Vector3.forward);
            Assert.That(_module.TryCatch(), Is.True);
            Assert.That(_shared.TryDequeueFeedback(out _), Is.False);
        }
        [Test] public void CurseHooksUseExactIdsCapAndLeaveConfigUnchanged()
        {
            _shared.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("mannequin-fewer-lamps"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-longer-strides"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("mannequin-broken-lights"), EffectKind.Curse, 1) }));
            _world.Lit = true; Assert.That(Step().HoldPosition, Is.False);
            Assert.That(_module.SpeedMultiplier, Is.EqualTo(Mathf.Pow(1.2f, 3)).Within(.0001f));
            bool budget = false, broken = false;
            while (_module.TakeFact(out var fact))
            {
                if (fact.Kind == MannequinFactKind.LampBudget) { budget = true; Assert.That(fact.Value, Is.EqualTo(Mathf.Pow(.8f, 3)).Within(.0001f)); }
                if (fact.Kind == MannequinFactKind.RoomLightOverride) broken = fact.Permanent;
            }
            Assert.That(budget && broken, Is.True); Assert.That(_config.LongerStridesMultiplier, Is.EqualTo(1.2f));
        }
        [Test] public void PeripheralCreepNarrowsOnlyObservationNotLightSafety()
        {
            _tick++; var view = new HunterPlayerView(new Vector3(0, 1, 10), Quaternion.Euler(0, 150, 0), 90, 60, _tick);
            _shared.SetPlayerView(view); _shared.ObservePlayerView(true);
            Assert.That(_shared.Tick(default, .1f, _tick).HoldPosition, Is.True);
            _shared.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("mannequin-peripheral-creep"), EffectKind.Curse, 1) }));
            _tick++; _shared.SetPlayerView(new HunterPlayerView(view.Origin, view.Rotation, 90, 60, _tick));
            _shared.Tick(new SightProbe(true, true, true), .1f, _tick); Assert.That(_module.Hold, Is.False);
            _world.Lit = true; Assert.That(Step().HoldPosition, Is.True);
        }
    }
}
