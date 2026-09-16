// ============================================================================
// AudioWorldPresenter.cs
// ============================================================================
//
// PURPOSE:
//   Selects at most four torches using distance and supplied room/portal evidence.
//   It fades voice assignments before replacement and schedules quiet accents only at known anchors in the listener room.
//
// ARCHITECTURAL ROLE:
//   Presenter (§7b) · Presentation · Audio.
//
// KEY RESPONSIBILITIES:
//   - Keep ambient emitters bounded and tied to supplied room geometry.
//   - Preserve transient presentation ownership and deterministic verification.
//
// DEPENDENCIES:
//   - Core room geometry values and the owning Audio presentation system.
//
// USAGE NOTES:
//   No engine calls; time and cosmetic random state are injected.
//   Same-room torches and nearby shared-doorway neighbors are eligible; consumed rooms never emit.
//   All four physical voices remain bounded during fades, with stable identities while retained.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
namespace Worsen.Presentation.Audio
{
    public sealed class AudioWorldPresenter
    {
        public void SetRooms(AudioWorldDriverState state, IReadOnlyList<GeneratedRoomSample> rooms)
        {
            state.Rooms.Clear();
            if (rooms != null) foreach (GeneratedRoomSample room in rooms) state.Rooms[room.RoomId] = room;
        }
        public void SetTorches(AudioWorldDriverState state, int room, Vector3[] positions)
        {
            int count = positions != null ? positions.Length : 0;
            state.RoomTorches.TryGetValue(room, out AudioTorchAnchor[] previous);
            var anchors = new AudioTorchAnchor[count];
            for (int i = 0; i < count; i++) anchors[i] = new AudioTorchAnchor
            { Id = previous != null && i < previous.Length ? previous[i].Id : ++state.NextAnchor, Room = room, Position = positions[i] };
            state.RoomTorches[room] = anchors;
        }
        public void SelectTorches(AudioWorldDriverState state, Vector3 listener, float range)
        {
            state.Nearest.Clear();
            if (!TryRoom(state, listener, out GeneratedRoomSample current) || state.ConsumedRooms.Contains(current.RoomId)) return;
            foreach (var pair in state.RoomTorches)
            {
                if (state.ConsumedRooms.Contains(pair.Key) || !state.Rooms.TryGetValue(pair.Key, out GeneratedRoomSample room)) continue;
                bool same = current.RoomId == room.RoomId;
                if (!same && !NearSharedPortal(current, room, listener)) continue;
                foreach (AudioTorchAnchor source in pair.Value)
                {
                    AudioTorchAnchor candidate = source; candidate.DistanceSquared = (source.Position - listener).sqrMagnitude;
                    if (candidate.DistanceSquared > range * range) continue;
                    candidate.AcrossPortal = !same;
                    int insert = 0;
                    while (insert < state.Nearest.Count && (state.Nearest[insert].DistanceSquared < candidate.DistanceSquared ||
                        (state.Nearest[insert].DistanceSquared == candidate.DistanceSquared && state.Nearest[insert].Id < candidate.Id))) insert++;
                    if (insert >= 4) continue;
                    state.Nearest.Insert(insert, candidate); if (state.Nearest.Count > 4) state.Nearest.RemoveAt(4);
                }
            }
        }
        public void TickTorches(AudioWorldDriverState state, float dt, int clipCount)
        {
            dt = float.IsNaN(dt) || float.IsInfinity(dt) ? 0f : Mathf.Max(0f, dt);
            for (int i = 0; i < state.Slots.Length; i++)
            {
                AudioTorchSlot slot = state.Slots[i]; slot.Changed = false; bool retained = false;
                foreach (AudioTorchAnchor desired in state.Nearest)
                    if (desired.Id == slot.Id)
                    { retained = true; slot.Position = desired.Position; slot.AcrossPortal = desired.AcrossPortal; break; }
                slot.Gain = Mathf.MoveTowards(slot.Gain, retained ? 1f : 0f, dt / .3f);
                if (!retained && slot.Gain <= 0f)
                {
                    slot.Id = 0;
                    foreach (AudioTorchAnchor desired in state.Nearest)
                    {
                        bool assigned = false;
                        for (int j = 0; j < state.Slots.Length; j++) if (j != i && state.Slots[j].Id == desired.Id) assigned = true;
                        if (assigned || clipCount <= 0) continue;
                        int clip = state.Random.Next(clipCount > 1 && state.LastTorchClip >= 0 ? clipCount - 1 : clipCount);
                        if (clipCount > 1 && state.LastTorchClip >= 0 && clip >= state.LastTorchClip) clip++;
                        state.LastTorchClip = clip;
                        slot.Id = desired.Id; slot.Position = desired.Position; slot.AcrossPortal = desired.AcrossPortal;
                        slot.Clip = clip; slot.Pitch = Mathf.Lerp(.94f, 1.06f, (float)state.Random.NextDouble()); slot.Changed = true;
                        break;
                    }
                }
                state.Slots[i] = slot;
            }
        }
        public bool TickAccent(AudioWorldDriverState state, Vector3 listener, bool alive, float dt, float minimum, float maximum, out AudioFeedbackCommand command)
        {
            command = default;
            dt = float.IsNaN(dt) || float.IsInfinity(dt) ? 0f : Mathf.Max(0f, dt); state.Time += dt;
            if (!alive || !TryRoom(state, listener, out GeneratedRoomSample room) || state.ConsumedRooms.Contains(room.RoomId)) return false;
            if (!state.AccentDue.TryGetValue(room.RoomId, out float due))
            { state.AccentDue[room.RoomId] = state.Time + Mathf.Max(1f, minimum); return false; }
            if (state.Time < due || state.Time < state.NextGlobalAccent) return false;
            // Choose a real supplied portal anchor, or this room's known center; never an unobserved random world point.
            Vector3 position = room.Bounds.center;
            if (room.PortalCenters != null && room.PortalCenters.Length > 0) position = room.PortalCenters[state.Random.Next(room.PortalCenters.Length)];
            CueId preferred = room.OpenSky ? CueId.Drip : CueId.ChainCreak;
            CueId cue = preferred != state.LastAccent ? preferred : (preferred == CueId.Drip ? CueId.ChainCreak : CueId.Drip);
            state.LastAccent = cue;
            minimum = Mathf.Max(2f, minimum); maximum = Mathf.Max(minimum, maximum);
            float interval = Mathf.Lerp(minimum, maximum, (float)state.Random.NextDouble());
            state.AccentDue[room.RoomId] = state.Time + interval; state.NextGlobalAccent = state.Time + minimum;
            command = new AudioFeedbackCommand { Cue = cue, Position = position, Emitter = -400000 - room.RoomId, Gain = room.Refuge ? .12f : .2f };
            return true;
        }
        private bool TryRoom(AudioWorldDriverState state, Vector3 position, out GeneratedRoomSample result)
        {
            foreach (GeneratedRoomSample room in state.Rooms.Values) if (room.Bounds.Contains(position)) { result = room; return true; }
            result = default; return false;
        }
        private bool NearSharedPortal(GeneratedRoomSample first, GeneratedRoomSample second, Vector3 listener)
        {
            if (first.PortalCenters == null || second.PortalCenters == null) return false;
            foreach (Vector3 a in first.PortalCenters)
                foreach (Vector3 b in second.PortalCenters)
                    if ((a - b).sqrMagnitude < .25f && (listener - a).sqrMagnitude < 36f) return true;
            return false;
        }
    }
}
