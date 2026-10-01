// ============================================================================
// StareControllerTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the attention window using acknowledged placements and camera poses.
//   These rules tests prove cue identity, counterplay and escalation independently
//   of the coordinator's native navigation and visibility checks.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Verify edge placement, held looks, return cadence, curses and seeded subversion.
// DEPENDENCIES:
//   - Stare/shared Hunter controllers, Core views and existing NUnit fixtures.
// USAGE NOTES:
//   Time, randomness and placement/occlusion evidence are injected.
// ============================================================================
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Stare;
using Worsen.Domain.Player;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class StareControllerTests
    {
        private StareConfig _config;
        private HunterProfile _profile;
        private StareController _module;
        private HunterController _shared;
        private HunterBehaviorState _hunter;
        private Vector3 _point;
        private long _tick;
        [SetUp] public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<StareConfig>(); _profile = ScriptableObject.CreateInstance<HunterProfile>();
            EchoControllerTests.Tune(_config, "_subversionChance", 0f); EchoControllerTests.Tune(_profile, "_sensorIntervalTicks", 1);
            _hunter = new HunterBehaviorState(); _tick = 0;
            _module = new StareController(_config, new System.Random(11));
            _shared = new HunterController(_hunter, _profile, new System.Random(7),
                new PlayerBehaviorState { Id = new EntityId(1), Health = 100, SprintSpeed = 8 }, new EchoControllerTests.World(), _module);
            _shared.Reset(new EntityId(-1), Vector3.back * 10, Vector3.forward);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(_config); Object.DestroyImmediate(_profile); }
        private HunterTickResult Step(float dt, bool look = false, bool clear = true, float yaw = 0)
        {
            _tick++; Vector3 origin = Vector3.up;
            Quaternion rotation = look ? Quaternion.LookRotation(_point + Vector3.up - origin) : Quaternion.Euler(0, yaw, 0);
            _shared.SetPlayerView(new HunterPlayerView(origin, rotation, 90, 60, _tick)); _shared.ObservePlayerView(clear);
            return _shared.Tick(default, dt, _tick);
        }
        private void Place()
        {
            _point = _module.Candidate(); Assert.That(_module.AcceptPlacement(_point, true, true), Is.True);
            _shared.CommitPose(_point, Vector3.zero, Vector3.forward);
        }
        private List<StareFact> Facts()
        { var facts = new List<StareFact>(); while (_module.TakeFact(out var fact)) facts.Add(fact); return facts; }
        [Test] public void NormalPlacementTracksFrontViewEdgeAndNeverBehind()
        {
            Step(.1f); Place();
            for (int i = 1; i < 20; i++)
            {
                float yaw = i * 19f; Step(.1f, yaw: yaw); Place();
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * _point;
                Assert.That(local.z, Is.GreaterThan(0));
                Assert.That(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg), Is.EqualTo(45 * _config.EdgeFraction).Within(.001f));
                Assert.That(_module.Dormant, Is.True); Assert.That(_shared.TryAcceptContact(new EntityId(1), out _), Is.False);
            }
        }
        [Test] public void ContinuousLookScaresAwayAndRespawnsOnlyAfterCadence()
        {
            Step(.1f); Place(); Facts();
            Step(.5f, true); Assert.That(_module.Present, Is.True); Assert.That(_module.NeedsPlacement, Is.False);
            Step(.5f, true); Assert.That(_module.Present, Is.False);
            Assert.That(Facts().Exists(f => f.Kind == StareFactKind.Disappeared), Is.True);
            Step(24f); Assert.That(_module.NeedsPlacement, Is.False);
            Step(1f); Assert.That(_module.NeedsPlacement, Is.True); Place();
            Assert.That(Facts().Exists(f => f.Kind == StareFactKind.Appeared), Is.True);
        }
        [Test] public void BrokenOrOccludedLookResetsHoldAndFailureUsesHarderSharedLoss()
        {
            Step(.1f); Place(); Step(.6f, true); Step(.1f, true, false); Step(.6f, true);
            Assert.That(_module.Present, Is.True);
            Step(8f); Assert.That(_module.Dormant, Is.False);
            Assert.That(_hunter.PlayerVisible, Is.True); Assert.That(_hunter.PursuitSuppressed, Is.False);
            Assert.That(_hunter.LossSeconds, Is.EqualTo(_profile.LossSeconds * 2f));
            Assert.That(_hunter.LossDistance, Is.EqualTo(_profile.LossDistance * 2f));
            Assert.That(Facts().Exists(f => f.Kind == StareFactKind.ChaseStarted), Is.True);
        }
        [Test] public void SpokenCueRepeatsExactlyOnceAtHalfWindowAndFindMeChangesPlacement()
        {
            Step(.1f); Place(); var start = Facts();
            Assert.That(start.FindAll(f => f.SoundId == "stare.i-see-you").Count, Is.EqualTo(1));
            Step(3.9f); Assert.That(Facts(), Is.Empty); Step(.1f);
            Assert.That(Facts().FindAll(f => f.SoundId == "stare.i-see-you").Count, Is.EqualTo(1));
            Step(.1f); Assert.That(Facts(), Is.Empty);
            EchoControllerTests.Tune(_config, "_subversionChance", 1f);
            _shared.Reset(new EntityId(-2), Vector3.back * 10, Vector3.forward); Step(.1f); Place();
            Assert.That(_module.Hidden, Is.True);
            Assert.That(HunterViewUtility.Contains(new HunterPlayerView(Vector3.up, Quaternion.identity, 90, 60, _tick), _point + Vector3.up), Is.False);
            Assert.That(Facts().Exists(f => f.SoundId == "stare.find-me"), Is.True);
            Step(.1f); Assert.That(_module.NeedsPlacement, Is.False); Step(1f, true); Assert.That(_module.Present, Is.False);
        }
        [Test] public void FailedPlacementCannotStartInvisibleWindowAndCursesCapIndependently()
        {
            _shared.SetActiveEffects(new ActiveEffects(new[] {
                new ActiveEffect(new EffectId("stare-shorter-window"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("stare-quieter-call"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("stare-sooner-return"), EffectKind.Curse, 99),
                new ActiveEffect(new EffectId("stare-wider-wander"), EffectKind.Curse, 99) }));
            Step(.1f); Assert.That(_module.AcceptPlacement(_module.Candidate(), false, true), Is.False);
            Assert.That(_module.AcceptPlacement(Vector3.back * 6, true, true), Is.False);
            Step(100f); Assert.That(_module.Dormant, Is.True); Assert.That(Facts(), Is.Empty);
            Assert.That(_module.WindowSeconds, Is.EqualTo(8 * Mathf.Pow(.8f, 3)).Within(.0001f));
            Assert.That(_module.ReturnSeconds, Is.EqualTo(25 * Mathf.Pow(.8f, 3)).Within(.0001f));
            Assert.That(_module.CallVolume, Is.EqualTo(Mathf.Pow(.7f, 3)).Within(.0001f));
            Place(); Assert.That(_point.z, Is.GreaterThan(0)); Assert.That(_config.WindowSeconds, Is.EqualTo(8f));
        }
        [Test] public void FailedRepositionHidesBodyAndPausesRatherThanChargingInvisibleTime()
        {
            Step(.1f); Place(); Step(2f); _module.PlacementFailed();
            Assert.That(_module.Present, Is.False); Step(100f); Assert.That(_module.Dormant, Is.True);
            Place(); Step(5f); Assert.That(_module.Dormant, Is.True); Step(1f); Assert.That(_module.Dormant, Is.False);
        }
        [Test] public void SeededSubversionAndWanderAreRepeatableAndWidenOnlyWithCurse()
        {
            EchoControllerTests.Tune(_config, "_subversionChance", .03f);
            var context = new HunterArchetypeContext(_hunter, new PlayerBehaviorState { Id = new EntityId(1), Health = 100 },
                new EchoControllerTests.World(), null, null, null, null, .1f, 1, true, 1f);
            var a = new StareController(_config, new System.Random(19)); var b = new StareController(_config, new System.Random(19));
            int hidden = 0;
            for (int i = 0; i < 1000; i++)
            {
                a.Reset(context); b.Reset(context);
                Assert.That(a.Hidden, Is.EqualTo(b.Hidden)); if (a.Hidden) hidden++;
            }
            Assert.That(hidden, Is.InRange(1, 70));
            _shared.SetActiveEffects(new ActiveEffects(new[] { new ActiveEffect(new EffectId("stare-wider-wander"), EffectKind.Curse, 3) }));
            Step(.1f); Place(); Vector3 first = _point; Step(2f); Place(); Assert.That(_point, Is.Not.EqualTo(first));
        }
    }
}
