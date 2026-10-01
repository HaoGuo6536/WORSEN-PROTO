// ============================================================================
// TickingGuidanceTests.cs
// ============================================================================
// PURPOSE:
//   Exercises the Ticking's current-tick navigation contract without Unity.
//   The same FloorPresenter used by its Driver supplies path corner lookahead,
//   while failed or old navigation never becomes a line through a wall.
// ARCHITECTURAL ROLE:
//   Tests (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Cover passed corners, lookahead, room crossings and failed-path invalidation.
// DEPENDENCIES:
//   - Hunter/Ticking rules, FloorPresenter, Core values and NUnit.
// USAGE NOTES:
//   Managed configuration shells receive explicit values, not native asset defaults.
// ============================================================================
using System;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Floor;
using Worsen.Domain.Hunter;
using Worsen.Domain.Hunter.Archetypes.Ticking;
using Worsen.Domain.Player;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class TickingGuidanceTests
    {
        private TickingController clock;
        private PlayerBehaviorState player;
        private RosterBTestHunter hunter;
        private long tick;
        private HunterArchetypeContext Context(float dt) => new HunterArchetypeContext(hunter, player,
            null, null, null, null, null, dt, tick, true, 1f);
        [SetUp] public void Setup()
        {
            var config = (TickingConfig)FormatterServices.GetUninitializedObject(typeof(TickingConfig));
            EchoControllerTests.Tune(config, "_springSeconds", 45f);
            EchoControllerTests.Tune(config, "_keySeconds", 1f);
            EchoControllerTests.Tune(config, "_keyDistance", new Vector2(6, 10));
            EchoControllerTests.Tune(config, "_fullTickInterval", .35f);
            EchoControllerTests.Tune(config, "_emptyTickInterval", 2f);
            EchoControllerTests.Tune(config, "_fartherKeysMultiplier", 1.25f);
            EchoControllerTests.Tune(config, "_runsFasterMultiplier", .8f);
            player = new PlayerBehaviorState { Id = new Worsen.Core.EntityId(1), Health = 100 };
            hunter = new RosterBTestHunter { Id = new Worsen.Core.EntityId(-1), IsActive = true };
            tick = 0; clock = new TickingController(config, new System.Random(4)); clock.Reset(Context(0));
            tick++; clock.Tick(Context(1)); Assert.That(clock.PlaceKey(Vector3.right * 8, true), Is.True);
        }
        private void Path(Vector3 origin, Vector3[] corners, float lookahead = 1f)
            => clock.SetGuidancePath(corners, new FloorPresenter().FirstDirection(origin, corners, lookahead));
        [Test] public void KeyBearingUsesNextUnpassedCornerWithLookaheadInsteadOfKeyVector()
        {
            var corners = new[] { Vector3.zero, Vector3.forward * 4, new Vector3(8, 0, 4), Vector3.right * 8 };
            Path(Vector3.zero, corners);
            Assert.That(clock.Guidance.WorldDirection, Is.EqualTo(Vector3.forward));
            Assert.That(clock.Guidance.IsFallback, Is.False); Assert.That(clock.GuidanceValid, Is.True);
            Path(new Vector3(.5f, 0, 4), corners);
            Assert.That(clock.Guidance.WorldDirection, Is.EqualTo(Vector3.right), "Passed doorway must not pull back.");
            Path(new Vector3(0, 0, 3.5f), corners);
            Assert.That(clock.Guidance.WorldDirection.x, Is.GreaterThan(.99f), "Look past the nearby corner.");
        }
        [Test] public void RoomCrossingRequiresFreshRouteOnTheVeryNextTick()
        {
            var rooms = new[] { new LevelRoom(1, Vector3.up * 2, new Vector3(4, 4, 4)),
                new LevelRoom(2, new Vector3(4, 2, 0), new Vector3(4, 4, 4)) };
            player.Position = Vector3.right * 1.9f;
            Assert.That(rooms[0].ContainsXZ(player.Position), Is.True);
            Path(player.Position, new[] { player.Position, Vector3.forward * 4, clock.KeyPosition });
            Assert.That(clock.GuidanceValid, Is.True);
            player.Position = Vector3.right * 2.1f; tick++; clock.Tick(Context(.01f));
            Assert.That(rooms[1].ContainsXZ(player.Position), Is.True);
            Assert.That(clock.GuidanceValid, Is.False); Assert.That(clock.Guidance.WorldDirection, Is.EqualTo(Vector3.zero));
            Path(player.Position, new[] { player.Position, clock.KeyPosition });
            Assert.That(clock.GuidanceValid, Is.True); Assert.That(clock.Guidance.WorldDirection, Is.EqualTo(Vector3.right));
        }
        [Test] public void FailedPartialOrMalformedRouteImmediatelyClearsPreviousBearing()
        {
            Path(Vector3.zero, new[] { Vector3.zero, clock.KeyPosition });
            foreach (var corners in new[] { null, Array.Empty<Vector3>(), new[] { Vector3.zero },
                new[] { Vector3.zero, new Vector3(float.NaN, 0, 0) } })
            {
                clock.SetGuidancePath(corners, Vector3.right);
                Assert.That(clock.GuidanceValid, Is.False); Assert.That(clock.Guidance.WorldDirection, Is.EqualTo(Vector3.zero));
                Assert.That(clock.Guidance.IsFallback, Is.False);
            }
            Path(Vector3.zero, new[] { Vector3.zero, clock.KeyPosition });
            Assert.That(clock.TakeKey(player.Id, clock.KeySerial), Is.True); Assert.That(clock.GuidanceValid, Is.False);
        }
    }
}
