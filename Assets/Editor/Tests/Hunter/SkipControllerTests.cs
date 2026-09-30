// ============================================================================
// SkipControllerTests.cs
// ============================================================================
// PURPOSE:
//   Verifies floor-local learning and atomic silent interception without a scene.
//   Placement success, time and seeded choices are injected as explicit evidence.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover both route kinds, cooldown, deduplication, curses and slow foot motion.
// DEPENDENCIES:
//   - Hunter rules, Core effects, existing world fixture and NUnit.
// USAGE NOTES:
//   Physics and Session routing require coordinator integration checks.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Skip;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class SkipControllerTests
    {
        private SkipConfig _config;
        private HunterProfile _profile;
        private SkipController _skip;
        private RosterBTestHunter _hunter;
        private PlayerBehaviorState _player;
        private EchoControllerTests.World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(float dt) => new HunterArchetypeContext(_hunter, _player, _world, _world,
            _world.Doors, _world, _effects, dt, _tick, true, 1f);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SkipConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            EchoControllerTests.Tune(_profile, "_patrolSpeed", 1.5f);
            _hunter = new RosterBTestHunter { Id = new EntityId(-1), IsActive = true };
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8f };
            _world = new EchoControllerTests.World(); _effects = default(ActiveEffects); _tick = 0;
            _skip = new SkipController(_config, _profile, new System.Random(19)); _skip.Reset(Context(0)); _skip.BeginFloor(1);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_config); Object.DestroyImmediate(_profile); }
        private void Advance(float dt) { _tick++; _skip.Tick(Context(dt)); }
        private SkipTraversalUse Use(int sequence, SkipRouteKind kind = SkipRouteKind.Doorway, long floor = 1)
            => new SkipTraversalUse(floor, sequence, _player.Id, 7, kind, Vector3.right * 5, 8);
        private List<SkipFact> Facts() { var facts = new List<SkipFact>(); while (_skip.TakeFact(out var fact)) facts.Add(fact); return facts; }
        [TestCase(SkipRouteKind.Doorway)] [TestCase(SkipRouteKind.VaultWindow)]
        public void NeedsThreeCompletedUsesAndCooldownBeforeSilentInvisibleTeleport(SkipRouteKind kind)
        {
            _skip.RecordUse(Use(1, kind)); _skip.RecordUse(Use(2, kind)); Advance(12f);
            Assert.That(_skip.TryTeleport(out _), Is.False);
            _skip.RecordUse(Use(3, kind)); Advance(.1f); Assert.That(_skip.TryTeleport(out var position), Is.True);
            Assert.That(Facts(), Is.Empty); _skip.CommitTeleport(true, position);
            var facts = Facts(); Assert.That(facts.Count, Is.EqualTo(2));
            Assert.That(facts[0].Kind, Is.EqualTo(SkipFactKind.Teleported)); Assert.That(facts[1].Kind, Is.EqualTo(SkipFactKind.Marked));
            foreach (var fact in facts)
            { Assert.That(fact.Audible, Is.False); Assert.That(fact.VisibleTransition, Is.False); Assert.That(fact.MarkId, Is.EqualTo(8)); }
            Advance(11f); Assert.That(_skip.TryTeleport(out _), Is.False);
            Advance(1f); Assert.That(_skip.TryTeleport(out _), Is.True);
        }
        [Test] public void FloorResetClearsCountsPendingTargetsCooldownAndRejectsOldDeliveries()
        {
            for (int i = 1; i <= 3; i++) Assert.That(_skip.RecordUse(Use(i)), Is.True);
            Assert.That(_skip.RecordUse(Use(3)), Is.False); Advance(12f);
            Assert.That(_skip.BeginFloor(2), Is.True); Assert.That(_skip.BeginFloor(1), Is.False); Assert.That(_skip.BeginFloor(2), Is.False);
            Assert.That(_skip.Uses(7), Is.Zero); Assert.That(_skip.TryTeleport(out _), Is.False); Assert.That(_skip.RecordUse(Use(4)), Is.False);
            _skip.TryMovement(out var target, out float speed); Assert.That(target, Is.EqualTo(_hunter.Position)); Assert.That(speed, Is.EqualTo(1.5f));
            for (int i = 1; i <= 3; i++) _skip.RecordUse(Use(i, floor: 2));
            Advance(11f); Assert.That(_skip.TryTeleport(out _), Is.False); Advance(1f); Assert.That(_skip.TryTeleport(out _), Is.True);
        }
        [Test] public void FootMotionSlowlyFollowsObservedRouteNotHiddenPlayerPosition()
        {
            _skip.RecordUse(Use(1)); _player.Position = Vector3.back * 100;
            _skip.TryMovement(out var target, out float speed);
            Assert.That(target, Is.EqualTo(Use(1).Position)); Assert.That(speed, Is.EqualTo(1.5f));
            Advance(.1f); Assert.That(_skip.TryTeleport(out _), Is.False);
        }
        [Test] public void FailedPlacementAndClosedRoomsPublishNothing()
        {
            for (int i = 1; i <= 3; i++) _skip.RecordUse(Use(i)); Advance(12f);
            _skip.TryTeleport(out var position); _skip.CommitTeleport(false, position); Assert.That(Facts(), Is.Empty);
            Advance(.1f); Assert.That(_skip.TryTeleport(out _), Is.True); _skip.CommitTeleport(true, position + Vector3.one);
            Assert.That(Facts(), Is.Empty); _world.Phases[1] = RoomPhase.Closed; Advance(.1f); Assert.That(_skip.TryTeleport(out _), Is.False);
        }
        [Test] public void CursesAreNeutralUntilActiveAndCapped()
        {
            Assert.That(_skip.Threshold, Is.EqualTo(3)); Assert.That(_skip.Cooldown, Is.EqualTo(12f));
            _effects = new ActiveEffects(new[] { new ActiveEffect(SkipController.ShorterCooldown, EffectKind.Curse, 99),
                new ActiveEffect(SkipController.QuickerLearner, EffectKind.Curse, 99), new ActiveEffect(SkipController.NoTell, EffectKind.Curse, 99) });
            Advance(.1f); Assert.That(_skip.Threshold, Is.EqualTo(1)); Assert.That(_skip.Cooldown, Is.EqualTo(12f * Mathf.Pow(.8f, 3)).Within(.0001f));
            _skip.RecordUse(Use(1)); Advance(12f); _skip.TryTeleport(out var position); _skip.CommitTeleport(true, position);
            Assert.That(Facts().Count, Is.EqualTo(1)); Assert.That(_config.UsesRequired, Is.EqualTo(3));
        }
        [TestCase(SkipRouteKind.StairHead)] [TestCase(SkipRouteKind.Drop)] public void WiderReachAloneAdmitsOtherRoutes(SkipRouteKind kind)
        {
            for (int i = 1; i <= 3; i++) _skip.RecordUse(Use(i, kind)); Advance(12f); Assert.That(_skip.TryTeleport(out _), Is.False);
            _effects = new ActiveEffects(new[] { new ActiveEffect(SkipController.WiderReach, EffectKind.Curse, 1) });
            Advance(.1f); Assert.That(_skip.TryTeleport(out _), Is.True);
        }
        [Test] public void SharedDormancySuppressesPursuitLungesAndSounds()
        {
            var state = new HunterBehaviorState();
            var shared = new HunterController(state, _profile, new System.Random(9), _player, _world, _skip);
            shared.Reset(new EntityId(-1), Vector3.forward, Vector3.back);
            var result = shared.Tick(new SightProbe(true, true, true), .1f, 1);
            Assert.That(result.Phase, Is.EqualTo(HunterLungePhase.None)); Assert.That(state.PursuitSuppressed, Is.True);
            Assert.That(shared.TryDequeueFeedback(out _), Is.False); Assert.That(shared.TryAcceptContact(_player.Id, out _), Is.False);
        }
        [Test] public void WrongPlayerAndInvalidRouteAreRejectedAndDuplicatesOwnTheirCounts()
        {
            Assert.That(_skip.RecordUse(new SkipTraversalUse(1, 1, new EntityId(8), 7, SkipRouteKind.Doorway, Vector3.zero)), Is.False);
            Assert.That(_skip.RecordUse(new SkipTraversalUse(1, 1, _player.Id, 0, SkipRouteKind.Doorway, Vector3.zero)), Is.False);
            var other = new SkipController(_config, _profile, new System.Random(19)); other.Reset(Context(0)); other.BeginFloor(1);
            _skip.RecordUse(Use(1)); Assert.That(other.Uses(7), Is.Zero);
            for (int i = 1; i <= 3; i++) { _skip.RecordUse(Use(i)); other.RecordUse(Use(i)); }
            _tick++; _skip.Tick(Context(12)); other.Tick(Context(12));
            _skip.TryTeleport(out var a); other.TryTeleport(out var b); Assert.That(a, Is.EqualTo(b));
        }
    }
}
