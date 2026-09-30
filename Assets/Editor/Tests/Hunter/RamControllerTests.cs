// ============================================================================
// RamControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies stamped commitment, straight motion and wall punishment as pure rules.
//   Shared-controller coverage prevents a normal lunge from bypassing the new attack.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Exercise injected time, seeded shared decisions, curses and hit admission.
// DEPENDENCIES:
//   - Hunter rules, existing world fixture, Core facts and NUnit.
// USAGE NOTES:
//   Coordinator runs in Edit Mode; physical sweep checks are in RosterBIntegrationTests.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ram;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class RamControllerTests
    {
        private RamConfig _config;
        private HunterProfile _profile;
        private RamController _ram;
        private RosterBTestHunter _hunter;
        private PlayerBehaviorState _player;
        private EchoControllerTests.World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(float dt) => new HunterArchetypeContext(_hunter, _player, _world, _world,
            _world.Doors, _world, _effects, dt, _tick, true, 1f);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<RamConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _hunter = new RosterBTestHunter { Id = new EntityId(-1), IsActive = true, Forward = Vector3.forward,
                PlayerVisible = true, LastKnownPosition = Vector3.forward * 10 };
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, Position = Vector3.forward * 10, SprintSpeed = 8f };
            _world = new EchoControllerTests.World(); _effects = default(ActiveEffects); _tick = 0;
            _ram = new RamController(_config, _profile); _ram.Reset(Context(0));
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_config); Object.DestroyImmediate(_profile); }
        private void Advance(float dt) { _tick++; _ram.Tick(Context(dt)); }
        private List<RamFact> Facts() { var facts = new List<RamFact>(); while (_ram.TakeFact(out var fact)) facts.Add(fact); return facts; }
        private void Charge() { Advance(.1f); Advance(_ram.WindupSeconds); Advance(.1f); }
        [Test] public void StampPrecedesBellowAndHeadingStaysFixedThroughWarningAndCharge()
        {
            Advance(.1f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Windup)); Assert.That(_ram.Motion, Is.EqualTo(Vector3.zero));
            Assert.That(Facts()[0].Kind, Is.EqualTo(RamFactKind.Stamp));
            _player.Position = Vector3.right * 10; _hunter.LastKnownPosition = _player.Position;
            Advance(.9f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Windup));
            Advance(.1f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Charge));
            Assert.That(Facts()[0].Kind, Is.EqualTo(RamFactKind.Bellow)); Assert.That(_ram.Motion, Is.EqualTo(Vector3.zero));
            for (int i = 0; i < 4; i++)
            {
                Advance(.1f); Assert.That(_ram.Motion.x, Is.Zero); Assert.That(_ram.Direction, Is.EqualTo(Vector3.forward));
                Vector3 end = _hunter.Position + _ram.Motion; _ram.CommitMotion(end, false); _hunter.Position = end;
            }
            Assert.That(Facts().Exists(f => f.Kind == RamFactKind.Stride), Is.True);
        }
        [TestCase(false)] [TestCase(true)] public void WallStopsAndStaggersAndOnlyBreakerPublishesImpact(bool breaker)
        {
            if (breaker) _effects = new ActiveEffects(new[] { new ActiveEffect(RamController.PartitionBreaker, EffectKind.Curse, 99) });
            Charge(); Vector3 wall = _hunter.Position + Vector3.forward;
            _ram.CommitMotion(wall, true, 17); _hunter.Position = wall;
            Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Stagger)); Assert.That(_ram.Motion, Is.EqualTo(Vector3.zero));
            var facts = Facts(); Assert.That(facts.Exists(f => f.Kind == RamFactKind.PartitionImpact && f.ObjectId == 17), Is.EqualTo(breaker));
            Assert.That(facts.Exists(f => f.Kind == RamFactKind.WallStagger && f.Position == wall), Is.True);
            _ram.CommitMotion(wall, true, 17); Assert.That(Facts(), Is.Empty);
            Advance(.6f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Stagger)); Assert.That(_ram.TryHit(_player.Id, out _), Is.False);
            Advance(.6f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Ready));
        }
        [Test] public void VariantBreaksWithoutCurseAndChargeUsesNormalOneHitPath()
        {
            EchoControllerTests.Tune(_config, "_partitionBreakerVariant", true); Charge();
            Assert.That(_ram.TryHit(new EntityId(2), out _), Is.False);
            Assert.That(_ram.TryHit(_player.Id, out HunterHit hit), Is.True);
            Assert.That(hit.Damage, Is.EqualTo(_profile.LungeDamage)); Assert.That(hit.Source, Is.EqualTo(HitSource.Lunge));
            Assert.That(_ram.TryHit(_player.Id, out _), Is.False);
            _ram.CommitMotion(Vector3.forward, true, 7);
            Assert.That(Facts().Exists(f => f.Kind == RamFactKind.PartitionImpact), Is.True);
        }
        [Test] public void NumericCursesCapWithoutChangingAssets()
        {
            _effects = new ActiveEffects(new[] { new ActiveEffect(RamController.LongerCharge, EffectKind.Curse, 99),
                new ActiveEffect(RamController.ShorterWindup, EffectKind.Curse, 99) }); Advance(.1f);
            Assert.That(_ram.WindupSeconds, Is.EqualTo(1f * Mathf.Pow(.8f, 3)).Within(.0001f));
            Assert.That(_ram.ChargeDistance, Is.EqualTo(18f * Mathf.Pow(1.25f, 3)).Within(.0001f));
            Assert.That(_config.WindupSeconds, Is.EqualTo(1f)); Assert.That(_config.ChargeDistance, Is.EqualTo(18f));
        }
        [Test] public void SecondChargeTurnsOnlyBetweenChargesAndDoesNotChainForever()
        {
            _effects = new ActiveEffects(new[] { new ActiveEffect(RamController.SecondCharge, EffectKind.Curse, 99) });
            Charge(); _hunter.LastKnownPosition = Vector3.right * 10;
            Advance(2f); Vector3 end = _hunter.Position + _ram.Motion; _ram.CommitMotion(end, false); _hunter.Position = end;
            Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Windup));
            Assert.That(Vector3.Dot(_ram.Direction, (_hunter.LastKnownPosition - end).normalized), Is.GreaterThan(.999f));
            Advance(1f); Advance(2f); end = _hunter.Position + _ram.Motion; _ram.CommitMotion(end, false); _hunter.Position = end;
            Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Stagger)); Advance(1.2f); Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Ready));
        }
        [Test] public void SharedLungeNeverStartsAndDefaultModuleStillAttacks()
        {
            _player.Position = Vector3.forward;
            var state = new HunterBehaviorState();
            var shared = new HunterController(state, _profile, new System.Random(9), _player, _world, _ram);
            shared.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
            for (int i = 1; i <= 10; i++)
            { var result = shared.Tick(new SightProbe(true, true, true), .1f, i);
                Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None)); Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False); }
            shared = new HunterController(state, _profile, new System.Random(9), _player, _world);
            shared.Reset(new EntityId(-1), Vector3.zero, Vector3.forward);
            Assert.That(shared.Tick(new SightProbe(true, true, true), .1f, 1).BeginLunge, Is.True);
        }
        [Test] public void DuplicateTicksInvalidTimeAndCatchDoNotAdvanceACharge()
        {
            Charge(); Vector3 motion = _ram.Motion; _ram.Tick(Context(10f)); Assert.That(_ram.Motion, Is.EqualTo(motion));
            Advance(float.NaN); Assert.That(_ram.Motion, Is.EqualTo(motion));
            _tick++;
            _ram.Tick(new HunterArchetypeContext(_hunter, _player, _world, _world, _world.Doors, _world, _effects, .1f, _tick, false, 1f));
            Assert.That(_ram.Phase, Is.EqualTo(RamPhase.Ready)); Assert.That(_ram.Motion, Is.EqualTo(Vector3.zero));
        }
    }
}
