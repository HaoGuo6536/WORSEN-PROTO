// ============================================================================
// FloorCakeDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes Floor-owned trap spawns, resolved contacts and optional cake rules.
//   These immutable records let the owner route effects without Floor calling them.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Floor.
// KEY RESPONSIBILITIES:
//   - Keep trap identity separate from real pickup counts and carry the contacted player.
//   - Expose default-off hooks without assigning catalogue EffectId keys or stacks.
// DEPENDENCIES:
//   Core identities/anchors and UnityEngine value types only.
// USAGE NOTES:
//   Floor-local boundary per PLAN-019; the coordinator must translate facts to Core
//   primitives before Presentation calls. No effect lifetime or foreign state lives here.
//   Hooks are snapshotted at floor initialization and reset on teardown.
// ============================================================================
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;

namespace Worsen.Domain.Floor
{
    public enum FloorTrapKind { Blind, Slow, Announce }

    public readonly struct FloorTrapSpawn
    {
        public FloorTrapSpawn(LevelAnchor anchor, FloorTrapKind kind) { Anchor = anchor; Kind = kind; }
        public LevelAnchor Anchor { get; }
        public FloorTrapKind Kind { get; }
    }

    public readonly struct FloorTrapSprungFact
    {
        public FloorTrapSprungFact(int trapId, FloorTrapKind kind, int roomId, Vector3 position, long tick, EntityId playerId)
        { TrapId = trapId; Kind = kind; RoomId = roomId; Position = position; Tick = tick; PlayerId = playerId; }
        public int TrapId { get; }
        public FloorTrapKind Kind { get; }
        public int RoomId { get; }
        public Vector3 Position { get; }
        public long Tick { get; }
        public EntityId PlayerId { get; }
    }

    public readonly struct FloorCakeHooks
    {
        public FloorCakeHooks(bool sweetTooth = false, bool blindFaith = false, bool goldenSense = false,
            bool moreTraps = false, bool silentTraps = false, bool greedyDoor = false)
        {
            SweetTooth = sweetTooth; BlindFaith = blindFaith; GoldenSense = goldenSense;
            MoreTraps = moreTraps; SilentTraps = silentTraps; GreedyDoor = greedyDoor;
        }
        public bool SweetTooth { get; }
        public bool BlindFaith { get; }
        public bool GoldenSense { get; }
        public bool MoreTraps { get; }
        public bool SilentTraps { get; }
        public bool GreedyDoor { get; }
    }
}
