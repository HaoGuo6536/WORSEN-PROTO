// ============================================================================
// WeaverControllerTests.cs
// ============================================================================
// PURPOSE:
//   Tests Weaver plans, warning time, effects and habits from injected values.
//   Sweep booleans stand for engine observations here; separate integration
//   fixtures verify the real SphereCast and navigation implementation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Prove fail-closed shots, deterministic clocks/nests and catalogue identity.
// DEPENDENCIES:
//   - Hunter rules, Core values, Player state, NUnit and the existing world fixture.
// USAGE NOTES:
//   Coordinator runs Edit Mode tests; no physics or scene simulation in this fixture.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Echo;
using Worsen.Domain.Hunter.Archetypes.Weaver;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    public sealed class WeaverControllerTests
    {
        private sealed class HunterView : IReadOnlyHunterPursuitState
        {
            public EntityId Id => new EntityId(-1);
            public EntityId TargetId => new EntityId(1);
            public Vector3 Position { get; set; }
            public Vector3 Velocity { get; set; }
            public Vector3 Forward => Vector3.forward;
            public bool PlayerVisible { get; set; } = true;
            public Vector3 LastKnownPosition => Vector3.forward * 8;
            public long LastKnownTick => 0;
            public float BeliefConfidence => 1f;
            public long Tick => 0;
            public bool IsActive => true;
            public float LossSeconds => 2.5f;
            public float LossDistance => 14f;
            public bool PursuitSuppressed { get; set; }
        }
        private WeaverConfig _config;
        private HunterProfile _profile;
        private WeaverBehaviorState _state;
        private WeaverController _weaver;
        private HunterView _hunter;
        private PlayerBehaviorState _player;
        private EchoControllerTests.World _world;
        private IReadOnlyActiveEffects _effects;
        private long _tick;
        private HunterArchetypeContext Context(float dt = .1f, bool canReplay = true) => new HunterArchetypeContext(
            _hunter, _player, _world, _world, _world.Doors, _world, _effects, dt, _tick, canReplay, 1.05f);
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<WeaverConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            _state = new WeaverBehaviorState(); _hunter = new HunterView();
            _player = new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8, Position = Vector3.forward * 8 };
            _world = new EchoControllerTests.World(); _effects = default(ActiveEffects); _tick = 0;
            _weaver = new WeaverController(_state, _config, _profile, new System.Random(7)); _weaver.Reset(Context());
        }
        [TearDown] public void TearDown() { UnityEngine.Object.DestroyImmediate(_config); UnityEngine.Object.DestroyImmediate(_profile); }
        private void Step(float dt = .1f, bool clear = true, bool grounded = true, WeaverShotSpot[] spots = null, long? observedTick = null, bool moving = true)
        {
            _tick++;
            _weaver.Observe(new WeaverObservation(observedTick ?? _tick, _hunter.Position + Vector3.up,
                _weaver.Aim(1f), _weaver.Warning ? _weaver.WarnedRadius : _weaver.Radius, clear, grounded, spots));
            _weaver.Tick(Context(dt, moving));
        }
        private List<WeaverFact> Facts()
        { var facts = new List<WeaverFact>(); while (_weaver.TryTakeWeaverFact(out WeaverFact fact)) facts.Add(fact); return facts; }
        [Test] public void ObstructionRequiresRepositionAndOnlyVerifiedCandidateCanStartWarning()
        {
            Step(clear: false, spots: new[] { new WeaverShotSpot(Vector3.right, true, false),
                new WeaverShotSpot(Vector3.left, false, true), new WeaverShotSpot(Vector3.right * 2, true, true) });
            Assert.That(_weaver.Action, Is.EqualTo(WeaverAction.Reposition));
            Assert.That(_weaver.TryMovement(out Vector3 target, out float speed), Is.True);
            Assert.That(target, Is.EqualTo(Vector3.right * 2)); Assert.That(speed, Is.GreaterThan(0));
            Assert.That(_weaver.Warning, Is.False); Assert.That(_weaver.Fire, Is.False);
            _hunter.Position = target; Step(); Assert.That(_weaver.Warning, Is.True);
        }
        [Test] public void NoClearOrStaleSweepCannotWarnOrShoot()
        {
            Step(clear: false); Assert.That(_weaver.Action, Is.EqualTo(WeaverAction.None));
            Step(observedTick: 0); Assert.That(_weaver.Warning, Is.False);
            Step(); Assert.That(_weaver.Warning, Is.True);
            Step(clear: false, dt: 2f); Assert.That(_weaver.Fire, Is.False); Assert.That(_weaver.Warning, Is.False);
            Assert.That(Facts().Exists(f => f.Kind == WeaverFactKind.WarningCancelled), Is.True);
        }
        [Test] public void WarningPrecedesFixedLineShotByConfiguredTimeAndLaunchNeedsAcknowledgement()
        {
            Step(); long warned = _tick;
            Assert.That(Facts().Find(f => f.Kind == WeaverFactKind.WetClick).Duration, Is.EqualTo(.7f));
            Vector3 aim = _weaver.WarnedTarget; _player.Position = Vector3.right * 12;
            Step(.69f); Assert.That(_weaver.Fire, Is.False);
            Step(.01f); Assert.That(_weaver.Fire, Is.True); Assert.That(_weaver.WarnedTarget, Is.EqualTo(aim));
            _weaver.CommitLaunch(true);
            var shot = Facts().Find(f => f.Kind == WeaverFactKind.WebLaunched);
            Assert.That(shot.Tick, Is.GreaterThan(warned)); Assert.That(shot.End, Is.EqualTo(aim));
            Assert.That(_weaver.TryHit(_player.Id, shot.Serial, _tick, out WebHitFact hit), Is.True);
            Assert.That(hit.SlowMultiplier, Is.EqualTo(.55f)); Assert.That(hit.Duration, Is.EqualTo(2.5f));
            Assert.That(hit.SlowStrengthMultiplier, Is.EqualTo(1));
            Assert.That(_weaver.TryHit(_player.Id, shot.Serial, _tick, out _), Is.False);
        }
        [Test] public void FailedLaunchAndForeignContactsNeverPublishSlow()
        {
            Step(); Step(.7f); _weaver.CommitLaunch(false);
            Assert.That(_weaver.TryHit(_player.Id, 1, _tick, out _), Is.False);
            Assert.That(Facts().Exists(f => f.Kind == WeaverFactKind.WebLaunched), Is.False);
            Step(3f); Step(.7f); _weaver.CommitLaunch(true);
            Assert.That(_weaver.TryHit(new EntityId(999), _weaver.Serial, _tick, out _), Is.False);
            Assert.That(_weaver.TryHit(_player.Id, _weaver.Serial, _tick, out _), Is.True);
        }
        [Test] public void CursesAreCappedRadiusNeverExceedsPointOneAndWebCutterHalvesStrength()
        {
            _effects = new ActiveEffects(new[] { new ActiveEffect(WeaverController.StickierWebs, EffectKind.Curse, 100),
                new ActiveEffect(WeaverController.WiderWebs, EffectKind.Curse, 100), new ActiveEffect(WeaverController.QuickSpin, EffectKind.Curse, 100),
                new ActiveEffect(WeaverController.WebCutter, EffectKind.Upgrade, 1) });
            Step(clear: false); // Supply the newly injected effects before probing with their radius.
            Assert.That(_weaver.Radius, Is.EqualTo(.1f)); Assert.That(_weaver.WarningSeconds, Is.EqualTo(.7f * Mathf.Pow(.75f, 3)));
            Assert.That(_weaver.SlowSeconds, Is.EqualTo(2.5f * Mathf.Pow(1.5f, 3)));
            Step(); Step(_weaver.WarningSeconds); _weaver.CommitLaunch(true);
            Assert.That(_weaver.TryHit(_player.Id, _weaver.Serial, _tick, out WebHitFact hit), Is.True);
            Assert.That(hit.SlowStrengthMultiplier, Is.EqualTo(.5f));
            Assert.That(1f - (1f - hit.SlowMultiplier) * hit.SlowStrengthMultiplier, Is.EqualTo(.775f).Within(.00001f));
            Assert.That(_config.ProjectileRadius, Is.EqualTo(.06f), "Effects must not edit assets.");
        }
        [Test] public void DropsBeforeWarningAndCatchAndHonoursRetreat()
        {
            Step(grounded: false); Assert.That(_weaver.Ceiling, Is.False); Assert.That(_weaver.Warning, Is.False);
            Step(); Assert.That(_weaver.Warning, Is.True);
            Step(moving: false); Assert.That(_weaver.Ceiling, Is.False); Assert.That(_weaver.Fire, Is.False);
            _hunter.PursuitSuppressed = true; Step(); Assert.That(_weaver.Warning, Is.False);
            _hunter.PursuitSuppressed = false; _player.Position = Vector3.forward * .5f; Step();
            Assert.That(_weaver.Ceiling, Is.False); Assert.That(_weaver.Warning, Is.False);
        }
        [Test] public void PassedDoorHabitAndSeededNestsHaveIndependentNeutralHooks()
        {
            _world.TwoRooms(); _world.Door = new InteractableState(9, InteractableKind.Door, 1, Vector3.right * 2, InteractableStateValue.Open, 7);
            _weaver.Reset(Context()); Step(clear: false); Assert.That(Facts(), Is.Empty);
            _hunter.Position = Vector3.right * 4f; Step(clear: false); // A link transfer also crossed the doorway.
            var nest = Facts().Find(f => f.Kind == WeaverFactKind.DoorwayWebbed);
            Assert.That(nest.Serial, Is.GreaterThan(0)); Assert.That(nest.Position, Is.EqualTo(_world.Door.Position));
            Assert.That(_weaver.TryHit(_player.Id, nest.Serial, _tick, out _), Is.True);
            _effects = new ActiveEffects(new[] { new ActiveEffect(WeaverController.DoorwayNests, EffectKind.Curse, 1) });
            EchoControllerTests.Tune(_config, "_nestChancePerStack", 1f); _weaver.Reset(Context());
            Step(clear: false); Assert.That(Facts().Exists(f => f.Kind == WeaverFactKind.DoorwayWebbed), Is.True);
            Step(clear: false); Assert.That(Facts(), Is.Empty);
        }
        [Test] public void SameSeedAndInputsRepeatNestChoicesAndDuplicateTickCannotAdvanceWarning()
        {
            _world.TwoRooms(); _world.Door = new InteractableState(9, InteractableKind.Door, 1, Vector3.right * 2, InteractableStateValue.Open, 7);
            _effects = new ActiveEffects(new[] { new ActiveEffect(WeaverController.DoorwayNests, EffectKind.Curse, 2) });
            List<WeaverFactKind> Run()
            {
                _tick = 0; _weaver = new WeaverController(new WeaverBehaviorState(), _config, _profile, new System.Random(42));
                _weaver.Reset(Context()); Step(); var kinds = Facts().ConvertAll(f => f.Kind);
                _weaver.Tick(Context(20f)); Assert.That(_weaver.Fire, Is.False);
                Step(.7f); _weaver.CommitLaunch(true); kinds.AddRange(Facts().ConvertAll(f => f.Kind)); return kinds;
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
        [Test] public void EchoIdsAreExactCatalogueKeys()
        {
            Assert.That(EchoController.ShorterDelay.Value, Is.EqualTo("echo-shorter-delay"));
            Assert.That(EchoController.FasterPlayback.Value, Is.EqualTo("echo-faster-playback"));
            Assert.That(EchoController.SilentSteps.Value, Is.EqualTo("echo-silent-steps"));
        }
    }
}
