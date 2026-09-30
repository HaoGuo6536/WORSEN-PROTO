// ============================================================================
// SkipFactDefinitions.cs
// ============================================================================
// PURPOSE:
//   Shares completed route uses and committed Skip arrivals without engine objects.
//   Floor identity prevents counts leaking between floors or duplicate delivery.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · Hunter Skip contracts.
// KEY RESPONSIBILITIES:
//   - Describe a completed traversal and its safe interception anchor.
//   - Distinguish a silent relocation from its optional persistent mark.
// DEPENDENCIES:
//   - Core identities and UnityEngine value types only.
// USAGE NOTES:
//   Sequence is monotonic per route per floor; Floor is a monotonic generation id.
//   MarkId names a Level ThresholdMark, not the doorway's own interactable id.
//   Teleports have no sound, animation, trail or flash; only the arrived body remains.
// ============================================================================
using UnityEngine;
namespace Worsen.Core
{
    public enum SkipRouteKind { Doorway, VaultWindow, StairHead, Drop }
    public readonly struct SkipTraversalUse
    {
        public SkipTraversalUse(long floor, long sequence, EntityId player, int routeId,
            SkipRouteKind kind, Vector3 position, int markId = -1)
        { Floor = floor; Sequence = sequence; Player = player; RouteId = routeId;
            Kind = kind; Position = position; MarkId = markId; }
        public long Floor { get; }
        public long Sequence { get; }
        public EntityId Player { get; }
        public int RouteId { get; }
        public SkipRouteKind Kind { get; }
        public Vector3 Position { get; }
        public int MarkId { get; }
    }
    public enum SkipFactKind { Teleported, Marked }
    public readonly struct SkipFact
    {
        public SkipFact(EntityId hunter, long floor, long tick, SkipFactKind kind,
            int routeId, int markId, Vector3 position)
        { Hunter = hunter; Floor = floor; Tick = tick; Kind = kind;
            RouteId = routeId; MarkId = markId; Position = position; }
        public EntityId Hunter { get; }
        public long Floor { get; }
        public long Tick { get; }
        public SkipFactKind Kind { get; }
        public int RouteId { get; }
        public int MarkId { get; }
        public Vector3 Position { get; }
        public bool Audible => false;
        public bool VisibleTransition => false;
    }
}
