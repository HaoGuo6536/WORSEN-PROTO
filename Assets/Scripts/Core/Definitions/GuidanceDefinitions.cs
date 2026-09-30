// ============================================================================
// GuidanceDefinitions.cs
// ============================================================================
// PURPOSE:
//   Describes typed guidance without changing the legacy floor display snapshot.
//   Consumers can distinguish objective and threat arrows and disclose fallbacks.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Core · shared Guidance contracts.
// KEY RESPONSIBILITIES:
//   - Carry world-space direction, target position and optional stable identities.
// DEPENDENCIES:
//   - Core EntityId and UnityEngine.Vector3 as values only.
// USAGE NOTES:
//   AnchorId -1 and EntityId.None mean absent; producers set the relevant identity.
//   Direction is supplied by the producer, not normalized here. IsFallback marks
//   a straight-line or retained direction rather than a successful route refresh.
// ============================================================================
using UnityEngine;

namespace Worsen.Core
{
    /// <summary>The objective or threat channel represented by a guidance target.</summary>
    public enum GuidanceKind { WhiteArrow, ThreatArrow, GoldenSense, ExitThroughWalls }

    /// <summary>A world-space guidance result with separate anchor and entity identity domains.</summary>
    public readonly struct GuidanceTarget
    {
        public GuidanceTarget(GuidanceKind kind, Vector3 worldDirection, Vector3 targetPosition,
            int anchorId = -1, EntityId entityId = default, bool isFallback = false)
        {
            Kind = kind; WorldDirection = worldDirection; TargetPosition = targetPosition;
            AnchorId = anchorId; EntityId = entityId; IsFallback = isFallback;
        }
        public GuidanceKind Kind { get; }
        public Vector3 WorldDirection { get; }
        public Vector3 TargetPosition { get; }
        public int AnchorId { get; }
        public EntityId EntityId { get; }
        public bool IsFallback { get; }
    }
}
