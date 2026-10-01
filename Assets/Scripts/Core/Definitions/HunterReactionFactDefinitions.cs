// ============================================================================
// HunterReactionFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries world evidence for hunter reactions without a Session dependency.
//   Camera samples and jam revisions are explicit so stale inputs cannot move a
//   view-dependent threat or complete a replacement door jam.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter reactions.
// KEY RESPONSIBILITIES:
//   - Describe camera observation, room lighting, jams and completed break work.
// DEPENDENCIES:
//   - Core identities, System collections and Unity value types only.
// USAGE NOTES:
//   Session owns snapshots. Jam ids are interactable ids; Revision changes on
//   every new jam. A completion is emitted once, then motion waits for removal.
//   Camera rotation/FOV must include actual look-back, pitch and camera effects.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
namespace Worsen.Core
{
    public readonly struct HunterPlayerView
    {
        public HunterPlayerView(Vector3 origin, Quaternion rotation, float horizontalFov, float verticalFov, long tick)
        { Origin = origin; Rotation = rotation; HorizontalFov = horizontalFov; VerticalFov = verticalFov; Tick = tick; }
        public Vector3 Origin { get; }
        public Quaternion Rotation { get; }
        public float HorizontalFov { get; }
        public float VerticalFov { get; }
        public long Tick { get; }
    }
    public readonly struct HunterDoorJam
    {
        public HunterDoorJam(int doorId, long revision, Bounds bounds, float breakSeconds)
        { DoorId = doorId; Revision = revision; Bounds = bounds; BreakSeconds = breakSeconds; }
        public int DoorId { get; }
        public long Revision { get; }
        public Bounds Bounds { get; }
        public float BreakSeconds { get; }
    }
    public interface IReadOnlyHunterWorldView
    {
        bool TryGetRoomLit(int roomId, out bool lit);
        IReadOnlyList<HunterDoorJam> JammedDoors { get; }
    }
    public readonly struct HunterDoorBreakFact
    {
        public HunterDoorBreakFact(EntityId hunter, int doorId, long revision, long tick)
        { Hunter = hunter; DoorId = doorId; Revision = revision; Tick = tick; }
        public EntityId Hunter { get; }
        public int DoorId { get; }
        public long Revision { get; }
        public long Tick { get; }
    }
}
