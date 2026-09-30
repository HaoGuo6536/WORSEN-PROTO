// ============================================================================
// HunterStallFact.cs
// ============================================================================
// PURPOSE:
//   Freezes the navigation evidence at the first stalled tick of an episode.
//   It is an observation, not an instruction to replan, move or recover.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Hunter.
// KEY RESPONSIBILITIES:
//   - Carry identity, physical dimensions, action and an immutable path snapshot.
// DEPENDENCIES:
//   - Core EntityId, HunterAction and UnityEngine value types only.
// USAGE NOTES:
//   Hunter-local by PLAN-014 ownership. Core telemetry translation belongs to the
//   coordinator. Room is last known; null obstacle means no cheap physical probe.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using Worsen.Core;
using EntityId = Worsen.Core.EntityId;
namespace Worsen.Domain.Hunter
{
    public readonly struct HunterStallFact
    {
        public HunterStallFact(EntityId hunter, long tick, Vector3 position, int? roomId,
            Vector3[] corners, float agentRadius, float capsuleRadius, float motorRadius,
            HunterAction action, double remaining, Vector3? nearestObstaclePoint = null)
        {
            Hunter = hunter; Tick = tick; Position = position; RoomId = roomId;
            PathCorners = Array.AsReadOnly((Vector3[])corners.Clone());
            AgentRadius = agentRadius; CapsuleRadius = capsuleRadius; MotorRadius = motorRadius;
            Action = action; RemainingDistance = remaining; NearestObstaclePoint = nearestObstaclePoint;
        }
        public EntityId Hunter { get; }
        public long Tick { get; }
        public Vector3 Position { get; }
        public int? RoomId { get; }
        public IReadOnlyList<Vector3> PathCorners { get; }
        public float AgentRadius { get; }
        public float CapsuleRadius { get; }
        public float MotorRadius { get; }
        public HunterAction Action { get; }
        public double RemainingDistance { get; }
        public Vector3? NearestObstaclePoint { get; }
    }
}
