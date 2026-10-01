// ============================================================================
// ExpeditionHunterControllerTests.cs
// ============================================================================
// PURPOSE:
//   Checks world lighting, door revisions and once-only Skip floor/route admission.
// ARCHITECTURAL ROLE:
//   Editor tool (§11 tests) · Session · Expedition.
// KEY RESPONSIBILITIES:
//   - Reject stale breaks and duplicate routes while preserving floor-local identities.
//   - Observe live room lights without depending on Presentation or engine objects.
// DEPENDENCIES:
//   - Expedition pure Controller/state, Core, NUnit and Unity value types.
// USAGE NOTES:
//   Pure managed fixture; not evidence of camera sampling or physical placement.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Tests.Expedition
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class ExpeditionHunterControllerTests
    {
        private sealed class Items : IReadOnlyInteractableSet
        {
            public InteractableState[] Values = Array.Empty<InteractableState>();
            public bool TryGet(int id, out InteractableState state)
            { foreach (var item in Values) if (item.Id == id) { state = item; return true; } state = default; return false; }
            public IReadOnlyList<InteractableState> InRoom(int room) => Array.FindAll(Values, item => item.RoomId == room);
        }
        private static ExpeditionHunterController Create(Items items, int floor = 7) => new ExpeditionHunterController(
            new ExpeditionHunterBehaviorState(), floor, new LevelGraph(new[] { new LevelRoom(1, Vector3.zero, Vector3.one) },
                Array.Empty<LevelEdge>(), Array.Empty<LevelAnchor>(), 1, Vector3.zero), items);
        [Test] public void LightingChangesAreLiveAndUnknownRoomFailsClosed()
        {
            var items = new Items(); var controller = Create(items);
            Assert.That(controller.TryGetRoomLit(99, out _), Is.False);
            Assert.That(controller.TryGetRoomLit(1, out bool lit), Is.True); Assert.That(lit, Is.False);
            items.Values = new[] { new InteractableState(2, InteractableKind.Light, 1, Vector3.zero, InteractableStateValue.Lit) };
            Assert.That(controller.TryGetRoomLit(1, out lit), Is.True); Assert.That(lit, Is.True);
            items.Values = Array.Empty<InteractableState>();
            controller.TryGetRoomLit(1, out lit); Assert.That(lit, Is.False);
        }
        [Test] public void RejammingRejectsOldCompletionAndRemovalIsFinal()
        {
            var controller = Create(new Items()); var hunter = new EntityId(-1);
            Assert.That(controller.BindHunter(hunter), Is.True); Assert.That(controller.BindHunter(hunter), Is.False);
            var fact = new DoorJamFact(101, Vector3.zero, 10f, 2f, true);
            var bounds = new Bounds(Vector3.one, new Vector3(2, 3, 1));
            controller.SetJam(fact, bounds); long first = controller.JammedDoors[0].Revision;
            Assert.That(controller.JammedDoors[0].Bounds, Is.EqualTo(bounds));
            Assert.That(controller.MatchesBreak(new HunterDoorBreakFact(hunter, 101, first, 1)), Is.True);
            controller.SetJam(fact, bounds);
            Assert.That(controller.MatchesBreak(new HunterDoorBreakFact(hunter, 101, first, 2)), Is.False);
            long next = controller.JammedDoors[0].Revision;
            Assert.That(controller.MatchesBreak(new HunterDoorBreakFact(new EntityId(-2), 101, next, 2)), Is.False);
            controller.RemoveJam(101);
            Assert.That(controller.MatchesBreak(new HunterDoorBreakFact(hunter, 101, next, 2)), Is.False);
        }
        [Test] public void RouteDeliveryDeduplicatesAndNewFloorResetsBindingAndCounts()
        {
            var controller = Create(new Items()); var player = new EntityId(1);
            Assert.That(controller.TryRoute(player, 10, 7, SkipRouteKind.VaultWindow, Vector3.one, -1, out var use), Is.True);
            Assert.That(use.Floor, Is.EqualTo(7)); Assert.That(use.Sequence, Is.EqualTo(10));
            Assert.That(use.Position, Is.EqualTo(Vector3.one));
            Assert.That(controller.TryRoute(player, 10, 7, SkipRouteKind.VaultWindow, Vector3.one, -1, out _), Is.False);
            Assert.That(controller.TryRoute(player, 9, 7, SkipRouteKind.VaultWindow, Vector3.one, -1, out _), Is.False);
            Assert.That(controller.TryRoute(player, 11, 7, SkipRouteKind.VaultWindow, Vector3.one, -1, out _), Is.True);
            Assert.That(controller.TryRoute(player, 11, 0, SkipRouteKind.VaultWindow, Vector3.one, -1, out _), Is.False);
            Assert.That(Create(new Items(), 8).TryRoute(player, 1, 7, SkipRouteKind.VaultWindow, Vector3.one, -1, out use), Is.True);
            Assert.That(use.Floor, Is.EqualTo(8));
        }
        [Test] public void DoorwayUsesAuthoredEdgeAndThresholdMarkNotSyntheticRoomHash()
        {
            var items = new Items { Values = new[] { new InteractableState(301001, InteractableKind.ThresholdMark,
                1, new Vector3(2, .02f, 3), InteractableStateValue.Inactive, 1001) } };
            var controller = Create(items); var player = new EntityId(1);
            Assert.That(controller.TryDoorway(player, 1, new Vector3(2, 0, 3), Vector3.forward, out var use), Is.True);
            Assert.That(use.RouteId, Is.EqualTo(1001)); Assert.That(use.MarkId, Is.EqualTo(301001));
            Assert.That(use.Kind, Is.EqualTo(SkipRouteKind.Doorway));
            Assert.That(controller.TryDoorway(player, 1, new Vector3(2, 0, 3), Vector3.forward, out _), Is.False);
            Assert.That(controller.TryDoorway(player, 2, Vector3.zero, Vector3.forward, out _), Is.False);
        }
    }
}
