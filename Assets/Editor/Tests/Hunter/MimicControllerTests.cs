// ============================================================================
// MimicControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies false-cake facts, one-shot hit routing and curse-driven arrow betrayal.
//   Seeded disguise rolls and explicit tick deltas make the rule reproducible.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover arrow exclusion, dormant shared-rule touch, damage, holds, curses and teardown.
// DEPENDENCIES:
//   - Hunter rules, Core facts, existing world fixture and NUnit.
// USAGE NOTES:
//   Player grace acceptance and Floor rendering remain coordinator integration gates.
//   Managed config shells use explicit inputs, not native asset creation/defaults.
//   RosterBIntegrationTests separately checks the native default damage and hold relay.
// ============================================================================
using System.Collections.Generic;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Mimic;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MimicControllerTests
    {
        private MimicConfig _config;
        private MimicController _mimic;
        private RosterBTestHunter _hunter;
        private PlayerBehaviorState _player;
        private EchoControllerTests.World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(float dt) => new HunterArchetypeContext(_hunter, _player, _world, _world,
            _world.Doors, _world, _effects, dt, _tick, true, 1f);
        [SetUp] public void SetUp()
        {
            _config = (MimicConfig)FormatterServices.GetUninitializedObject(typeof(MimicConfig));
            EchoControllerTests.Tune(_config, "_biteSeconds", 1.2f); EchoControllerTests.Tune(_config, "_biteDamage", 25);
            EchoControllerTests.Tune(_config, "_touchRadius", .65f); EchoControllerTests.Tune(_config, "_longerBiteMultiplier", 1.25f);
            EchoControllerTests.Tune(_config, "_goldenChance", .25f); EchoControllerTests.Tune(_config, "_faithlessInterval", 20f);
            EchoControllerTests.Tune(_config, "_faithlessSeconds", 2f); EchoControllerTests.Tune(_config, "_biteSound", "mimic-wrong-bite");
            EchoControllerTests.Tune(_config, "_winSound", "mimic-win");
            _hunter = new RosterBTestHunter { Id = new EntityId(-1), IsActive = true, Position = Vector3.right * 5 };
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100 };
            _world = new EchoControllerTests.World(); _effects = default(ActiveEffects); _tick = 0;
            _mimic = new MimicController(_config, new System.Random(19)); _mimic.Reset(Context(0));
        }

        private void Advance(float dt) { _tick++; _mimic.Tick(Context(dt)); }
        private List<MimicFact> Facts() { var facts = new List<MimicFact>(); while (_mimic.TakeFact(out var fact)) facts.Add(fact); return facts; }
        [Test] public void PoseUsesSpawnPositionIsSilentAndNeverWhiteArrowEligible()
        {
            Advance(.1f); var facts = Facts(); var pose = facts.Find(f => f.Kind == MimicFactKind.Pose);
            Assert.That(pose.Hunter, Is.EqualTo(_hunter.Id)); Assert.That(pose.Position, Is.EqualTo(_hunter.Position));
            Assert.That(pose.WhiteArrowEligible, Is.False); Assert.That(pose.Golden, Is.False); Assert.That(pose.SoundId, Is.Empty);
            _mimic.TryMovement(out var target, out float speed); Assert.That(target, Is.EqualTo(_hunter.Position)); Assert.That(speed, Is.Zero);
            Advance(100); Assert.That(Facts(), Is.Empty);
        }
        [Test] public void TouchReturnsNormalHitAndOneShortHoldWithoutMutatingPlayer()
        {
            Advance(.1f); Facts(); Assert.That(_mimic.Touch(new EntityId(9), out _), Is.False);
            Assert.That(_mimic.Touch(_player.Id, out HunterHit hit), Is.True);
            Assert.That(hit.Damage, Is.EqualTo(25)); Assert.That(hit.Target, Is.EqualTo(_player.Id));
            Assert.That(hit.Source, Is.EqualTo(HitSource.Trap)); Assert.That(hit.Severity, Is.EqualTo(HitSeverity.Light));
            Assert.That(_player.Health, Is.EqualTo(100)); Assert.That(_mimic.Holding, Is.True); Assert.That(_mimic.Posed, Is.False);
            var facts = Facts(); Assert.That(facts.Find(f => f.Kind == MimicFactKind.BiteStarted).Seconds, Is.EqualTo(1.2f));
            Assert.That(facts.Exists(f => f.Kind == MimicFactKind.PoseRemoved), Is.True);
            Assert.That(_mimic.Touch(_player.Id, out _), Is.False); Advance(.6f); Assert.That(_mimic.Holding, Is.True);
            Advance(.6f); Assert.That(_mimic.Holding, Is.False); Assert.That(Facts()[0].Kind, Is.EqualTo(MimicFactKind.BiteEnded));
            Advance(10); Assert.That(_mimic.Touch(_player.Id, out _), Is.False);
        }
        [Test] public void InterruptedTouchDoesNotSpendTheOneShotBite()
        {
            _tick++;
            _mimic.Tick(new HunterArchetypeContext(_hunter, _player, _world, _world,
                _world.Doors, _world, _effects, .1f, _tick, false, 1f));
            Assert.That(_mimic.Posed, Is.True); Facts();
            Assert.That(_mimic.Touch(_player.Id, out _), Is.False);
            Assert.That(_mimic.Holding, Is.False); Assert.That(Facts(), Is.Empty);
            Advance(.1f);
            Assert.That(_mimic.Touch(_player.Id, out HunterHit hit), Is.True);
            Assert.That(hit.Damage, Is.EqualTo(25)); Assert.That(hit.Tick, Is.EqualTo(_tick));
            Assert.That(_mimic.Touch(_player.Id, out _), Is.False);
            Assert.That(Facts().FindAll(f => f.Kind == MimicFactKind.BiteStarted).Count, Is.EqualTo(1));
            Assert.That(_player.Health, Is.EqualTo(100));
        }
        [Test] public void FaithlessCurseEnablesWindowsWithoutOwnerOptIn()
        {
            _effects = new ActiveEffects(new[] { new ActiveEffect(MimicController.FaithlessArrow, EffectKind.Curse, 99) });
            Advance(100);
            Assert.That(_mimic.FaithlessEnabled, Is.True);
            var facts = Facts();
            Assert.That(facts.Exists(f => f.Kind == MimicFactKind.FaithlessWindow), Is.True);
            var fact = facts.Find(f => f.Kind == MimicFactKind.FaithlessWindow);
            Assert.That(fact.Seconds, Is.EqualTo(2f)); Assert.That(fact.WhiteArrowEligible, Is.False);
            _effects = default(ActiveEffects); Advance(100); Assert.That(Facts(), Is.Empty);
        }
        [Test] public void GoldenAndPopulationHooksAreCappedAndGoldenStillExcluded()
        {
            EchoControllerTests.Tune(_config, "_goldenChance", 1f);
            _effects = new ActiveEffects(new[] { new ActiveEffect(MimicController.GoldenMimic, EffectKind.Curse, 99),
                new ActiveEffect(MimicController.MoreMimics, EffectKind.Curse, 99), new ActiveEffect(MimicController.LongerBite, EffectKind.Curse, 99) });
            Advance(.1f); var facts = Facts(); Assert.That(facts.Find(f => f.Kind == MimicFactKind.Pose).Golden, Is.True);
            Assert.That(facts.Find(f => f.Kind == MimicFactKind.Population).ExtraCount, Is.EqualTo(3));
            foreach (var fact in facts) Assert.That(fact.WhiteArrowEligible, Is.False);
            Assert.That(_mimic.BiteSeconds, Is.EqualTo(1.2f * Mathf.Pow(1.25f, 3)).Within(.0001f));
            Assert.That(_config.BiteSeconds, Is.EqualTo(1.2f));
        }
        [Test] public void TeardownReleasesHoldOnceAndResetRestoresFreshLife()
        {
            Advance(.1f); _mimic.Touch(_player.Id, out _); Facts(); _mimic.Teardown();
            Assert.That(Facts()[0].Kind, Is.EqualTo(MimicFactKind.BiteEnded)); _mimic.Teardown(); Assert.That(Facts(), Is.Empty);
            _mimic.Reset(Context(0)); Advance(.1f); Assert.That(_mimic.Posed, Is.True);
            _mimic.Teardown(); Assert.That(Facts().Exists(f => f.Kind == MimicFactKind.PoseRemoved), Is.True);
        }
        [Test] public void SharedRulesCannotRevealOrLungeAtThePlayer()
        {
            var profile = HunterAttackControllerTests.Profile();
            EchoControllerTests.Tune(profile, "_sightRange", 30f);
            EchoControllerTests.Tune(profile, "_sightConeDegrees", 110f);
            EchoControllerTests.Tune(profile, "_sensorIntervalTicks", 4);
            var shared = new HunterController(new HunterBehaviorState(), profile, new System.Random(8), _player, _world, _mimic);
            shared.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
            var result = shared.Tick(new SightProbe(true, true, true), .1f, 1);
            Assert.That(result.Speed, Is.Zero); Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None));
            Assert.That(shared.TryDequeueFeedback(out _), Is.False); Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
            Assert.That(shared.Dormant, Is.True); Assert.That(_mimic.Posed, Is.True);
            Assert.That(_mimic.Touch(_player.Id, out HunterHit hit), Is.True, "Pursuit suppression must not suppress Mimic touch.");
            Assert.That(hit.Damage, Is.EqualTo(_config.BiteDamage)); Assert.That(hit.Source, Is.EqualTo(HitSource.Trap));
            Assert.That(hit.Severity, Is.EqualTo(HitSeverity.Light)); Assert.That(hit.Tick, Is.EqualTo(1));
            Assert.That(_player.Health, Is.EqualTo(100));
            shared.Tick(default, .1f, 2);
            Assert.That(_mimic.Touch(_player.Id, out _), Is.False, "Repeated contact must not spend a second bite.");
        }
    }
}
