// ============================================================================
// LevelInteractableControllerTests.cs
// ============================================================================
// PURPOSE:
//   Checks the floor registry's command semantics, immutable observations and the
//   exact closed-door dictionary supplied to the shared acoustic calculation.
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · Tests · Level.
// KEY RESPONSIBILITIES:
//   - Validate identity/edge ownership and atomic loading.
//   - Check state-change facts, no-op suppression, teardown and hearing loss.
// DEPENDENCIES:
//   - Core, Domain.Level, NUnit and temporary Unity objects for Manager integration.
// USAGE NOTES:
//   Most tests use pure controllers; one fixture exercises Manager fact publication.
// ============================================================================
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Domain.Level;

namespace Worsen.Tests.Level
{
    public sealed class LevelInteractableControllerTests
    {
        private static LevelGraph Graph() => LevelGraphUtility.Build(new[] {
            new LevelRoom(1, new Vector3(0f, 2f, 0f), new Vector3(12f, 4f, 12f)),
            new LevelRoom(2, new Vector3(12f, 2f, 0f), new Vector3(12f, 4f, 12f)) },
            new[] { new LevelEdge(11, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 1, Vector3.zero);
        private static InteractableState Item(int id, InteractableKind kind, InteractableStateValue value, int edge = -1)
            => new InteractableState(id, kind, 1, new Vector3(1f, 1f, 1f), value, edge);

        [Test]
        public void ClosedPortalIsConsumedByHearingAndBreakingIsTerminal()
        {
            var state = new LevelInteractableBehaviorState(); var controller = new LevelInteractableController(state);
            var graph = Graph(); controller.Load(graph, new[] { Item(101, InteractableKind.Door, InteractableStateValue.Open, 11) });
            var view = state.ClosedDoors;
            var settings = new HearingModelSettings(1f, 0f, 0.8f, 0.25f, 0.01f);
            var open = AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 2, Vector3.right * 12f, 1f, settings, view);
            Assert.That(controller.Change(101, InteractableKind.Door, InteractableStateValue.Inactive, out var before, out var after), Is.True);
            Assert.That(before.Value, Is.EqualTo(InteractableStateValue.Open)); Assert.That(after.Value, Is.EqualTo(InteractableStateValue.Inactive));
            var closed = AcousticOcclusionUtility.Sample(graph, 1, Vector3.zero, 2, Vector3.right * 12f, 1f, settings, view);
            Assert.That(closed.CrossedClosedDoor, Is.True); Assert.That(closed.PerceivedLoudness, Is.EqualTo(open.PerceivedLoudness * 0.25f).Within(0.0001f));
            Assert.That(controller.Change(101, InteractableKind.Door, InteractableStateValue.Broken, out _, out _), Is.True);
            Assert.That(view[11], Is.False);
            Assert.That(controller.Change(101, InteractableKind.Door, InteractableStateValue.Open, out _, out _), Is.False);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<int, bool>)view)[11] = true);
            controller.Clear(); Assert.That(view.Count, Is.Zero); Assert.That(state.InRoom(1), Is.Empty);
        }

        [Test]
        public void InvalidLoadDoesNotPublishAPartialRegistry()
        {
            var state = new LevelInteractableBehaviorState(); var controller = new LevelInteractableController(state);
            var door = Item(101, InteractableKind.Door, InteractableStateValue.Open, 11);
            Assert.Throws<ArgumentException>(() => controller.Load(Graph(), new[] { door, door }));
            Assert.That(state.InRoom(1), Is.Empty);
            Assert.Throws<ArgumentException>(() => controller.Load(Graph(), new[] { Item(102, InteractableKind.Door, InteractableStateValue.Open, 999) }));
            Assert.Throws<ArgumentException>(() => controller.Load(Graph(), new[] { Item(102, InteractableKind.Light, InteractableStateValue.Open) }));
            Assert.That(controller.Change(999, InteractableKind.Door, InteractableStateValue.Open, out _, out _), Is.False);
        }

        [Test]
        public void ManagerCommandsPublishOnlyCommittedChangesAndResetBetweenFloors()
        {
            var owner = new GameObject("Level interactable fact test");
            try
            {
                var manager = owner.AddComponent<LevelManager>();
                manager.InitializeGenerated(Graph(), new[] {
                    Item(101, InteractableKind.Door, InteractableStateValue.Open, 11),
                    Item(102, InteractableKind.Light, InteractableStateValue.Lit),
                    Item(103, InteractableKind.KnockableProp, InteractableStateValue.Inactive),
                    Item(104, InteractableKind.Partition, InteractableStateValue.Inactive),
                    Item(105, InteractableKind.ThresholdMark, InteractableStateValue.Inactive) });
                int facts = 0;
                manager.InteractableChanged += (before, after) => {
                    facts++; Assert.That(before.Id, Is.EqualTo(after.Id));
                    Assert.That(manager.Interactables.TryGet(after.Id, out var stored), Is.True);
                    Assert.That(stored, Is.EqualTo(after));
                    if (after.Kind == InteractableKind.Door) Assert.That(manager.ClosedDoors[11], Is.EqualTo(after.Value == InteractableStateValue.Inactive));
                };
                Assert.That(manager.CloseDoor(101), Is.True); Assert.That(manager.CloseDoor(101), Is.False);
                Assert.That(manager.OpenDoor(101), Is.True);
                Assert.That(manager.SetLit(102, false), Is.True); Assert.That(manager.SetLit(102, true), Is.True);
                Assert.That(manager.Knock(103), Is.True); Assert.That(manager.Knock(103), Is.False);
                Assert.That(manager.Break(104), Is.True); Assert.That(manager.Break(104), Is.False);
                Assert.That(manager.Mark(105), Is.True); Assert.That(manager.Mark(105), Is.False);
                Assert.That(manager.CloseDoor(102), Is.False); Assert.That(manager.Break(102), Is.False);
                Assert.That(facts, Is.EqualTo(7));
                manager.InitializeGenerated(Graph());
                Assert.That(manager.Interactables.InRoom(1), Is.Empty); Assert.That(manager.ClosedDoors, Is.Empty);
                Assert.That(manager.CloseDoor(101), Is.False); Assert.That(facts, Is.EqualTo(7));
            }
            finally { UnityEngine.Object.DestroyImmediate(owner); }
        }
    }
}
