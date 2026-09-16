// ============================================================================
// AudioWorldPresenterTests.cs
// ============================================================================
//
// PURPOSE:
//   Tests bounded torch admission and quiet room accent sequencing without playing audio.
//   It verifies geometry admission, stable loops, consumed-room release and cooldown/variation behavior.
//
// ARCHITECTURAL ROLE:
//   Editor tool (§10) · test suite (§11) · Audio.
//
// KEY RESPONSIBILITIES:
//   - Keep ambient emitters bounded and tied to supplied room geometry.
//   - Preserve transient presentation ownership and deterministic verification.
//
// DEPENDENCIES:
//   - Core room geometry values and the owning Audio presentation system.
//
// USAGE NOTES:
//   Pure tests use supplied room bounds, explicit time and seeded cosmetic randomness.
//
// ============================================================================

using NUnit.Framework;
using UnityEngine;
using Worsen.Core;
using Worsen.Presentation.Audio;
namespace Worsen.Tests.Audio
{
    public sealed class AudioWorldPresenterTests
    {
        private AudioWorldDriverState State()
        {
            var s = new AudioWorldDriverState();
            s.Rooms[1] = new GeneratedRoomSample(1, new Bounds(Vector3.zero, Vector3.one * 20), false, false, new[] { Vector3.right * 9 });
            return s;
        }
        [Test]
        public void SelectsOnlyNearestFourAndRetainedSlotsDoNotRestart()
        {
            var p = new AudioWorldPresenter(); var s = State();
            p.SetTorches(s, 1, new[] { Vector3.right, Vector3.right * 2, Vector3.right * 3, Vector3.right * 4, Vector3.right * 8, Vector3.right * 9 });
            p.SelectTorches(s, Vector3.zero, 16); Assert.That(s.Nearest.Count, Is.EqualTo(4));
            Assert.That(s.Nearest[3].Position, Is.EqualTo(Vector3.right * 4));
            p.TickTorches(s, .3f, 2); int first = s.Slots[0].Id;
            p.TickTorches(s, .3f, 2); Assert.That(s.Slots[0].Id, Is.EqualTo(first)); Assert.That(s.Slots[0].Changed, Is.False);
            Assert.That(s.Slots.Length, Is.EqualTo(4));
        }
        [Test]
        public void ConsumedRoomsFadeOutAndDisconnectedRoomsCannotEmitThroughWalls()
        {
            var p = new AudioWorldPresenter(); var s = State();
            s.Rooms[2] = new GeneratedRoomSample(2, new Bounds(Vector3.right * 30, Vector3.one * 20), false, false, new Vector3[0]);
            p.SetTorches(s, 1, new[] { Vector3.right }); p.SetTorches(s, 2, new[] { Vector3.right * 2 });
            p.SelectTorches(s, Vector3.zero, 16); Assert.That(s.Nearest.Count, Is.EqualTo(1));
            p.TickTorches(s, .3f, 2); p.TickTorches(s, .3f, 2);
            s.ConsumedRooms.Add(1); p.SelectTorches(s, Vector3.zero, 16); p.TickTorches(s, .4f, 2);
            Assert.That(s.Nearest, Is.Empty); Assert.That(s.Slots[0].Id, Is.Zero); Assert.That(s.Slots[0].Gain, Is.Zero);
        }
        [Test]
        public void ReplacingTorchPositionsPreservesIdsAndClipSelectionAvoidsImmediateRepeat()
        {
            var p = new AudioWorldPresenter(); var s = State();
            p.SetTorches(s, 1, new[] { Vector3.right, Vector3.left }); int id = s.RoomTorches[1][0].Id;
            p.SetTorches(s, 1, new[] { Vector3.forward, Vector3.back }); Assert.That(s.RoomTorches[1][0].Id, Is.EqualTo(id));
            p.SelectTorches(s, Vector3.zero, 16); p.TickTorches(s, .3f, 2);
            Assert.That(s.Slots[0].Clip, Is.Not.EqualTo(s.Slots[1].Clip));
        }
        [Test]
        public void AccentsUseSuppliedCurrentRoomAnchorsRespectCooldownAndAlternate()
        {
            var p = new AudioWorldPresenter(); var s = State();
            Assert.That(p.TickAccent(s, Vector3.zero, true, 1, 3, 3, out _), Is.False);
            Assert.That(p.TickAccent(s, Vector3.zero, true, 4, 3, 3, out var first), Is.True);
            Assert.That(first.Position, Is.EqualTo(Vector3.right * 9)); Assert.That(first.Gain, Is.LessThanOrEqualTo(.2f));
            Assert.That(p.TickAccent(s, Vector3.zero, true, 1, 3, 3, out _), Is.False);
            Assert.That(p.TickAccent(s, Vector3.zero, true, 3, 3, 3, out var second), Is.True);
            Assert.That(first.Cue, Is.Not.EqualTo(second.Cue));
            s.ConsumedRooms.Add(1); Assert.That(p.TickAccent(s, Vector3.zero, true, 30, 3, 3, out _), Is.False);
            var reset = new AudioWorldDriverState(); Assert.That(reset.RoomTorches, Is.Empty); Assert.That(reset.AccentDue, Is.Empty);
            Assert.That(p.TickAccent(reset, Vector3.zero, true, 30, 3, 3, out _), Is.False);
        }
    }
}
