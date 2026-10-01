// ============================================================================
// MannequinLightingTests.cs
// ============================================================================
// PURPOSE:
//   Reproduces the room-light evidence that controls the Mannequin's movement gate.
//   One remaining lit lamp keeps the whole room safe regardless of camera direction
//   or distance to the lamp. A dark route must therefore be supplied by floor content,
//   not by weakening Mannequin observation, illumination or Wick protections.
// ARCHITECTURAL ROLE:
//   Editor tool (§10), test suite (§11) · Editor · Hunter.
// KEY RESPONSIBILITIES:
//   - Guard known-dark versus unknown-room evidence without engine objects.
//   - Show why extinguishing only one lamp cannot release an all-lit spawn room.
// DEPENDENCIES:
//   - Production ExpeditionHunterController, Core values and scripted Level snapshots.
// USAGE NOTES:
//   Managed/headless input characterization, not evidence of a repaired generated
//   floor. Native Mannequin motion/contact is covered by RosterFunctionalTests.
//   Floor generation and spawn selection are outside this worker's ownership.
// ============================================================================
using System;
using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Session.Expedition;
namespace Worsen.Tests.Hunter
{
    [Worsen.Tests.Infrastructure.FixtureTimeGuard]
    public sealed class MannequinLightingTests
    {
        private static RosterFunctionalWorld World() => new RosterFunctionalWorld {
            Graph = new LevelGraph(new[] { new LevelRoom(1, Vector3.up * 2f, new Vector3(40, 4, 40)),
                new LevelRoom(2, new Vector3(40, 2, 0), new Vector3(40, 4, 40)) },
                new[] { new LevelEdge(7, 1, 2, true, TraversalAccess.All) }, Array.Empty<LevelAnchor>(), 2, Vector3.right * 40) };
        private static InteractableState Lamp(int id, int room, bool lit) => new InteractableState(id,
            InteractableKind.Light, room, new Vector3(room * 20, 3, 19),
            lit ? InteractableStateValue.Lit : InteractableStateValue.Inactive);

        [Test]
        public void AnyRemainingLampKeepsRoomSafe_AllOffProducesKnownDarkness()
        {
            var level = World();
            level.Lights = new[] { Lamp(11, 1, true), Lamp(12, 1, true), Lamp(21, 2, true) };
            var view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, level);
            Assert.That(view.TryGetRoomLit(1, out bool lit), Is.True); Assert.That(lit, Is.True);
            level.Lights = new[] { Lamp(11, 1, false), Lamp(12, 1, true), Lamp(21, 2, true) };
            Assert.That(view.TryGetRoomLit(1, out lit), Is.True); Assert.That(lit, Is.True,
                "Partial lamp removal must not silently redefine a lit refuge as darkness.");
            level.Lights = new[] { Lamp(11, 1, false), Lamp(12, 1, false), Lamp(21, 2, true) };
            Assert.That(view.TryGetRoomLit(1, out lit), Is.True); Assert.That(lit, Is.False);
            Assert.That(view.TryGetRoomLit(2, out lit), Is.True); Assert.That(lit, Is.True,
                "An intentionally dark route must not also extinguish its neighbouring refuge.");
        }

        [Test]
        public void KnownRoomWithoutLampsIsDark_UnknownRoomOrMissingWorldIsNotDarkEvidence()
        {
            var level = World();
            var view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, level);
            Assert.That(view.TryGetRoomLit(1, out bool lit), Is.True); Assert.That(lit, Is.False);
            Assert.That(view.TryGetRoomLit(999, out _), Is.False);
            view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, null, level);
            Assert.That(view.TryGetRoomLit(1, out _), Is.False);
            view = new ExpeditionHunterController(new ExpeditionHunterBehaviorState(), 1, level.Graph, null);
            Assert.That(view.TryGetRoomLit(1, out _), Is.False);
        }
    }
}
