// ============================================================================
// PlayerDefinitions.cs
// ============================================================================
// PURPOSE:
//   Carries movement and damage decisions between the Player stacks as plain values.
//   This is part of the solo movement prototype. Explicit inputs keep its
//   behavior reproducible and its ownership visible during integration.
// ARCHITECTURAL ROLE:
//   Definitions (§5) · Domain · Player local results.
// KEY RESPONSIBILITIES:
//   - Implement only the Player responsibility named by this script.
//   - Keep game rules, passive state, and engine interactions in separate roles.
// DEPENDENCIES:
//   - Worsen.Core contracts and the owning Worsen.Domain.Player system only.
//   - Editor scripts additionally use UnityEditor; tests additionally use NUnit.
// USAGE NOTES:
//   Cross-system movement and traversal facts are Core types. These results remain inside Player.
//   No other Domain system or Presentation system is referenced.
// ============================================================================
using UnityEngine;
using Worsen.Core;

namespace Worsen.Domain.Player
{
    public enum PlayerHealthState { Healthy, Injured, Critical, Dead }

    public readonly struct PlayerTickResult
    {
        public PlayerTickResult(Vector3 displacement, bool crouched, PlayerTraversalFact[] facts,
            bool traversing = false, Vector3 traversalStart = default, Vector3 traversalTarget = default, float traversalProgress = 0f, float traversalHeight = 0f)
        {
            Displacement = displacement; Crouched = crouched; Facts = facts;
            Traversing = traversing; TraversalStart = traversalStart; TraversalTarget = traversalTarget; TraversalProgress = traversalProgress;
            TraversalHeight = traversalHeight;
        }
        public Vector3 Displacement { get; }
        public bool Crouched { get; }
        public PlayerTraversalFact[] Facts { get; }
        public bool Traversing { get; }
        public Vector3 TraversalStart { get; }
        public Vector3 TraversalTarget { get; }
        public float TraversalProgress { get; }
        public float TraversalHeight { get; }
    }

    public readonly struct PlayerMoveResult
    {
        public PlayerMoveResult(Vector3 position, Vector3 velocity, bool grounded, bool ceiling)
        { Position = position; Velocity = velocity; Grounded = grounded; Ceiling = ceiling; }
        public Vector3 Position { get; }
        public Vector3 Velocity { get; }
        public bool Grounded { get; }
        public bool Ceiling { get; }
    }

    public readonly struct PlayerHitResult
    {
        public PlayerHitResult(bool changed, bool died) { Changed = changed; Died = died; }
        public bool Changed { get; }
        public bool Died { get; }
    }
}
